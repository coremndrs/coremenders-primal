using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// Per-map authoritative entity state (TDD §1.11, 0.2.7a).
    /// Serialised to map_{mapId}.json alongside world.json and dreamer files.
    ///
    /// 0.2.9e1: the former groundItems + processables collections are collapsed into a
    /// single <see cref="worldObjects"/> collection — one unified world-object model. A
    /// world Instance's role is read from its location, not a stored type:
    ///   ground item  = InWorld, location.accrual == null, Def has no world actions
    ///   world object = InWorld, Def has world actions (accrual != null when it has timed actions)
    ///   carried      = CarriedBy (excluded from world-visual sync)
    /// nextInstanceId allocates stable ids for every world instance on this map.
    /// The runtime sync profile (full-state) drives all of them; the authored delta-only
    /// profile is 0.2.9f.
    /// </summary>
    public class MapEntityLayer
    {
        public string             mapId;
        public int                nextInstanceId = 1;
        public InstanceCollection worldObjects   = new InstanceCollection();
    }

    /// <summary>
    /// A flat list of world Instances. Replaces the former GroundItemCollection and
    /// ProcessableCollection; see Instance.location to distinguish role at runtime.
    /// </summary>
    public class InstanceCollection
    {
        public List<Instance> items = new List<Instance>();
    }
}
