namespace Game.Simulation
{
    /// <summary>
    /// Pure-data view of a definition consumed by Simulation code.
    /// The authoritative source is the composed Def SO in the Networking layer;
    /// this struct is projected from its aspects (Def.ToData) via IItemDefLookup so
    /// Simulation stays engine-free.
    /// </summary>
    public struct ItemDefData
    {
        public int          defId;
        public string       displayName;
        public ItemCategory category;
        public float        weight;
        public bool         stackable;
        public int          maxStack;
        // 0.2.2b: consume effect and per-def restoration values
        public ConsumeEffect consumeEffect;
        public float         hungerRestore;
        public float         thirstRestore;
        // 0.2.3: action definition fields
        public float         timeCost;        // IG minutes for the timed action; 0 = instant (legacy)
        public float         energyCost;      // energy drawn from dreamer energy pool at queue time
        public bool          allowsOverdraft; // whether this action may take pool below zero
        public ActionPayout  payoutShape;     // Gradual (buff) or EndEffect (fires at completion)
        // 0.2.4a — spoilage
        public bool          perishable;
        public float         effectiveLifespan; // IG minutes until fully spoiled at ambient storage; 0 = never
        // 0.2.5a — container def
        public bool          isContainer;
        public int           slotCapacity;      // max item stacks inside (0 = unlimited)
        public float         weightCapacity;    // max total contents weight in kg (0 = unlimited)
        public ItemTypeTag   typeFilter;        // items must match this tag to enter (None = accept all)
        public float         spoilageModifier;  // perishable rate multiplier for stored items (1 = ambient)
        public EquipSlotType equipsIntoSlot;    // which dreamer equip slot this container fits in
        // 0.2.5b — physical + type
        public ItemTypeTag   itemTypeTag;       // what this item IS (matched against container typeFilter)
        // 0.2.6: durability tool
        public bool  isDurabilityTool;
        public float maxDurability;            // starting and maximum durability; 0 = not applicable
        // 0.2.8: tool category for gathering/processing actions
        // 0 = not a tool; positive int = matches the matching GatherableActionDef.allowedToolCategoryIds entry.
        // Category ids are project-defined stable ints (e.g. 1=Axe, 2=Saw, 3=Knife).
        public int   toolCategoryId;
    }
}
