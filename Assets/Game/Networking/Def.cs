using System;
using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Unified ScriptableObject definition for any world object — item, tool, container,
    /// gatherable resource, or multi-role composite (TDD §1.12, 0.2.9a1).
    ///
    /// Replaces the previously separate ItemDef and GatherableObjectDef.
    /// What the object IS = its aspects (embedded [Serializable] data; only relevant ones).
    /// What you can DO to it = its actions (references to shared ActionDef SOs).
    ///
    /// The defId namespace is shared across all Defs — never assign the same defId to two Defs.
    /// </summary>
    [CreateAssetMenu(fileName = "Def_New", menuName = "Coremenders/Def")]
    public class Def : ScriptableObject
    {
        [Tooltip("Stable numeric id — never change after authoring. Used on the wire and in saves.")]
        public int        defId;
        public string     displayName;
        [Tooltip("World-visual prefab; also used as ground-item visual when the instance is in the world.")]
        public GameObject prefab;

        [Header("Aspects — what this object IS")]
        [Tooltip("Add only the aspects this object has. Each aspect type may appear at most once.")]
        [SerializeReference]
        public List<DefAspect> aspects = new List<DefAspect>();

        [Header("Actions — what you can DO to it")]
        [Tooltip("Ordered list of per-item action bindings. Each names a generic verb (ActionDef) and " +
                 "carries THIS item's labor / yields / costs / rewards / tool. Index order is preserved; " +
                 "actionIndex in accrual state maps here.")]
        public ItemAction[] actions = Array.Empty<ItemAction>();

        // ── Aspect accessors ──────────────────────────────────────────────────────

        public T GetAspect<T>() where T : DefAspect
        {
            foreach (var a in aspects)
                if (a is T t) return t;
            return null;
        }

        public bool HasAspect<T>() where T : DefAspect => GetAspect<T>() != null;

        // ── Aspect-backed convenience accessors (0.2.9d) ──────────────────────────
        // These mirror the former ItemDef fields so item-system readers (inventory,
        // consume, spoilage, containers, durability) resolve everything through this
        // one composed Def, reading the relevant aspect. Behaviour is identical to the
        // pre-refactor ItemDef field reads; a Def missing an aspect returns the field's
        // natural default. `groundPrefab` aliases `prefab` (the unified world visual).

        public GameObject    groundPrefab      => prefab;

        // Inventory aspect
        public ItemCategory  category          => GetAspect<InventoryAspect>()?.category    ?? default;
        public float         weight            => GetAspect<InventoryAspect>()?.weight      ?? 0f;
        public bool          stackable         => GetAspect<InventoryAspect>()?.stackable   ?? false;
        public int           maxStack          => GetAspect<InventoryAspect>()?.maxStack    ?? 1;
        public ItemTypeTag   itemTypeTag       => GetAspect<InventoryAspect>()?.itemTypeTag ?? default;

        // Consumable aspect
        public ConsumeEffect consumeEffect     => GetAspect<ConsumableAspect>()?.consumeEffect   ?? default;
        public float         hungerRestore     => GetAspect<ConsumableAspect>()?.hungerRestore   ?? 0f;
        public float         thirstRestore     => GetAspect<ConsumableAspect>()?.thirstRestore   ?? 0f;
        public float         timeCost          => GetAspect<ConsumableAspect>()?.timeCost        ?? 0f;
        public float         energyCost        => GetAspect<ConsumableAspect>()?.energyCost      ?? 0f;
        public bool          allowsOverdraft   => GetAspect<ConsumableAspect>()?.allowsOverdraft ?? false;
        public ActionPayout  payoutShape       => GetAspect<ConsumableAspect>()?.payoutShape     ?? default;
        public bool          interruptsSkip    => GetAspect<ConsumableAspect>()?.interruptsSkip  ?? false;

        // Perishable aspect
        public bool          perishable        => HasAspect<PerishableAspect>();
        public float         effectiveLifespan => GetAspect<PerishableAspect>()?.effectiveLifespan ?? 0f;

        // Container aspect
        public bool          isContainer       => HasAspect<ContainerAspect>();
        public int           slotCapacity      => GetAspect<ContainerAspect>()?.slotCapacity   ?? 0;
        public float         weightCapacity    => GetAspect<ContainerAspect>()?.weightCapacity ?? 0f;
        public ItemTypeTag   typeFilter        => GetAspect<ContainerAspect>()?.typeFilter     ?? default;
        public EquipSlotType equipsIntoSlot    => GetAspect<ContainerAspect>()?.equipsIntoSlot ?? default;
        public float         spoilageModifier
        {
            get
            {
                var c = GetAspect<ContainerAspect>();
                return c != null ? (c.spoilageModifier > 0f ? c.spoilageModifier : 1f) : 1f;
            }
        }

        // Tool aspect
        public bool          isDurabilityTool  => GetAspect<ToolAspect>()?.isDurabilityTool ?? false;
        public float         maxDurability     => GetAspect<ToolAspect>()?.maxDurability    ?? 0f;
        public int           toolCategoryId    => GetAspect<ToolAspect>()?.toolCategoryId   ?? 0;
        public int           brokenFormDefId   => GetAspect<ToolAspect>()?.brokenFormDefId  ?? 0;

        // Weapon aspect (combat seam — inert until §15 combat; 0.2.10)
        public bool          isWeapon          => HasAspect<WeaponAspect>();
        public int           attackTier        => GetAspect<WeaponAspect>()?.attackTier ?? 0;
        public float         totalDamage       => GetAspect<WeaponAspect>()?.TotalDamage ?? 0f;

        /// <summary>True when this Def presents any world-context action (chop, fell, pickup, carry).</summary>
        public bool HasWorldActions()
        {
            if (actions == null) return false;
            foreach (var a in actions)
                if (a?.action != null && (a.context & ActionContext.World) != 0) return true;
            return false;
        }

        // ── IItemDefLookup bridge ─────────────────────────────────────────────────

        /// <summary>
        /// Produces the flat ItemDefData view consumed by Simulation code.
        /// The bridge allows SimResolver and other Simulation systems to remain
        /// unchanged through the 0.2.9 refactor.
        /// </summary>
        public ItemDefData ToData()
        {
            var inv  = GetAspect<InventoryAspect>();
            var con  = GetAspect<ConsumableAspect>();
            var per  = GetAspect<PerishableAspect>();
            var tool = GetAspect<ToolAspect>();
            var ctr  = GetAspect<ContainerAspect>();

            return new ItemDefData
            {
                defId             = defId,
                displayName       = displayName,
                category          = inv?.category          ?? default,
                weight            = inv?.weight            ?? 0f,
                stackable         = inv?.stackable         ?? false,
                maxStack          = inv?.maxStack          ?? 1,
                itemTypeTag       = inv?.itemTypeTag       ?? default,
                consumeEffect     = con?.consumeEffect     ?? default,
                hungerRestore     = con?.hungerRestore     ?? 0f,
                thirstRestore     = con?.thirstRestore     ?? 0f,
                timeCost          = con?.timeCost          ?? 0f,
                energyCost        = con?.energyCost        ?? 0f,
                allowsOverdraft   = con?.allowsOverdraft   ?? false,
                payoutShape       = con?.payoutShape       ?? default,
                perishable        = per != null,
                effectiveLifespan = per?.effectiveLifespan ?? 0f,
                isContainer       = ctr != null,
                slotCapacity      = ctr?.slotCapacity      ?? 0,
                weightCapacity    = ctr?.weightCapacity    ?? 0f,
                typeFilter        = ctr?.typeFilter        ?? default,
                spoilageModifier  = ctr != null ? (ctr.spoilageModifier > 0f ? ctr.spoilageModifier : 1f) : 1f,
                equipsIntoSlot    = ctr?.equipsIntoSlot    ?? default,
                isDurabilityTool  = tool?.isDurabilityTool ?? false,
                maxDurability     = tool?.maxDurability    ?? 0f,
                toolCategoryId    = tool?.toolCategoryId   ?? 0,
            };
        }
    }
}
