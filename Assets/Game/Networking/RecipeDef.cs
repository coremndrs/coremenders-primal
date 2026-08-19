using System;
using UnityEngine;

namespace Game.Networking
{
    // ── Recipe sub-records ────────────────────────────────────────────────────────

    /// <summary>
    /// One material a recipe consumes (TDD §5.7.0, 0.2.10a). Ids-not-strings (rule 8): the item
    /// is referenced by Def.defId. Consumption/refund code ITERATES the inputs list — it never
    /// reads numbered fields — so 2 → 5 materials is pure authoring, no code change (rule 9).
    /// </summary>
    [Serializable]
    public class RecipeInput
    {
        [Tooltip("The material consumed (pick by name; stores its Def.defId).")]
        [DefId(DefIdFilter.Item)]
        public int   itemDefId;
        [Tooltip("Quantity consumed per craft (reserved on commit, refunded on cancel).")]
        public float amount = 1f;
    }

    /// <summary>
    /// One entry in a recipe's tier table (TDD §5.7.2, 0.2.10b1). A tier IS a complete output Def
    /// (its own stats/aspects/prefab/icon) — variety without shattering stacks, because same-tier
    /// units are the same Def and therefore stack. Adding a tier = author a Def + add one entry.
    /// The low entry can be a Broken Def (§5.7.5) with a small weight — nothing special-cased.
    /// </summary>
    [Serializable]
    public class TierEntry
    {
        [Tooltip("The output produced when this tier is rolled (pick by name; stores its Def.defId).")]
        [DefId(DefIdFilter.Item)]
        public int   outputDefId;
        [Tooltip("Relative weight in the (unskilled) distribution. Skill reshapes this at Cluster 7.")]
        public float weight = 1f;
    }

    /// <summary>Whether a multi-unit craft rolls a tier per unit or once for the whole batch (§5.7.3).</summary>
    public enum RollGranularity
    {
        PerUnit,   // f(craftSeed, unitIndex) per unit → a realistic tier spread → a few clean stacks
        PerBatch,  // f(craftSeed) once, all N identical → one stack (bulk, low-stakes outputs)
    }

    // ── RecipeDef ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A craftable recipe (TDD §5.7.0). Authoring a new craftable is a new RecipeDef (+ its output
    /// Defs) — no code. Crafting is a recipe-driven configuration of the §5.5/§5.6 task engine, not
    /// a new system: hand crafts run as a self-contained §5.5 task on the dreamer; station crafts run
    /// as an object-bound §5.6.5 task on the required station.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_New", menuName = "Coremenders/Recipe")]
    public class RecipeDef : ScriptableObject
    {
        [Tooltip("Stable numeric id — never change after authoring. Used on the wire and in the craft record.")]
        public int    recipeId;
        public string displayName;

        [Header("Materials (iterated list — 2 or 5 works with no code change)")]
        [Tooltip("Materials consumed on commit; refunded on cancel. Iterated, never numbered fields.")]
        public RecipeInput[] inputs = Array.Empty<RecipeInput>();

        [Header("Requirement gates (§5.7.1)")]
        [Tooltip("Tool category required to craft (1=Axe 2=Saw 3=Knife 4=Pickaxe). 0 = hand-craftable (no tool).")]
        public int requiredToolCategoryId;
        [Tooltip("The station this craft must run on (pick by name). None = anywhere (hand/tool). Set → object-bound station craft.")]
        [DefId(DefIdFilter.WorldObject)]
        public int requiredStationDefId;

        [Header("Cost — hand craft (self-contained §5.5 task)")]
        [Tooltip("IG minutes the hand craft takes; also the timePool cost debited up front.")]
        public float timeCost;
        [Tooltip("Energy debited up front for a hand craft; refunded pro-rata on cancel.")]
        public float energyCost;

        [Header("Cost — station craft (object-bound §5.6.5 task)")]
        [Tooltip("Total labor to complete the station craft (overrides the station action's timeRequired).")]
        public float requiredLabor;
        [Tooltip("Energy per committed labor-minute for a station craft (seam; reserved via the §5.5 engine).")]
        public float energyPerMinute;

        [Header("Co-op / unlock")]
        [Tooltip("§5.6.4: true = combined-labor accrual (two dreamers assist); false = single-crafter lock.")]
        public bool coop;
        [Tooltip("Discovery gate id — INERT now (starting recipes are known); activated at Cluster 9. 0 = known.")]
        public int  unlockConditionId;

        [Header("Output (§5.7.2 tier table + §5.7.3 roll granularity)")]
        [Tooltip("Number of output units produced per craft (a batch of 20 arrows = 20).")]
        public int          outputAmount = 1;
        [Tooltip("Weighted list of output Defs. Rolled at COMPLETION (§5.7.3). One entry = a fixed output.")]
        public TierEntry[]  tierTable = Array.Empty<TierEntry>();
        [Tooltip("PerUnit rolls each unit (a spread of stacks); PerBatch rolls once (one uniform stack).")]
        public RollGranularity rollGranularity = RollGranularity.PerBatch;

        // ── Gate helpers ───────────────────────────────────────────────────────────

        /// <summary>Hand-craftable when no station is required (§5.7.1). Tool may still be required.</summary>
        public bool IsHandCraft   => requiredStationDefId == 0;
        /// <summary>Recipe is discoverable now (unlocks inert until Cluster 9).</summary>
        public bool IsKnown       => true;
    }
}
