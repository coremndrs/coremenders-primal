namespace Game.Simulation
{
    /// <summary>
    /// Coverage label for a protection buff — used only for the revert-vote HUD (GDD §18).
    /// Actual validity is always derived from who holds the live buff, not from this label.
    /// </summary>
    public enum BuffCoverage : byte
    {
        None     = 0, // not a protection buff
        Shared   = 1,
        Personal = 2,
    }
}
