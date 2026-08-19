using System;

namespace Game.Simulation
{
    /// <summary>
    /// Tuning knobs for item spoilage bucket thresholds (TDD §5.2, 0.2.4c).
    /// Exposed as SerializeField on WorldClockDriver; set thresholds in Inspector.
    ///
    /// Mapping: condition ≥ freshThreshold → Fresh
    ///          condition ≥ goodThreshold  → Good
    ///          condition >  0             → Stale
    ///          condition ≤  0             → Spoiled
    /// </summary>
    [Serializable]
    public class SpoilageConfig
    {
        public float freshThreshold = 70f;
        public float goodThreshold  = 40f;

        public ConditionBucket GetBucket(float condition)
        {
            if (condition <= 0f)             return ConditionBucket.Spoiled;
            if (condition >= freshThreshold) return ConditionBucket.Fresh;
            if (condition >= goodThreshold)  return ConditionBucket.Good;
            return ConditionBucket.Stale;
        }
    }
}
