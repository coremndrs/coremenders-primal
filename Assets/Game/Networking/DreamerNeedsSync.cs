using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Per-dreamer NetworkBehaviour that synchronises needs, energy, Vitality, afflictions,
    /// and incapacitation state to all clients (TDD §0.1.0 / §0.1.1 / §0.1.2).
    ///
    /// Server periodically pushes NetworkVariables from the RDM; clients read them.
    /// The owning client renders the full status HUD via OnGUI (bottom-left).
    ///
    /// Add this component to the dreamer prefab alongside DreamerNetworkAdapter.
    /// </summary>
    public class DreamerNeedsSync : NetworkBehaviour
    {
        [SerializeField] private float _syncIntervalSeconds = 1f;
        [SerializeField] private float _timePoolMax = 1440f;

        // ── Server-authoritative NetworkVariables ─────────────────────────────────

        private readonly NetworkVariable<float> _hunger = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _thirst = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _warmth = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _energy = new NetworkVariable<float>(
            80f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _vitality = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Afflictions
        private readonly NetworkVariable<bool> _isStarving = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDehydrated = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isHypothermic = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isIncapacitated = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Cached references ────────────────────────────────────────────────────

        private DreamerNetworkAdapter  _adapter;
        private DreamerInventorySync   _inventorySync;
        private float                  _syncTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _adapter       = GetComponent<DreamerNetworkAdapter>();
            _inventorySync = GetComponent<DreamerInventorySync>();
            if (IsServer) PushFromRdm();
        }

        // ── Server tick ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;
            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncIntervalSeconds) return;
            _syncTimer = 0f;
            PushFromRdm();
        }

        private void PushFromRdm()
        {
            if (_adapter == null) return;
            var r = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (r == null) return;

            _hunger.Value        = r.needs.hunger;
            _thirst.Value        = r.needs.thirst;
            _warmth.Value        = r.needs.warmth;
            _energy.Value        = r.energy;
            _vitality.Value      = r.vitality;
            _isStarving.Value    = r.afflictions.isStarving;
            _isDehydrated.Value  = r.afflictions.isDehydrated;
            _isHypothermic.Value = r.afflictions.isHypothermic;
            _isIncapacitated.Value = r.isIncapacitated;
        }

        /// <summary>Called by SkipManager after a skip completes to push final state immediately.</summary>
        public void ForcePush()
        {
            if (!IsServer) return;
            PushFromRdm();
        }

        // ── Public accessors ─────────────────────────────────────────────────────

        public float Hunger          => _hunger.Value;
        public float Thirst          => _thirst.Value;
        public float Warmth          => _warmth.Value;
        public float Energy          => _energy.Value;
        public float Vitality        => _vitality.Value;
        public bool  IsIncapacitated => _isIncapacitated.Value;

        // ── HUD — owning client only ─────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;

            float x = 10f;
            float y = Screen.height - 230f;

            DrawNeedRow(x, y,       "Hunger",   _hunger.Value,   100f);
            DrawNeedRow(x, y + 26,  "Thirst",   _thirst.Value,   100f);
            DrawNeedRow(x, y + 52,  "Warmth",   _warmth.Value,   100f);
            DrawNeedRow(x, y + 78,  "Energy",   _energy.Value,   100f);
            DrawNeedRow(x, y + 104, "Vitality", _vitality.Value, 100f);

            float pool = _inventorySync != null ? _inventorySync.TimePool : 0f;
            DrawNeedRow(x, y + 130, "Pool",     pool,            _timePoolMax, "m");

            // Affliction tags
            float tagX = x;
            float tagY = y + 158f;
            if (_isStarving.Value)    { GUI.Label(new Rect(tagX, tagY, 90,  20), "[STARVING]");    tagX += 95f; }
            if (_isDehydrated.Value)  { GUI.Label(new Rect(tagX, tagY, 100, 20), "[DEHYDRATED]");  tagX += 105f; }
            if (_isHypothermic.Value) { GUI.Label(new Rect(tagX, tagY, 110, 20), "[HYPOTHERMIC]"); }

            if (_isIncapacitated.Value)
                GUI.Label(new Rect(x, y + 182, 200, 24), "⚠  DOWNED");
        }

        private static void DrawNeedRow(float x, float y, string label, float value, float max, string unit = "")
        {
            GUI.Label(new Rect(x,      y, 60,  22), $"{label}:");
            GUI.Label(new Rect(x + 60, y, 150, 22), $"{value:F0} / {max:F0}{unit}");
        }
    }
}
