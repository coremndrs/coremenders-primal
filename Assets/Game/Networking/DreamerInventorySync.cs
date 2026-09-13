using System;
using System.Collections.Generic;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Wire payload for the dreamer's multi-container inventory (0.2.5 / TDD §5.4).
    /// Structure unchanged from pre-0.2.9b; underlying type is now Instance.
    /// </summary>
    internal struct InventoryPayload : INetworkSerializable
    {
        public const byte NonPerishable = 255;

        public int     OccupiedCount;
        public byte[]  SlotTypes;
        public int[]   ContainerDefIds;
        public float[] ContainerQtys;
        public byte[]  ContainerConds;
        public int[]   SlotItemCounts;

        public int     TotalItems;
        public int[]   ItemDefIds;
        public float[] ItemQtys;
        public byte[]  ItemConds;

        public int     NestedGroupCount;
        public byte[]  NestedParentSlot;
        public int[]   NestedParentItem;
        public int[]   NestedItemCounts;

        public int     TotalNested;
        public int[]   NestedDefIds;
        public float[] NestedQtys;
        public byte[]  NestedConds;

        public float CarriedWeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CarriedWeight);
            serializer.SerializeValue(ref OccupiedCount);
            if (serializer.IsReader)
            {
                SlotTypes       = new byte[OccupiedCount];
                ContainerDefIds = new int[OccupiedCount];
                ContainerQtys   = new float[OccupiedCount];
                ContainerConds  = new byte[OccupiedCount];
                SlotItemCounts  = new int[OccupiedCount];
            }
            for (int i = 0; i < OccupiedCount; i++)
            {
                serializer.SerializeValue(ref SlotTypes[i]);
                serializer.SerializeValue(ref ContainerDefIds[i]);
                serializer.SerializeValue(ref ContainerQtys[i]);
                serializer.SerializeValue(ref ContainerConds[i]);
                serializer.SerializeValue(ref SlotItemCounts[i]);
            }

            serializer.SerializeValue(ref TotalItems);
            if (serializer.IsReader)
            {
                ItemDefIds = new int[TotalItems];
                ItemQtys   = new float[TotalItems];
                ItemConds  = new byte[TotalItems];
            }
            for (int i = 0; i < TotalItems; i++)
            {
                serializer.SerializeValue(ref ItemDefIds[i]);
                serializer.SerializeValue(ref ItemQtys[i]);
                serializer.SerializeValue(ref ItemConds[i]);
            }

            serializer.SerializeValue(ref NestedGroupCount);
            if (serializer.IsReader)
            {
                NestedParentSlot = new byte[NestedGroupCount];
                NestedParentItem = new int[NestedGroupCount];
                NestedItemCounts = new int[NestedGroupCount];
            }
            for (int i = 0; i < NestedGroupCount; i++)
            {
                serializer.SerializeValue(ref NestedParentSlot[i]);
                serializer.SerializeValue(ref NestedParentItem[i]);
                serializer.SerializeValue(ref NestedItemCounts[i]);
            }

            serializer.SerializeValue(ref TotalNested);
            if (serializer.IsReader)
            {
                NestedDefIds = new int[TotalNested];
                NestedQtys   = new float[TotalNested];
                NestedConds  = new byte[TotalNested];
            }
            for (int i = 0; i < TotalNested; i++)
            {
                serializer.SerializeValue(ref NestedDefIds[i]);
                serializer.SerializeValue(ref NestedQtys[i]);
                serializer.SerializeValue(ref NestedConds[i]);
            }
        }
    }

    /// <summary>Active hand-craft summary pushed to the owning client (0.2.10a). Ids only (rule 8);
    /// the client resolves the recipe name from RecipeRegistry and computes progress from the clock.</summary>
    internal struct CraftStatusPayload : INetworkSerializable
    {
        public int   RecipeId;
        public float StartTime;
        public float Duration;
        public byte  Active;   // 1 = a hand craft is in progress

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RecipeId);
            serializer.SerializeValue(ref StartTime);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref Active);
        }
    }

    /// <summary>Object-bound world tasks the dreamer is contributing to (gather / station craft),
    /// pushed to the owning client so its Actions HUD lists them (0.2.10). Ids only (rule 8): the
    /// client resolves names from the registries and computes progress from the synced accrual labor.
    /// Changes only when the task SET or a threshold changes — progress is read live, not synced here.</summary>
    internal struct WorldTaskPayload : INetworkSerializable
    {
        public int     Count;
        public int[]   InstanceIds;   // for the ✕ stop button
        public int[]   DefIds;        // → world-object displayName
        public int[]   ActionIndices; // → action label (gather)
        public int[]   RecipeIds;     // station craft → recipe name; 0 = gather
        public float[] Thresholds;    // labor-to-complete; client fraction = accrualLabor / threshold

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Count);
            if (serializer.IsReader)
            {
                InstanceIds   = new int[Count];
                DefIds        = new int[Count];
                ActionIndices = new int[Count];
                RecipeIds     = new int[Count];
                Thresholds    = new float[Count];
            }
            for (int i = 0; i < Count; i++)
            {
                serializer.SerializeValue(ref InstanceIds[i]);
                serializer.SerializeValue(ref DefIds[i]);
                serializer.SerializeValue(ref ActionIndices[i]);
                serializer.SerializeValue(ref RecipeIds[i]);
                serializer.SerializeValue(ref Thresholds[i]);
            }
        }
    }

    /// <summary>Active action + queue summary pushed to the owning client (0.2.3b3).</summary>
    internal struct ActionQueuePayload : INetworkSerializable
    {
        public int   ActiveDefId;
        public float ActiveStart;
        public float ActiveDuration;
        public byte  QueueCount;
        public float TimePool;
        /// <summary>Index of ActiveDefId's entry in the real slot queue (0.2.11a3). The queue now
        /// also carries object-bound labor and crafts, which this payload skips, so the consumable
        /// the HUD is showing is not necessarily at index 0 — the cancel button needs the real one.</summary>
        public byte  ActiveQueueIndex;
        /// <summary>Remaining trivial-bracket minutes (0.2.11d1/d7). Kept beside TimePool but a
        /// separate field: the HUD must show them apart, since bracket minutes cannot be spent on
        /// work and reading them as pool would badly mislead the player's planning.</summary>
        public float TrivialBracket;
        /// <summary>Committed minutes and commit clock of the active commitment (0.2.11c8), so the
        /// client can compute elapsed/remaining exactly rather than extrapolating.</summary>
        public float CommittedMinutes;
        public float CommitStart;
        /// <summary>Minutes still owed on whatever occupies the slot, however that work is measured
        /// — a committed window for object-bound labor, a duration for a needs task or consumable.
        /// This is what the wait-or-skip affordance offers to skip (0.2.11c5).</summary>
        public float SlotRemaining;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ActiveDefId);
            serializer.SerializeValue(ref ActiveStart);
            serializer.SerializeValue(ref ActiveDuration);
            serializer.SerializeValue(ref QueueCount);
            serializer.SerializeValue(ref TimePool);
            serializer.SerializeValue(ref ActiveQueueIndex);
            serializer.SerializeValue(ref TrivialBracket);
            serializer.SerializeValue(ref CommittedMinutes);
            serializer.SerializeValue(ref CommitStart);
            serializer.SerializeValue(ref SlotRemaining);
        }
    }

    /// <summary>
    /// Per-dreamer NetworkBehaviour that replicates the dreamer's multi-container inventory
    /// and action queue to the owning client (TDD §5.4, 0.2.0b / 0.2.3b / 0.2.5).
    /// Updated at 0.2.9b to use Instance throughout (replaces ItemInstance / WorldItem).
    /// </summary>
    public class DreamerInventorySync : NetworkBehaviour
    {
        // ── Static slot registry ─────────────────────────────────────────────────

        private static readonly Dictionary<int, DreamerInventorySync> _instancesBySlot =
            new Dictionary<int, DreamerInventorySync>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instancesBySlot.Clear();

        public static DreamerInventorySync GetForSlot(int slot) =>
            _instancesBySlot.TryGetValue(slot, out var s) ? s : null;

        [SerializeField] private float _syncInterval = 1f;

        private readonly NetworkVariable<InventoryPayload> _inventory = new NetworkVariable<InventoryPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ActionQueuePayload> _actionQueue = new NetworkVariable<ActionQueuePayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<CraftStatusPayload> _craft = new NetworkVariable<CraftStatusPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<WorldTaskPayload> _worldTasks = new NetworkVariable<WorldTaskPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DreamerNetworkAdapter _adapter;
        private InventoryPayload      _lastSynced;
        private ActionQueuePayload    _lastActionSynced;
        private CraftStatusPayload    _lastCraftSynced;
        private WorldTaskPayload      _lastWorldTasksSynced;
        private NeedsConfig           _needsConfig;
        private BuffConfig            _buffConfig;
        private SpoilageConfig        _spoilageConfig;
        private float                 _syncTimer;
        private bool                  _inventoryOpen;

        private string _devContainerDefId = "1";
        private string _devAddDefId       = "2";
        private string _devAddQty         = "1";

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _adapter = GetComponent<DreamerNetworkAdapter>();
            if (IsServer)
            {
                _instancesBySlot[_adapter.Slot] = this;
                PushFromRdm();
                CommitActionPayload(BuildActionPayload());
                CommitCraftPayload(BuildCraftPayload());
                CommitWorldTaskPayload(BuildWorldTaskPayload());
            }
        }

        public override void OnNetworkDespawn()
        {
            UiFocus.Release(this);
            if (IsServer && _adapter != null)
                _instancesBySlot.Remove(_adapter.Slot);
            base.OnNetworkDespawn();
        }

        // ── Server convergence tick ──────────────────────────────────────────────

        private void Update()
        {
            if (IsOwner && Input.GetKeyDown(KeyCode.I))
                _inventoryOpen = !_inventoryOpen;

            // The inventory panel is click-driven; gameplay runs with the cursor locked (FP build b2).
            if (IsOwner) UiFocus.Set(this, _inventoryOpen);

            if (!IsServer) return;
            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncInterval) return;
            _syncTimer = 0f;
            CheckHandCraftCompletion();   // fire any hand craft whose window has elapsed (§5.7.3)
            PushFromRdm();
            PushActionFromRdm();
            PushCraftFromRdm();
            PushWorldTasksFromRdm();
        }

        // ── Inventory NV ─────────────────────────────────────────────────────────

        private InventoryPayload BuildPayload()
        {
            if (_adapter == null) return default;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer?.equipSlots == null) return default;

            float now  = GetCurrentClockTime();
            var   defs = DefRegistry.Instance;

            var allSlotTypes      = new List<byte>();
            var allContainerDefs  = new List<int>();
            var allContainerQtys  = new List<float>();
            var allContainerConds = new List<byte>();
            var allSlotItemCounts = new List<int>();

            var allItemDefs  = new List<int>();
            var allItemQtys  = new List<float>();
            var allItemConds = new List<byte>();

            var nestedParentSlot  = new List<byte>();
            var nestedParentItem  = new List<int>();
            var nestedItemCounts  = new List<int>();
            var nestedDefIds      = new List<int>();
            var nestedQtys        = new List<float>();
            var nestedConds       = new List<byte>();

            foreach (var (type, container) in dreamer.equipSlots.AllOccupied())
            {
                byte contCond = container.rate > 0f
                    ? (byte)Mathf.Clamp(Mathf.RoundToInt(container.ComputeCondition(now)), 0, 100)
                    : InventoryPayload.NonPerishable;
                allSlotTypes.Add((byte)type);
                allContainerDefs.Add(container.defId);
                allContainerQtys.Add(container.quantity);
                allContainerConds.Add(contCond);

                var items = container.contents?.items;
                int count = items?.Count ?? 0;
                allSlotItemCounts.Add(count);

                for (int i = 0; i < count; i++)
                {
                    var  inst   = items[i];
                    var  instSo = defs?.Get(inst.defId);
                    byte cond;
                    if (instSo != null && instSo.isDurabilityTool && instSo.maxDurability > 0f)
                        cond = (byte)Mathf.Clamp(Mathf.RoundToInt(inst.durability / instSo.maxDurability * 100f), 0, 100);
                    else
                        cond = inst.rate > 0f
                            ? (byte)Mathf.Clamp(Mathf.RoundToInt(inst.ComputeCondition(now)), 0, 100)
                            : InventoryPayload.NonPerishable;
                    allItemDefs.Add(inst.defId);
                    allItemQtys.Add(inst.quantity);
                    allItemConds.Add(cond);

                    if (inst.contents?.items.Count > 0)
                    {
                        nestedParentSlot.Add((byte)type);
                        nestedParentItem.Add(i);
                        nestedItemCounts.Add(inst.contents.items.Count);
                        foreach (var nested in inst.contents.items)
                        {
                            byte nCond = nested.rate > 0f
                                ? (byte)Mathf.Clamp(Mathf.RoundToInt(nested.ComputeCondition(now)), 0, 100)
                                : InventoryPayload.NonPerishable;
                            nestedDefIds.Add(nested.defId);
                            nestedQtys.Add(nested.quantity);
                            nestedConds.Add(nCond);
                        }
                    }
                }
            }

            float carried = defs != null ? dreamer.equipSlots.ComputeCarriedWeight(defs) : 0f;

            return new InventoryPayload
            {
                CarriedWeight    = carried,
                OccupiedCount    = allSlotTypes.Count,
                SlotTypes        = allSlotTypes.ToArray(),
                ContainerDefIds  = allContainerDefs.ToArray(),
                ContainerQtys    = allContainerQtys.ToArray(),
                ContainerConds   = allContainerConds.ToArray(),
                SlotItemCounts   = allSlotItemCounts.ToArray(),
                TotalItems       = allItemDefs.Count,
                ItemDefIds       = allItemDefs.ToArray(),
                ItemQtys         = allItemQtys.ToArray(),
                ItemConds        = allItemConds.ToArray(),
                NestedGroupCount = nestedParentSlot.Count,
                NestedParentSlot = nestedParentSlot.ToArray(),
                NestedParentItem = nestedParentItem.ToArray(),
                NestedItemCounts = nestedItemCounts.ToArray(),
                TotalNested      = nestedDefIds.Count,
                NestedDefIds     = nestedDefIds.ToArray(),
                NestedQtys       = nestedQtys.ToArray(),
                NestedConds      = nestedConds.ToArray(),
            };
        }

        private void CommitPayload(InventoryPayload payload)
        { _lastSynced = payload; _inventory.Value = payload; }

        private void PushFromRdm()
        {
            var payload = BuildPayload();
            if (PayloadEquals(payload, _lastSynced)) return;
            CommitPayload(payload);
        }

        public void ForcePush()
        {
            if (!IsServer) return;
            CommitPayload(BuildPayload());
        }

        // ── Action queue NV ───────────────────────────────────────────────────────

        private ActionQueuePayload BuildActionPayload()
        {
            if (_adapter == null) return default;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return default;

            // 0.2.11a3: the slot queue is now shared with object-bound labor and crafts, which have
            // their own HUD payloads (_worldTasks / _craft). Report only the consumable-style
            // entries here, so the "Actions" list neither double-counts them nor offers a ✕ that
            // the consumable cancel path would refuse.
            var p = new ActionQueuePayload
            {
                TimePool       = dreamer.timePool,
                TrivialBracket = dreamer.trivialBracket,
            };

            // The active slot entry — including object-bound work and needs tasks, which the
            // consumable scan below skips but which the c8 commitment HUD and the c5 wait-or-skip
            // affordance both need.
            float nowM   = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;
            var   active = dreamer.ActiveAction();
            if (active != null && active.started)
            {
                if (active.committedMinutes > 0f)
                {
                    p.CommittedMinutes = active.committedMinutes;
                    p.CommitStart      = active.commitStart;
                    p.SlotRemaining    = active.CommittedRemainingAt(nowM);
                }
                else
                {
                    p.SlotRemaining = Mathf.Max(0f, active.duration - active.ElapsedAt(nowM));
                }
            }

            int count = 0, activeIndex = -1;
            var queue = dreamer.actionQueue;
            for (int i = 0; queue != null && i < queue.Count; i++)
            {
                // Skip object-bound work (externallyResolved) and needs tasks: both have their own
                // HUD source (_worldTasks / DreamerTaskSync), and a needs task has no itemDefId, so
                // listing it here would render a "def#0" row with a cancel button that does nothing.
                if (queue[i] == null || queue[i].externallyResolved || queue[i].kind == ActionSlotKind.Task) continue;
                count++;
                if (activeIndex >= 0) continue;

                activeIndex      = i;
                p.ActiveDefId    = queue[i].itemDefId;
                // Report a window only once the entry is actually running — an entry still waiting
                // behind object-bound work has no start time, and a stale one would draw a bar that
                // is already "finished".
                p.ActiveStart    = queue[i].started ? queue[i].startTime : 0f;
                p.ActiveDuration = queue[i].started ? queue[i].duration  : 0f;
            }

            p.QueueCount       = (byte)Math.Min(count, 255);
            p.ActiveQueueIndex = (byte)Math.Max(0, activeIndex);
            return p;
        }

        private void CommitActionPayload(ActionQueuePayload p)
        { _lastActionSynced = p; _actionQueue.Value = p; }

        private void PushActionFromRdm() => CommitActionPayload(BuildActionPayload());

        public void ForcePushAction()
        {
            if (!IsServer) return;
            CommitActionPayload(BuildActionPayload());
        }

        public float TimePool       => _actionQueue.Value.TimePool;
        /// <summary>Remaining trivial-bracket minutes (0.2.11d1). Read by the needs HUD.</summary>
        public float TrivialBracket => _actionQueue.Value.TrivialBracket;

        // ── Craft status NV (0.2.10a) ─────────────────────────────────────────────

        private CraftStatusPayload BuildCraftPayload()
        {
            if (_adapter == null) return default;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            var c       = dreamer?.craft;
            if (c == null) return default;
            return new CraftStatusPayload
            {
                RecipeId  = c.recipeId,
                StartTime = c.startTime,
                Duration  = c.duration,
                Active    = 1,
            };
        }

        private static bool CraftPayloadEquals(CraftStatusPayload a, CraftStatusPayload b) =>
            a.Active == b.Active && a.RecipeId == b.RecipeId &&
            a.StartTime == b.StartTime && a.Duration == b.Duration;

        private void CommitCraftPayload(CraftStatusPayload p)
        { _lastCraftSynced = p; _craft.Value = p; }

        private void PushCraftFromRdm()
        {
            var p = BuildCraftPayload();
            if (!CraftPayloadEquals(p, _lastCraftSynced)) CommitCraftPayload(p);
        }

        public void ForcePushCraft()
        {
            if (!IsServer) return;
            CommitCraftPayload(BuildCraftPayload());
        }

        // ── World-task summary NV (gather / station craft, 0.2.10) ────────────────

        private WorldTaskPayload BuildWorldTaskPayload()
        {
            if (_adapter == null) return default;
            var tasks = MapEntitySync.Instance?.GetActiveTasksFor(_adapter.Slot);
            if (tasks == null || tasks.Count == 0) return default;

            int n = tasks.Count;
            var p = new WorldTaskPayload
            {
                Count         = n,
                InstanceIds   = new int[n],
                DefIds        = new int[n],
                ActionIndices = new int[n],
                RecipeIds     = new int[n],
                Thresholds    = new float[n],
            };
            for (int i = 0; i < n; i++)
            {
                var t = tasks[i];
                p.InstanceIds[i]   = t.instanceId;
                p.DefIds[i]        = t.defId;
                p.ActionIndices[i] = t.actionIndex;
                p.RecipeIds[i]     = t.recipeId;
                p.Thresholds[i]    = t.threshold;
            }
            return p;
        }

        private static bool WorldTaskPayloadEquals(WorldTaskPayload a, WorldTaskPayload b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a.InstanceIds[i]   != b.InstanceIds[i]   ||
                    a.DefIds[i]        != b.DefIds[i]        ||
                    a.ActionIndices[i] != b.ActionIndices[i] ||
                    a.RecipeIds[i]     != b.RecipeIds[i]     ||
                    a.Thresholds[i]    != b.Thresholds[i]) return false;
            return true;
        }

        private void CommitWorldTaskPayload(WorldTaskPayload p)
        { _lastWorldTasksSynced = p; _worldTasks.Value = p; }

        private void PushWorldTasksFromRdm()
        {
            var p = BuildWorldTaskPayload();
            if (!WorldTaskPayloadEquals(p, _lastWorldTasksSynced)) CommitWorldTaskPayload(p);
        }

        // ── Inventory equality guard ──────────────────────────────────────────────

        private static bool PayloadEquals(InventoryPayload a, InventoryPayload b)
        {
            if (a.OccupiedCount    != b.OccupiedCount
                || a.TotalItems    != b.TotalItems
                || a.TotalNested   != b.TotalNested
                || a.NestedGroupCount != b.NestedGroupCount
                || a.CarriedWeight != b.CarriedWeight) return false;
            for (int i = 0; i < a.OccupiedCount; i++)
                if (a.SlotTypes[i]       != b.SlotTypes[i]       ||
                    a.ContainerDefIds[i] != b.ContainerDefIds[i] ||
                    a.ContainerQtys[i]   != b.ContainerQtys[i]   ||
                    a.ContainerConds[i]  != b.ContainerConds[i]  ||
                    a.SlotItemCounts[i]  != b.SlotItemCounts[i]) return false;
            for (int i = 0; i < a.TotalItems; i++)
                if (a.ItemDefIds[i] != b.ItemDefIds[i] ||
                    a.ItemQtys[i]   != b.ItemQtys[i]   ||
                    a.ItemConds[i]  != b.ItemConds[i]) return false;
            for (int i = 0; i < a.NestedGroupCount; i++)
                if (a.NestedParentSlot[i] != b.NestedParentSlot[i] ||
                    a.NestedParentItem[i] != b.NestedParentItem[i] ||
                    a.NestedItemCounts[i] != b.NestedItemCounts[i]) return false;
            for (int i = 0; i < a.TotalNested; i++)
                if (a.NestedDefIds[i] != b.NestedDefIds[i] ||
                    a.NestedQtys[i]   != b.NestedQtys[i]   ||
                    a.NestedConds[i]  != b.NestedConds[i]) return false;
            return true;
        }

        // ── Dev: equip a fresh container instance into a slot (0.2.5a) ──────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevEquipContainerServerRpc(int containerDefId, byte slotType)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;
            var so = DefRegistry.Instance?.Get(containerDefId);
            if (so == null || !so.isContainer) return;
            var slotEnum = (EquipSlotType)slotType;
            if (dreamer.equipSlots.GetSlot(slotEnum) != null) return;
            dreamer.equipSlots.SetSlot(slotEnum, new Instance { defId = containerDefId, quantity = 1f });
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Dev: equipped container defId={containerDefId} in slot {slotEnum} for dreamer {_adapter.Slot}.");
        }

        // ── Dev: add item to first available container (0.2.0a5, updated 0.2.5) ──

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevAddItemServerRpc(int defId, float quantity)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;
            float now  = GetCurrentClockTime();
            var   inst = new Instance { defId = defId, quantity = quantity };
            var   so   = DefRegistry.Instance?.Get(defId);
            if (so != null && so.perishable && so.effectiveLifespan > 0f)
                inst.InitConditionTriple(now, so.effectiveLifespan);
            if (so != null && so.isDurabilityTool && so.maxDurability > 0f)
                inst.durability = so.maxDurability;
            if (!TryAddToEquipment(dreamer, inst, now))
                Debug.Log($"[DreamerInventorySync] Dev: no container with space for dreamer {_adapter.Slot}.");
            else
            {
                ForcePush();
                Debug.Log($"[DreamerInventorySync] Dev: added defId={defId} qty={quantity} to dreamer {_adapter.Slot}.");
            }
        }

        // ── Equip container on pickup (unified path) ─────────────────────────────

        /// <summary>
        /// Host: equip a container Instance — already removed from the world by the caller — into its
        /// slot. Applies storage-modifier re-stamps to its contents and force-pushes the inventory NV.
        /// Returns false (the caller re-adds it to the world) when the target slot is occupied.
        /// Called by <see cref="MapEntitySync.InstantPickup"/> so a container's shared `pickup` action
        /// auto-equips — this subsumes the former EquipContainerServerRpc / PickupItemServerRpc
        /// container branch, keeping one pickup path for every item (TDD §1.12).
        /// </summary>
        public bool TryEquipContainerFromWorld(Instance container, float now)
        {
            if (!IsServer) return false;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return false;

            var defs = DefRegistry.Instance;
            var so   = defs?.Get(container.defId);
            if (so == null || !so.isContainer) return false;

            var slotEnum = so.equipsIntoSlot;
            if (dreamer.equipSlots.GetSlot(slotEnum) != null) return false;

            if (container.contents != null)
                foreach (var item in container.contents.items)
                    ApplyStorageModifier(item, container, now, defs);

            container.location = new InstanceLocation { kind = LocationKind.InContainer };
            dreamer.equipSlots.SetSlot(slotEnum, container);
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Equipped container def={container.defId} in slot {slotEnum} for dreamer {_adapter.Slot}.");
            return true;
        }

        // ── Unequip container to ground (0.2.5c) ─────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void UnequipContainerServerRpc(byte slotType)
        {
            var world   = RuntimeDataManager.Instance?.WorldState;
            var layer   = RuntimeDataManager.Instance?.CurrentMapLayer;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (world == null || layer == null || dreamer == null) return;

            var slotEnum  = (EquipSlotType)slotType;
            var container = dreamer.equipSlots.GetSlot(slotEnum);
            if (container == null) return;

            float now  = GetCurrentClockTime();
            var   defs = DefRegistry.Instance;
            if (container.contents != null)
                foreach (var item in container.contents.items)
                    RemoveStorageModifier(item, container, now, defs);

            var pos = RuntimeDataManager.Instance.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;
            pos = MapEntitySync.Instance != null
                ? MapEntitySync.Instance.ResolveGroundPosition(pos, container.defId)
                : pos;
            container.id = layer.nextInstanceId++;
            container.location = new InstanceLocation { kind = LocationKind.InWorld, position = pos.ToFloat3() };
            layer.worldObjects.items.Add(container);
            dreamer.equipSlots.SetSlot(slotEnum, null);
            MapEntitySync.Instance?.ForcePush();
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Unequipped container def={container.defId} from slot {slotEnum} for dreamer {_adapter.Slot}.");
        }

        // ── Move item between top-level containers (0.2.5a) ──────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void MoveItemServerRpc(byte fromSlotType, int fromItemIndex, byte toSlotType)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;

            var fromContainer = dreamer.equipSlots.GetSlot((EquipSlotType)fromSlotType);
            var toContainer   = dreamer.equipSlots.GetSlot((EquipSlotType)toSlotType);
            if (fromContainer?.contents == null || toContainer == null) return;
            if (fromItemIndex < 0 || fromItemIndex >= fromContainer.contents.items.Count) return;

            var  inst = fromContainer.contents.items[fromItemIndex];
            var  defs = DefRegistry.Instance;
            if (!CanAddToContainer(toContainer, inst, defs)) return;

            float now = GetCurrentClockTime();
            fromContainer.contents.items.RemoveAt(fromItemIndex);
            RemoveStorageModifier(inst, fromContainer, now, defs);
            ApplyStorageModifier(inst, toContainer, now, defs);
            toContainer.contents ??= new InstanceContainer();
            toContainer.contents.TryAdd(inst, defs, now, GetSpoilageConfig());
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Moved def={inst.defId} from {(EquipSlotType)fromSlotType} to {(EquipSlotType)toSlotType}.");
        }

        // Pickup is unified (TDD §1.12): the shared `pickup` ActionDef on every inventory-capable Def
        // routes through Interactable → DreamerTaskSync.DispatchWorldActionServerRpc →
        // MapEntitySync.InstantPickup (container → TryEquipContainerFromWorld; other → TryDeliverItem).
        // The former proximity-panel PickupItemServerRpc / EquipContainerServerRpc are removed.

        // ── Drop item from container to ground (0.2.1c, updated 0.2.5) ──────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DropItemServerRpc(byte slotType, int itemIndex)
        {
            var world   = RuntimeDataManager.Instance?.WorldState;
            var layer   = RuntimeDataManager.Instance?.CurrentMapLayer;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (world == null || layer == null || dreamer == null) return;

            var container = dreamer.equipSlots.GetSlot((EquipSlotType)slotType);
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;

            float now  = GetCurrentClockTime();
            var   defs = DefRegistry.Instance;
            var   inst = container.contents.items[itemIndex];
            var   pos  = RuntimeDataManager.Instance.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;

            RemoveStorageModifier(inst, container, now, defs);
            container.contents.items.RemoveAt(itemIndex);
            pos = MapEntitySync.Instance != null
                ? MapEntitySync.Instance.ResolveGroundPosition(pos, inst.defId)
                : pos;
            inst.id = layer.nextInstanceId++;
            inst.location = new InstanceLocation { kind = LocationKind.InWorld, position = pos.ToFloat3() };
            // Rebuild world accrual: a dropped world-action object (e.g. Large Tree Log) lost its
            // accrual when it entered the container as a plain item. Without this it drops as an
            // inert ground instance (ActionCount 0) — not processable or re-pickable. Plain items
            // stay accrual-null.
            MapEntitySync.InitWorldAccrual(inst);
            layer.worldObjects.items.Add(inst);
            MapEntitySync.Instance?.ForcePush();
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Dropped def={inst.defId} from slot {(EquipSlotType)slotType} for dreamer {_adapter.Slot}.");
        }

        // ── Consume intent (0.2.2b / 0.2.3b+d, updated 0.2.5) ───────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ConsumeItemServerRpc(byte slotType, int itemIndex)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;

            var container = dreamer.equipSlots.GetSlot((EquipSlotType)slotType);
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;

            var inst = container.contents.items[itemIndex];
            var def  = DefRegistry.Instance?.Get(inst.defId);
            if (def == null || def.consumeEffect == ConsumeEffect.None) return;

            float consumeNow = GetCurrentClockTime();

            if (inst.IsSpoiled(consumeNow) &&
                (def.consumeEffect == ConsumeEffect.Food || def.consumeEffect == ConsumeEffect.Drink))
            {
                inst.quantity -= 1f;
                if (inst.quantity <= 0f) container.contents.items.RemoveAt(itemIndex);
                ApplySicknessDebuff(dreamer);
                ForcePush(); ForcePushAction();
                Debug.Log($"[DreamerInventorySync] Consumed SPOILED def={inst.defId} slot {_adapter.Slot} — sickness applied.");
                return;
            }

            bool  isPartial = inst.remaining.HasValue;
            float duration, hungerTotal, thirstTotal;

            if (isPartial)
            {
                float rem = inst.remaining.Value;
                if (def.consumeEffect == ConsumeEffect.SaveConsumable)
                {
                    duration = rem; hungerTotal = 0f; thirstTotal = 0f;
                }
                else
                {
                    float baseRestore = def.hungerRestore > 0f ? def.hungerRestore : def.thirstRestore;
                    float fraction    = baseRestore > 0.001f ? rem / baseRestore : 0f;
                    duration    = def.timeCost * fraction;
                    hungerTotal = def.hungerRestore > 0f ? rem : 0f;
                    thirstTotal = def.thirstRestore > 0f
                        ? (def.hungerRestore > 0f ? def.thirstRestore * fraction : rem) : 0f;
                }
            }
            else
            {
                duration = def.timeCost; hungerTotal = def.hungerRestore; thirstTotal = def.thirstRestore;
            }

            // ── Trivial execution against the bracket (§5.5.5, 0.2.11d2/d5/d6) ───────
            //
            // A Trivial consumable resolves NOW: ~1s of wall clock, no world-clock advance at all,
            // and its duration debited from the trivial bracket instead of the day pool. That is
            // what stops a handful of maintenance actions costing six real minutes each.
            //
            // Three outcomes, in order:
            //   bracket covers it            → instant, debit the bracket (d2)
            //   bracket short, but critical  → instant anyway, bracket floors at 0 (d5)
            //   bracket short, not critical  → fall through and run as a normal Active action at
            //                                  full duration (d6). Forgiving beats hard-blocking,
            //                                  and it keeps the bypass list short.
            bool instant       = duration <= 0f;
            bool trivialBudget = false;
            if (!instant && def.consumeActionClass == ActionClass.Trivial)
            {
                // Eating and drinking ALWAYS execute instantly. They are not gated on the bracket
                // and never fall back to the Active path: a meal must never occupy the single action
                // slot, because a dreamer who cannot eat without abandoning their work is a dreamer
                // the presence rule has turned against the player. The bracket is still debited so
                // the accounting stays honest, but it floors at 0 rather than blocking.
                if (def.isAlwaysInstantConsumable)                { instant = true; trivialBudget = true; }
                else if (dreamer.trivialBracket >= duration)      { instant = true; trivialBudget = true; }
                else if (def.consumeAllowsCriticalBypass)
                {
                    instant = true; trivialBudget = true;
                    Debug.Log($"[DreamerInventorySync] Slot {_adapter.Slot}: critical bypass — consuming " +
                              $"def={inst.defId} on a {dreamer.trivialBracket:F0} min bracket (d5).");
                }
                else
                {
                    Debug.Log($"[DreamerInventorySync] Slot {_adapter.Slot}: bracket too short for " +
                              $"def={inst.defId} ({dreamer.trivialBracket:F0} < {duration:F0}) — running it " +
                              $"as an Active action instead (d6).");
                }
            }

            if (instant)
            {
                // Blocked only by the single-slot rule, and only when it costs bracket: a genuinely
                // zero-duration consumable never occupied a slot and must stay usable mid-work.
                if (trivialBudget)
                    dreamer.trivialBracket = Mathf.Max(0f, dreamer.trivialBracket - duration);

                inst.quantity -= 1f;
                if (inst.quantity <= 0f) container.contents.items.RemoveAt(itemIndex);
                switch (def.consumeEffect)
                {
                    case ConsumeEffect.Food:
                    case ConsumeEffect.Drink:
                        // Trivial payout is immediate and whole — there is no window to spread a
                        // nourishment buff over, because the world clock does not move.
                        dreamer.needs.hunger = Mathf.Min(100f, dreamer.needs.hunger + hungerTotal);
                        dreamer.needs.thirst = Mathf.Min(100f, dreamer.needs.thirst + thirstTotal);
                        GetComponent<DreamerNeedsSync>()?.ForcePush();
                        break;
                    case ConsumeEffect.SaveConsumable:
                        GameFlowManager.Instance?.UseDebugSaveConsumable(_adapter.Slot);
                        break;
                }
                ForcePush(); ForcePushAction();
                return;
            }

            if (!def.allowsOverdraft && dreamer.timePool < duration)
            {
                Debug.Log($"[DreamerInventorySync] Action blocked: pool exhausted for dreamer {_adapter.Slot}.");
                return;
            }

            inst.quantity -= 1f;
            if (inst.quantity <= 0f) container.contents.items.RemoveAt(itemIndex);
            dreamer.timePool -= duration;
            dreamer.energy    = Mathf.Max(0f, dreamer.energy - def.energyCost);
            if (dreamer.timePool < 0f) ApplyExhaustionDebuff(dreamer);

            var payoutShape = def.consumeEffect == ConsumeEffect.SaveConsumable ? ActionPayout.EndEffect : ActionPayout.Gradual;
            var endEffect   = def.consumeEffect == ConsumeEffect.SaveConsumable ? ActionEndEffect.DreamSave : ActionEndEffect.None;

            dreamer.actionQueue ??= new List<ActionRecord>();
            dreamer.actionQueue.Add(new ActionRecord
            {
                itemDefId             = inst.defId,
                itemQuantity          = 1f,
                consumedItemRemaining = inst.remaining,
                timeCost              = duration,
                energyCost            = def.energyCost,
                duration              = duration,
                hungerTotal           = hungerTotal,
                thirstTotal           = thirstTotal,
                payout                = payoutShape,
                endEffect             = endEffect,
                interruptsSkip        = def.interruptsSkip,
                started               = false,
                // 0.2.11a1: the class is baked in at commit so a def re-authored mid-action does not
                // change it in flight. Consumables keep FIFO queue-ahead on the surviving slot —
                // that is what makes standing at a tree for the 1× wait tolerable (§5.5.16).
                kind                  = ActionSlotKind.Consumable,
                taskType              = TaskType.Eating,
                actionClass           = def.consumeActionClass,
            });

            ForcePush(); ForcePushAction();
            GetComponent<DreamerNeedsSync>()?.ForcePush();
            Debug.Log($"[DreamerInventorySync] Queued action def={inst.defId} dur={duration:F1}m for dreamer {_adapter.Slot}.");
        }

        // ── Cancel intent (0.2.3c/d, updated 0.2.5) ─────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void CancelActionServerRpc(int queueIndex)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer?.actionQueue == null || queueIndex < 0 || queueIndex >= dreamer.actionQueue.Count) return;

            var   action = dreamer.actionQueue[queueIndex];
            float now    = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;

            // 0.2.11a3: the queue now also holds object-bound labor and crafts. Those carry reserved
            // materials, open accrual segments and per-contributor refunds — none of which this
            // consumable refund path knows about — so they must be stopped through their own action
            // (StopWorldActionServerRpc / StopStationCraftServerRpc / CancelCraftServerRpc).
            if (action.externallyResolved)
            {
                Debug.LogWarning($"[DreamerInventorySync] CancelAction ignored: slot entry {queueIndex} is " +
                                 $"{action.kind}; stop it through its own action.");
                return;
            }

            if (queueIndex == 0 && action.started)
            {
                float elapsed           = Mathf.Max(0f, now - action.startTime);
                float fraction          = Mathf.Clamp01(elapsed / Mathf.Max(action.duration, 0.001f));
                float remainingFraction = 1f - fraction;

                dreamer.timePool += action.timeCost * remainingFraction;
                dreamer.energy    = Mathf.Min(100f, dreamer.energy + action.energyCost * remainingFraction);
                dreamer.buffs?.RemoveAll(b =>
                    b.defId == ActionRecord.NourishmentHungerBuffId ||
                    b.defId == ActionRecord.NourishmentThirstBuffId);

                float partialRemaining = 0f;
                if (action.payout == ActionPayout.EndEffect)
                    partialRemaining = action.duration * remainingFraction;
                else if (action.hungerTotal > 0f)
                    partialRemaining = action.hungerTotal * remainingFraction;
                else if (action.thirstTotal > 0f)
                    partialRemaining = action.thirstTotal * remainingFraction;

                if (partialRemaining > 0.05f)
                    TryAddToEquipment(dreamer, new Instance
                    {
                        defId     = action.itemDefId,
                        quantity  = 1f,
                        remaining = partialRemaining,
                    }, now);

                Debug.Log($"[DreamerInventorySync] Cancelled active action def={action.itemDefId} at {fraction*100:F0}% for dreamer {_adapter.Slot}.");
            }
            else
            {
                dreamer.timePool += action.timeCost;
                dreamer.energy    = Mathf.Min(100f, dreamer.energy + action.energyCost);
                TryAddToEquipment(dreamer, new Instance
                {
                    defId     = action.itemDefId,
                    quantity  = action.itemQuantity,
                    remaining = action.consumedItemRemaining,
                }, now);
                Debug.Log($"[DreamerInventorySync] Cancelled queued action def={action.itemDefId} (full refund) for dreamer {_adapter.Slot}.");
            }

            dreamer.RemoveAt(queueIndex);
            ForcePush(); ForcePushAction();
            GetComponent<DreamerNeedsSync>()?.ForcePush();
        }

        // ── Debug re-stamp (0.2.4b3, updated 0.2.5) ─────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevReStampItemServerRpc(byte slotType, int itemIndex, float newModifier)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;
            var container = dreamer.equipSlots.GetSlot((EquipSlotType)slotType);
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;
            var inst = container.contents.items[itemIndex];
            var so   = DefRegistry.Instance?.Get(inst.defId);
            if (so == null || !so.perishable || so.effectiveLifespan <= 0f) return;
            inst.ReStamp(GetCurrentClockTime(), (100f / so.effectiveLifespan) * newModifier);
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Dev: re-stamped {(EquipSlotType)slotType}[{itemIndex}] modifier={newModifier}.");
        }

        // ── Split intent (0.2.2a3, updated 0.2.5) ────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SplitItemServerRpc(byte slotType, int itemIndex)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;
            var container = dreamer.equipSlots.GetSlot((EquipSlotType)slotType);
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;
            float half = Mathf.Floor(container.contents.items[itemIndex].quantity / 2f);
            if (container.contents.Split(itemIndex, half))
                ForcePush();
        }

        // ── Debug use-tool (0.2.6a3) ─────────────────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void DevUseToolServerRpc(byte slotType, int itemIndex)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return;
            var container = dreamer.equipSlots.GetSlot((EquipSlotType)slotType);
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;
            var inst = container.contents.items[itemIndex];
            var so   = DefRegistry.Instance?.Get(inst.defId);
            if (so == null || !so.isDurabilityTool || so.maxDurability <= 0f) return;
            if (inst.durability <= 0f)
            {
                Debug.Log($"[DreamerInventorySync] Dev use rejected: tool def={inst.defId} is already broken.");
                return;
            }
            bool broke = inst.ApplyUse(so.maxDurability * 0.1f);
            if (broke)
            {
                // 0.2.6 revision (§5.7.6, 0.2.10d3): durability 0 = DESTROY-and-REPLACE, not a broken
                // flag. The worn instance is removed and a fresh brokenForm instance takes its place in
                // the same container slot (location inherited, stats/aspects/icon come from the new Def).
                // No lingering 0-durability tool; revert across the break is just instances in the save.
                ReplaceToolWithBrokenForm(container, itemIndex, so.brokenFormDefId, GetCurrentClockTime());
            }
            ForcePush();
            Debug.Log($"[DreamerInventorySync] Dev: used tool def={inst.defId}, durability={inst.durability:F1}/{so.maxDurability:F1}{(broke ? " — BROKEN → destroy+replace" : "")}.");
        }

        /// <summary>
        /// Destroy-and-replace a worn-out tool in a container with its broken form (§5.7.6, 0.2.10d3).
        /// Removes the 0-durability instance and adds a fresh brokenFormDefId instance to the same
        /// container. If the tool names no broken form (brokenFormDefId 0) the instance is simply
        /// destroyed (no replacement). Location is inherited; nothing else carries over.
        /// </summary>
        private void ReplaceToolWithBrokenForm(Instance container, int itemIndex, int brokenFormDefId, float now)
        {
            if (container?.contents == null || itemIndex < 0 || itemIndex >= container.contents.items.Count) return;
            container.contents.items.RemoveAt(itemIndex);   // destroy the worn tool
            if (brokenFormDefId <= 0) return;               // no broken form authored → just destroyed
            container.contents.TryAdd(MakeItemInstance(brokenFormDefId, 1f, now),
                                      DefRegistry.Instance, now, GetSpoilageConfig());
        }

        // ── Hand crafting (self-contained §5.5 task, TDD §5.7.1, 0.2.10a) ─────────

        /// <summary>
        /// Owner requests a hand/tool craft. Host validates the §5.7.1 gates (no station; tool
        /// category present if required; crafter free; affordable), reserves the materials/time/
        /// energy on commit (§5.7.0 / §5.5.3), and starts a self-contained timed craft on the
        /// dreamer. The tier is rolled at COMPLETION (§5.7.3) from the saved craft record, so
        /// quality is unknown until it finishes and re-derives identically across skip/revert.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestHandCraftServerRpc(int recipeId)
        {
            var world   = RuntimeDataManager.Instance?.WorldState;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (world == null || dreamer == null) return;

            var recipe = RecipeRegistry.Instance?.Get(recipeId);
            if (recipe == null)
            {
                Debug.LogWarning($"[DreamerInventorySync] Craft rejected: no recipe id={recipeId}.");
                return;
            }
            if (!recipe.IsKnown) return; // unlock gate inert until Cluster 9

            // Gate: station recipes are object-bound (§5.7.1) — routed through the station path (0.2.10c).
            if (!recipe.IsHandCraft)
            {
                Debug.LogWarning($"[DreamerInventorySync] Craft rejected: recipe '{recipe.displayName}' requires a station.");
                return;
            }
            // Gate: tool category (§5.7.1). requiredToolCategoryId 0 = hand-craftable.
            if (recipe.requiredToolCategoryId != 0 && !HasToolCategory(dreamer, recipe.requiredToolCategoryId))
            {
                Debug.LogWarning($"[DreamerInventorySync] Craft rejected: no tool of category {recipe.requiredToolCategoryId}.");
                return;
            }
            // Busy check: ONE active action at a time (§5.5, 0.2.11a3). The slot now covers every
            // kind of committed work — consumable, needs task, world labor, station craft — so this
            // one test replaces the old "task channel is Idle" check and closes the hole where a
            // dreamer could hand-craft while felling a tree on the other channel.
            if (dreamer.craft != null || dreamer.IsBusy())
            {
                Debug.LogWarning($"[DreamerInventorySync] Craft rejected: dreamer {_adapter.Slot} is busy ({dreamer.CurrentTaskType()}).");
                return;
            }
            // Affordability: crafts do not overdraft (§5.5.1). Hard gate on timePool + materials.
            if (dreamer.timePool < recipe.timeCost)
            {
                Debug.Log($"[DreamerInventorySync] Craft blocked: pool exhausted for dreamer {_adapter.Slot}.");
                return;
            }
            if (!CanAffordInputs(dreamer, recipe.inputs))
            {
                Debug.Log($"[DreamerInventorySync] Craft blocked: missing materials for '{recipe.displayName}'.");
                return;
            }

            float now      = GetCurrentClockTime();
            var   reserved = ConsumeInputs(dreamer, recipe.inputs);

            dreamer.timePool -= recipe.timeCost;
            dreamer.energy    = Mathf.Max(0f, dreamer.energy - recipe.energyCost);

            dreamer.craft = new CraftRecord
            {
                recipeId        = recipe.recipeId,
                crafterSlot     = _adapter.Slot,
                startTime       = now,
                duration        = recipe.timeCost,
                craftCounter    = world.craftCounter++,   // monotonic seed uniqueness (§5.7.3 b2)
                outputAmount    = Mathf.Max(1, recipe.outputAmount),
                rollGranularity = (int)recipe.rollGranularity,
                isStation       = false,
                reservedTime    = recipe.timeCost,
                reservedEnergy  = recipe.energyCost,
                reservedInputs  = reserved,
            };
            // Occupy the single action slot for the duration of the craft (0.2.11a3). Unlike the old
            // one-tick cosmetic task marker, this record is authoritative occupancy: it is what stops
            // the dreamer starting a second action. It is externallyResolved because completion is
            // driven by CheckHandCraftCompletion against the craft's own clock window, not by
            // SimResolver timing out a duration. dreamer.craft remains the craft PAYLOAD.
            dreamer.actionQueue ??= new List<ActionRecord>();
            dreamer.actionQueue.Insert(0, new ActionRecord
            {
                kind               = ActionSlotKind.HandCraft,
                taskType           = TaskType.Crafting,
                actionClass        = recipe.actionClass,
                recipeId           = recipe.recipeId,
                duration           = recipe.timeCost,
                externallyResolved = true,
                started            = true,
                startTime          = now,
                committedMinutes   = recipe.timeCost,
                commitStart        = now,
                // Presence anchor (§5.5.3, 0.2.11b3): a hand craft has no object to stand at, so it
                // binds to the spot where it was committed. Walking off cancels it.
                presenceAnchored   = true,
                presenceAnchor     = CurrentPresenceAnchor(),
            });

            ForcePush(); ForcePushCraft();
            GetComponent<DreamerTaskSync>()?.ForcePush();
            Debug.Log($"[DreamerInventorySync] Slot {_adapter.Slot} started hand craft '{recipe.displayName}' ({recipe.timeCost:F0}m, counter={dreamer.craft.craftCounter}).");
        }

        /// <summary>
        /// Owner cancels the active hand craft. Refunds the unelapsed time + energy (§5.5.4) and
        /// returns the reserved materials. (Partial/resume — materialising an in-progress item
        /// instead of refunding materials — is 0.2.10c2; a returns the materials in full.)
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void CancelCraftServerRpc() => CancelActiveHandCraft();

        /// <summary>Host-side body shared by the player's explicit cancel and the presence-loss
        /// cancel (0.2.11b3), so the two can never drift apart on refunds.</summary>
        private void CancelActiveHandCraft()
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            var c       = dreamer?.craft;
            if (c == null) return;

            float now               = GetCurrentClockTime();
            float elapsed           = Mathf.Max(0f, now - c.startTime);
            float remainingFraction = 1f - Mathf.Clamp01(elapsed / Mathf.Max(c.duration, 0.001f));

            dreamer.timePool += c.reservedTime * remainingFraction;
            dreamer.energy    = Mathf.Min(100f, dreamer.energy + c.reservedEnergy * remainingFraction);
            RefundInputs(dreamer, c.reservedInputs, now);

            dreamer.craft = null;
            ClearHandCraftSlot(dreamer);

            ForcePush(); ForcePushCraft();
            GetComponent<DreamerTaskSync>()?.ForcePush();
            Debug.Log($"[DreamerInventorySync] Slot {_adapter.Slot} cancelled hand craft (refunded {remainingFraction*100:F0}% cost + materials).");
        }

        /// <summary>
        /// Host tick: complete any hand craft whose window has elapsed (§5.5.0 timestamp model).
        /// Rolls the tier(s) at completion (§5.7.3) and delivers the outputs to the crafter's
        /// inventory (overflow drops at their feet). One path for real-time and skip.
        /// </summary>
        private void CheckHandCraftCompletion()
        {
            if (_adapter == null) return;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            var c       = dreamer?.craft;
            if (c == null) return;

            float now = GetCurrentClockTime();
            if (!c.HandComplete(now)) return;

            var recipe  = RecipeRegistry.Instance?.Get(c.recipeId);
            var outputs = CraftResolver.BuildOutputs(recipe, c, now);

            dreamer.craft = null;
            ClearHandCraftSlot(dreamer);

            var pos = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;
            foreach (var outInst in outputs)
                if (!TryAddToEquipment(dreamer, outInst, now))
                    MapEntitySync.Instance?.PlaceItem(outInst.defId, outInst.quantity, pos);

            ForcePush(); ForcePushCraft();
            GetComponent<DreamerTaskSync>()?.ForcePush();
            Debug.Log($"[DreamerInventorySync] Slot {_adapter.Slot} completed hand craft '{recipe?.displayName}' → {outputs.Count} stack(s).");
        }

        /// <summary>Where this dreamer is standing right now, as the Simulation-side Float3 an
        /// ActionRecord's presence anchor holds (0.2.11b3).</summary>
        private Float3 CurrentPresenceAnchor()
        {
            var p = RuntimeDataManager.Instance?.GetDreamerPosition(_adapter.Slot) ?? Vector3.zero;
            return new Float3(p.x, p.y, p.z);
        }

        /// <summary>
        /// Host: the crafter walked away from their hand craft (§5.5.3, 0.2.11b3). Same refund and
        /// material-return path as an explicit cancel — presence loss is not a penalty, it just
        /// stops the work (§5.5.4). Called by MapEntitySync's presence monitor, not by an RPC.
        /// </summary>
        public void CancelHandCraftForPresence()
        {
            if (!IsServer) return;
            CancelActiveHandCraft();
        }

        /// <summary>
        /// Vacates the single action slot when it holds this dreamer's hand craft (0.2.11a3).
        /// Matched on kind rather than blindly clearing index 0: by the time a craft completes the
        /// slot could legitimately hold something else if a future path ever displaces it, and
        /// dropping an unrelated record would strand a world-object contributor.
        /// </summary>
        private static void ClearHandCraftSlot(Game.Simulation.DreamerRecord dreamer)
        {
            if (dreamer?.actionQueue == null) return;
            for (int i = 0; i < dreamer.actionQueue.Count; i++)
                if (dreamer.actionQueue[i]?.kind == ActionSlotKind.HandCraft)
                {
                    dreamer.RemoveAt(i);
                    return;
                }
        }

        // ── Craft material + tool helpers (list-iterated, TDD §5.7.0 / 0.2.10a2) ──

        /// <summary>True if a tool of the given category is carried and usable (durability > 0 if a
        /// durability tool — so a worn/broken tool doesn't satisfy the gate).</summary>
        private bool HasToolCategory(DreamerRecord dreamer, int categoryId)
        {
            foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
            {
                if (container.contents == null) continue;
                foreach (var it in container.contents.items)
                {
                    var so = DefRegistry.Instance?.Get(it.defId);
                    if (so == null || so.toolCategoryId != categoryId) continue;
                    if (so.isDurabilityTool && so.maxDurability > 0f && it.durability <= 0f) continue;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Aggregates the recipe inputs by defId (so a recipe that lists the same material in
        /// two entries totals correctly, not double-counted). Iterated, never numbered fields.</summary>
        private static Dictionary<int, float> AggregateInputs(RecipeInput[] inputs)
        {
            var agg = new Dictionary<int, float>();
            if (inputs == null) return agg;
            foreach (var inp in inputs)
            {
                if (inp == null || inp.amount <= 0f) continue;
                agg[inp.itemDefId] = agg.TryGetValue(inp.itemDefId, out var a) ? a + inp.amount : inp.amount;
            }
            return agg;
        }

        /// <summary>Checks each (aggregated) material is fully available across the dreamer's
        /// containers (whole-unit stacks only; not partials).</summary>
        private bool CanAffordInputs(DreamerRecord dreamer, RecipeInput[] inputs)
        {
            foreach (var kv in AggregateInputs(inputs))
            {
                float need = kv.Value;
                foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
                {
                    if (container.contents == null) continue;
                    foreach (var it in container.contents.items)
                        if (it.defId == kv.Key && !it.remaining.HasValue)
                            need -= it.quantity;
                }
                if (need > 0.001f) return false;
            }
            return true;
        }

        /// <summary>Removes the required amount of each (aggregated) material from the dreamer's
        /// containers. Returns the reserved list for a later refund. Assumes
        /// <see cref="CanAffordInputs"/> passed (caller checks first).</summary>
        private List<CraftReservedInput> ConsumeInputs(DreamerRecord dreamer, RecipeInput[] inputs)
        {
            var reserved = new List<CraftReservedInput>();
            foreach (var kv in AggregateInputs(inputs))
            {
                float need = kv.Value;
                foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
                {
                    if (container.contents == null) continue;
                    for (int i = container.contents.items.Count - 1; i >= 0 && need > 0.001f; i--)
                    {
                        var it = container.contents.items[i];
                        if (it.defId != kv.Key || it.remaining.HasValue) continue;
                        float take = Mathf.Min(need, it.quantity);
                        it.quantity -= take;
                        need        -= take;
                        if (it.quantity <= 0f) container.contents.items.RemoveAt(i);
                    }
                }
                reserved.Add(new CraftReservedInput { itemDefId = kv.Key, amount = kv.Value });
            }
            return reserved;
        }

        /// <summary>Returns the reserved materials to the dreamer's inventory (overflow is dropped
        /// only if inventory is full — kept simple for a; c2 replaces this with an in-progress item).</summary>
        private void RefundInputs(DreamerRecord dreamer, List<CraftReservedInput> reserved, float now)
        {
            if (reserved == null) return;
            foreach (var r in reserved)
                TryAddToEquipment(dreamer, MakeItemInstance(r.itemDefId, r.amount, now), now);
        }

        /// <summary>Builds a fresh item Instance, initialising perishable/durability from its Def.</summary>
        private static Instance MakeItemInstance(int defId, float quantity, float now)
        {
            var inst = new Instance { defId = defId, quantity = quantity };
            var so   = DefRegistry.Instance?.Get(defId);
            if (so != null && so.perishable && so.effectiveLifespan > 0f)
                inst.InitConditionTriple(now, so.effectiveLifespan);
            if (so != null && so.isDurabilityTool && so.maxDurability > 0f)
                inst.durability = so.maxDurability;
            return inst;
        }

        // ── Public craft-material API (called by MapEntitySync for station crafts, 0.2.10c) ──

        /// <summary>Host: does this dreamer carry a usable tool of the given category? (§5.7.1 gate)</summary>
        public bool CrafterHasTool(int categoryId)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter?.Slot ?? -1);
            return dreamer != null && HasToolCategory(dreamer, categoryId);
        }

        /// <summary>Host: can this dreamer afford the recipe's material list? (list-iterated)</summary>
        public bool CrafterCanAfford(RecipeInput[] inputs)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter?.Slot ?? -1);
            return dreamer != null && CanAffordInputs(dreamer, inputs);
        }

        /// <summary>Host: consume the recipe's materials from this dreamer; returns the reserved list.</summary>
        public List<CraftReservedInput> CrafterConsumeInputs(RecipeInput[] inputs)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter?.Slot ?? -1);
            if (dreamer == null) return new List<CraftReservedInput>();
            var reserved = ConsumeInputs(dreamer, inputs);
            ForcePush();
            return reserved;
        }

        /// <summary>Host: return reserved materials to this dreamer (station craft cancel).</summary>
        public void CrafterRefundInputs(List<CraftReservedInput> reserved, float now)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter?.Slot ?? -1);
            if (dreamer == null) return;
            RefundInputs(dreamer, reserved, now);
            ForcePush();
        }

        // ── Public delivery API (called by MapEntitySync for gather yields) ───────

        /// <summary>Host: add an item to this dreamer's equipped containers and force-push.</summary>
        public bool TryDeliverItem(Instance inst, float clockNow)
        {
            if (!IsServer) return false;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(_adapter.Slot);
            if (dreamer == null) return false;
            bool ok = TryAddToEquipment(dreamer, inst, clockNow);
            if (ok) ForcePush();
            return ok;
        }

        // ── Helper: add to first available container ──────────────────────────────

        private bool TryAddToEquipment(DreamerRecord dreamer, Instance inst, float now, EquipSlotType? preferred = null)
        {
            var defs  = DefRegistry.Instance;
            var spoil = GetSpoilageConfig();

            if (preferred.HasValue)
            {
                var c = dreamer.equipSlots.GetSlot(preferred.Value);
                if (c != null && CanAddToContainer(c, inst, defs))
                {
                    ApplyStorageModifier(inst, c, now, defs);
                    c.contents ??= new InstanceContainer();
                    c.contents.TryAdd(inst, defs, now, spoil);
                    return true;
                }
            }

            foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
            {
                if (!CanAddToContainer(container, inst, defs)) continue;
                ApplyStorageModifier(inst, container, now, defs);
                container.contents ??= new InstanceContainer();
                container.contents.TryAdd(inst, defs, now, spoil);
                return true;
            }
            return false;
        }

        private static bool CanAddToContainer(Instance container, Instance item, IItemDefLookup defs)
        {
            if (defs == null || !defs.TryGetDef(container.defId, out var cDef)) return true;
            if (!cDef.isContainer) return false;
            if (!defs.TryGetDef(item.defId, out var iDef)) return true;
            if (cDef.typeFilter != ItemTypeTag.None && (iDef.itemTypeTag & cDef.typeFilter) == 0) return false;
            var contents = container.contents;
            if (cDef.slotCapacity > 0 && (contents?.items.Count ?? 0) >= cDef.slotCapacity) return false;
            if (cDef.weightCapacity > 0f)
            {
                float w = 0f;
                if (contents != null)
                    foreach (var e in contents.items)
                        if (defs.TryGetDef(e.defId, out var ed)) w += ed.weight * e.quantity;
                if (w + iDef.weight * item.quantity > cDef.weightCapacity) return false;
            }
            return true;
        }

        // ── Storage modifier re-stamp helpers (0.2.5d) ───────────────────────────

        private static void ApplyStorageModifier(Instance inst, Instance container, float now, IItemDefLookup defs)
        {
            if (inst.rate <= 0f || defs == null) return;
            if (!defs.TryGetDef(container.defId, out var cDef)) return;
            float mod = cDef.spoilageModifier > 0f ? cDef.spoilageModifier : 1f;
            if (Math.Abs(mod - 1f) < 0.0001f) return;
            if (!defs.TryGetDef(inst.defId, out var iDef) || iDef.effectiveLifespan <= 0f) return;
            inst.ReStamp(now, (100f / iDef.effectiveLifespan) * mod);
        }

        private static void RemoveStorageModifier(Instance inst, Instance container, float now, IItemDefLookup defs)
        {
            if (inst.rate <= 0f || defs == null) return;
            if (!defs.TryGetDef(container.defId, out var cDef)) return;
            float mod = cDef.spoilageModifier > 0f ? cDef.spoilageModifier : 1f;
            if (Math.Abs(mod - 1f) < 0.0001f) return;
            if (!defs.TryGetDef(inst.defId, out var iDef) || iDef.effectiveLifespan <= 0f) return;
            inst.ReStamp(now, 100f / iDef.effectiveLifespan);
        }

        // ── Debuff helpers ────────────────────────────────────────────────────────

        private void ApplySicknessDebuff(DreamerRecord dreamer)
        {
            if (dreamer.buffs == null) return;
            foreach (var b in dreamer.buffs)
                if (b.defId == "sickness") return;
            float dur = GetBuffConfig()?.GetDef("sickness")?.durationMinutes ?? 120f;
            dreamer.buffs.Add(new BuffInstance { defId = "sickness", remainingMinutes = dur });
            GetComponent<DreamerBuffSync>()?.ForcePush();
        }

        private void ApplyExhaustionDebuff(DreamerRecord dreamer)
        {
            if (dreamer.buffs == null) return;
            foreach (var b in dreamer.buffs)
                if (b.defId == ActionRecord.ExhaustionBuffId) return;
            dreamer.buffs.Add(new BuffInstance
            {
                defId            = ActionRecord.ExhaustionBuffId,
                remainingMinutes = GetNeedsConfig().exhaustionDurationMinutes,
            });
        }

        private NeedsConfig GetNeedsConfig()
        {
            if (_needsConfig == null)
                _needsConfig = FindFirstObjectByType<WorldClockDriver>()?.NeedsConfig;
            return _needsConfig ?? new NeedsConfig();
        }

        private BuffConfig GetBuffConfig()
        {
            if (_buffConfig == null)
                _buffConfig = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig;
            return _buffConfig ?? new BuffConfig();
        }

        private SpoilageConfig GetSpoilageConfig()
        {
            if (_spoilageConfig == null)
                _spoilageConfig = FindFirstObjectByType<WorldClockDriver>()?.SpoilageConfig;
            return _spoilageConfig ?? new SpoilageConfig();
        }

        private float GetCurrentClockTime() =>
            RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;

        // ── HUD — owning client only ─────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;

            float x = 10f, y = 10f;

            var aq = _actionQueue.Value;
            var cs = _craft.Value;
            var wt = _worldTasks.Value;
            GUI.Label(new Rect(x, y, 240, 20), "── Actions ──");
            y += 22f;

            // Commitment bar (§5.5.3, 0.2.11c8): committed / elapsed / remaining, computed on the
            // client from the synced record plus the clock — no extrapolation, so it is exact on
            // both peers and after a revert. Drawn above the action rows because "how long am I
            // locked in for" is the question a committed player is actually asking.
            if (aq.CommittedMinutes > 0f)
            {
                float nowM      = GetCurrentClockTime();
                float elapsed   = Mathf.Max(0f, nowM - aq.CommitStart);
                float remaining = Mathf.Max(0f, aq.CommittedMinutes - elapsed);
                float pct       = Mathf.Clamp01(elapsed / aq.CommittedMinutes) * 100f;

                var savedC = GUI.color;
                GUI.color  = new Color(0.75f, 0.9f, 1f);
                GUI.Label(new Rect(x, y, 280, 18),
                    $"  ⏱ committed {aq.CommittedMinutes:F0}m — {elapsed:F0}m in, {remaining:F0}m left ({pct:F0}%)");
                GUI.color = savedC;
                y += 20f;
            }

            // Wait-or-skip (c5) — offered for anything with time still owed, committed labor and
            // needs tasks alike. Skipping an eight-hour sleep is the commonest case of all, so
            // gating this on a commitment window would miss the point.
            if (aq.SlotRemaining > 0f)
            {
                var skipMgr = FindFirstObjectByType<SkipManager>();
                if (skipMgr != null && !skipMgr.IsSkipping && !skipMgr.HasPendingSkipRequest)
                    if (GUI.Button(new Rect(x + 4f, y, 200f, 20f), $"Skip the remaining {aq.SlotRemaining:F0}m"))
                        skipMgr.RequestCommitmentSkipServerRpc();
                y += 24f;
            }

            // Idle only when nothing is running in ANY channel (eating queue, hand craft, world tasks).
            if (aq.QueueCount == 0 && cs.Active == 0 && wt.Count == 0)
            {
                GUI.Label(new Rect(x, y, 200, 18), "  (idle)");
                y += 20f;
            }

            if (aq.QueueCount > 0)
            {
                var activeSo = aq.ActiveDefId != 0 ? DefRegistry.Instance?.Get(aq.ActiveDefId) : null;
                string activeName = activeSo != null ? activeSo.displayName : $"def#{aq.ActiveDefId}";
                GUI.Label(new Rect(x, y, 160, 18), $"  {activeName}");
                if (GUI.Button(new Rect(x + 163f, y, 18f, 18f), "✕"))
                    CancelActionServerRpc(aq.ActiveQueueIndex);
                y += 20f;
                if (aq.QueueCount > 1) { GUI.Label(new Rect(x, y, 200, 18), $"  +{aq.QueueCount - 1} queued"); y += 20f; }
            }

            // Active hand craft (0.2.10a) — progress is client-computed from the synced record + clock.
            if (cs.Active == 1)
            {
                var    recipe = RecipeRegistry.Instance?.Get(cs.RecipeId);
                string cName  = recipe != null ? recipe.displayName : $"recipe#{cs.RecipeId}";
                float  now    = GetCurrentClockTime();
                float  pct    = cs.Duration > 0f ? Mathf.Clamp01((now - cs.StartTime) / cs.Duration) * 100f : 100f;
                GUI.Label(new Rect(x, y, 160, 18), $"  ⚒ {cName} {pct:F0}%");
                if (GUI.Button(new Rect(x + 163f, y, 18f, 18f), "✕"))
                    CancelCraftServerRpc();
                y += 20f;
            }

            // Active object-bound world tasks (gathering + station crafting, 0.2.10) — so the player
            // sees what they triggered in the world alongside eating + hand crafts. Progress is read
            // live from the synced accrual labor; ✕ leaves the task (partial persists on the object).
            for (int i = 0; i < wt.Count; i++)
            {
                int   inst      = wt.InstanceIds[i];
                int   actIdx    = wt.ActionIndices[i];
                int   recipeId  = wt.RecipeIds[i];
                float threshold = wt.Thresholds[i];
                var   wDef      = DefRegistry.Instance?.Get(wt.DefIds[i]);
                float labor     = 0f;
                MapEntitySync.Instance?.TryGetProcessableActionState(inst, actIdx, out labor, out _);
                float wpct      = threshold > 0f ? Mathf.Clamp01(labor / threshold) * 100f : 0f;
                string objName  = wDef != null ? wDef.displayName : $"#{wt.DefIds[i]}";

                string wlabel;
                if (recipeId != 0)
                {
                    var recipe = RecipeRegistry.Instance?.Get(recipeId);
                    wlabel = $"  ⚒ {(recipe != null ? recipe.displayName : $"recipe#{recipeId}")} @ {objName} {wpct:F0}%";
                }
                else
                {
                    string verb = wDef?.actions != null && actIdx >= 0 && actIdx < wDef.actions.Length
                        ? wDef.actions[actIdx].DisplayLabel : "work";
                    wlabel = $"  ⛏ {verb} {objName} {wpct:F0}%";
                }
                GUI.Label(new Rect(x, y, 160, 18), wlabel);
                if (GUI.Button(new Rect(x + 163f, y, 18f, 18f), "✕"))
                {
                    var ts = GetComponent<DreamerTaskSync>();
                    if (recipeId != 0) ts?.StopStationCraftServerRpc(inst);
                    else               ts?.StopWorldActionServerRpc(inst, actIdx);
                }
                y += 20f;
            }

            y += 4f;
            if (GUI.Button(new Rect(x, y, 170, 18), _inventoryOpen ? "[I] Close Inventory" : "[I] Open Inventory"))
                _inventoryOpen = !_inventoryOpen;
            y += 24f;

            if (_inventoryOpen)
            {
                var payload = _inventory.Value;
                GUI.Label(new Rect(x, y, 300, 20), $"── Inventory ── Carried: {payload.CarriedWeight:F1} kg");
                y += 22f;

                if (payload.OccupiedCount == 0)
                {
                    GUI.Label(new Rect(x, y, 240, 18), "  (no containers equipped)");
                    y += 20f;
                }
                else
                {
                    int itemBase = 0;
                    for (int s = 0; s < payload.OccupiedCount; s++)
                    {
                        byte   slotByte  = payload.SlotTypes[s];
                        int    cDefId    = payload.ContainerDefIds[s];
                        byte   cCond     = payload.ContainerConds[s];
                        int    itemCount = payload.SlotItemCounts[s];
                        var    slotEnum  = (EquipSlotType)slotByte;
                        var    cSo       = DefRegistry.Instance?.Get(cDefId);
                        string cName     = cSo != null ? cSo.displayName : $"def#{cDefId}";
                        string cCondLbl  = cCond == InventoryPayload.NonPerishable ? "" : $" [{GetSpoilageConfig().GetBucket(cCond)} {cCond}%]";
                        GUI.Label(new Rect(x, y, 210, 18), $"[{slotEnum}] {cName}{cCondLbl}");
                        if (GUI.Button(new Rect(x + 214f, y, 52f, 18f), "Unequip"))
                            UnequipContainerServerRpc(slotByte);
                        y += 20f;

                        if (itemCount == 0)
                        {
                            GUI.Label(new Rect(x + 10f, y, 200, 18), "  (empty)");
                            y += 20f;
                        }

                        for (int i = 0; i < itemCount; i++)
                        {
                            int    flatIdx  = itemBase + i;
                            int    defId    = payload.ItemDefIds[flatIdx];
                            float  qty      = payload.ItemQtys[flatIdx];
                            byte   cond     = payload.ItemConds[flatIdx];
                            var    so       = DefRegistry.Instance?.Get(defId);
                            string name     = so != null ? so.displayName : $"def#{defId}";
                            int    qtyInt   = Mathf.FloorToInt(qty);
                            string freshLbl = cond == InventoryPayload.NonPerishable ? "" : $" [{GetSpoilageConfig().GetBucket(cond)} {cond}%]";

                            int nestedGroupIdx = -1;
                            for (int ng = 0; ng < payload.NestedGroupCount; ng++)
                                if (payload.NestedParentSlot[ng] == slotByte && payload.NestedParentItem[ng] == i)
                                { nestedGroupIdx = ng; break; }

                            string nestSuffix = nestedGroupIdx >= 0 ? " [bag]" : "";
                            bool isTool = so != null && so.isDurabilityTool && so.maxDurability > 0f;
                            // 0.2.10d3: a live tool never lingers at 0 durability — it is destroyed and
                            // replaced by its broken Def (a separate, non-Tool item) in place. So the old
                            // [BROKEN] state for a live tool is retired; tools just show durability.
                            string durLbl = isTool ? $" [Dur {cond}%]" : freshLbl;
                            GUI.Label(new Rect(x + 10f, y, 200, 18), $"  {name} x{qtyInt}{durLbl}{nestSuffix}");

                            bool canUse = so != null && nestedGroupIdx < 0 &&
                                (isTool ? cond > 0 : so.consumeEffect != ConsumeEffect.None);
                            GUI.enabled = canUse;
                            // The verb names the action (§5.5.1): eating and drinking are their own
                            // Trivial actions, not a generic "use". A single Use button for food,
                            // water and axes alike hides the one distinction that matters here —
                            // that two of those are instant and the third is not.
                            string verb = isTool ? "Use" : so?.consumeEffect switch
                            {
                                ConsumeEffect.Food  => "Eat",
                                ConsumeEffect.Drink => "Drink",
                                _                   => "Use",
                            };
                            if (GUI.Button(new Rect(x + 153f, y, 40f, 18f), verb))
                            {
                                if (isTool) DevUseToolServerRpc(slotByte, i);
                                else        ConsumeItemServerRpc(slotByte, i);
                            }
                            GUI.enabled = qtyInt >= 2 && nestedGroupIdx < 0;
                            if (GUI.Button(new Rect(x + 196f, y, 38f, 18f), "Split"))
                                SplitItemServerRpc(slotByte, i);
                            GUI.enabled = true;
                            if (GUI.Button(new Rect(x + 237f, y, 38f, 18f), "Drop"))
                                DropItemServerRpc(slotByte, i);
                            y += 20f;

                            if (nestedGroupIdx >= 0)
                            {
                                int nestedOff = 0;
                                for (int k = 0; k < nestedGroupIdx; k++) nestedOff += payload.NestedItemCounts[k];
                                int nCount = payload.NestedItemCounts[nestedGroupIdx];
                                for (int ni = 0; ni < nCount; ni++)
                                {
                                    int    nDef   = payload.NestedDefIds[nestedOff + ni];
                                    float  nQty   = payload.NestedQtys[nestedOff + ni];
                                    byte   nCond  = payload.NestedConds[nestedOff + ni];
                                    var    nSo    = DefRegistry.Instance?.Get(nDef);
                                    string nName  = nSo != null ? nSo.displayName : $"def#{nDef}";
                                    string nFresh = nCond == InventoryPayload.NonPerishable ? "" : $" [{GetSpoilageConfig().GetBucket(nCond)} {nCond}%]";
                                    GUI.Label(new Rect(x + 22f, y, 200, 18), $"    {nName} x{Mathf.FloorToInt(nQty)}{nFresh}");
                                    y += 18f;
                                }
                            }
                        }
                        itemBase += itemCount;
                    }
                }

                y += 4f;
                GUI.Label(new Rect(x, y, 240, 18), "── Dev ──");
                y += 20f;

                GUI.Label(new Rect(x, y, 94, 18), "Container defId:");
                _devContainerDefId = GUI.TextField(new Rect(x + 96f, y, 38f, 18f), _devContainerDefId);
                if (int.TryParse(_devContainerDefId, out int cDevId))
                {
                    if (GUI.Button(new Rect(x + 138f, y, 20f, 18f), "B"))  DevEquipContainerServerRpc(cDevId, (byte)EquipSlotType.Backpack);
                    if (GUI.Button(new Rect(x + 160f, y, 22f, 18f), "P0")) DevEquipContainerServerRpc(cDevId, (byte)EquipSlotType.Pouch0);
                    if (GUI.Button(new Rect(x + 184f, y, 22f, 18f), "P1")) DevEquipContainerServerRpc(cDevId, (byte)EquipSlotType.Pouch1);
                    if (GUI.Button(new Rect(x + 208f, y, 20f, 18f), "Q"))  DevEquipContainerServerRpc(cDevId, (byte)EquipSlotType.Quiver);
                }
                y += 22f;

                GUI.Label(new Rect(x, y, 58, 18), "Item defId:");
                _devAddDefId = GUI.TextField(new Rect(x + 60f, y, 38f, 18f), _devAddDefId);
                GUI.Label(new Rect(x + 102f, y, 26, 18), "qty:");
                _devAddQty = GUI.TextField(new Rect(x + 130f, y, 34f, 18f), _devAddQty);
                if (int.TryParse(_devAddDefId, out int devDefId) && float.TryParse(_devAddQty, out float devQty))
                    if (GUI.Button(new Rect(x + 168f, y, 40f, 18f), "Add"))
                        DevAddItemServerRpc(devDefId, devQty);
                y += 22f;
            }

            // The former top-right "Ground (nearby)" pickup panel is retired: pickup is unified for
            // every item through the Interactable HUD (InteractableDetector → the shared `pickup`
            // action). One pickup path, no proximity-panel fork (TDD §1.12).
        }
    }
}
