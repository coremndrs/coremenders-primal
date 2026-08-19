using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Per-dreamer NetworkBehaviour that synchronises task state and provides a dev UI
    /// for assigning tasks (TDD §0.1.1).
    ///
    /// Server pushes current task type and progress to all clients via NetworkVariables.
    /// The owning client sends task-change requests via RPC; the server creates the task
    /// using NeedsConfig defaults and writes it to the RDM.
    ///
    /// NeedsConfig is duplicated here (also on WorldClockDriver) so both components are
    /// independently Inspector-configurable. Keep values in sync during dev.
    ///
    /// Add this component to the dreamer prefab alongside DreamerNetworkAdapter.
    /// </summary>
    public class DreamerTaskSync : NetworkBehaviour
    {
        [SerializeField] private float _syncInterval = 1f;

        // ── Server-authoritative NetworkVariables ─────────────────────────────────

        private readonly NetworkVariable<int> _taskType = new NetworkVariable<int>(
            (int)TaskType.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _elapsed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _duration = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Cached references ────────────────────────────────────────────────────

        private DreamerNetworkAdapter  _adapter;
        private DreamerInventorySync   _inventorySync;
        private NeedsConfig            _needsConfig;
        private float                  _syncTimer;

        // ── Dev menu UI state (client-local, not networked) ──────────────────────

        private bool _devMenuOpen;
        private int  _devMenuPage; // 0 = top, 1 = AddItems, 2 = OtherActions, 3 = Gather, 4 = Craft

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _adapter       = GetComponent<DreamerNetworkAdapter>();
            _inventorySync = GetComponent<DreamerInventorySync>();

            if (IsServer)
                PushFromRdm();
        }

        // ── Server tick (periodic push) ──────────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;

            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncInterval) return;
            _syncTimer = 0f;

            PushFromRdm();
        }

        private void PushFromRdm()
        {
            if (_adapter == null) return;
            var task = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot)?.task;
            if (task == null) return;

            _taskType.Value = (int)task.type;
            _elapsed.Value  = task.elapsedMinutes;
            _duration.Value = task.durationMinutes;
        }

        // ── Task assignment RPC ───────────────────────────────────────────────────

        /// <summary>Owner sends desired task type; server creates and writes it to the RDM.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTaskServerRpc(int taskTypeInt)
        {
            var type   = (TaskType)taskTypeInt;
            var record = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (record == null) return;

            record.task = GetNeedsConfig().CreateTask(type);
            Debug.Log($"[DreamerTaskSync] Slot {_adapter.Slot} → task {type} ({record.task.durationMinutes} min)");
        }

        /// <summary>Called by SkipManager after a skip completes to push final state immediately.</summary>
        public void ForcePush()
        {
            if (!IsServer) return;
            PushFromRdm();
        }

        private NeedsConfig GetNeedsConfig()
        {
            if (_needsConfig == null)
                _needsConfig = FindFirstObjectByType<WorldClockDriver>()?.NeedsConfig;
            return _needsConfig ?? new NeedsConfig();
        }

        /// <summary>Client helper (0.2.10c dev): nearest world object of <paramref name="stationDefId"/>
        /// within interaction range, or -1. Used by the Craft dev page to target a debug-placed station.</summary>
        private int FindNearbyStation(int stationDefId, Vector3 origin)
        {
            var nearby = MapEntitySync.Instance?.GetNearbyProcessables(origin, MapEntitySync.PickupRange * 2f);
            if (nearby == null) return -1;
            int  best     = -1;
            float bestDist = float.MaxValue;
            foreach (var e in nearby)
            {
                if (e.defId != stationDefId) continue;
                float d = Vector3.Distance(origin, e.pos);
                if (d < bestDist) { bestDist = d; best = e.id; }
            }
            return best;
        }

        // ── Public accessors ─────────────────────────────────────────────────────

        public TaskType CurrentTask  => (TaskType)_taskType.Value;
        public float    Elapsed      => _elapsed.Value;
        public float    Duration     => _duration.Value;

        // ── Dev RPCs ─────────────────────────────────────────────────────────────

        /// <summary>Sets all Tier-1 needs to 5 (well below the default critical threshold of 20)
        /// so afflictions trigger within a few ticks. Dev testing shortcut.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevSetNeedsCriticalServerRpc()
        {
            var record = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (record == null) return;
            record.needs.hunger = 5f;
            record.needs.thirst = 5f;
            record.needs.warmth = 5f;
            Debug.Log($"[DreamerTaskSync] Dev: slot {_adapter.Slot} needs set to critical.");
        }

        /// <summary>Resets Vitality to 100, clears incapacitation and all afflictions, and
        /// restores needs to 100 so the dreamer is fully recovered. Dev testing shortcut.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevReviveServerRpc()
        {
            var record = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (record == null) return;
            record.vitality         = 100f;
            record.isIncapacitated  = false;
            record.afflictions      = new Game.Simulation.AfflictionSet();
            record.needs.hunger     = 100f;
            record.needs.thirst     = 100f;
            record.needs.warmth     = 100f;
            Debug.Log($"[DreamerTaskSync] Dev: slot {_adapter.Slot} revived.");
        }

        /// <summary>
        /// Applies the built-in test buff (60 min, halves all drain rates) to prove the
        /// modifier path is wired (0.1.5a acceptance).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevApplyTestBuffServerRpc()
        {
            var record = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (record == null) return;
            record.buffs.Add(new Game.Simulation.BuffInstance
            {
                defId            = Game.Simulation.BuffConfig.DefaultTestBuff.id,
                remainingMinutes = Game.Simulation.BuffConfig.DefaultTestBuff.durationMinutes,
            });
            Debug.Log($"[DreamerTaskSync] Dev: test buff applied to slot {_adapter.Slot}.");
        }

        /// <summary>Debug trigger for the save consumable (0.1.5c). Banks a personal checkpoint.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevSaveConsumableServerRpc()
        {
            GameFlowManager.Instance?.UseDebugSaveConsumable(_adapter.Slot);
        }

        // ── Gather RPCs (0.2.8b2) ────────────────────────────────────────────────

        /// <summary>
        /// Owner sends intent to start contributing to a processable action (TDD §5.5.5, 0.2.8b2).
        /// Host validates prerequisites, co-op flag, and opens the accrual segment.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestGatherServerRpc(int processableId, int actionIndex)
        {
            MapEntitySync.Instance?.StartContributor(processableId, actionIndex, _adapter.Slot);
        }

        /// <summary>
        /// Owner sends intent to stop contributing to a processable action (TDD §5.5.4, 0.2.8b2).
        /// Host closes the accrual segment; partial progress persists on the object.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void StopGatherServerRpc(int processableId, int actionIndex)
        {
            MapEntitySync.Instance?.StopContributor(processableId, actionIndex, _adapter.Slot);
        }

        /// <summary>
        /// Owner selects a world action on an Interactable; server routes to instant or timed
        /// dispatch based on ActionDef.timeRequired (TDD §1.12, 0.2.9c3).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DispatchWorldActionServerRpc(int instanceId, int actionIndex)
        {
            MapEntitySync.Instance?.DispatchWorldAction(instanceId, actionIndex, _adapter.Slot);
        }

        /// <summary>Owner stops contributing to a timed world action (companion to DispatchWorldActionServerRpc).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void StopWorldActionServerRpc(int instanceId, int actionIndex)
        {
            MapEntitySync.Instance?.StopContributor(instanceId, actionIndex, _adapter.Slot);
        }

        /// <summary>Owner commits/assists a station craft on the given station instance (TDD §5.7.1, 0.2.10c).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestStationCraftServerRpc(int instanceId, int recipeId)
        {
            MapEntitySync.Instance?.StartStationCraft(instanceId, recipeId, _adapter.Slot);
        }

        /// <summary>Owner leaves a station craft; the partial persists on the station, resumable (§5.5.4).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void StopStationCraftServerRpc(int instanceId)
        {
            MapEntitySync.Instance?.StopStationCraft(instanceId, _adapter.Slot);
        }

        /// <summary>Debug: spawn a processable by def id at the dreamer's feet (0.2.8 dev).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevSpawnProcessableServerRpc(int defId)
        {
            var pos = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;
            MapEntitySync.Instance?.SpawnProcessable(defId, pos + new Vector3(1.5f, 0f, 0f));
        }

        // ── HUD — owning client only ─────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;

            float x = Screen.width - 215f;
            float y = Screen.height - 185f;

            // Current task + progress
            var task = CurrentTask;
            GUI.Label(new Rect(x, y, 200, 22), $"Task: {task}");

            if (task != TaskType.Idle && _duration.Value > 0f)
            {
                float pct = _elapsed.Value / _duration.Value * 100f;
                GUI.Label(new Rect(x, y + 22, 200, 22),
                    $"  {_elapsed.Value:F0} / {_duration.Value:F0} min ({pct:F0}%)");
            }

            // Task assignment buttons (Eat removed 0.2.2b3 — food is now item consumption)
            float btnY = y + 52f;
            if (GUI.Button(new Rect(x,       btnY, 95f, 24f), "Rest"))
                RequestTaskServerRpc((int)TaskType.Resting);
            if (GUI.Button(new Rect(x + 100, btnY, 95f, 24f), "Sleep"))
                RequestTaskServerRpc((int)TaskType.Sleeping);
            if (GUI.Button(new Rect(x, btnY + 30, 95f, 24f), "Idle"))
                RequestTaskServerRpc((int)TaskType.Idle);

            // ── Collapsible dev menu (top-right corner) ──────────────────────────

            float dx = Screen.width - 110f;
            float dy = 10f;

            if (GUI.Button(new Rect(dx, dy, 100f, 22f), _devMenuOpen ? "Dev ▲" : "Dev ▼"))
            {
                _devMenuOpen = !_devMenuOpen;
                _devMenuPage = 0;
            }

            if (!_devMenuOpen) return;

            dx  = Screen.width - 330f;   // wider dev panel so def/recipe names don't truncate
            dy += 26f;

            if (_devMenuPage == 0)
            {
                if (GUI.Button(new Rect(dx,        dy, 70f, 22f), "Items"))
                    _devMenuPage = 1;
                if (GUI.Button(new Rect(dx + 75f,  dy, 70f, 22f), "Other"))
                    _devMenuPage = 2;
                if (GUI.Button(new Rect(dx + 150f, dy, 70f, 22f), "Gather"))
                    _devMenuPage = 3;
                dy += 26f;
                if (GUI.Button(new Rect(dx,        dy, 70f, 22f), "Craft"))
                    _devMenuPage = 4;
            }
            else if (_devMenuPage == 1) // Items: +Inv column always, +World column host-only
            {
                if (GUI.Button(new Rect(dx, dy, 80f, 22f), "← Back"))
                    _devMenuPage = 0;
                dy += 26f;
                var defs = DefRegistry.Instance?.AllDefs;
                if (defs != null)
                    foreach (var def in defs)
                    {
                        // Items page: only inventory-capable defs (0.2.9d — world-object
                        // defs live in the Gather page).
                        if (def == null || !def.HasAspect<InventoryAspect>()) continue;
                        // Prefix the defId so rows stay unambiguous even when the name truncates
                        // (e.g. "Stone Axe" vs "Stone Axe (Broken)" both clip to "Stone Axe").
                        GUI.Label(new Rect(dx, dy, 195f, 22f),
                            new GUIContent($"{def.defId}: {def.displayName}", $"{def.displayName} (defId {def.defId})"));
                        if (GUI.Button(new Rect(dx + 200f, dy, 52f, 22f), "+Inv"))
                            _inventorySync?.DevAddItemServerRpc(def.defId, 1f);
                        if (IsServer && GUI.Button(new Rect(dx + 255f, dy, 60f, 22f), "+World"))
                        {
                            var p = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;
                            MapEntitySync.Instance?.PlaceItem(def.defId, 1f, p + new Vector3(1.5f, 0f, 0f));
                        }
                        dy += 26f;
                    }
            }
            else if (_devMenuPage == 2)
            {
                if (GUI.Button(new Rect(dx, dy, 80f, 22f), "← Back"))
                    _devMenuPage = 0;
                dy += 26f;
                if (GUI.Button(new Rect(dx,        dy, 107f, 22f), "Set Critical"))
                    DevSetNeedsCriticalServerRpc();
                if (GUI.Button(new Rect(dx + 112f, dy, 107f, 22f), "Revive"))
                    DevReviveServerRpc();
                dy += 26f;
                if (GUI.Button(new Rect(dx,        dy, 107f, 22f), "Test Buff"))
                    DevApplyTestBuffServerRpc();
                if (GUI.Button(new Rect(dx + 112f, dy, 107f, 22f), "Bank Save"))
                    DevSaveConsumableServerRpc();
            }
            else if (_devMenuPage == 3) // Gather dev panel (0.2.8)
            {
                if (GUI.Button(new Rect(dx, dy, 80f, 22f), "← Back"))
                    _devMenuPage = 0;
                dy += 26f;

                // Spawn processable by def id (host only)
                var defs = DefRegistry.Instance?.AllDefs;
                if (defs != null)
                {
                    GUI.Label(new Rect(dx, dy, 215f, 20f), "— Spawn processable —");
                    dy += 22f;
                    foreach (var def in defs)
                    {
                        if (def == null) continue;
                        GUI.Label(new Rect(dx, dy, 130f, 22f), $"[{def.defId}] {def.displayName}");
                        if (IsServer && GUI.Button(new Rect(dx + 135f, dy, 75f, 22f), "+Spawn"))
                            DevSpawnProcessableServerRpc(def.defId);
                        dy += 24f;
                    }
                }

                // Nearby processables — start / stop gather
                dy += 4f;
                GUI.Label(new Rect(dx, dy, 215f, 20f), "— Nearby processables —");
                dy += 22f;

                var dreamerPos = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot);
                if (!dreamerPos.HasValue) return;

                var nearby = MapEntitySync.Instance?.GetNearbyProcessables(dreamerPos.Value, MapEntitySync.PickupRange * 2f);
                if (nearby != null)
                {
                    foreach (var proc in nearby)
                    {
                        var procDef = DefRegistry.Instance?.Get(proc.defId);
                        string label = procDef != null ? procDef.displayName : $"def={proc.defId}";
                        GUI.Label(new Rect(dx, dy, 215f, 20f), $"[{proc.id}] {label}");
                        dy += 20f;

                        int ac = proc.actionCount;
                        for (int a = 0; a < ac && a < (procDef?.actions?.Length ?? 0); a++)
                        {
                            var actionDef = procDef?.actions[a];
                            float pct     = actionDef != null && actionDef.timeRequired > 0f
                                ? proc.labors[a] / actionDef.timeRequired * 100f : 0f;
                            string status = proc.completes[a] ? "✓" : $"{pct:F0}%";
                            GUI.Label(new Rect(dx + 8f, dy, 130f, 20f), $"  {actionDef?.DisplayLabel ?? $"action{a}"} {status}");
                            if (!proc.completes[a])
                            {
                                if (GUI.Button(new Rect(dx + 142f, dy, 35f, 20f), "Go"))
                                    RequestGatherServerRpc(proc.id, a);
                                if (GUI.Button(new Rect(dx + 180f, dy, 35f, 20f), "Stop"))
                                    StopGatherServerRpc(proc.id, a);
                            }
                            dy += 22f;
                        }
                    }
                }
            }
            else // page 4 — Craft dev panel (0.2.10)
            {
                if (GUI.Button(new Rect(dx, dy, 80f, 22f), "← Back"))
                    _devMenuPage = 0;
                dy += 26f;

                GUI.Label(new Rect(dx, dy, 215f, 20f), "— Recipes —");
                dy += 22f;

                var recipes = RecipeRegistry.Instance?.AllRecipes;
                if (recipes == null || recipes.Length == 0)
                {
                    GUI.Label(new Rect(dx, dy, 215f, 20f), "  (no recipes authored)");
                    dy += 22f;
                }
                else
                {
                    var craftPos = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot);
                    foreach (var recipe in recipes)
                    {
                        if (recipe == null) continue;
                        if (recipe.IsHandCraft)
                        {
                            string tag = recipe.requiredToolCategoryId != 0 ? $" (tool {recipe.requiredToolCategoryId})" : "";
                            GUI.Label(new Rect(dx, dy, 150f, 20f), $"{recipe.displayName}{tag}");
                            if (GUI.Button(new Rect(dx + 152f, dy, 63f, 20f), "Craft"))
                                _inventorySync?.RequestHandCraftServerRpc(recipe.recipeId);
                            dy += 22f;
                        }
                        else
                        {
                            // Station recipe: find a nearby station of the required Def and dispatch
                            // there (0.2.10c uses a debug-placed station; a proper station-UI is a
                            // Cluster-4/UI-pass deferral). Assist re-uses the same button (co-op).
                            int stationId = craftPos.HasValue ? FindNearbyStation(recipe.requiredStationDefId, craftPos.Value) : -1;
                            GUI.Label(new Rect(dx, dy, 120f, 20f), $"{recipe.displayName} (stn)");
                            GUI.enabled = stationId >= 0;
                            if (GUI.Button(new Rect(dx + 122f, dy, 48f, 20f), "Craft"))
                                RequestStationCraftServerRpc(stationId, recipe.recipeId);
                            if (GUI.Button(new Rect(dx + 172f, dy, 43f, 20f), "Stop"))
                                StopStationCraftServerRpc(stationId);
                            GUI.enabled = true;
                            dy += 22f;
                        }
                    }
                }
            }
        }
    }
}
