namespace Game.Simulation
{
    /// <summary>
    /// Exposes definition data to Simulation without a Unity dependency.
    /// DefRegistry (Networking layer) implements this interface, resolving each defId
    /// to the composed Def and projecting its aspects into ItemDefData via Def.ToData()
    /// (TDD §1.12 / §5.1; unified at 0.2.9d).
    /// </summary>
    public interface IItemDefLookup
    {
        bool         TryGetDef(int defId, out ItemDefData data);
        ItemDefData  GetDef(int defId);
    }
}
