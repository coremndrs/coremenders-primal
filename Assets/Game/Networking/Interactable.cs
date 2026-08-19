using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Placed on any world prefab that can be interacted with (TDD §1.12, 0.2.9c2).
    ///
    /// For authored scene objects: set Def in the Inspector; id is baked in 0.2.9d.
    /// For runtime-spawned processables: MapEntitySync sets instanceId and def after Instantiate.
    ///
    /// WorldActions() / InventoryActions() filter this Def's action list by context.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Tooltip("The Def SO defining this object's identity and actions.")]
        public Def def;

        /// <summary>Stable world instance id. Baked by the id-baking tool for authored scene nodes
        /// (0.2.9f); set at spawn time by MapEntitySync for runtime-spawned objects.</summary>
        [HideInInspector] public int instanceId;

        /// <summary>
        /// True for an authored, in-scene node (TDD §5.6.6 / 0.2.9f): the id-baking tool sets this
        /// (and the baked <see cref="instanceId"/>). An authored node self-registers with the host so
        /// its mutable state materialises on first interaction (delta-only; pristine costs nothing),
        /// and the scene GameObject IS the visual (MapEntitySync never spawns one for it). Runtime
        /// objects leave this false.
        /// </summary>
        public bool authored;

        // Self-registration: authored nodes register with MapEntitySync's static registry so the host
        // can resolve interactions (materialise the delta) and clients can bind the scene visual by
        // baked id. Runtime-spawned Interactables (authored = false) are ignored.
        private void OnEnable()  => MapEntitySync.RegisterAuthored(this);
        private void OnDisable() => MapEntitySync.UnregisterAuthored(this);

        /// <summary>Returns all ItemAction bindings whose verb has World context (shown in the world).</summary>
        public IEnumerable<(int index, ItemAction action)> WorldActions()
        {
            if (def?.actions == null) yield break;
            for (int i = 0; i < def.actions.Length; i++)
            {
                var a = def.actions[i];
                if (a?.action != null && (a.context & ActionContext.World) != 0)
                    yield return (i, a);
            }
        }

        /// <summary>Returns all ItemAction bindings whose verb has Inventory context (shown in a container).</summary>
        public IEnumerable<(int index, ItemAction action)> InventoryActions()
        {
            if (def?.actions == null) yield break;
            for (int i = 0; i < def.actions.Length; i++)
            {
                var a = def.actions[i];
                if (a?.action != null && (a.context & ActionContext.Inventory) != 0)
                    yield return (i, a);
            }
        }
    }
}
