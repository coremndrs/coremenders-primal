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

        // ── Helpers ──────────────────────────────────────────────────────────────

        public float GetActivityMultiplier(TaskType type) => type switch
        {
            TaskType.Sleeping => sleepingDrainMultiplier,
            TaskType.Resting  => restingDrainMultiplier,
            _                 => 1.0f,
        };

        public DreamerTask CreateTask(TaskType type) => type switch
        {
            TaskType.Eating   => new DreamerTask { type = TaskType.Eating,   durationMinutes = eatDurationMinutes   },
            TaskType.Sleeping => new DreamerTask { type = TaskType.Sleeping, durationMinutes = sleepDurationMinutes },
            TaskType.Resting  => new DreamerTask { type = TaskType.Resting,  durationMinutes = restDurationMinutes  },
            _                 => new DreamerTask(),
        };
    }
}
