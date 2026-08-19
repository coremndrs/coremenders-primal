using System;

namespace Game.Simulation
{
    /// <summary>
    /// Runtime buff instance on a DreamerRecord (TDD §0.1.5a/b).
    /// Decremented each tick by SimResolver stage 2; removed at remainingMinutes ≤ 0.
    /// checkpointId is non-empty for protection buffs; it is host-only (validity query +
    /// revert) and never crosses the wire. coverage IS synced to clients for the HUD label.
    ///
    /// magnitudeOverride (0.2.3b): when non-zero, overrides the BuffDef's modifier value for
    /// all stats on this buff instance. Used by dynamically-created nourishment buffs whose
    /// per-minute rate varies per item. Zero = use the def's static magnitude.
    /// </summary>
    [Serializable]
    public class BuffInstance
    {
        public string      defId;
        public float       remainingMinutes;
        public string      checkpointId      = "";
        public BuffCoverage coverage         = BuffCoverage.None;
        public float       magnitudeOverride = 0f;
    }
}
