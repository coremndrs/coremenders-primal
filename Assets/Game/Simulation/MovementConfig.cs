using System;

namespace Game.Simulation
{
    /// <summary>
    /// Tuning knobs for movement-based time-pool and energy drain (TDD §0.2.3e1).
    /// Three gait tiers: walk (cautious), run (default), sprint (hold Shift).
    /// The host buckets per-frame displacement by instantaneous speed against the two thresholds.
    /// All rates default to 0 (inert) until Inspector values are set.
    /// </summary>
    [Serializable]
    public class MovementConfig
    {
        // ── Per-gait drain rates ──────────────────────────────────────────────────
        public float walkTimePerMeter   = 0f;
        public float walkEnergyPerMeter = 0f;

        public float runTimePerMeter    = 0f;
        public float runEnergyPerMeter  = 0f;

        public float sprintTimePerMeter   = 0f;
        public float sprintEnergyPerMeter = 0f;

        // ── Speed thresholds (m/s) — set between the corresponding gait speeds ───
        // displacement / Time.deltaTime < runSpeedThreshold  → walk bucket
        // displacement / Time.deltaTime < sprintSpeedThreshold → run bucket
        // otherwise                                           → sprint bucket
        public float runSpeedThreshold    = 3.5f; // between walk (~2.5) and run (~5)
        public float sprintSpeedThreshold = 6.5f; // between run  (~5)   and sprint (~8)
    }
}
