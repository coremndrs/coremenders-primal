using System;

namespace Game.Simulation
{
    /// <summary>
    /// Tuning knobs for Tier-1 need drain, activity modifiers, task outputs, affliction/Vitality
    /// thresholds, and action pool (TDD §4.9). Exposed as SerializeField on WorldClockDriver and
    /// DreamerTaskSync/DreamerInventorySync for Inspector tweaking.
    ///
    /// Default drain rates:
    ///   Hunger — critical in ~12 IG hours (720 steps)
    ///   Thirst — critical in ~8 IG hours  (480 steps)
    ///   Warmth — critical in ~16 IG hours (960 steps) at base (no cold environment)
    /// </summary>
    [Serializable]
    public class NeedsConfig
    {
        // ── Stage 3: Tier-1 drain rates ─────────────────────────────────────────

        public float hungerDrainPerMinute = 0.139f;
        public float thirstDrainPerMinute = 0.208f;
        public float warmthDrainPerMinute = 0.104f;

        // ── Stage 3: activity multipliers ───────────────────────────────────────

        public float sleepingDrainMultiplier = 0.3f;
        public float restingDrainMultiplier  = 0.6f;

        // ── Stage 4: eat task ────────────────────────────────────────────────────

        public float eatDurationMinutes = 10f;
        public float eatHungerRestore   = 50f;
        public float eatThirstRestore   = 15f;

        // ── Stage 4: sleep task ──────────────────────────────────────────────────

        public float sleepDurationMinutes = 480f;
        public float sleepEnergyRestore   = 80f;

        // ── Stage 4: rest task ───────────────────────────────────────────────────

        public float restDurationMinutes = 30f;
        public float restEnergyRestore   = 25f;

        // ── Action classes for the needs tasks (0.2.11a1/a2) ─────────────────────
        // Sleep and rest are not def-backed the way world actions and consumables are, so their
        // class is authored here rather than on an SO. Both are Active per the 0.2.11a2 pass:
        // they are hours of world time spent present, and neither may execute out of the bracket.

        public ActionClass sleepActionClass = ActionClass.Active;
        public ActionClass restActionClass  = ActionClass.Active;

        // ── Stage 5: critical thresholds (need ≤ threshold → affliction) ────────

        public float hungerCriticalThreshold = 20f;
        public float thirstCriticalThreshold = 20f;
        public float warmthCriticalThreshold = 20f;

        // ── Stage 6: Vitality drain per active affliction per tick ───────────────

        public float vitalityDrainPerAfflictionPerMinute = 2f;

        // ── Stage 7: incapacitation ──────────────────────────────────────────────

        public float incapacitationVitalityThreshold = 0f;

        // ── Stage 11: skip guards ─────────────────────────────────────────────────
        // Skip halts after the tick a need first drops below its guard floor.
        // Default 30 % sits above the critical threshold (20) so skip always stops
        // before any affliction or Vitality drain can trigger.

        public float skipGuardPercent        = 0.30f;  // global default (fraction of max, 0–1)
        public float skipHungerGuardOverride = -1f;    // negative → use skipGuardPercent
        public float skipThirstGuardOverride = -1f;
        public float skipWarmthGuardOverride = -1f;

        public float GetHungerGuard() => skipHungerGuardOverride >= 0f ? skipHungerGuardOverride : skipGuardPercent;
        public float GetThirstGuard() => skipThirstGuardOverride >= 0f ? skipThirstGuardOverride : skipGuardPercent;
        public float GetWarmthGuard() => skipWarmthGuardOverride  >= 0f ? skipWarmthGuardOverride  : skipGuardPercent;

        // ── Action pool (0.2.3a) ─────────────────────────────────────────────────

        public float timePoolMaxMinutes        = 1440f; // 24h × 60 min = full day pool
        public float exhaustionDurationMinutes = 1440f; // exhaustion debuff lasts until midnight reset

        // ── Trivial bracket (§5.5.5, 0.2.11d) ────────────────────────────────────

        /// <summary>Minutes the bracket resets to at wake (d1/d4). 60 = one hour of maintenance a
        /// day for free; everything beyond it must be earned by idling through a skip (d3).</summary>
        public float trivialBracketDailyBase = 60f;

        /// <summary>
        /// Presence tolerance for self-contained Active actions (§5.5.3, 0.2.11b3): how far a
        /// dreamer may drift from where they committed before hand craft / rest / sleep cancels.
        /// Large enough to absorb controller settle and ground snapping, small enough that a
        /// deliberate step away reads as leaving.
        /// </summary>
        public float presenceMoveTolerance = 0.75f;

        /// <summary>
        /// Presence range for object-bound work (§5.5.3, 0.2.11b1): how far a contributor may get
        /// from the Instance before their contribution ends. Should sit slightly ABOVE
        /// InteractableDetector's interact range — being dropped from work you can still reach
        /// would read as a bug.
        /// </summary>
        public float presenceRange = 4f;

        // ── Helpers ──────────────────────────────────────────────────────────────

        public float GetActivityMultiplier(TaskType type) => type switch
        {
            TaskType.Sleeping => sleepingDrainMultiplier,
            TaskType.Resting  => restingDrainMultiplier,
            _                 => 1.0f,
        };

        /// <summary>
        /// Mints the slot entry for a self-contained needs task (0.2.11a3). Returns null for
        /// <see cref="TaskType.Idle"/> and for any type that is not a needs task — those occupy
        /// the slot through their own subsystem (world labor, crafts), not through this factory.
        /// The caller places it in <c>DreamerRecord.actionQueue</c>; SimResolver stage 4a starts
        /// and times it out.
        ///
        /// The retired Eating task (0.2.2b3 — food became item consumption) is deliberately not
        /// minted here; consumables go through DreamerInventorySync.ConsumeItemServerRpc.
        ///
        /// timeCost is 0 on purpose: the shipped Task channel never debited the day pool for
        /// sleep or rest, and 0.2.11a is a channel collapse, not an economy change. §5.5.2 does
        /// put sleep/rest on the pool — logged in TODO.md for the 0.2.11c commit-flow pass.
        /// </summary>
        public ActionRecord CreateTaskAction(TaskType type) => type switch
        {
            TaskType.Sleeping => new ActionRecord
            {
                kind = ActionSlotKind.Task, taskType = TaskType.Sleeping,
                actionClass = sleepActionClass, duration = sleepDurationMinutes, timeCost = 0f,
            },
            TaskType.Resting  => new ActionRecord
            {
                kind = ActionSlotKind.Task, taskType = TaskType.Resting,
                actionClass = restActionClass, duration = restDurationMinutes,  timeCost = 0f,
            },
            _ => null,
        };
    }
}
