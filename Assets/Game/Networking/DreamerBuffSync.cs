using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Compact payload for up to 4 buff slots.
    /// Carries: BuffNetId (int), absolute end-tick (float), BuffCoverage (byte).
    /// No strings and no per-tick remaining-time cross the wire — the client extrapolates
    /// remaining = endTick − WorldClockSync.GetCurrentMinutes() locally, exactly as
    /// WorldClockSync does for the clock (§1.6 / §E3).
    /// endTick = totalInGameMinutes + remainingMinutes is invariant between add and remove,
    /// so the payload is only dirty on structural change (add / remove).
    /// </summary>
    internal struct BuffListPayload : INetworkSerializable
    {
        public byte  Count;
        public int   Id0; public float EndTick0; public byte Cov0;
        public int   Id1; public float EndTick1; public byte Cov1;
        public int   Id2; public float EndTick2; public byte Cov2;
        public int   Id3; public float EndTick3; public byte Cov3;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Id0); serializer.SerializeValue(ref EndTick0); serializer.SerializeValue(ref Cov0);
            serializer.SerializeValue(ref Id1); serializer.SerializeValue(ref EndTick1); serializer.SerializeValue(ref Cov1);
            serializer.SerializeValue(ref Id2); serializer.SerializeValue(ref EndTick2); serializer.SerializeValue(ref Cov2);
            serializer.SerializeValue(ref Id3); serializer.SerializeValue(ref EndTick3); serializer.SerializeValue(ref Cov3);
        }
    }

    /// <summary>
    /// Per-dreamer NetworkBehaviour that synchronises the active buff list to all clients
    /// (TDD §0.1.5a / §0.1.5b). Event-driven: the NV is dirtied only when the buff set
    /// changes structurally, not every tick.
    ///
    /// The periodic Update() call is kept as a convergence safety net — the equality
    /// guard ensures it is a no-op while the buff set is stable.
    ///
    /// Add this component to the dreamer prefab alongside DreamerNetworkAdapter.
    /// </summary>
    public class DreamerBuffSync : NetworkBehaviour
    {
        [SerializeField] private float _syncInterval = 1f;

        private readonly NetworkVariable<BuffListPayload> _buffList = new NetworkVariable<BuffListPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DreamerNetworkAdapter _adapter;
        private BuffConfig            _buffConfig;
        private WorldClockSync        _clockSync;
        private BuffListPayload       _lastSynced; // tracks what was last sent so we skip no-op pushes
        private float                 _syncTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _adapter = GetComponent<DreamerNetworkAdapter>();
            if (IsServer) PushFromRdm();
        }

        // ── Server tick (convergence safety net) ─────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;
            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncInterval) return;
            _syncTimer = 0f;
            PushFromRdm(); // equality guard makes this a no-op when buff set is stable
        }

        private BuffListPayload BuildPayload()
        {
            if (_adapter == null) return default;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            float now   = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;

            var payload = new BuffListPayload();
            if (dreamer?.buffs == null || dreamer.buffs.Count == 0) return payload;

            int count = System.Math.Min(dreamer.buffs.Count, 4);
            payload.Count = (byte)count;
            for (int i = 0; i < count; i++)
            {
                int   id      = (int)BuffConfig.ToNetId(dreamer.buffs[i].defId);
                float endTick = now + dreamer.buffs[i].remainingMinutes;
                byte  cov     = (byte)dreamer.buffs[i].coverage;
                switch (i)
                {
                    case 0: payload.Id0 = id; payload.EndTick0 = endTick; payload.Cov0 = cov; break;
                    case 1: payload.Id1 = id; payload.EndTick1 = endTick; payload.Cov1 = cov; break;
                    case 2: payload.Id2 = id; payload.EndTick2 = endTick; payload.Cov2 = cov; break;
                    case 3: payload.Id3 = id; payload.EndTick3 = endTick; payload.Cov3 = cov; break;
                }
            }
            return payload;
        }

        private void CommitPayload(BuffListPayload payload)
        {
            _lastSynced     = payload;
            _buffList.Value = payload;
        }

        private void PushFromRdm()
        {
            var payload = BuildPayload();
            if (PayloadEquals(payload, _lastSynced)) return;
            CommitPayload(payload);
        }

        /// <summary>
        /// Called by SkipManager and GameFlowManager after bulk state changes.
        /// Always commits — no equality guard (same contract as WorldItemsSync.ForcePush).
        /// </summary>
        public void ForcePush()
        {
            if (!IsServer) return;
            CommitPayload(BuildPayload()); // always commits — no equality guard
        }

        // ── Equality guard ────────────────────────────────────────────────────────

        private static bool PayloadEquals(BuffListPayload a, BuffListPayload b)
            => a.Count     == b.Count
            && a.Id0       == b.Id0       && a.EndTick0 == b.EndTick0 && a.Cov0 == b.Cov0
            && a.Id1       == b.Id1       && a.EndTick1 == b.EndTick1 && a.Cov1 == b.Cov1
            && a.Id2       == b.Id2       && a.EndTick2 == b.EndTick2 && a.Cov2 == b.Cov2
            && a.Id3       == b.Id3       && a.EndTick3 == b.EndTick3 && a.Cov3 == b.Cov3;

        // ── HUD — owning client only ─────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;

            var payload = _buffList.Value;
            if (payload.Count == 0) return;

            float currentMinutes = GetClockSync()?.GetCurrentMinutes() ?? 0f;
            var   config         = GetBuffConfig();
            float x              = 10f;
            float baseY          = Screen.height * 0.45f;

            GUI.Label(new Rect(x, baseY, 180, 20), "── Buffs ──");

            for (int i = 0; i < payload.Count && i < 4; i++)
            {
                var   netId   = (BuffNetId)  (i switch { 0 => payload.Id0,     1 => payload.Id1,     2 => payload.Id2,     _ => payload.Id3     });
                float endTick =              (i switch { 0 => payload.EndTick0, 1 => payload.EndTick1, 2 => payload.EndTick2, _ => payload.EndTick3 });
                var   cov     = (BuffCoverage)(i switch { 0 => payload.Cov0,    1 => payload.Cov1,    2 => payload.Cov2,    _ => payload.Cov3    });

                string defId     = BuffConfig.ToDefId(netId);
                string name      = config?.GetDef(defId)?.displayName ?? defId;
                string covSuffix = cov == BuffCoverage.None ? "" : $" [{cov}]";
                float  remaining = Mathf.Max(0f, endTick - currentMinutes);

                GUI.Label(new Rect(x, baseY + 22f + i * 20f, 240, 20),
                    $"{name}{covSuffix}: {remaining:F0} min");
            }
        }

        // ── Cached lookups ────────────────────────────────────────────────────────

        private BuffConfig GetBuffConfig()
        {
            if (_buffConfig == null)
                _buffConfig = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig;
            return _buffConfig;
        }

        private WorldClockSync GetClockSync()
        {
            if (_clockSync == null)
                _clockSync = FindFirstObjectByType<WorldClockSync>();
            return _clockSync;
        }
    }
}
