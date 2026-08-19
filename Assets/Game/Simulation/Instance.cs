using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    public enum LocationKind { InContainer, InWorld, CarriedBy }

    /// <summary>
    /// Location tag for a runtime Instance (TDD §1.12, 0.2.9b1).
    /// InContainer: inside a container (containerId = the container Instance's id; 0 for equipped slots).
    /// InWorld:     at a world position; accrual != null when it carries active actions.
    /// CarriedBy:   held by a dreamer (large objects too big for inventory; not yet used).
    /// </summary>
    [Serializable]
    public class InstanceLocation
    {
        public LocationKind          kind        = LocationKind.InContainer;
        public int                   containerId;              // InContainer
        public Float3                position;                 // InWorld / CarriedBy
        public ActionAccrualState[]  accrual;                  // InWorld; null = ground item (no actions)
        public int                   dreamerSlot;              // CarriedBy
    }

    /// <summary>
    /// Unified runtime object instance — item, tool, container, or world object (TDD §1.12, 0.2.9b1).
    /// Replaces the previously separate ItemInstance, WorldItem, and ProcessableInstance types.
    ///
    /// Inventory items:           location.kind = InContainer
    /// Ground items:              location.kind = InWorld, location.accrual = null
    /// Actionable world objects:  location.kind = InWorld, location.accrual = ActionAccrualState[]
    /// Carried objects:           location.kind = CarriedBy
    ///
    /// id = 0 for inventory items (no host-assigned id; managed by slot/index).
    /// id > 0 for world instances (allocated from MapEntityLayer.nextInstanceId).
    /// </summary>
    [Serializable]
    public class Instance
    {
        public int    id;
        public int    defId;
        public float  quantity          = 1f;
        public float? remaining         = null;     // partial consumable
        public InstanceContainer contents;          // non-null when this is a loaded container
        public float  durability;
        // Condition triple (perishable items only; rate = 0 → non-perishable)
        public float conditionAtStamp   = 100f;
        public float stampTime          = 0f;
        public float rate               = 0f;
        // Location
        public InstanceLocation location = new InstanceLocation();
        // Authored node (TDD §5.6.6 / 0.2.9f): materialised from an in-scene Interactable on first
        // interaction. The scene GameObject is the visual (client does NOT spawn one) and its position
        // is local (from the scene), so this is a DELTA record — pristine authored nodes are never
        // materialised, cost nothing, and send no traffic. depleted = harvested → hide the scene GO
        // (kept so the depleted state persists + reverts, instead of removing like a runtime object).
        public bool authored;
        public bool depleted;
        // Active station craft bound to this world object (TDD §5.7.1/§5.7.8, 0.2.10c). null = none.
        // The labor accrues on location.accrual[craft.stationActionIndex]; this carries the recipe
        // identity + RNG-as-state seed inputs so completion rolls the tier (§5.7.3).
        public CraftRecord craft = null;

        // ── Condition API ─────────────────────────────────────────────────────────

        public float ComputeCondition(float now)
        {
            if (rate <= 0f) return 100f;
            return (float)Math.Max(0.0, Math.Min(100.0,
                conditionAtStamp - (now - stampTime) * rate));
        }

        public bool IsSpoiled(float now) => rate > 0f && ComputeCondition(now) <= 0f;

        public void InitConditionTriple(float now, float lifespan, float storageModifier = 1f)
        {
            conditionAtStamp = 100f;
            stampTime        = now;
            rate             = lifespan > 0f ? (100f / lifespan) * storageModifier : 0f;
        }

        public void ReStamp(float now, float newRate)
        {
            conditionAtStamp = ComputeCondition(now);
            stampTime        = now;
            rate             = newRate;
        }

        public void ReStampCondition(float now, float newConditionAtNow)
        {
            conditionAtStamp = (float)Math.Max(0.0, Math.Min(100.0, newConditionAtNow));
            stampTime        = now;
        }

        public bool ApplyUse(float amount)
        {
            durability = (float)Math.Max(0.0, durability - amount);
            return durability <= 0f;
        }
    }

    /// <summary>
    /// Ordered list of Instance items — replaces ItemContainer (TDD §1.12, 0.2.9b1).
    /// Used for container contents in EquipSlots and for nested bags.
    /// </summary>
    public class InstanceContainer
    {
        public List<Instance> items = new List<Instance>();

        /// <summary>
        /// Add inst respecting stacking, condition-window merge, and non-stackable rules.
        /// Mirrors the ItemContainer.TryAdd contract exactly; only the element type changed.
        /// </summary>
        public void TryAdd(Instance inst, IItemDefLookup defs,
                           float now = 0f, SpoilageConfig spoilageConfig = null)
        {
            if (inst.quantity <= 0f) return;

            if (inst.remaining.HasValue) { items.Add(inst); return; }

            if (defs == null || !defs.TryGetDef(inst.defId, out var def)) { items.Add(inst); return; }

            if (!def.stackable)
            {
                items.Add(new Instance
                {
                    defId            = inst.defId,
                    quantity         = 1f,
                    conditionAtStamp = inst.conditionAtStamp,
                    stampTime        = inst.stampTime,
                    rate             = inst.rate,
                    durability       = inst.durability,
                });
                return;
            }

            bool  useTimeWindow = spoilageConfig != null && def.perishable && inst.rate > 0f;
            float incomingCond  = useTimeWindow ? inst.ComputeCondition(now) : 0f;
            float condWindow    = useTimeWindow ? inst.rate * 5f : 0f;
            float remaining     = inst.quantity;

            foreach (var stack in items)
            {
                if (stack.defId != inst.defId) continue;
                if (stack.remaining.HasValue) continue;

                float stackCond = 0f;
                if (useTimeWindow)
                {
                    stackCond = stack.ComputeCondition(now);
                    if (Math.Abs(incomingCond - stackCond) > condWindow) continue;
                }

                float available = def.maxStack - stack.quantity;
                if (available <= 0f) continue;

                float toMerge   = Math.Min(remaining, available);
                stack.quantity += toMerge;
                remaining      -= toMerge;

                if (useTimeWindow && incomingCond > stackCond)
                    stack.ReStampCondition(now, incomingCond);

                if (remaining <= 0f) return;
            }

            while (remaining > 0f)
            {
                float toTake = Math.Min(remaining, def.maxStack);
                items.Add(new Instance
                {
                    defId            = inst.defId,
                    quantity         = toTake,
                    conditionAtStamp = inst.conditionAtStamp,
                    stampTime        = inst.stampTime,
                    rate             = inst.rate,
                    durability       = inst.durability,
                });
                remaining -= toTake;
            }
        }

        public bool Split(int index, float splitQty)
        {
            if (index < 0 || index >= items.Count) return false;
            var stack = items[index];
            if (splitQty <= 0f || splitQty >= stack.quantity) return false;
            stack.quantity -= splitQty;
            items.Insert(index + 1, new Instance
            {
                defId            = stack.defId,
                quantity         = splitQty,
                conditionAtStamp = stack.conditionAtStamp,
                stampTime        = stack.stampTime,
                rate             = stack.rate,
                durability       = stack.durability,
            });
            return true;
        }
    }
}
