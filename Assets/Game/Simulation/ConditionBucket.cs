namespace Game.Simulation
{
    /// <summary>
    /// Qualitative freshness tier for perishable items (TDD §5.2, 0.2.4c).
    /// Maps a computed condition float (0–100) to a discrete merge key.
    /// Bucket thresholds are tunable in SpoilageConfig.
    /// </summary>
    public enum ConditionBucket : byte
    {
        Fresh   = 0,
        Good    = 1,
        Stale   = 2,
        Spoiled = 3,
    }
}
