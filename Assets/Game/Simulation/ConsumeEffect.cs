namespace Game.Simulation
{
    /// <summary>
    /// The instant effect applied when a dreamer consumes an item (TDD §0.2.2b1).
    /// Defined on the Def's ConsumableAspect; resolved by DreamerInventorySync.ConsumeItemServerRpc on the host.
    /// Eating-as-a-timed-task (a future refinement) will be a separate pathway.
    /// </summary>
    public enum ConsumeEffect
    {
        None           = 0,
        Food           = 1, // restores hunger (and optionally thirst)
        Drink          = 2, // restores thirst (and optionally hunger)
        SaveConsumable = 3, // banks a personal dream-save checkpoint
    }
}
