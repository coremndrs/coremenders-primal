using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// Dreamer equip slots (0.2.5a): backpack + two pouches + quiver.
    /// Each slot holds at most one container Instance (null = empty).
    /// Items belong to a container's contents, not directly to the dreamer.
    /// Weight rollup is one level deep: container own weight + sum(direct contents weights).
    /// </summary>
    [Serializable]
    public class EquipSlots
    {
        public Instance backpack;
        public Instance pouch0;
        public Instance pouch1;
        public Instance quiver;

        public Instance GetSlot(EquipSlotType type) => type switch
        {
            EquipSlotType.Backpack => backpack,
            EquipSlotType.Pouch0   => pouch0,
            EquipSlotType.Pouch1   => pouch1,
            EquipSlotType.Quiver   => quiver,
            _                      => null,
        };

        public void SetSlot(EquipSlotType type, Instance inst)
        {
            switch (type)
            {
                case EquipSlotType.Backpack: backpack = inst; break;
                case EquipSlotType.Pouch0:   pouch0   = inst; break;
                case EquipSlotType.Pouch1:   pouch1   = inst; break;
                case EquipSlotType.Quiver:   quiver   = inst; break;
            }
        }

        public IEnumerable<(EquipSlotType type, Instance inst)> AllOccupied()
        {
            if (backpack != null) yield return (EquipSlotType.Backpack, backpack);
            if (pouch0   != null) yield return (EquipSlotType.Pouch0,   pouch0);
            if (pouch1   != null) yield return (EquipSlotType.Pouch1,   pouch1);
            if (quiver   != null) yield return (EquipSlotType.Quiver,   quiver);
        }

        public float ComputeCarriedWeight(IItemDefLookup defs)
        {
            float total = 0f;
            foreach (var (_, container) in AllOccupied())
            {
                if (defs.TryGetDef(container.defId, out var cDef))
                    total += cDef.weight * container.quantity;
                if (container.contents == null) continue;
                foreach (var item in container.contents.items)
                    if (defs.TryGetDef(item.defId, out var iDef))
                        total += iDef.weight * item.quantity;
            }
            return total;
        }
    }
}
