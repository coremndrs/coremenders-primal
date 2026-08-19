using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// The elapsed-time resolver (TDD §1.7, §4.1).
    ///
    /// One canonical Step = 1 IG minute. Called by real-time (WorldClockDriver),
    /// Skip (0.1.3), Scheduler, and inactive-map re-entry. tick == jump holds by
    /// construction — there is no second code path.
    ///
    /// Entity order is fixed (dreamers array index) for deterministic tie-breaking
    /// required by save/revert reproducibility (§4.4).
    ///
    /// Stages per build:
    ///   0.1.0 — 1 (clock advance), 3 (needs drain)
    ///   0.1.1 — 3 updated (activity modifier), 4b (task advance + outputs)
    ///   0.1.2 — 5 (afflictions), 6 (Vitality drain), 7 (incapacitation check)
    ///   0.1.4 — CheckSkipGuards() called by SkipManager after each Step() (stage 11 equivalent)
    ///   0.1.5 — 2 (buff tick + expiry), 3 updated (buff modifiers applied to drain)
    ///   0.1.6 — stage 4b populates sleepEnded output for full-sleep completions
    ///   0.1.7 — stage 4b applies EnergyRecoveryMultiplier (Nightmare debuff)
    ///   0.2.3 — 3 updated (nourishment buff rates), 4a (action queue advance + completion),
    ///           10 (day-boundary pool reset + exhaustion clear), 10b (movement drain);
    ///           Step gains actionsEnded + distanceMoved + movementConfig optional params
    ///   0.2.9 — action economy is up-front (debit on start) + pro-rata refund on stop, applied by
    ///           MapEntitySync (ApplyActionEffect helper here); no per-minute work stage in Step
    /// </summary>
    public static class SimResolver
    {
        /// <param name="sleepEnded">
        /// Optional output list. Any full-sleep task that completes this tick appends a
        /// SleepEndedResult (WasCutShort=false). Callers process this list to bank
        /// sleep checkpoints (0.1.6). Nap (Resting) completions are not reported here.
        /// </param>
        /// <param name="actionsEnded">
        /// Optional output list. Any end-payout action that completes this tick appends an
        /// ActionEndedResult. WorldClockDriver processes these to trigger host-side effects
        /// such as dream-save checkpoints (0.2.3d).
        /// </param>
        /// <param name="walkDistanceMoved">Per-dreamer walk distance since last tick. Null → no walk drain.</param>
        /// <param name="runDistanceMoved">Per-dreamer run distance since last tick. Null → no run drain.</param>
        /// <param name="sprintDistanceMoved">Per-dreamer sprint distance since last tick. Null → no sprint drain.</param>
        /// <param name="movementConfig">Movement tuning rates. Null → no drain.</param>
        public static void Step(
            WorldState world,
            NeedsConfig config,
            BuffConfig buffConfig = null,
            IList<SleepEndedResult> sleepEnded = null,
            IList<ActionEndedResult> actionsEnded = null,
            float[] walkDistanceMoved = null,
            float[] runDistanceMoved = null,
            float[] sprintDistanceMoved = null,
            MovementConfig movementConfig = null)
        {
            // Stage 1: advance clock by exactly 1 IG minute.
            world.clock.totalInGameMinutes += 1f;

            // Stage 2: tick down active buffs; expire at remainingMinutes ≤ 0.
            // Modifiers are taken from the surviving list; expired buffs are gone this tick.
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.buffs == null) continue;
                for (int i = dreamer.buffs.Count - 1; i >= 0; i--)
                {
                    dreamer.buffs[i].remainingMinutes -= 1f;
                    if (dreamer.buffs[i].remainingMinutes <= 0f)
                        dreamer.buffs.RemoveAt(i);
                }
            }

            // Stage 3: drain Tier-1 needs, scaled by activity × buff modifiers.
            // After drain: apply additive nourishment buff rates (0.2.3b5).
            // Modifiers are read from tick-start task so each minute has one consistent rate.
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.needs == null) continue;
                float actMod = config.GetActivityMultiplier(dreamer.task?.type ?? TaskType.Idle);
                float hMod   = GetBuffMod(dreamer, BuffStat.HungerDrainMultiplier, buffConfig);
                float tMod   = GetBuffMod(dreamer, BuffStat.ThirstDrainMultiplier, buffConfig);
                float wMod   = GetBuffMod(dreamer, BuffStat.WarmthDrainMultiplier, buffConfig);
                dreamer.needs.hunger = (float)Math.Max(0.0, dreamer.needs.hunger - config.hungerDrainPerMinute * actMod * hMod);
                dreamer.needs.thirst = (float)Math.Max(0.0, dreamer.needs.thirst - config.thirstDrainPerMinute * actMod * tMod);
                dreamer.needs.warmth = (float)Math.Max(0.0, dreamer.needs.warmth - config.warmthDrainPerMinute * actMod * wMod);
                // Nourishment buff restore: additive per-minute rates from active eating actions (0.2.3b5)
                float hRate = GetBuffSum(dreamer, BuffStat.HungerRestoreRate, buffConfig);
                float tRate = GetBuffSum(dreamer, BuffStat.ThirstRestoreRate, buffConfig);
                if (hRate > 0f) dreamer.needs.hunger = (float)Math.Min(100.0, dreamer.needs.hunger + hRate);
                if (tRate > 0f) dreamer.needs.thirst = (float)Math.Min(100.0, dreamer.needs.thirst + tRate);
            }

            // Stage 4a: advance action queue; fire completions; auto-advance (0.2.3b2/b3).
            // Ordering: drain (3) → action outputs (4a) → task outputs (4b) → affliction check (5).
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.actionQueue == null || dreamer.actionQueue.Count == 0) continue;

                var active = dreamer.actionQueue[0];

                // Start the action when it first reaches the head of the queue
                if (!active.started)
                {
                    active.started   = true;
                    active.startTime = world.clock.totalInGameMinutes;
                    InstallNourishmentBuffs(dreamer, active);
                }

                // Fire completion when end time reached
                if (world.clock.totalInGameMinutes >= active.startTime + active.duration)
                {
                    FireActionCompletion(dreamer, active, actionsEnded);
                    RemoveNourishmentBuffs(dreamer);
                    dreamer.actionQueue.RemoveAt(0);

                    // Auto-advance: start the next queued action in the same tick (0.2.3b2)
                    if (dreamer.actionQueue.Count > 0)
                    {
                        var next     = dreamer.actionQueue[0];
                        next.started   = true;
                        next.startTime = world.clock.totalInGameMinutes;
                        InstallNourishmentBuffs(dreamer, next);
                    }
                }
            }

            // Stage 4b: advance tasks; apply outputs on completion.
            // Critical ordering: drain (3) → outputs (4b) → affliction check (5).
            // A meal completing this tick refills hunger before the affliction check.
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.task == null || dreamer.task.type == TaskType.Idle) continue;

                dreamer.task.elapsedMinutes += 1f;
                if (!dreamer.task.IsComplete) continue;

                switch (dreamer.task.type)
                {
                    case TaskType.Sleeping:
                    {
                        float rMod = GetBuffMod(dreamer, BuffStat.EnergyRecoveryMultiplier, buffConfig);
                        dreamer.energy = (float)Math.Min(100.0, dreamer.energy + config.sleepEnergyRestore * rMod);
                        sleepEnded?.Add(new SleepEndedResult
                        {
                            DreamerSlot  = dreamer.slot,
                            SleptMinutes = dreamer.task.elapsedMinutes, // includes this tick
                            WasCutShort  = false,
                        });
                        break;
                    }
                    case TaskType.Resting:
                    {
                        float rMod = GetBuffMod(dreamer, BuffStat.EnergyRecoveryMultiplier, buffConfig);
                        dreamer.energy = (float)Math.Min(100.0, dreamer.energy + config.restEnergyRestore * rMod);
                        break;
                    }
                }

                dreamer.task = new DreamerTask();
            }

            // (Action economy is applied up front on start and refunded pro-rata on stop by
            // MapEntitySync — see StartContributor / StopContributor — not per-minute here.)

            // Stage 5: reconcile needs-driven afflictions.
            // Each flag is set or cleared this tick based on the post-drain, post-meal
            // need value — self-resolving (GDD §4).
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.afflictions == null || dreamer.needs == null) continue;
                dreamer.afflictions.isStarving    = dreamer.needs.hunger <= config.hungerCriticalThreshold;
                dreamer.afflictions.isDehydrated  = dreamer.needs.thirst <= config.thirstCriticalThreshold;
                dreamer.afflictions.isHypothermic = dreamer.needs.warmth <= config.warmthCriticalThreshold;
            }

            // Stage 6: each active affliction drains Vitality; stacks additively (GDD §4).
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.afflictions == null) continue;
                int count = dreamer.afflictions.ActiveCount;
                if (count == 0) continue;
                dreamer.vitality = (float)Math.Max(0.0,
                    dreamer.vitality - config.vitalityDrainPerAfflictionPerMinute * count);
            }

            // Stage 7: Vitality / incapacitation check.
            // Dreamer → downed state; full dream flow wired in 0.1.7.
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer == null || dreamer.isIncapacitated) continue;
                if (dreamer.vitality <= config.incapacitationVitalityThreshold)
                    dreamer.isIncapacitated = true;
            }

            // Stage 8: engaged-entity AI + coarse movement (post-Cluster-1)
            // Stage 9: weather roll (post-Cluster-1)

            // Stage 10: day boundary → pool reset + remove exhaustion debuffs (0.2.3a3).
            // Fires once per midnight crossing, including during a chunked skip.
            float now   = world.clock.totalInGameMinutes;
            int prevDay = (int)((now - 1f) / config.timePoolMaxMinutes);
            int curDay  = (int)(now / config.timePoolMaxMinutes);
            if (curDay > prevDay)
            {
                foreach (var dreamer in world.dreamers)
                {
                    if (dreamer == null) continue;
                    dreamer.timePool = config.timePoolMaxMinutes;
                    dreamer.buffs?.RemoveAll(b => b.defId == ActionRecord.ExhaustionBuffId);
                }
            }

            // Stage 10b: movement drain — real-time only; skip passes null (0.2.3e2/e3).
            // Walk, run, and sprint distances are tracked separately and use different rates.
            if (movementConfig != null &&
                (walkDistanceMoved != null || runDistanceMoved != null || sprintDistanceMoved != null))
            {
                foreach (var dreamer in world.dreamers)
                {
                    if (dreamer == null) continue;
                    int slot = dreamer.slot;

                    float walkDist   = (walkDistanceMoved   != null && slot < walkDistanceMoved.Length)
                                       ? walkDistanceMoved[slot]   : 0f;
                    float runDist    = (runDistanceMoved    != null && slot < runDistanceMoved.Length)
                                       ? runDistanceMoved[slot]    : 0f;
                    float sprintDist = (sprintDistanceMoved != null && slot < sprintDistanceMoved.Length)
                                       ? sprintDistanceMoved[slot] : 0f;
                    if (walkDist <= 0f && runDist <= 0f && sprintDist <= 0f) continue;

                    dreamer.timePool -= walkDist   * movementConfig.walkTimePerMeter
                                      + runDist    * movementConfig.runTimePerMeter
                                      + sprintDist * movementConfig.sprintTimePerMeter;
                    dreamer.energy    = (float)Math.Max(0.0,
                        dreamer.energy - walkDist   * movementConfig.walkEnergyPerMeter
                                       - runDist    * movementConfig.runEnergyPerMeter
                                       - sprintDist * movementConfig.sprintEnergyPerMeter);

                    if (dreamer.timePool < 0f)
                        ApplyExhaustionDebuff(dreamer, config);
                }
            }

            // Stage 11: skip guard check — called externally by SkipManager via CheckSkipGuards()
        }

        // ── Stage 4a helpers ────────────────────────────────────────────────────

        private static void InstallNourishmentBuffs(DreamerRecord dreamer, ActionRecord action)
        {
            if (action.payout != ActionPayout.Gradual) return;
            float hRate = action.HungerPerMin;
            float tRate = action.ThirstPerMin;
            // remainingMinutes = duration + 1 so Stage 2 doesn't evict the buff before Stage 3
            // delivers the last tick's nourishment (Stage 2 runs before Stage 3 each tick).
            if (hRate > 0f)
                dreamer.buffs.Add(new BuffInstance
                {
                    defId             = ActionRecord.NourishmentHungerBuffId,
                    remainingMinutes  = action.duration + 1f,
                    magnitudeOverride = hRate,
                });
            if (tRate > 0f)
                dreamer.buffs.Add(new BuffInstance
                {
                    defId             = ActionRecord.NourishmentThirstBuffId,
                    remainingMinutes  = action.duration + 1f,
                    magnitudeOverride = tRate,
                });
        }

        private static void RemoveNourishmentBuffs(DreamerRecord dreamer)
        {
            dreamer.buffs?.RemoveAll(b =>
                b.defId == ActionRecord.NourishmentHungerBuffId ||
                b.defId == ActionRecord.NourishmentThirstBuffId);
        }

        private static void FireActionCompletion(DreamerRecord dreamer, ActionRecord action,
            IList<ActionEndedResult> actionsEnded)
        {
            if (action.payout == ActionPayout.EndEffect)
                actionsEnded?.Add(new ActionEndedResult
                {
                    DreamerSlot = dreamer.slot,
                    EndEffect   = action.endEffect,
                });
            // Gradual payout: nourishment is delivered tick-by-tick via the buff; nothing extra at completion
        }

        /// <summary>
        /// Applies one signed action-economy delta (<paramref name="amount"/>) to the mapped
        /// DreamerRecord field. The single place a <see cref="ResourceStat"/> maps to a field — extend
        /// here, not per action. Used by the up-front debit / pro-rata refund in MapEntitySync. TimePool
        /// may go negative (exhaustion applied by the caller); all others clamp to [0,100].
        /// </summary>
        public static void ApplyActionEffect(DreamerRecord dreamer, ResourceStat stat, float amount, NeedsConfig config)
        {
            switch (stat)
            {
                case ResourceStat.Energy:
                    dreamer.energy = (float)Math.Max(0.0, Math.Min(100.0, dreamer.energy + amount));
                    break;
                case ResourceStat.TimePool:
                    dreamer.timePool = (float)Math.Min(config.timePoolMaxMinutes, dreamer.timePool + amount);
                    break;
                case ResourceStat.Hunger:
                    if (dreamer.needs != null)
                        dreamer.needs.hunger = (float)Math.Max(0.0, Math.Min(100.0, dreamer.needs.hunger + amount));
                    break;
                case ResourceStat.Thirst:
                    if (dreamer.needs != null)
                        dreamer.needs.thirst = (float)Math.Max(0.0, Math.Min(100.0, dreamer.needs.thirst + amount));
                    break;
                case ResourceStat.Warmth:
                    if (dreamer.needs != null)
                        dreamer.needs.warmth = (float)Math.Max(0.0, Math.Min(100.0, dreamer.needs.warmth + amount));
                    break;
                case ResourceStat.Vitality:
                    dreamer.vitality = (float)Math.Max(0.0, Math.Min(100.0, dreamer.vitality + amount));
                    break;
            }
        }

        private static void ApplyExhaustionDebuff(DreamerRecord dreamer, NeedsConfig config)
        {
            if (dreamer.buffs == null) return;
            foreach (var b in dreamer.buffs)
                if (b.defId == ActionRecord.ExhaustionBuffId) return; // already present
            dreamer.buffs.Add(new BuffInstance
            {
                defId            = ActionRecord.ExhaustionBuffId,
                remainingMinutes = config.exhaustionDurationMinutes,
            });
        }

        // ── Buff helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the product of all buff modifiers for <paramref name="stat"/> on this dreamer.
        /// 1.0 if no buffs apply (multiplicative identity = no change).
        /// </summary>
        private static float GetBuffMod(DreamerRecord dreamer, BuffStat stat, BuffConfig buffConfig)
        {
            if (buffConfig == null || dreamer.buffs == null || dreamer.buffs.Count == 0) return 1f;
            float mod = 1f;
            foreach (var bi in dreamer.buffs)
            {
                var def = buffConfig.GetDef(bi.defId);
                if (def?.modifiers == null) continue;
                foreach (var m in def.modifiers)
                    if (m.stat == stat) mod *= m.value;
            }
            return mod;
        }

        /// <summary>
        /// Returns the sum of additive buff modifiers for <paramref name="stat"/>.
        /// 0 if no buffs apply. Uses magnitudeOverride when non-zero (0.2.3 nourishment buffs).
        /// </summary>
        private static float GetBuffSum(DreamerRecord dreamer, BuffStat stat, BuffConfig buffConfig)
        {
            if (buffConfig == null || dreamer.buffs == null || dreamer.buffs.Count == 0) return 0f;
            float sum = 0f;
            foreach (var bi in dreamer.buffs)
            {
                var def = buffConfig.GetDef(bi.defId);
                if (def?.modifiers == null) continue;
                foreach (var m in def.modifiers)
                    if (m.stat == stat)
                        sum += bi.magnitudeOverride != 0f ? bi.magnitudeOverride : m.value;
            }
            return sum;
        }

        /// <summary>
        /// Stage 11 guard check — called by SkipManager after each Step() (skip mode only).
        /// Returns the first guard violation found, or null if all needs are above their floors.
        /// Real-time callers (WorldClockDriver) never call this; it is a no-op outside the skip coroutine.
        /// </summary>
        public static SkipStopReason CheckSkipGuards(WorldState world, NeedsConfig config)
        {
            float hFloor = config.GetHungerGuard() * 100f;
            float tFloor = config.GetThirstGuard() * 100f;
            float wFloor = config.GetWarmthGuard()  * 100f;

            foreach (var dreamer in world.dreamers)
            {
                if (dreamer?.needs == null) continue;
                if (dreamer.needs.hunger < hFloor)
                    return new SkipStopReason { Cause = SkipStopCause.GuardHunger, DreamerSlot = dreamer.slot };
                if (dreamer.needs.thirst < tFloor)
                    return new SkipStopReason { Cause = SkipStopCause.GuardThirst, DreamerSlot = dreamer.slot };
                if (dreamer.needs.warmth < wFloor)
                    return new SkipStopReason { Cause = SkipStopCause.GuardWarmth, DreamerSlot = dreamer.slot };
            }
            return null;
        }
    }
}
