using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Singleton registry for all Def SO assets (TDD §1.12, 0.2.9a4).
    ///
    /// Replaces the previously separate ItemDefRegistry and GatherableObjectDefRegistry.
    /// All Defs share one defId namespace — no two Defs may have the same defId.
    ///
    /// Implements IItemDefLookup so Simulation code (SimResolver, spoilage, etc.) can read
    /// flat ItemDefData without a Unity engine dependency. The bridge is Def.ToData().
    ///
    /// Place this MonoBehaviour in the Action scene (or a DontDestroyOnLoad manager).
    /// Populate the _defs list in the Inspector with all authored Def assets.
    /// </summary>
    public class DefRegistry : MonoBehaviour, IItemDefLookup
    {
        public static DefRegistry Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [SerializeField] private Def[] _defs = System.Array.Empty<Def>();

        private readonly Dictionary<int, Def> _byId = new Dictionary<int, Def>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _byId.Clear();
            foreach (var def in _defs)
            {
                if (def == null) continue;
                if (_byId.ContainsKey(def.defId))
                    Debug.LogWarning($"[DefRegistry] Duplicate defId {def.defId} — '{def.displayName}' ignored (already registered as '{_byId[def.defId].displayName}').");
                else
                    _byId[def.defId] = def;
            }
            Debug.Log($"[DefRegistry] Built with {_byId.Count} def(s).");
        }

        // ── IItemDefLookup (Simulation-facing) ────────────────────────────────────

        public bool TryGetDef(int defId, out ItemDefData data)
        {
            if (_byId.TryGetValue(defId, out var def)) { data = def.ToData(); return true; }
            data = default;
            return false;
        }

        public ItemDefData GetDef(int defId)
        {
            TryGetDef(defId, out var data);
            return data;
        }

        // ── SO accessor ───────────────────────────────────────────────────────────

        /// <summary>Returns the Def SO for the given defId, or null if not registered.</summary>
        public Def Get(int defId) => _byId.TryGetValue(defId, out var def) ? def : null;

        /// <summary>All registered defs in inspector order.</summary>
        public Def[] AllDefs => _defs;

        // ── Action context filter (TDD §1.12, 0.2.9c4) ───────────────────────────

        /// <summary>
        /// Returns all ItemAction bindings on the given Def whose verb matches the requested context
        /// flags, paired with their index in the Def's actions array.
        /// Returns an empty sequence if the defId is not registered.
        /// </summary>
        public System.Collections.Generic.IEnumerable<(int index, ItemAction action)>
            GetActions(int defId, ActionContext ctx)
        {
            if (!_byId.TryGetValue(defId, out var def)) yield break;
            for (int i = 0; i < def.actions.Length; i++)
            {
                var a = def.actions[i];
                if (a?.action != null && (a.context & ctx) != 0)
                    yield return (i, a);
            }
        }
    }
}
