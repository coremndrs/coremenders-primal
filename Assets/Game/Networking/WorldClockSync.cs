using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// In-scene NetworkBehaviour that synchronises the world clock to all clients (TDD §E3/E4/E5).
    ///
    /// E3 — Sync: the server periodically writes _syncedMinutes from the RDM clock.
    ///      The client extrapolates locally between syncs using NGO's NetworkTime as the
    ///      shared real-time reference, correcting on each received update.
    ///
    /// E4 — Pause/resume: server sets _isPaused; client stops/resumes extrapolation.
    ///      PauseMenuController calls SetPaused() on the host.
    ///
    /// E5 — HUD: OnGUI shows "Day N — HH:MM" on all clients. No gameplay effects yet.
    ///
    /// Requires a NetworkObject component on the same GameObject (in-scene NetworkObject).
    /// </summary>
    public class WorldClockSync : NetworkBehaviour
    {
        [SerializeField] private float _syncIntervalSeconds = 1f;

        // ── Server-authoritative NetworkVariables ────────────────────────────────

        private readonly NetworkVariable<float> _syncedMinutes = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isPaused = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _realSecondsPerMinute = new NetworkVariable<float>(
            6f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Client-side extrapolation state ──────────────────────────────────────

        private float  _extrapolationBaseMinutes;
        private double _extrapolationBaseNetworkTime;
        private float  _syncTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                var clock = RuntimeDataManager.Instance?.WorldState?.clock;
                if (clock != null)
                {
                    _syncedMinutes.Value         = clock.totalInGameMinutes;
                    _isPaused.Value              = clock.isPaused;
                    _realSecondsPerMinute.Value  = clock.realSecondsPerInGameMinute;
                }
            }

            _syncedMinutes.OnValueChanged -= OnSyncedMinutesChanged;
            _syncedMinutes.OnValueChanged += OnSyncedMinutesChanged;
            _isPaused.OnValueChanged      -= OnPausedChanged;
            _isPaused.OnValueChanged      += OnPausedChanged;

            ResetExtrapolation(_syncedMinutes.Value);
        }

        public override void OnNetworkDespawn()
        {
            _syncedMinutes.OnValueChanged -= OnSyncedMinutesChanged;
            _isPaused.OnValueChanged      -= OnPausedChanged;
            base.OnNetworkDespawn();
        }

        // ── Server tick (periodic push) ──────────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;

            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncIntervalSeconds) return;
            _syncTimer = 0f;

            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (clock == null) return;

            _syncedMinutes.Value = clock.totalInGameMinutes;
            _isPaused.Value      = clock.isPaused;
        }

        // ── NetworkVariable callbacks ────────────────────────────────────────────

        private void OnSyncedMinutesChanged(float _, float current)
            => ResetExtrapolation(current);

        private void OnPausedChanged(bool _, bool current)
        {
            if (!current) ResetExtrapolation(_syncedMinutes.Value); // reset base on resume
        }

        private void ResetExtrapolation(float baseMinutes)
        {
            _extrapolationBaseMinutes     = baseMinutes;
            _extrapolationBaseNetworkTime = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time
                : 0;
        }

        // ── E4 — Pause / resume ──────────────────────────────────────────────────

        /// <summary>Host-only. Sets the authoritative paused state on the clock and syncs to all.</summary>
        public void SetPaused(bool paused)
        {
            if (!IsServer) return;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (clock == null) return;
            if (paused) clock.Pause(); else clock.Resume();
            _isPaused.Value = paused;
            Debug.Log($"[WorldClockSync] Clock {(paused ? "paused" : "resumed")}");
        }

        /// <summary>Called by SkipManager after a skip completes to push the final clock immediately.</summary>
        public void ForcePush()
        {
            if (!IsServer) return;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (clock == null) return;
            _syncedMinutes.Value = clock.totalInGameMinutes;
            _isPaused.Value      = clock.isPaused;
            ResetExtrapolation(_syncedMinutes.Value);
        }

        // ── Public state ─────────────────────────────────────────────────────────

        /// <summary>Current authoritative paused state — readable on all clients.</summary>
        public bool IsPaused => _isPaused.Value;

        // ── E3 — Time read (used by HUD and callers) ─────────────────────────────

        public float GetCurrentMinutes()
        {
            // Server reads directly from the authoritative RDM clock.
            if (IsServer)
                return RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes
                       ?? _syncedMinutes.Value;

            // Clients extrapolate from the last sync using NetworkTime as shared reference.
            if (_isPaused.Value)
                return _syncedMinutes.Value;

            double elapsed = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time - _extrapolationBaseNetworkTime
                : 0;
            return _extrapolationBaseMinutes + (float)(elapsed / _realSecondsPerMinute.Value);
        }

        public string GetTimeString()
        {
            float minutes   = GetCurrentMinutes();
            int totalInt    = (int)minutes;
            int day         = totalInt / 1440 + 1;
            int remaining   = totalInt % 1440;
            int hour        = remaining / 60;
            int minute      = remaining % 60;
            return $"Day {day} — {hour:D2}:{minute:D2}";
        }

        // ── E5 — HUD readout ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned) return;
            GUI.Label(new Rect(Screen.width / 2f - 70, 10, 140, 25), GetTimeString());
        }
    }
}
