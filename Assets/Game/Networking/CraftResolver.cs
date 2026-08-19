using System.Collections.Generic;
using Game.Simulation;

namespace Game.Networking
{
    /// <summary>
    /// Rolls a craft's output tier(s) at completion and builds the output Instances (TDD §5.7.2/§5.7.3,
    /// 0.2.10b). Pure of delivery — the caller routes the returned Instances to inventory (hand /
    /// station-to-crafter) or to the ground (§5.7.1). Shared by the hand-craft path
    /// (DreamerInventorySync) and the station path (MapEntitySync), so both re-derive the identical
    /// output from the same saved <see cref="CraftRecord"/>.
    ///
    /// The roll is a pure function of the saved craft record (RNG-as-state, §5.7.3): a skip across
    /// completion and a replay produce the same tiers; a revert-then-recraft (new startTime → new
    /// seed) is a new roll. Same-tier units are the same Def, so they stack (§5.7.2 / b6).
    /// </summary>
    public static class CraftResolver
    {
        /// <summary>
        /// Builds the output Instances for a completed craft — no delivery, no world mutation.
        /// PerBatch → one roll → one stack of outputAmount; PerUnit → a roll per unit, grouped by
        /// tier into a small number of clean per-tier stacks. Empty if the recipe has no tier table.
        /// </summary>
        public static List<Instance> BuildOutputs(RecipeDef recipe, CraftRecord rec, float now)
        {
            var outputs = new List<Instance>();
            if (recipe == null || rec == null || recipe.tierTable == null || recipe.tierTable.Length == 0)
                return outputs;

            int seed = CraftRng.Seed(rec.crafterSlot, rec.recipeId, rec.startTime, rec.craftCounter);

            var weights = new float[recipe.tierTable.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = recipe.tierTable[i].weight;

            int amount = rec.outputAmount > 0 ? rec.outputAmount : 1;

            if ((RollGranularity)rec.rollGranularity == RollGranularity.PerBatch)
            {
                int tier = CraftRng.TierIndex(seed, 0, weights);
                AddOutput(outputs, recipe.tierTable[tier].outputDefId, amount, now);
            }
            else // PerUnit
            {
                // Group same-tier units into stacks (a PerUnit batch forms a few tier stacks, not N
                // uniques). Preserve tier order so the stacks read predictably. b6.
                var perTier = new int[recipe.tierTable.Length];
                for (int u = 0; u < amount; u++)
                    perTier[CraftRng.TierIndex(seed, u, weights)]++;

                for (int t = 0; t < perTier.Length; t++)
                    if (perTier[t] > 0)
                        AddOutput(outputs, recipe.tierTable[t].outputDefId, perTier[t], now);
            }

            return outputs;
        }

        /// <summary>Builds one output stack Instance, initialising perishable/durability from its Def.</summary>
        private static void AddOutput(List<Instance> list, int defId, float amount, float now)
        {
            if (amount <= 0f) return;
            var def  = DefRegistry.Instance?.Get(defId);
            var inst = new Instance { defId = defId, quantity = amount };
            if (def != null && def.perishable && def.effectiveLifespan > 0f)
                inst.InitConditionTriple(now, def.effectiveLifespan);
            if (def != null && def.isDurabilityTool && def.maxDurability > 0f)
                inst.durability = def.maxDurability;
            list.Add(inst);
        }
    }
}
