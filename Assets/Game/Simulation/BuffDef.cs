using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// Static definition of a buff or debuff (TDD §0.1.5a).
    /// Authored in the BuffConfig list on WorldClockDriver, or via the built-in fallback defs.
    /// durationMinutes is the default; individual instances can override remainingMinutes at creation.
    /// </summary>
    [Serializable]
    public class BuffDef
    {
        public string             id;
        public string             displayName;
        public float              durationMinutes;  // default; overridden at instance creation if needed
        public List<BuffModifier> modifiers = new List<BuffModifier>();
    }
}
