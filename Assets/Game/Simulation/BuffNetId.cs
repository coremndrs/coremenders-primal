namespace Game.Simulation
{
    /// <summary>
    /// Numeric wire identity for buff defs — sent over the network instead of the string id.
    /// The client resolves the display name and icon by looking up the local BuffConfig def.
    /// Add a new entry here when adding a new buff type; keep values stable once used in saves.
    /// </summary>
    public enum BuffNetId : int
    {
        None              = 0,
        Protection        = 1,
        TestDrainHalf     = 2,
        NightmareTier1    = 3,
        NightmareTier2    = 4,
        // 0.2.3 — action nourishment + exhaustion
        NourishmentHunger = 5,
        NourishmentThirst = 6,
        Exhaustion        = 7,
        // 0.2.4 — spoilage
        Sickness          = 8,
    }
}
