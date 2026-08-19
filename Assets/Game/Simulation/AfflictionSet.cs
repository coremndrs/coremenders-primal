using System;

namespace Game.Simulation
{
    /// <summary>
    /// Per-dreamer set of active Tier-1 needs-based afflictions (TDD §4.4 stages 5–6, GDD §4).
    /// Afflictions are self-resolving: stage 5 sets/clears each flag every tick based on
    /// whether the underlying need is above or below its critical threshold.
    /// Multiple active afflictions stack their per-tick Vitality drain (GDD §4).
    /// </summary>
    [Serializable]
    public class AfflictionSet
    {
        public bool isStarving;    // Hunger ≤ critical threshold
        public bool isDehydrated;  // Thirst ≤ critical threshold
        public bool isHypothermic; // Warmth ≤ critical threshold

        public bool HasAny    => isStarving || isDehydrated || isHypothermic;
        public int  ActiveCount => (isStarving ? 1 : 0) + (isDehydrated ? 1 : 0) + (isHypothermic ? 1 : 0);
    }
}
