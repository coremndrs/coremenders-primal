using System;
using System.Collections.Generic;
using Game.Persistence;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Networking
{
    /// <summary>
    /// Persistent (DontDestroyOnLoad) orchestrator for the real game flow.
    /// Lives in the Splash scene so it survives all subsequent scene transitions.
    ///
    /// Responsibilities:
    ///   - Load the template / save and populate the RDM on host start.
    ///   - Register slot→client ownership as clients connect.
    ///   - Drive networked scene transitions.
    ///   - Spawn dreamers after Action loads (C3 / D5), with SnapToPositionRpc override.
    ///   - Save current state to disk (D2/D3).
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [SerializeField] private GameObject _dreamerPrefab;

        private bool _shouldDoFirstDream; // set only by StartGame (new session); not set by BeginLoad

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        // ── New Game host flow ───────────────────────────────────────────────────

        public void BeginHost()
        {
            var (world, mapLayer) = TemplateLoader.Load();
            RuntimeDataManager.Instance.Populate(world);
            RuntimeDataManager.Instance.SetMapLayer(mapLayer);

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("CharacterCreation", LoadSceneMode.Single);
        }

        // ── Load Game host flow (D7) ─────────────────────────────────────────────

        /// <summary>
        /// Host path from the main menu: loads a save, skips CharacterCreation, goes
        /// directly into the Action scene. The client joins and follows via NGO scene sync.
        ///
        /// Timing note: OnLoadEventCompleted fires once the host (and any already-connected
        /// clients) load Action. If the client connects after that fire, OnClientConnected
        /// detects we are already in Action and spawns slot 1 then.
        /// </summary>
        public void BeginLoad(string slot = SaveSystem.DefaultSlot)
        {
            if (!SaveSystem.SlotExists(slot))
            {
                Debug.LogWarning($"[GameFlowManager] No save found for slot '{slot}'.");
                return;
            }

            var (world, mapLayer) = SaveSystem.LoadSlot(slot);
            world.clock?.Resume(); // always start running after a cold load
            RuntimeDataManager.Instance.Populate(world);
            RuntimeDataManager.Instance.SetMapLayer(mapLayer);
            SaveSystem.DiscardCheckpointsNewerThan(world.clock?.totalInGameMinutes ?? 0f); // c4

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartHost();

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnActionSceneLoaded;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnActionSceneLoaded;
            NetworkManager.Singleton.SceneManager.LoadScene("Action", LoadSceneMode.Single);
        }

        // ── Client flow ──────────────────────────────────────────────────────────

        public void BeginClient()
        {
            NetworkManager.Singleton.StartClient();
        }

        // ── Ownership registration ───────────────────────────────────────────────

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            int slot = (clientId == NetworkManager.Singleton.LocalClientId) ? 0 : 1;
            RuntimeDataManager.Instance.SetOwnership(slot, clientId);

            // D5 / D7: if a non-host client joins while the Action scene is already active
            // (BeginLoad flow, where Action loads before the client connects), spawn slot 1 now.
            if (slot == 1 && IsInActionScene())
                SpawnSlot(1, clientId, RuntimeDataManager.Instance);
        }

        // ── Start game (from CharacterCreation ready flow) ───────────────────────

        public void StartGame()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            _shouldDoFirstDream = true;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnActionSceneLoaded;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnActionSceneLoaded;
            NetworkManager.Singleton.SceneManager.LoadScene("Action", LoadSceneMode.Single);
        }

        // ── Post-Action-load spawning ────────────────────────────────────────────

        private void OnActionSceneLoaded(string sceneName, LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (sceneName != "Action") return;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnActionSceneLoaded;

            if (clientsTimedOut.Count > 0)
                Debug.LogWarning($"[GameFlowManager] {clientsTimedOut.Count} client(s) timed out loading Action.");

            SpawnDreamers(clientsCompleted);

            if (_shouldDoFirstDream)
            {
                _shouldDoFirstDream = false;
                DoFirstDream();
            }
        }

        // ── Save (D2 + D3) ───────────────────────────────────────────────────────

        public void SaveGame(string slot = SaveSystem.DefaultSlot)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var rdm = RuntimeDataManager.Instance;
            for (int s = 0; s < 2; s++)
            {
                var livePos = rdm.GetDreamerPosition(s);
                var record  = rdm.GetDreamer(s);
                if (livePos.HasValue && record != null)
                    record.position = livePos.Value.ToFloat3();
            }

            SaveSystem.Save(rdm.WorldState, rdm.CurrentMapLayer, slot);
        }

        // ── Warm load — in-place apply (D5, D7 pause menu entry point) ────────────

        /// <summary>
        /// Applies a saved slot to the current live session without leaving the Action scene.
        /// Dreamers are not despawned — their position and appearance are patched in place.
        ///
        /// Position: authority-override via SnapToPositionRpc (host snaps directly;
        ///   client-owned dreamers receive the RPC and snap themselves).
        /// Appearance: server overwrites the NetworkVariable, replicating to all clients.
        ///
        /// This is the disk-backed sibling of 0.0.5's in-memory revert; both share the
        /// same apply mechanism.
        /// </summary>
        public void LoadGame(string slot = SaveSystem.DefaultSlot)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            if (!SaveSystem.SlotExists(slot))
            {
                Debug.LogWarning($"[GameFlowManager] No save found for slot '{slot}'.");
                return;
            }

            var (world, mapLayer) = SaveSystem.LoadSlot(slot);
            RuntimeDataManager.Instance.Populate(world);
            RuntimeDataManager.Instance.SetMapLayer(mapLayer);
            SaveSystem.DiscardCheckpointsNewerThan(world.clock?.totalInGameMinutes ?? 0f); // c4

            foreach (var adapter in FindObjectsByType<DreamerNetworkAdapter>(FindObjectsSortMode.None))
            {
                int s = adapter.Slot;
                if (s < 0 || s > 1) continue;

                var record = RuntimeDataManager.Instance.GetDreamer(s);
                if (record == null) continue;

                adapter.SnapToPositionRpc(record.position.ToVector3());
                adapter.SetAppearanceFromServer(AppearanceDataDto.From(record.appearance));
            }

            // Always resume after a warm load — user explicitly requested load.
            FindFirstObjectByType<WorldClockSync>()?.SetPaused(false);

            // Push inventory state to owning clients after rehydration (0.2.0c3).
            foreach (var sync in FindObjectsByType<DreamerInventorySync>(FindObjectsSortMode.None))
                sync.ForcePush();

            // Push map entity layer to all clients after rehydration (0.2.7a).
            MapEntitySync.Instance?.ForcePush();

            Debug.Log("[GameFlowManager] Warm load applied.");
        }

        // ── Checkpoint & revert (F1 / F2 + named store 0.1.5b) ─────────────────

        // Duration for the dev-checkpoint protection buff. Long enough to outlast any
        // test session; the buff (and its checkpoint) are replaced on the next TakeCheckpoint call.
        private const float DevCheckpointBuffMinutes = 99999f;

        /// <summary>
        /// F1 — Dev trigger: banks a "dev" checkpoint and grants both dreamers a protection
        /// buff pointing to it, so CleanOrphans keeps it alive. Re-taking replaces the previous
        /// buff and checkpoint cleanly.
        /// </summary>
        public void TakeCheckpoint()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Replace old dev buff first so it isn't stale in the snapshot.
            foreach (var d in RuntimeDataManager.Instance.WorldState.dreamers)
            {
                d?.buffs?.RemoveAll(b => b.checkpointId == "dev");
            }

            // Add the buff BEFORE saving so the snapshot includes it.
            // After revert the buff is present → CleanOrphans keeps the checkpoint alive
            // and the player can revert again without re-taking.
            foreach (var d in RuntimeDataManager.Instance.WorldState.dreamers)
            {
                if (d == null) continue;
                d.buffs.Add(new BuffInstance
                {
                    defId            = "protection",
                    remainingMinutes = DevCheckpointBuffMinutes,
                    checkpointId     = "dev",
                    coverage         = BuffCoverage.Shared,
                });
            }

            TakeNamedCheckpoint("dev", "Shared");

            foreach (var sync in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))
                sync.ForcePush();

            Debug.Log("[GameFlowManager] Dev checkpoint saved.");
        }

        /// <summary>
        /// F2 — Dev trigger: warm-applies the "dev" checkpoint through the D5 path.
        /// </summary>
        public void RevertToCheckpoint()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            const string devSlot = "checkpoint_dev";
            if (!SaveSystem.SlotExists(devSlot))
            {
                Debug.LogWarning("[GameFlowManager] No dev checkpoint found.");
                return;
            }
            LoadGame(devSlot);
            Debug.Log("[GameFlowManager] Reverted to dev checkpoint.");
        }

        /// <summary>
        /// Banks a named checkpoint via the 0.1.5b store (samples live positions first).
        /// </summary>
        public void TakeNamedCheckpoint(string id, string coverageLabel)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var rdm = RuntimeDataManager.Instance;
            for (int s = 0; s < 2; s++)
            {
                var livePos = rdm.GetDreamerPosition(s);
                var record  = rdm.GetDreamer(s);
                if (livePos.HasValue && record != null)
                    record.position = livePos.Value.ToFloat3();
            }
            SaveSystem.TakeNamedCheckpoint(rdm.WorldState, rdm.CurrentMapLayer, id, coverageLabel);
        }

        /// <summary>
        /// Re-saves the current RDM state to an existing named checkpoint WITHOUT sampling
        /// live transform positions. Used post-revert to bake a nightmare buff into the
        /// checkpoint so subsequent reverts to the same point escalate correctly (0.1.7d d2).
        /// </summary>
        public void ResaveNamedCheckpoint(string id, string coverageLabel)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            SaveSystem.TakeNamedCheckpoint(RuntimeDataManager.Instance.WorldState, RuntimeDataManager.Instance.CurrentMapLayer, id, coverageLabel);
        }

        /// <summary>
        /// Save-consumable mechanic (0.1.5c): banks a personal checkpoint for
        /// <paramref name="slot"/> and grants only that dreamer a personal protection buff.
        /// Debug trigger — the inventory-item wrapper comes with the inventory cluster.
        /// </summary>
        public void UseDebugSaveConsumable(int slot)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            string id      = $"personal_{slot}_{System.DateTime.UtcNow.Ticks}";
            var    dreamer = RuntimeDataManager.Instance.GetDreamer(slot);
            if (dreamer == null) return;

            // Grant buff BEFORE banking so the snapshot includes it (0.1.7d a1).
            var config = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();
            dreamer.buffs.Add(new BuffInstance
            {
                defId            = "protection",
                remainingMinutes = config.consumableDurationMinutes,
                checkpointId     = id,
                coverage         = BuffCoverage.Personal,
            });

            TakeNamedCheckpoint(id, "Personal");

            foreach (var sync in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))
                sync.ForcePush();

            Debug.Log($"[GameFlowManager] Save consumable: slot {slot} banked checkpoint '{id}'.");
        }

        // ── Inventory debug API (0.2.0a5) ────────────────────────────────────────

        /// <summary>
        /// Host-side debug trigger: adds a stack of defId/quantity to a dreamer's inventory
        /// and syncs it to the owning client (TDD §0.2.0a5 — Split, logic: Claude Code).
        /// Wire a dev button in the pause menu or task panel to call this.
        /// </summary>
        public void DebugAddItem(int slot, int defId, float quantity)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (dreamer == null) return;

            // Add to first available container (0.2.5: items belong to containers)
            foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
            {
                var inst = new Game.Simulation.Instance { defId = defId, quantity = quantity };
                var so   = DefRegistry.Instance?.Get(defId);
                if (so != null && so.isDurabilityTool && so.maxDurability > 0f)
                    inst.durability = so.maxDurability;
                container.contents ??= new Game.Simulation.InstanceContainer();
                container.contents.TryAdd(inst, DefRegistry.Instance);
                break;
            }

            foreach (var sync in FindObjectsByType<DreamerInventorySync>(FindObjectsSortMode.None))
                sync.ForcePush();

            Debug.Log($"[GameFlowManager] Debug: added defId={defId} qty={quantity} to slot {slot}.");
        }

        // ── World-item debug API (0.2.1a5) ──────────────────────────────────────

        /// <summary>
        /// Host-side debug trigger: places a ground item at the given world position.
        /// Delegates to MapEntitySync.PlaceItem; exposed here for external callers such as
        /// a pause-menu dev button (TDD §0.2.1a5 — Split, logic: Claude Code; input: Manual).
        /// </summary>
        public void DebugPlaceItem(int defId, float quantity, Vector3 position)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            MapEntitySync.Instance?.PlaceItem(defId, quantity, position);
        }

        // ── Dream flow hooks (0.1.7) ──────────────────────────────────────────────

        /// <summary>
        /// Reverts the session to a named checkpoint via the warm-apply path (same as LoadGame).
        /// Called by DreamFlowManager after the Wake Up vote consensus is reached (0.1.7b).
        /// </summary>
        public void RevertToNamedCheckpoint(string checkpointId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            string slot = $"checkpoint_{checkpointId}";
            if (!SaveSystem.SlotExists(slot))
            {
                Debug.LogWarning($"[GameFlowManager] Checkpoint slot '{slot}' not found for revert.");
                return;
            }
            LoadGame(slot);
            Debug.Log($"[GameFlowManager] Reverted to checkpoint '{checkpointId}'.");
        }

        /// <summary>
        /// Marks the current save as game-over and writes it to disk so a subsequent load
        /// can surface the end-of-run state (0.1.7c). Save is locked — this run cannot continue.
        /// </summary>
        public void LockSaveForGameOver()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var world = RuntimeDataManager.Instance?.WorldState;
            if (world == null) return;
            world.isGameOver = true;
            SaveGame(SaveSystem.DefaultSlot);
            Debug.Log("[GameFlowManager] Save locked for game over.");
        }

        // Save and Load are both in PauseMenuController (Escape to open).

        // ── Sleep → checkpoint (0.1.6) ───────────────────────────────────────────

        /// <summary>
        /// Called by WorldClockDriver (real-time) and SkipManager (per-tick + force-wake)
        /// whenever one or more full-sleep tasks ended on the same tick (0.1.6 §a3).
        ///
        /// Determines coverage from the qualifying waker count:
        ///   2 qualifying → shared (one checkpoint, two buffs)
        ///   1 qualifying → personal
        ///   0 qualifying → nothing banked
        ///
        /// Each buff is scaled linearly to the individual dreamer's actual slept minutes
        /// relative to a full sleep (NeedsConfig.sleepDurationMinutes).
        /// </summary>
        public void HandleSleepWake(IList<SleepEndedResult> wakers)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (wakers == null || wakers.Count == 0) return;

            var driver      = FindFirstObjectByType<WorldClockDriver>();
            var buffConfig  = driver?.BuffConfig  ?? new BuffConfig();
            var needsConfig = driver?.NeedsConfig ?? new NeedsConfig();

            // Filter to dreamers who slept at least the minimum threshold.
            var qualifying = new List<SleepEndedResult>();
            foreach (var w in wakers)
                if (w.SleptMinutes >= buffConfig.sleepMinBankThresholdMinutes)
                    qualifying.Add(w);

            if (qualifying.Count == 0)
            {
                Debug.Log("[GameFlowManager] Sleep ended but all wakers below min-bank threshold — no checkpoint.");
                return;
            }

            // One checkpoint per wake-tick, keyed by the current IG minute.
            int igMin = (int)(RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f);
            string id            = $"sleep_{igMin}";
            string coverageLabel = qualifying.Count >= 2 ? "Shared" : "Personal";

            // Grant protection buffs BEFORE banking so the snapshot includes them (0.1.7d a1).
            float fullSleepMin    = needsConfig.sleepDurationMinutes;
            float fullDurationMin = buffConfig.normalDurationMinutes;
            var   coverage        = qualifying.Count >= 2 ? BuffCoverage.Shared : BuffCoverage.Personal;

            foreach (var w in qualifying)
            {
                var dreamer = RuntimeDataManager.Instance.GetDreamer(w.DreamerSlot);
                if (dreamer == null) continue;

                double ratio    = Math.Min(1.0, Math.Max(0.0, w.SleptMinutes / (double)fullSleepMin));
                float  duration = (float)(ratio * fullDurationMin);

                dreamer.buffs.Add(new BuffInstance
                {
                    defId            = "protection",
                    remainingMinutes = duration,
                    checkpointId     = id,
                    coverage         = coverage,
                });
            }

            TakeNamedCheckpoint(id, coverageLabel);

            foreach (var sync in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))
                sync.ForcePush();

            Debug.Log($"[GameFlowManager] Sleep checkpoint banked: '{id}' ({coverageLabel}), {qualifying.Count} dreamer(s).");
        }

        // ── First Dream (0.1.5b) ─────────────────────────────────────────────────

        private void DoFirstDream()
        {
            // Grant buffs BEFORE banking so the snapshot includes them (0.1.7d a1).
            var config = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();
            foreach (var d in RuntimeDataManager.Instance.WorldState.dreamers)
            {
                if (d == null) continue;
                d.buffs.Add(new BuffInstance
                {
                    defId            = "protection",
                    remainingMinutes = config.firstDreamDurationMinutes,
                    checkpointId     = "first_dream",
                    coverage         = BuffCoverage.Shared,
                });
            }

            TakeNamedCheckpoint("first_dream", "Shared");

            foreach (var sync in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))
                sync.ForcePush();

            Debug.Log("[GameFlowManager] First Dream: shared checkpoint banked, protection buffs granted.");
        }

        // ── Spawn helpers ────────────────────────────────────────────────────────

        private void SpawnDreamers(IEnumerable<ulong> connectedClients)
        {
            var rdm     = RuntimeDataManager.Instance;
            var localId = NetworkManager.Singleton.LocalClientId;

            SpawnSlot(0, localId, rdm);

            foreach (var clientId in connectedClients)
            {
                if (clientId != localId)
                {
                    SpawnSlot(1, clientId, rdm);
                    break;
                }
            }
        }

        private void SpawnSlot(int slot, ulong ownerId, RuntimeDataManager rdm)
        {
            var record = rdm.GetDreamer(slot);
            if (record == null)
            {
                Debug.LogError($"[GameFlowManager] No DreamerRecord for slot {slot}");
                return;
            }

            var savedPos = record.position.ToVector3();
            var go = Instantiate(_dreamerPrefab, savedPos, Quaternion.identity);
            go.GetComponent<DreamerNetworkAdapter>().Initialize(slot);
            go.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId);
            rdm.SetOwnership(slot, ownerId);

            // D5: authority override — snap the owner to the saved position.
            // For host-owned dreamers this runs locally; for client-owned dreamers
            // the [Rpc(SendTo.Owner)] delivers it to the owning client.
            go.GetComponent<DreamerNetworkAdapter>().SnapToPositionRpc(savedPos);

            Debug.Log($"[GameFlowManager] Spawned slot {slot} → client {ownerId} at {savedPos}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static bool IsInActionScene() =>
            SceneManager.GetActiveScene().name == "Action";
    }
}
