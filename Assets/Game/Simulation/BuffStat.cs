namespace Game.Simulation
{
    public enum BuffStat
    {
        HungerDrainMultiplier    = 0,
        ThirstDrainMultiplier    = 1,
        WarmthDrainMultiplier    = 2,
        EnergyDrainMultiplier    = 3,
        EnergyRecoveryMultiplier = 4, // <1 reduces sleep/rest energy restore (Nightmare debuff)
        HungerRestoreRate        = 5, // additive per-minute hunger restore (nourishment buff, 0.2.3b)
        ThirstRestoreRate        = 6, // additive per-minute thirst restore (nourishment buff, 0.2.3b)
    }
}
