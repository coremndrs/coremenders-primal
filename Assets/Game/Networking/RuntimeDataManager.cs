using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-side authoritative state store (TDD §1.3).
    /// Holds the WorldState and the slot→clientId ownership map.
    /// Game logic does not live here — systems operate on the state this holds.
    /// </summary>
    public class RuntimeDataManager : MonoBehaviour
    {
        public static RuntimeDataManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private WorldState _world;
        private readonly Dictionary<string, MapEntityLayer> _mapLayers         = new Dictionary<string, MapEntityLayer>();
        private readonly Dictionary<int, ulong>             _ownership         = new Dictionary<int, ulong>();
        private readonly Dictionary<int, Transform>         _dreamerTransforms = new Dictionary<int, Transform>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Populate(WorldState world)
        {
            _world = world;
            _mapLayers.Clear();
            _ownership.Clear();
            _dreamerTransforms.Clear();
            Debug.Log($"[RDM] Populated — map '{world.mapId}', {world.dreamers.Length} dreamers");
        }

        // ── Map Entity Layer store (0.2.7a) ─────────────────────────────────────

        public void SetMapLayer(MapEntityLayer layer)
        {
            if (layer == null) return;
            _mapLayers[layer.mapId ?? ""] = layer;
        }

        public MapEntityLayer GetMapLayer(string mapId) =>
            _mapLayers.TryGetValue(mapId ?? "", out var l) ? l : null;

        /// <summary>Convenience: the layer for the currently loaded map.</summary>
        public MapEntityLayer CurrentMapLayer => GetMapLayer(_world?.mapId);

        public WorldState WorldState => _world;

        public DreamerRecord GetDreamer(int slot) => _world?.dreamers[slot];

        public void SetOwnership(int slot, ulong clientId)
        {
            _ownership[slot] = clientId;
            Debug.Log($"[RDM] Slot {slot} → client {clientId}");
        }

        public ulong GetOwner(int slot) =>
            _ownership.TryGetValue(slot, out var id) ? id : ulong.MaxValue;

        public bool TryGetSlotForClient(ulong clientId, out int slot)
        {
            foreach (var kv in _ownership)
            {
                if (kv.Value == clientId) { slot = kv.Key; return true; }
            }
            slot = -1;
            return false;
        }

        // ── Live transform registry (TDD §1.5) ──────────────────────────────────
        // Holds a reference to each spawned dreamer's Transform so the save/snapshot
        // system can sample the live position on demand without a per-frame write.

        public void RegisterDreamerTransform(int slot, Transform t)
        {
            _dreamerTransforms[slot] = t;
        }

        public void UnregisterDreamerTransform(int slot)
        {
            _dreamerTransforms.Remove(slot);
        }

        /// <summary>Returns the dreamer's current world position, or null if not spawned.</summary>
        public Vector3? GetDreamerPosition(int slot) =>
            _dreamerTransforms.TryGetValue(slot, out var t) && t != null
                ? (Vector3?)t.position
                : null;
    }
}
