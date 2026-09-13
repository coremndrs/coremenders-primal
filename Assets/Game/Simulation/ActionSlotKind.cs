namespace Game.Simulation
{
    /// <summary>
    /// What kind of work occupies a dreamer's single active action slot (TDD §5.5.16, 0.2.11a3).
    ///
    /// After the channel collapse there is exactly one slot — <c>DreamerRecord.actionQueue[0]</c> —
    /// and every form of committed work funnels through it. The kind tells the host <b>who resolves
    /// completion</b>:
    ///
    ///   Consumable / Task — duration-driven; SimResolver stage 4a times them out.
    ///   WorldAction / HandCraft / StationCraft — <see cref="ActionRecord.externallyResolved"/>;
    ///       completion is labor-accrual driven (MapEntitySync) or clock-window driven
    ///       (DreamerInventorySync.CheckHandCraftCompletion), and those owners clear the slot.
    /// </summary>
    public enum ActionSlotKind
    {
        Consumable   = 0, // an item consumed from a container (eat / drink / save-consumable)
        Task         = 1, // a self-contained needs task (sleep / rest)
        WorldAction  = 2, // object-bound labor on a world Instance (gather / process / fell)
        HandCraft    = 3, // self-contained craft carried on DreamerRecord.craft
        StationCraft = 4, // object-bound craft carried on Instance.craft
    }
}
