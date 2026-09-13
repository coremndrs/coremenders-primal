using Game.Simulation;
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
        // ── Static slot registry (0.2.11e1) ──────────────────────────────────────
        // Every peer registers every dreamer's sync component, not just its own: the partner status
        // element needs to read the OTHER slot's replicated state, and all of these NetworkVariables
        // are already read-permission Everyone.

        private static readonly System.Collections.Generic.Dictionary<int, DreamerNeedsSync> _bySlot =
            new System.Collections.Generic.Dictionary<int, DreamerNeedsSync>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _bySlot.Clear();

        public static DreamerNeedsSync GetForSlot(int slot) =>
            _bySlot.TryGetValue(slot, out var s) ? s : null;

        [SerializeField] private float _syncIntervalSeconds = 1f;
        [SerializeField] private float _timePoolMax = 1440f;

        [Tooltip("Draw the partner status element (0.2.11e). Owner-only; hidden in solo play, where " +
                 "the second dreamer has no player to coordinate with.")]
        [SerializeField] private bool _showPartnerStatus = true;

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
        private DreamerTaskSync        _taskSync;
        private NeedsConfig            _needsConfig;
        private float                  _syncTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _adapter       = GetComponent<DreamerNetworkAdapter>();
            _inventorySync = GetComponent<DreamerInventorySync>();
            _taskSync      = GetComponent<DreamerTaskSync>();
            if (_adapter != null) _bySlot[_adapter.Slot] = this;
            if (IsServer) PushFromRdm();
        }

        public override void OnNetworkDespawn()
        {
            if (_adapter != null && _bySlot.TryGetValue(_adapter.Slot, out var s) && s == this)
                _bySlot.Remove(_adapter.Slot);
            base.OnNetworkDespawn();
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
            float y = Screen.height - 262f;

            DrawNeedRow(x, y,       "Hunger",   _hunger.Value,   100f);
            DrawNeedRow(x, y + 26,  "Thirst",   _thirst.Value,   100f);
            DrawNeedRow(x, y + 52,  "Warmth",   _warmth.Value,   100f);
            DrawNeedRow(x, y + 78,  "Energy",   _energy.Value,   100f);
            DrawNeedRow(x, y + 104, "Vitality", _vitality.Value, 100f);

            float pool = _inventorySync != null ? _inventorySync.TimePool : 0f;
            DrawNeedRow(x, y + 130, "Pool",     pool,            _timePoolMax, "m");

            // Trivial bracket (§5.5.5, 0.2.11d7) — drawn immediately under the pool but in a
            // distinct colour and with its own unit label, because these minutes are NOT
            // interchangeable with pool minutes: they buy maintenance only. Reading them as
            // spendable-on-work is exactly the misunderstanding the separate row prevents.
            float bracket = _inventorySync != null ? _inventorySync.TrivialBracket : 0f;
            var   savedColor = GUI.color;
            GUI.color = bracket > 0f ? new Color(0.7f, 0.95f, 0.7f) : new Color(0.8f, 0.6f, 0.6f);
            GUI.Label(new Rect(x,      y + 156, 60,  22), "Trivial:");
            GUI.Label(new Rect(x + 60, y + 156, 170, 22), $"{bracket:F0}m  (maintenance only)");
            GUI.color = savedColor;

            // Affliction tags
            float tagX = x;
            float tagY = y + 182f;
            if (_isStarving.Value)    { GUI.Label(new Rect(tagX, tagY, 90,  20), "[STARVING]");    tagX += 95f; }
            if (_isDehydrated.Value)  { GUI.Label(new Rect(tagX, tagY, 100, 20), "[DEHYDRATED]");  tagX += 105f; }
            if (_isHypothermic.Value) { GUI.Label(new Rect(tagX, tagY, 110, 20), "[HYPOTHERMIC]"); }

            if (_isIncapacitated.Value)
                GUI.Label(new Rect(x, y + 206, 200, 24), "⚠  DOWNED");

            if (_showPartnerStatus) DrawPartnerStatus();
        }

        private static void DrawNeedRow(float x, float y, string label, float value, float max, string unit = "")
        {
            GUI.Label(new Rect(x,      y, 60,  22), $"{label}:");
            GUI.Label(new Rect(x + 60, y, 150, 22), $"{value:F0} / {max:F0}{unit}");
        }

        // ── Partner status element (§5.5, 0.2.11e) ───────────────────────────────
        //
        // Live status for the OTHER dreamer, always visible — no panel to open. Everything here is
        // read from that dreamer's own replicated NetworkVariables, so it costs no extra wire
        // traffic; the element is purely a second reader of state both peers already hold.
        //
        // The point is coordination. Under presence binding a skip needs the partner to be in a
        // state the flow will accept, and "is now a good time" should be a glance, not a question
        // typed into chat. Hence e2's can-skip indicator sitting directly under the vitals.

        private void DrawPartnerStatus()
        {
            if (_adapter == null) return;
            int partnerSlot = _adapter.Slot == 0 ? 1 : 0;
            var partner     = GetForSlot(partnerSlot);
            if (partner == null || partner == this) return;

            float w = 230f;
            float x = Screen.width - w - 10f;
            float y = 40f;

            GUI.Box(new Rect(x - 6f, y - 6f, w + 12f, 176f), "");
            GUI.Label(new Rect(x, y, w, 20f), $"── Dreamer {partnerSlot} ──");
            y += 22f;

            DrawNeedRow(x, y,       "Vitality", partner._vitality.Value, 100f); y += 22f;
            DrawNeedRow(x, y,       "Hunger",   partner._hunger.Value,   100f); y += 22f;
            DrawNeedRow(x, y,       "Thirst",   partner._thirst.Value,   100f); y += 22f;
            DrawNeedRow(x, y,       "Warmth",   partner._warmth.Value,   100f); y += 22f;
            DrawNeedRow(x, y,       "Energy",   partner._energy.Value,   100f); y += 22f;

            // Engaged / idle, straight off their synced slot label.
            var task = partner._taskSync != null ? partner._taskSync.CurrentTask : TaskType.Idle;
            GUI.Label(new Rect(x, y, w, 20f),
                task == TaskType.Idle ? "State: idle" : $"State: {task}");
            y += 20f;

            // Distance — "map position" at the fidelity that is actually useful while coordinating.
            // Read from the partner's transform, which is replicated by their NetworkTransform.
            float dist = Vector3.Distance(transform.position, partner.transform.position);
            GUI.Label(new Rect(x, y, w, 20f), $"Distance: {dist:F0} m");
            y += 20f;

            DrawCanSkipIndicator(partner, x, y, w);
        }

        /// <summary>
        /// e2 — whether the skip flow would actually accept this partner right now. Derived from the
        /// same guard floors SimResolver checks, so the light cannot disagree with the outcome: a
        /// green light that produced an instantly-halted skip would be worse than no light at all.
        /// </summary>
        private void DrawCanSkipIndicator(DreamerNeedsSync partner, float x, float y, float w)
        {
            var cfg = GetNeedsConfig();

            bool   guardTripped = partner._hunger.Value < cfg.GetHungerGuard() * 100f
                               || partner._thirst.Value < cfg.GetThirstGuard() * 100f
                               || partner._warmth.Value < cfg.GetWarmthGuard() * 100f;
            bool   down         = partner._isIncapacitated.Value;
            // In-combat is a seam: there is no combat system until Cluster 3. When it lands, OR it in
            // here — the indicator is already the single place the answer is computed.
            const bool inCombat = false;

            bool   canSkip = !guardTripped && !down && !inCombat;
            string why     = down         ? "downed"
                           : guardTripped ? "needs low"
                           : inCombat     ? "in combat"
                                          : "ready";

            var saved = GUI.color;
            GUI.color = canSkip ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.65f, 0.55f);
            GUI.Label(new Rect(x, y, w, 20f), canSkip ? "Can skip: ✔ ready" : $"Can skip: ✘ {why}");
            GUI.color = saved;
        }

        private NeedsConfig GetNeedsConfig()
        {
            // The Inspector copy on WorldClockDriver, which is authored identically on host and
            // client (rule 6) — so the client's indicator uses the same floors the host enforces.
            if (_needsConfig == null)
                _needsConfig = FindFirstObjectByType<WorldClockDriver>()?.NeedsConfig;
            return _needsConfig ?? new NeedsConfig();
        }
    }
}
