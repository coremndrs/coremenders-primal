using Game.Simulation;

namespace Game.Networking
{
    /// <summary>
    /// Checkpoint retention helper (TDD §0.1.7d c1).
    ///
    /// The old delete-on-buff-expiry rule (0.1.5 b6) is removed: checkpoints are no longer
    /// deleted when their protection buff expires. Retention is now governed exclusively by
    /// the revert timeline rule: checkpoints newer than the revert target are discarded
    /// (SaveSystem.DiscardCheckpointsNewerThan). This class is kept as an empty shell so
    /// existing call sites compile without modification.
    /// </summary>
    internal static class CheckpointCleaner
    {
        public static void CleanOrphans(WorldState world) { }
    }
}
