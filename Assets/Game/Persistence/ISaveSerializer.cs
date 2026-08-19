namespace Game.Persistence
{
    /// <summary>
    /// Single serializer contract shared by disk saves and in-memory snapshots (TDD §1.8).
    /// Backed by Newtonsoft JSON for Phase 0; the interface allows a later swap to a
    /// binary format without touching callers.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize<T>(T obj);
        T Deserialize<T>(string json);
    }
}
