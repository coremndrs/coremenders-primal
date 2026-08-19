using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    // ── Aspect base ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Base class for all per-Def aspect data (TDD §1.12, 0.2.9a2).
    /// Aspects describe what an object IS; they are embedded inside a Def SO via
    /// [SerializeReference] so the Def stays composed rather than wide.
    /// All concrete aspects are pure C# — no UnityEngine references — so they
    /// can live in the Simulation assembly without violating noEngineReferences.
    /// </summary>
    [Serializable]
    public abstract class DefAspect { }

    // ── Concrete aspects ──────────────────────────────────────────────────────────

    /// <summary>
    /// The object can live in a dreamer's inventory as an item stack.
    /// Carries the fields that govern stacking, weight, and type-matching.
    /// </summary>
    [Serializable]
    public class InventoryAspect : DefAspect
    {
        public ItemCategory  category;
        public float         weight;
        public bool          stackable;
        public int           maxStack    = 64;
        public ItemTypeTag   itemTypeTag;
    }

    /// <summary>
    /// The object can be consumed (eaten, drunk, used).
    /// Carries nourishment values, timing, energy cost, and payout shape.
    /// </summary>
    [Serializable]
    public class ConsumableAspect : DefAspect
    {
        public ConsumeEffect consumeEffect;
        public float         hungerRestore;
        public float         thirstRestore;
        public float         timeCost;         // IG minutes; 0 = instant
        public float         energyCost;       // drawn from daily energy pool at queue time
        public bool          allowsOverdraft;
        public ActionPayout  payoutShape;
        public bool          interruptsSkip;
    }

    /// <summary>
    /// The object degrades over time. Presence of this aspect means the item is
    /// perishable; absence means it never spoils (rate = 0 in the condition triple).
    /// </summary>
    [Serializable]
    public class PerishableAspect : DefAspect
    {
        public float effectiveLifespan; // IG minutes from fresh (100%) to spoiled (0%) at ambient
    }

    /// <summary>
    /// The object is a tool with durability and a stable category id used by
    /// ActionDef.allowedToolCategoryIds to gate or bonus gathering actions.
    /// </summary>
    [Serializable]
    public class ToolAspect : DefAspect
    {
        public int   toolCategoryId;    // 1=Axe  2=Saw  3=Knife  4=Pickaxe
        public bool  isDurabilityTool;  // per-instance durability tracked
        public float maxDurability;     // starting and maximum durability; 0 = not applicable
        // Broken form (TDD §5.7.6, 0.2.10d): the Def.defId this tool becomes when durability hits 0
        // (destroy-and-replace in place). 0 = no broken form (tool just wears out with no replacement).
        public int   brokenFormDefId;
    }

    /// <summary>
    /// One typed damage contribution of a weapon (TDD §15 seam, 0.2.10). A weapon lists several of
    /// these — a sword: slash + pierce + a little bash; an axe: slash + bash; an arrow: pierce only.
    /// Iterated, never numbered fields (CLAUDE.md rule 9), so adding a damage type is pure authoring.
    /// </summary>
    [Serializable]
    public class WeaponDamage
    {
        public DamageType type;
        public float      amount;
    }

    /// <summary>
    /// The object is a weapon — it deals combat damage (TDD §15 combat, deferred; the data seam lands
    /// at 0.2.10 so weapons can be authored + crafted now). Combat itself is not yet wired, so these
    /// values are inert until Cluster 15/§15 combat resolution reads them.
    ///
    /// Durability is NOT here — a weapon reuses <see cref="ToolAspect"/> for wear + broken form (already
    /// wired: durability display, destroy-and-replace, etc.). A pure weapon sets toolCategoryId = 0
    /// (not a gathering tool) but isDurabilityTool = true; a dual-use axe sets a gathering category too.
    /// </summary>
    [Serializable]
    public class WeaponAspect : DefAspect
    {
        public int              attackTier;                        // vs a target's defense tier (§5.6.1 / §15)
        public List<WeaponDamage> damage = new List<WeaponDamage>(); // one entry per damage type dealt
        public float            reach;                             // melee reach (metres)
        public bool             isRanged;                          // bow / thrown / projectile

        // Impact wear (ammo / thrown — arrows, javelins). Stack-friendly: a fired unit (quantity 1)
        // either survives on impact and drops as a recoverable copy of this Def, or breaks. Modelled as
        // a per-impact CHANCE (not a durability pool, which would shatter stacks). The broken result is
        // ToolAspect.brokenFormDefId on the same Def (add a ToolAspect with isDurabilityTool = false just
        // to name the broken form) — 0 = destroyed. Inert until §15 combat/projectiles resolve impacts.
        public bool             wearsOnImpact;
        public float            impactBreakChance;                 // 0..1 (0 = never breaks; 1 = single-use)

        /// <summary>Total damage across all types (convenience; combat may weight by type later).</summary>
        public float TotalDamage
        {
            get { float t = 0f; if (damage != null) foreach (var d in damage) t += d.amount; return t; }
        }
    }

    /// <summary>
    /// The object can hold other items.
    /// Carries capacity, filter, spoilage modifier, and which equip slot it fits in.
    /// </summary>
    [Serializable]
    public class ContainerAspect : DefAspect
    {
        public int           slotCapacity;
        public float         weightCapacity;
        public ItemTypeTag   typeFilter;
        public float         spoilageModifier = 1f;
        public EquipSlotType equipsIntoSlot;
    }

    // ── Action vocabulary ─────────────────────────────────────────────────────────

    /// <summary>
    /// What an ActionDef does to the Instance when it completes (TDD §1.12, 0.2.9a1).
    /// ToInventory — move the instance into the contributor's container (pickup).
    /// Transform   — consume the instance and spawn its yields at the world position.
    /// Carry       — transition the instance to CarriedBy(dreamerId).
    /// Consume     — apply consumable effect and decrement quantity; no world change.
    /// </summary>
    public enum ActionOutcome
    {
        ToInventory,
        Transform,
        Carry,
        Consume,
    }

    /// <summary>
    /// Where an action is presented to the player (TDD §1.12, 0.2.9c).
    /// World     — action shown when the instance is in the world (chop, pickup, carry).
    /// Inventory — action shown when the instance is in a container (consume, equip, drop).
    /// </summary>
    [Flags]
    public enum ActionContext
    {
        World     = 1 << 0,
        Inventory = 1 << 1,
    }
}
