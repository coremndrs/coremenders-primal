using System;
using System.Collections.Generic;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    // ── Unified world-object payload ──────────────────────────────────────────────

    /// <summary>
    /// Variable-length full-state payload for a map's unified world-object collection
    /// (TDD §1.11 / §1.12, 0.2.9e1 — the runtime sync profile).
    ///
    /// Subsumes the former MapEntityPayload (ground items) and ProcessablePayload
    /// (processables): every InWorld/CarriedBy Instance rides one payload. Plain ground
    /// items carry ActionCount 0; objects with timed actions flatten their accrual state
    /// (labor + complete) into the shared Labors/Completes arrays.
    /// </summary>
    internal struct WorldObjectPayload : INetworkSerializable
    {
        public int[]   Ids;
        public int[]   DefIds;
        public float[] Quantities;
        public float[] Xs, Ys, Zs;
        public int[]   ActionCounts;
        public byte[]  Flags;     // per-instance: bit0 = authored (0.2.9f), bit1 = depleted

        public float[] Labors;    // flattened, length = sum(ActionCounts)
        public byte[]  Completes; // flattened, length = sum(ActionCounts)

        public const byte FlagAuthored = 1 << 0;
        public const byte FlagDepleted = 1 << 1;

        public int Count => Ids?.Length ?? 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count        = Ids?.Length ?? 0;
            int totalActions = 0;
            if (serializer.IsWriter && ActionCounts != null)
                foreach (var c in ActionCounts) totalActions += c;

            serializer.SerializeValue(ref count);
            serializer.SerializeValue(ref totalActions);

            if (serializer.IsReader)
            {
                Ids          = new int[count];
                DefIds       = new int[count];
                Quantities   = new float[count];
                Xs           = new float[count];
                Ys           = new float[count];
                Zs           = new float[count];
                ActionCounts = new int[count];
                Flags        = new byte[count];
                Labors       = new float[totalActions];
                Completes    = new byte[totalActions];
            }

            for (int i = 0; i < count; i++)
            {
                serializer.SerializeValue(ref Ids[i]);
                serializer.SerializeValue(ref DefIds[i]);
                serializer.SerializeValue(ref Quantities[i]);
                serializer.SerializeValue(ref Xs[i]);
                serializer.SerializeValue(ref Ys[i]);
                serializer.SerializeValue(ref Zs[i]);
                serializer.SerializeValue(ref ActionCounts[i]);
                serializer.SerializeValue(ref Flags[i]);
            }

            for (int i = 0; i < totalActions; i++)
            {
                serializer.SerializeValue(ref Labors[i]);
                serializer.SerializeValue(ref Completes[i]);
            }
        }
    }

    // ── MapEntitySync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton NetworkBehaviour that replicates the current map's unified world-object
    /// collection to all clients (TDD §1.11 / §1.12, 0.2.7a → collapsed at 0.2.9e1).
    ///
    /// Storage and sync are now one model: <see cref="MapEntityLayer.worldObjects"/> holds
    /// every InWorld/CarriedBy Instance, replicated through a single full-state payload
    /// (the runtime profile; the authored delta-only profile is 0.2.9f). A world Instance's
    /// presentation is read from its Def, not a stored type:
    ///   • Def has no world actions  → ground-item visual + ground-HUD pickup (legacy 0.2.1 path)
    ///   • Def has world actions      → Interactable visual driven by InteractableDetector
    /// This is what keeps a carried-then-dropped LargeTreeLog interactable (0.2.9e3): dropping
    /// only flips its location; the Def — and therefore its world actions — never change.
    ///
    /// Place as an in-scene NetworkObject in the Action scene.
    /// </summary>
    public class MapEntitySync : NetworkBehaviour
    {
        public static MapEntitySync Instance { get; private set; }

        // Authored in-scene nodes (0.2.9f), keyed by baked instanceId. Populated by Interactable
        // self-registration on scene load (host AND client). Host uses it to materialise a node's
        // delta on first interaction (reading Def + local position); clients use it to bind the
        // scene GameObject (the authored visual) and hide it when depleted. Not persisted — it is
        // derived from the scene, identical on all peers.
        private static readonly Dictionary<int, Interactable> _authoredById = new Dictionary<int, Interactable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            _authoredById.Clear();
        }

        /// <summary>An authored Interactable registers itself on enable (0.2.9f). Ignores runtime
        /// (non-authored) Interactables and unbaked ids.</summary>
        public static void RegisterAuthored(Interactable it)
        {
            if (it == null || !it.authored || it.instanceId <= 0) return;
            if (_authoredById.TryGetValue(it.instanceId, out var existing) && existing != null && existing != it)
                Debug.LogWarning($"[MapEntitySync] Authored id {it.instanceId} registered by two objects ('{it.name}' and '{existing.name}') — re-bake ids.");
            _authoredById[it.instanceId] = it;
        }

        public static void UnregisterAuthored(Interactable it)
        {
            if (it == null) return;
            if (_authoredById.TryGetValue(it.instanceId, out var e) && e == it)
                _authoredById.Remove(it.instanceId);
        }

        public const float PickupRange = 3f;

        /// <summary>Verb id of the station-craft action on a station Def (TDD §5.7.1, 0.2.10c).
        /// A station craft is recipe-driven, so it is committed only via <see cref="StartStationCraft"/>
        /// (which knows the recipe) — never through the generic gather/StartContributor path, whose
        /// completion would read the def action's fixed labor/yields instead of the recipe's.</summary>
        public const string CraftActionId = "craft";

        [SerializeField] private float      _syncInterval    = 1f;
        [Tooltip("Fallback visual for plain ground items whose Def has no prefab assigned.")]
        [SerializeField] private GameObject _groundItemPrefab;
        [Tooltip("Fallback visual for world objects (Def has world actions) with no prefab assigned.")]
        [SerializeField] private GameObject _processablePrefab;

        [Header("Ground snap (host-side placement)")]
        [Tooltip("Resolve every world placement onto the ground. Without this, a yield spawned from " +
                 "an object whose base is sunk into the terrain (a tree planted slightly low) appears " +
                 "underground, and a drop on a slope hangs in the air.")]
        [SerializeField] private bool _snapToGround = true;

        [Tooltip("Layers treated as ground. Dreamers are skipped regardless.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("How far above the placement to start the downward probe — also the depth from " +
                 "which a placement buried inside the terrain can be lifted back out.")]
        [SerializeField] private float _probeAbove = 5f;

        [Tooltip("How far below the placement to look for ground before giving up.")]
        [SerializeField] private float _probeBelow = 100f;

        [Tooltip("Gap left between the object's base and the surface.")]
        [SerializeField] private float _groundClearance = 0.02f;

        [Tooltip("Rest the object's visual base on the surface (measured from its Def prefab's " +
                 "renderer bounds) rather than its pivot. Turn off if prefabs are authored pivot-at-base.")]
        [SerializeField] private bool _alignToPrefabBase = true;

        // ── Network Variable (single runtime profile) ────────────────────────────

        private readonly NetworkVariable<WorldObjectPayload> _worldObjects = new NetworkVariable<WorldObjectPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Server-side state ────────────────────────────────────────────────────

        private WorldObjectPayload _lastSynced;
        private float              _syncTimer;

        // ── Client-side registries ───────────────────────────────────────────────

        private readonly Dictionary<int, GameObject> _worldVisuals = new Dictionary<int, GameObject>();

        // Objects with timed actions (accrual) — progress display for InteractableDetector.
        private readonly List<(int id, int defId, float x, float y, float z, int actionCount, float[] labors, bool[] completes)>
            _currentProcessables = new List<(int, int, float, float, float, int, float[], bool[])>();

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MapEntitySync] Duplicate instance — ignoring.");
                return;
            }
            Instance = this;

            if (IsServer)
            {
                PushFromRdm();
            }
            else
            {
                _worldObjects.OnValueChanged -= OnPayloadChanged;
                _worldObjects.OnValueChanged += OnPayloadChanged;
                ApplyPayload(_worldObjects.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            StopAllCoroutines();
            _worldObjects.OnValueChanged -= OnPayloadChanged;

            DestroyAllVisuals();
            _currentProcessables.Clear();
            _authoredHidden.Clear();
            _proportionalDelivered.Clear();

            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        // ── Server convergence tick ──────────────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;

            // Presence runs EVERY frame, not on the sync interval: a dreamer who walks out of range
            // must stop accruing at the instant they leave, not up to a second later. It is two
            // distance checks, so the cost is irrelevant next to the correctness.
            EnforcePresence();

            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncInterval) return;
            _syncTimer = 0f;

            CheckActionCompletions();
            PushFromRdm();
        }

        // ── Presence binding (§5.5.3, 0.2.11b) ───────────────────────────────────
        //
        // The rule this build exists for: committed active labor is presence-bound. The dreamer
        // stands at the work, and the work stops when they leave. Before 0.2.11 a commitment was
        // fire-and-forget — commit four hours to a tree, walk away, collect the log — which is
        // architecturally clean and experientially dead.
        //
        // Two shapes of presence, because there are two shapes of work:
        //   • Object-bound (gather / process / station craft): presence = within presenceRange of
        //     the Instance. Leaving fires the contributor-change event, which closes the accrual
        //     segment at that timestamp. Progress is retained; walking back does NOT auto-resume,
        //     it requires a fresh commit (b1).
        //   • Self-contained (hand craft / rest / sleep): there is no object to measure against, so
        //     the record carries an anchor stamped where the dreamer committed. Drifting further
        //     than presenceMoveTolerance from it cancels the action under the §5.5.4 refund rules (b3).
        //
        // Commitment expiry (c3) is resolved here too, because it is the same question asked of the
        // clock instead of the map: the committed window has run out, so contribution ends with the
        // remainder left on the object.

        private void EnforcePresence()
        {
            var world = RuntimeDataManager.Instance?.WorldState;
            if (world?.dreamers == null || world.clock == null) return;

            var   cfg = GetNeedsConfig();
            float now = world.clock.totalInGameMinutes;

            foreach (var dreamer in world.dreamers)
            {
                var active = dreamer?.ActiveAction();
                if (active == null || !active.started) continue;

                // Commitment expiry first — an expired commitment ends even if the dreamer never
                // moved, and settling it here means the presence checks below see a free slot.
                if (active.IsCommitmentExpiredAt(now))
                {
                    Debug.Log($"[MapEntitySync] Slot {dreamer.slot} commitment expired " +
                              $"({active.committedMinutes:F0} min served) — contribution ends, remainder stays.");
                    EndPresence(dreamer, active, now);
                    continue;
                }

                var pos = RuntimeDataManager.Instance?.GetDreamerPosition(dreamer.slot);
                if (!pos.HasValue) continue;

                if (active.presenceAnchored)
                {
                    // Self-contained: measure against where they committed (b3).
                    var anchor = new Vector3(active.presenceAnchor.x, active.presenceAnchor.y, active.presenceAnchor.z);
                    if (HorizontalDistance(pos.Value, anchor) <= cfg.presenceMoveTolerance) continue;

                    Debug.Log($"[MapEntitySync] Slot {dreamer.slot} moved away from {active.taskType} — cancelled (§5.5.4).");
                    EndPresence(dreamer, active, now);
                    continue;
                }

                if (active.instanceId < 0) continue; // nothing to measure against

                var obj = ResolveWorldObject(active.instanceId);
                if (obj == null)
                {
                    // The object is gone (depleted by the other dreamer, or removed). Nothing left
                    // to be present at — release the slot rather than stranding it.
                    EndPresence(dreamer, active, now);
                    continue;
                }

                var objPos = ResolvePosition(obj);
                if (HorizontalDistance(pos.Value, objPos) <= cfg.presenceRange) continue;

                Debug.Log($"[MapEntitySync] Slot {dreamer.slot} left the work at instance {active.instanceId} " +
                          $"— contribution ends, progress retained (b1).");
                EndPresence(dreamer, active, now);
            }
        }

        /// <summary>
        /// Horizontal separation only. A dreamer standing on a log or on a slope above a node is
        /// still present at it; billing vertical offset as distance would drop them for jumping.
        /// </summary>
        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Ends one dreamer's presence on their active action, routing to the stop path that owns
        /// the refund for that kind. Every path retains progress — presence loss is never a penalty,
        /// it just stops the clock on the contribution (§5.5.4).
        /// </summary>
        private void EndPresence(Game.Simulation.DreamerRecord dreamer, ActionRecord active, float now)
        {
            switch (active.kind)
            {
                case ActionSlotKind.WorldAction:
                    StopContributor(active.instanceId, active.actionIndex, dreamer.slot);
                    break;
                case ActionSlotKind.StationCraft:
                    StopStationCraft(active.instanceId, dreamer.slot);
                    break;
                case ActionSlotKind.HandCraft:
                    DreamerInventorySync.GetForSlot(dreamer.slot)?.CancelHandCraftForPresence();
                    break;
                case ActionSlotKind.Task:
                    EndNeedsTaskForPresence(dreamer, active, now);
                    break;
                default:
                    dreamer.RemoveAt(0);
                    break;
            }

            // Belt and braces: if a stop path declined to act (a mismatched instance, a craft already
            // gone) the slot would stay occupied and this would fire again every frame, spamming the
            // log. Clear it directly in that case.
            var still = dreamer.ActiveAction();
            if (still == active) dreamer.RemoveAt(0);
        }

        /// <summary>
        /// Ends a movement-cancelled sleep or rest. A cut-short sleep goes through the same wake
        /// resolver as a skip-interrupted one (0.1.6), so its partial protection buff and checkpoint
        /// are banked identically — otherwise walking out of bed would silently void the sleep.
        /// </summary>
        private static void EndNeedsTaskForPresence(Game.Simulation.DreamerRecord dreamer, ActionRecord active, float now)
        {
            float slept = active.ElapsedAt(now);
            dreamer.RemoveAt(0);

            if (active.taskType != TaskType.Sleeping) return;
            GameFlowManager.Instance?.HandleSleepWake(new List<SleepEndedResult>
            {
                new SleepEndedResult
                {
                    DreamerSlot  = dreamer.slot,
                    SleptMinutes = slept,
                    WasCutShort  = true,
                },
            });
        }

        /// <summary>
        /// Host: release every dreamer's presence on object-bound work (§5.5.3, 0.2.11b4). Called
        /// when a skip stops on an INTERRUPT — combat, damage, a predator — where the fiction is
        /// that the dreamer's attention broke. Progress is retained on the object at the interrupt's
        /// timestamp; resuming needs a fresh commit.
        ///
        /// Deliberately NOT called for a guard-capped skip: c7 says a guard stop leaves the
        /// commitment mid-progress to continue at 1×, and dropping it would contradict that.
        /// </summary>
        public void ReleaseAllPresenceOnInterrupt()
        {
            if (!IsServer) return;
            var world = RuntimeDataManager.Instance?.WorldState;
            if (world?.dreamers == null || world.clock == null) return;

            float now = world.clock.totalInGameMinutes;
            foreach (var dreamer in world.dreamers)
            {
                var active = dreamer?.ActiveAction();
                if (active == null || !active.started) continue;
                // Sleep is force-woken by SkipManager's own path, which reports it as cut short.
                if (active.kind == ActionSlotKind.Task && active.taskType == TaskType.Sleeping) continue;

                Debug.Log($"[MapEntitySync] Interrupt released slot {dreamer.slot} from {active.taskType} (b4).");
                EndPresence(dreamer, active, now);
            }
        }

        // ── Server: build + commit payload ───────────────────────────────────────

        private void PushFromRdm()
        {
            var payload = BuildPayload();
            if (!PayloadEquals(payload, _lastSynced))
                CommitPayload(payload);
        }

        public void ForcePush()
        {
            if (!IsServer) return;
            CommitPayload(BuildPayload());
        }

        private WorldObjectPayload BuildPayload()
        {
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var items = layer?.worldObjects?.items;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            int count = items?.Count ?? 0;

            var payload = new WorldObjectPayload();
            if (count == 0) return payload;

            float now = clock?.totalInGameMinutes ?? 0f;

            payload.Ids          = new int[count];
            payload.DefIds       = new int[count];
            payload.Quantities   = new float[count];
            payload.Xs           = new float[count];
            payload.Ys           = new float[count];
            payload.Zs           = new float[count];
            payload.ActionCounts = new int[count];
            payload.Flags        = new byte[count];

            int totalActions = 0;
            for (int i = 0; i < count; i++)
            {
                var ac = items[i].location.accrual?.Length ?? 0;
                payload.ActionCounts[i] = ac;
                totalActions += ac;
            }

            payload.Labors    = new float[totalActions];
            payload.Completes = new byte[totalActions];

            int actionOffset = 0;
            for (int i = 0; i < count; i++)
            {
                var inst = items[i];
                var pos  = ResolvePosition(inst);

                payload.Ids[i]        = inst.id;
                payload.DefIds[i]     = inst.defId;
                payload.Quantities[i] = inst.quantity;
                payload.Xs[i]         = pos.x;
                payload.Ys[i]         = pos.y;
                payload.Zs[i]         = pos.z;
                payload.Flags[i]      = (byte)((inst.authored ? WorldObjectPayload.FlagAuthored : 0)
                                             | (inst.depleted ? WorldObjectPayload.FlagDepleted : 0));

                int ac = payload.ActionCounts[i];
                for (int a = 0; a < ac; a++)
                {
                    var state = inst.location.accrual[a];
                    payload.Labors[actionOffset + a]    = state.LaborAt(now);
                    payload.Completes[actionOffset + a] = state.isComplete ? (byte)1 : (byte)0;
                }
                actionOffset += ac;
            }
            return payload;
        }

        /// <summary>World position of an instance — CarriedBy follows the carrying dreamer.</summary>
        private static Vector3 ResolvePosition(Instance inst)
        {
            if (inst.location.kind == LocationKind.CarriedBy)
            {
                var p = RuntimeDataManager.Instance?.GetDreamerPosition(inst.location.dreamerSlot);
                if (p.HasValue) return p.Value;
            }
            return inst.location.position.ToVector3();
        }

        private void CommitPayload(WorldObjectPayload payload)
        {
            _lastSynced         = payload;
            _worldObjects.Value = payload;
            ApplyPayload(payload);
        }

        // ── Client: react + reconcile ────────────────────────────────────────────

        private void OnPayloadChanged(WorldObjectPayload prev, WorldObjectPayload next) => ApplyPayload(next);

        private void ApplyPayload(WorldObjectPayload payload)
        {
            _currentProcessables.Clear();

            var newIds       = new HashSet<int>();
            var authoredSeen = new HashSet<int>();
            int count        = payload.Count;
            int actionOffset = 0;

            for (int i = 0; i < count; i++)
            {
                int  id         = payload.Ids[i];
                int  defId      = payload.DefIds[i];
                int  ac         = payload.ActionCounts[i];
                byte flags      = payload.Flags != null ? payload.Flags[i] : (byte)0;
                bool isAuthored = (flags & WorldObjectPayload.FlagAuthored) != 0;
                bool isDepleted = (flags & WorldObjectPayload.FlagDepleted) != 0;

                var  def            = DefRegistry.Instance?.Get(defId);
                bool hasWorldAction = def != null && def.HasWorldActions();

                newIds.Add(id);

                // Authored nodes use their in-scene GameObject as the visual (its local position);
                // runtime objects use the synced position for their spawned visual.
                Interactable authoredGo = null;
                Vector3 pos;
                if (isAuthored && _authoredById.TryGetValue(id, out authoredGo) && authoredGo != null)
                    pos = authoredGo.transform.position;
                else
                    pos = new Vector3(payload.Xs[i], payload.Ys[i], payload.Zs[i]);

                if (ac > 0)
                {
                    var labors    = new float[ac];
                    var completes = new bool[ac];
                    for (int a = 0; a < ac; a++)
                    {
                        labors[a]    = payload.Labors[actionOffset + a];
                        completes[a] = payload.Completes[actionOffset + a] != 0;
                    }
                    _currentProcessables.Add((id, defId, pos.x, pos.y, pos.z, ac, labors, completes));
                }
                actionOffset += ac;

                if (isAuthored)
                {
                    // The scene GameObject is the visual — never spawn one. Reflect depleted by hiding
                    // renderers + colliders (NOT SetActive(false), which would fire OnDisable and
                    // unregister the node, so it could never be restored on revert).
                    authoredSeen.Add(id);
                    if (authoredGo != null) SetAuthoredHidden(authoredGo, id, isDepleted);
                    continue;
                }

                // Runtime visual: spawn once, then keep its position current (carried objects move).
                if (_worldVisuals.TryGetValue(id, out var existing))
                {
                    if (existing != null) existing.transform.position = pos;
                }
                else
                {
                    GameObject prefab = def != null && def.prefab != null
                        ? def.prefab
                        : (hasWorldAction ? _processablePrefab : _groundItemPrefab);
                    if (prefab != null)
                    {
                        var go = Instantiate(prefab, pos, Quaternion.identity);
                        _worldVisuals[id] = go;
                        EnsureInteractable(go, id, def);
                        Debug.Log($"[MapEntitySync] Spawned world visual {id} (def={defId}) at {pos}.");
                    }
                }
            }

            var toRemove = new List<int>();
            foreach (var kv in _worldVisuals)
                if (!newIds.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value);
                    toRemove.Add(kv.Key);
                    Debug.Log($"[MapEntitySync] Despawned world visual {kv.Key}.");
                }
            foreach (var id in toRemove) _worldVisuals.Remove(id);

            // Authored nodes NOT in the payload are pristine (or reverted before materialisation) →
            // ensure their scene visual is shown.
            foreach (var kv in _authoredById)
                if (!authoredSeen.Contains(kv.Key) && kv.Value != null)
                    SetAuthoredHidden(kv.Value, kv.Key, false);
        }

        // Client: current hidden state of each authored scene visual — avoids re-toggling components
        // every payload. Hiding disables renderers + colliders (invisible + non-interactable) while
        // leaving the GameObject active so the Interactable stays registered (revert can restore it).
        private readonly Dictionary<int, bool> _authoredHidden = new Dictionary<int, bool>();

        private void SetAuthoredHidden(Interactable it, int id, bool hidden)
        {
            if (_authoredHidden.TryGetValue(id, out var cur) && cur == hidden) return;
            _authoredHidden[id] = hidden;
            foreach (var r in it.GetComponentsInChildren<Renderer>(true)) r.enabled = !hidden;
            foreach (var c in it.GetComponentsInChildren<Collider>(true)) c.enabled = !hidden;
        }

        private void DestroyAllVisuals()
        {
            foreach (var kv in _worldVisuals)
                if (kv.Value != null) Destroy(kv.Value);
            _worldVisuals.Clear();
        }

        /// <summary>
        /// Guarantees a spawned world visual is a detectable Interactable: sets its instanceId + Def,
        /// adding the Interactable component and a fallback trigger collider if the prefab lacks them.
        /// Since the unified pickup model (every inventory Def carries the shared `pickup` action) makes
        /// ALL world objects interactable, this removes the need to hand-wire an Interactable + collider
        /// onto every item prefab — the fallback visuals (GroundItemVisual / ProcesablePrefabDefault)
        /// and any bespoke prefab both work. The collider is a trigger so it never pushes the dreamer;
        /// InteractableDetector probes with QueryTriggerInteraction.Collide so triggers still register.
        /// </summary>
        private static void EnsureInteractable(GameObject go, int id, Def def)
        {
            var interactable = go.GetComponent<Interactable>();
            if (interactable == null) interactable = go.AddComponent<Interactable>();
            interactable.instanceId = id;
            interactable.def        = def;

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var col = go.AddComponent<SphereCollider>();
                col.radius    = 0.5f;
                col.isTrigger = true;
            }
        }

        // ── Action economy: up-front debit + pro-rata refund ─────────────────────
        //
        // A timed action's whole cost is committed when a worker starts it and refunded for the share
        // they did not do (early stop, or the job finishing early — e.g. via co-op). timePool cost is
        // ItemAction.timeRequired; other costs/rewards are ItemAction.effects totals. The worker's
        // "used share" = active minutes / timeRequired, tracked by a non-persisted join-clock keyed
        // per (instance, action, slot). Co-op: each worker debits the full cost and is refunded their
        // unused share, so each nets the cost of the minutes they personally worked.

        private NeedsConfig _needsConfig;
        private NeedsConfig GetNeedsConfig() =>
            _needsConfig ??= (FindFirstObjectByType<WorldClockDriver>()?.NeedsConfig ?? new NeedsConfig());

        // 0.2.11c3 replaced the in-memory `_joinClock` dictionary with `ActionRecord.commitStart` /
        // `committedMinutes` on the dreamer's slot entry. The dictionary was never persisted, so
        // after a revert every join clock was lost and the next refund computed against a missing
        // key — silently refunding nothing. The slot record is saved, so refunds are now revert-exact.

        /// <summary>
        /// Debits the COSTS of a commitment up front: <paramref name="committed"/> minutes from the
        /// day pool, plus the negative-amount effect totals scaled to the committed share of the
        /// action. Reward effects (positive amount) are NOT granted here — they are delivered by
        /// yieldModel (atomic = on completion, proportional = over the work).
        ///
        /// Scaling the effects matters: committing 15 minutes of a 60-minute fell must cost a
        /// quarter of the energy, not all of it, or a partial commit would be strictly punished.
        /// </summary>
        private void DebitCommitmentCost(int slot, ItemAction ia, float committed)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (dreamer == null || ia == null || committed <= 0f) return;
            var   cfg   = GetNeedsConfig();
            float share = ia.timeRequired > 0f ? Mathf.Clamp01(committed / ia.timeRequired) : 1f;

            SimResolver.ApplyActionEffect(dreamer, ResourceStat.TimePool, -committed, cfg);
            if (ia.effects != null)
                foreach (var e in ia.effects)
                    if (e.amount < 0f)   // costs only; rewards are delivered on completion / proportionally
                        SimResolver.ApplyActionEffect(dreamer, e.stat, e.amount * share, cfg);

            if (dreamer.timePool < 0f) ApplyExhaustion(dreamer, cfg);
        }

        /// <summary>Refunds the given unused fraction [0,1] of a commitment's COSTS back to the worker.</summary>
        private void RefundCommitmentCost(int slot, ItemAction ia, float committed, float unusedFraction)
        {
            if (ia == null || committed <= 0f || unusedFraction <= 0f) return;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (dreamer == null) return;
            var   cfg   = GetNeedsConfig();
            float share = ia.timeRequired > 0f ? Mathf.Clamp01(committed / ia.timeRequired) : 1f;

            SimResolver.ApplyActionEffect(dreamer, ResourceStat.TimePool, committed * unusedFraction, cfg);
            if (ia.effects != null)
                foreach (var e in ia.effects)
                    if (e.amount < 0f)   // only costs were debited, so only costs are refunded
                        SimResolver.ApplyActionEffect(dreamer, e.stat, -e.amount * share * unusedFraction, cfg);
        }

        /// <summary>
        /// Client-safe estimate of the largest committable duration, for the commit menu (c1).
        ///
        /// <b>Why this exists separately from <see cref="MaxCommitFor"/>.</b> That one reads the
        /// RuntimeDataManager, which is the HOST's authoritative store — on a client every lookup in
        /// it returns null, so the cap came back 0 and the menu reported "no time to commit" for a
        /// perfectly choppable tree. This version reads only replicated state: the accrual snapshot
        /// pushed in the world-object payload, the Def SOs (identical on both peers, rule 6), and
        /// the caller's own synced day pool.
        ///
        /// It is an estimate by design — the labor figure is as fresh as the last payload push, so
        /// it can lag by up to the sync interval. That is fine because it only decides which buttons
        /// to draw: the host caps again on receipt, and is the only opinion that counts.
        /// </summary>
        public float EstimateMaxCommit(Def def, int instanceId, int actionIndex, float availablePoolMinutes)
        {
            var ia = def?.actions != null && actionIndex >= 0 && actionIndex < def.actions.Length
                        ? def.actions[actionIndex] : null;
            if (ia == null || ia.timeRequired <= 0f) return 0f;

            // The Def is the source of truth for how much labor the work needs; the payload only
            // supplies how much has been DONE. Splitting it that way is what makes an authored,
            // never-touched node work: it has no Instance in the map layer until first interaction
            // (0.2.9f, delta-only — pristine costs nothing), so it is absent from the payload
            // entirely. Absent means pristine, not "nothing to commit". Looking the whole answer up
            // in the payload is why a fresh tree reported "no time to commit" and could not be
            // chopped from the menu at all.
            float laborDone = 0f;
            if (TryGetProcessableActionState(instanceId, actionIndex, out float labor, out bool complete))
            {
                if (complete) return 0f;
                laborDone = labor;
            }

            float remainingLabor = Mathf.Max(0f, ia.timeRequired - laborDone);
            return Mathf.Max(0f, Mathf.Min(remainingLabor, availablePoolMinutes));
        }

        /// <summary>
        /// The largest commitment this dreamer could make to the given action right now (§5.5.3,
        /// c1/c2): the lesser of the labor still owed on the work and the minutes left in their day
        /// pool. Committing more than the work needs would burn pool on nothing; committing more
        /// than the pool holds is the overdraft this build exists to prevent.
        ///
        /// HOST ONLY — it reads the RDM. Clients use <see cref="EstimateMaxCommit"/>.
        /// </summary>
        public float MaxCommitFor(int instanceId, int actionIndex, int dreamerSlot)
        {
            var obj   = ResolveWorldObject(instanceId);
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            var d     = RuntimeDataManager.Instance?.GetDreamer(dreamerSlot);
            if (obj?.location?.accrual == null || clock == null || d == null) return 0f;
            if (actionIndex < 0 || actionIndex >= obj.location.accrual.Length) return 0f;

            var   def   = DefRegistry.Instance?.Get(obj.defId);
            var   ia    = def?.actions != null && actionIndex < def.actions.Length ? def.actions[actionIndex] : null;
            var   state = obj.location.accrual[actionIndex];
            // A station craft's threshold is recipe-driven (Instance.craft.requiredLabor), not the
            // def action's fixed timeRequired — otherwise the cap would be wrong for every craft.
            float total = obj.craft != null && obj.craft.stationActionIndex == actionIndex
                        ? obj.craft.requiredLabor
                        : (ia?.timeRequired ?? 0f);
            if (total <= 0f) return 0f;

            float remainingLabor = Mathf.Max(0f, total - state.LaborAt(clock.totalInGameMinutes));
            return Mathf.Max(0f, Mathf.Min(remainingLabor, d.timePool));
        }

        /// <summary>Fraction [0,1] of their commitment the worker did NOT serve, read from their
        /// (persisted, therefore revert-exact) slot record.</summary>
        private static float UnusedFractionFor(ActionRecord commitment, float now)
        {
            if (commitment == null || commitment.committedMinutes <= 0f) return 0f;
            return Mathf.Clamp01(1f - (now - commitment.commitStart) / commitment.committedMinutes);
        }

        // ── Reward delivery (yieldModel: Atomic = on completion, Proportional = over the work) ─────

        // Fraction of each proportional action's rewards already delivered (non-persisted;
        // keyed per instance+action). Seeded to 0 when work starts; on post-load it initialises to
        // current progress so nothing is re-delivered.
        private readonly Dictionary<(int inst, int action), float> _proportionalDelivered =
            new Dictionary<(int, int), float>();

        /// <summary>
        /// Delivers the reward slice for progress advancing from <paramref name="fromFraction"/> to
        /// <paramref name="toFraction"/>: positive-amount effects (each active contributor gets the
        /// delta) and yields (cumulative floor of amount × fraction, to the primary contributor).
        /// </summary>
        private void DeliverActionRewards(ItemAction ia, Instance obj, List<int> contributors,
                                          float fromFraction, float toFraction)
        {
            if (ia == null || toFraction <= fromFraction) return;
            var   cfg = GetNeedsConfig();
            float now = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;

            if (ia.effects != null)
                foreach (var e in ia.effects)
                {
                    if (e.amount <= 0f) continue; // rewards only
                    float delta = e.amount * (toFraction - fromFraction);
                    foreach (var slot in contributors)
                    {
                        var d = RuntimeDataManager.Instance?.GetDreamer(slot);
                        if (d != null) SimResolver.ApplyActionEffect(d, e.stat, delta, cfg);
                    }
                }

            if (ia.yields != null && ia.yields.Length > 0)
            {
                int primary = contributors.Count > 0 ? contributors[0] : 0;
                foreach (var y in ia.yields)
                {
                    float count = Mathf.FloorToInt(toFraction * y.amount) - Mathf.FloorToInt(fromFraction * y.amount);
                    if (count > 0f) DeliverYield(y, count, obj.location.position, primary, now);
                }
            }
        }

        /// <summary>Delivers the proportional reward slice for one in-progress action up to <paramref name="cur"/>.</summary>
        private void DeliverProportionalProgress(Instance obj, int actionIdx, ItemAction ia, float cur, List<int> contributors)
        {
            var key = (obj.id, actionIdx);
            // Missing key on a post-load in-progress action → initialise to current so past progress
            // is not re-delivered. (StartContributor seeds 0 for freshly-started work.)
            float prev = _proportionalDelivered.TryGetValue(key, out var d) ? d : cur;
            _proportionalDelivered[key] = prev;
            if (cur <= prev) return;
            DeliverActionRewards(ia, obj, contributors, prev, cur);
            _proportionalDelivered[key] = cur;
        }

        /// <summary>
        /// Finishes the work instead of abandoning it, when the accrued labor already meets the
        /// threshold. Returns true if it completed — in which case the caller must NOT go on to stop
        /// the contributor, because completion has already settled and cleared every one of them.
        ///
        /// <b>Why every exit path has to ask this first (0.2.11 fix).</b> Completion is detected in
        /// CheckActionCompletions, which runs on the 1-second sync interval and ignores actions with
        /// no active contributors — reasonably, since without contributors no new labor accrues.
        /// But EnforcePresence runs EVERY frame, so a commitment that expires at the very moment the
        /// work finishes removes the last contributor first, and the completed action is then skipped
        /// forever: 100% progress, no depletion, no transform, no yields.
        ///
        /// A skip makes that the normal case rather than a rare race. The clock jumps straight past
        /// commitStart + committedMinutes, so expiry and completion land in the same instant every
        /// time — which is exactly the "chop a tree, skip the remainder, it hits 100% and never falls"
        /// report. Serving your whole commitment must finish the work, not abandon it on the doorstep.
        /// </summary>
        private bool TryCompleteInsteadOfStopping(Instance obj, int actionIndex, float now)
        {
            var accrual = obj?.location?.accrual;
            if (accrual == null || actionIndex < 0 || actionIndex >= accrual.Length) return false;

            var state = accrual[actionIndex];
            if (state.isComplete) return false;

            bool isStationCraft = obj.craft != null && obj.craft.isStation
                                                    && obj.craft.stationActionIndex == actionIndex;

            float threshold;
            if (isStationCraft)
            {
                threshold = obj.craft.requiredLabor;
            }
            else
            {
                var def = DefRegistry.Instance?.Get(obj.defId);
                var ia  = def?.actions != null && actionIndex < def.actions.Length ? def.actions[actionIndex] : null;
                threshold = ia?.timeRequired ?? 0f;
            }

            if (threshold <= 0f || state.LaborAt(now) < threshold) return false;

            if (isStationCraft) TriggerStationCraftComplete(obj, actionIndex, now);
            else                TriggerActionComplete(obj, actionIndex, now);
            ForcePush();
            return true;
        }

        /// <summary>
        /// Refunds a worker's unserved share of their commitment to this instance+action and vacates
        /// their slot. One helper for all three exit paths — stop, action complete, craft complete —
        /// so a refund can never be applied by one and skipped by another.
        /// </summary>
        private void SettleCommitment(int slot, int instanceId, int actionIndex, ItemAction ia, float now)
        {
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (dreamer == null) return;

            int idx = dreamer.IndexOfWorldAction(instanceId, actionIndex);
            if (idx >= 0)
            {
                var commitment = dreamer.actionQueue[idx];
                RefundCommitmentCost(slot, ia, commitment.committedMinutes, UnusedFractionFor(commitment, now));
                dreamer.RemoveAt(idx);
            }
        }

        private static void ApplyExhaustion(Game.Simulation.DreamerRecord dreamer, NeedsConfig cfg)
        {
            if (dreamer.buffs == null) return;
            foreach (var b in dreamer.buffs)
                if (b.defId == ActionRecord.ExhaustionBuffId) return;
            dreamer.buffs.Add(new BuffInstance
            {
                defId            = ActionRecord.ExhaustionBuffId,
                remainingMinutes = cfg.exhaustionDurationMinutes,
            });
        }

        // ── Server: completion detection ─────────────────────────────────────────

        private void CheckActionCompletions()
        {
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (layer == null || clock == null) return;

            float now = clock.totalInGameMinutes;

            var toComplete      = new List<(Instance obj, int actionIdx)>();
            var craftsToComplete = new List<(Instance obj, int actionIdx)>();
            foreach (var obj in layer.worldObjects.items)
            {
                if (obj.location.accrual == null) continue;
                var def = DefRegistry.Instance?.Get(obj.defId);
                if (def == null) continue;

                int len = Mathf.Min(obj.location.accrual.Length, def.actions.Length);
                for (int i = 0; i < len; i++)
                {
                    var state = obj.location.accrual[i];
                    var ia    = def.actions[i];
                    if (ia == null || state.isComplete) continue;

                    // Completion is checked WITHOUT requiring active contributors (0.2.11 fix). The
                    // old guard assumed no contributors meant no new progress and therefore nothing
                    // to finish — but an action can reach its threshold in the same instant its last
                    // contributor leaves, and would then sit at 100% forever. The stop paths catch
                    // that case directly; this is the backstop that also repairs any world already
                    // saved in the stranded state.
                    //
                    // Station craft (0.2.10c): threshold is the recipe's requiredLabor (on obj.craft),
                    // not the def action's timeRequired; completion rolls the tier (§5.7.3). Checked
                    // BEFORE the instant-action guard below, because a station's craft ItemAction may
                    // legitimately carry timeRequired 0 — the recipe supplies the real threshold.
                    if (obj.craft != null && obj.craft.isStation && obj.craft.stationActionIndex == i)
                    {
                        if (obj.craft.requiredLabor > 0f && state.LaborAt(now) >= obj.craft.requiredLabor)
                            craftsToComplete.Add((obj, i));
                        continue;
                    }

                    // Instant actions (timeRequired 0) are resolved by DispatchWorldAction's outcome
                    // path and never accrue. They MUST be excluded before the threshold test below,
                    // which would otherwise read 0 >= 0 as "finished" and spontaneously deplete every
                    // pickup in the world on the next sync tick. The old contributor guard hid this;
                    // relaxing that guard exposes it, so the exclusion is now explicit.
                    if (ia.timeRequired <= 0f) continue;

                    float labor = state.LaborAt(now);
                    if (labor >= ia.timeRequired)
                    {
                        toComplete.Add((obj, i));
                        continue;
                    }

                    // Everything below needs someone actually working: proportional rewards are
                    // delivered to the present contributors as the work accrues.
                    if (!state.HasActiveContributors) continue;

                    if (ia.yieldModel == YieldModel.Proportional && ia.timeRequired > 0f)
                        DeliverProportionalProgress(obj, i, ia,
                            Mathf.Clamp01(labor / ia.timeRequired), new List<int>(state.contributors));
                }
            }

            foreach (var (obj, actionIdx) in toComplete)
                TriggerActionComplete(obj, actionIdx, now);
            foreach (var (obj, actionIdx) in craftsToComplete)
                TriggerStationCraftComplete(obj, actionIdx, now);
        }

        // ── Server: action completion + yield resolution ─────────────────────────

        private void TriggerActionComplete(Instance obj, int actionIdx, float clockNow)
        {
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null || !layer.worldObjects.items.Contains(obj)) return;

            var def = DefRegistry.Instance?.Get(obj.defId);
            if (def == null || actionIdx >= def.actions.Length || def.actions[actionIdx] == null) return;

            var actionDef = def.actions[actionIdx];
            var state     = obj.location.accrual[actionIdx];

            var finalContributors = new List<int>(state.contributors);

            foreach (int slot in finalContributors)
                // Refund the share this worker did not personally serve — co-op finishes early, so a
                // worker's served time is usually less than the commitment they were debited up front.
                SettleCommitment(slot, obj.id, actionIdx, actionDef, clockNow);

            state.Complete(clockNow);
            Debug.Log($"[MapEntitySync] World object {obj.id} action '{actionDef.actionId}' complete.");

            // Deliver rewards (yields + positive effects): atomic delivers the whole lot now;
            // proportional delivered most of it during the work, so deliver only the remainder.
            float already = _proportionalDelivered.TryGetValue((obj.id, actionIdx), out var d) ? d : 0f;
            DeliverActionRewards(actionDef, obj, finalContributors, already, 1f);
            _proportionalDelivered.Remove((obj.id, actionIdx));

            if (actionDef.onDepletion == DepletionBehavior.Transform)
            {
                // depletionSpawns are the transform result (always full on completion), not a reward.
                ResolveActionYields(actionDef.depletionSpawns, obj.location.position, finalContributors);
                DepleteOrRemove(layer, obj);
                return;
            }

            bool allDone = true;
            foreach (var s in obj.location.accrual)
                if (!s.isComplete) { allDone = false; break; }

            if (allDone)
            {
                Debug.Log($"[MapEntitySync] World object {obj.id} all actions done — removing.");
                DepleteOrRemove(layer, obj);
            }
        }

        /// <summary>
        /// A runtime object is removed on depletion; an AUTHORED node (0.2.9f) is instead marked
        /// depleted and KEPT in the layer, so the delta persists + syncs (client hides the scene
        /// visual) and a revert can restore it. There is no scene GameObject to remove.
        /// </summary>
        private void DepleteOrRemove(MapEntityLayer layer, Instance obj)
        {
            if (obj.authored)
            {
                obj.depleted = true;
                Debug.Log($"[MapEntitySync] Authored node {obj.id} depleted (kept as delta; scene visual hidden).");
            }
            else
            {
                RemoveWorldObject(layer, obj.id);
            }
        }

        /// <summary>Delivers every yield in full (used for depletion spawns and any atomic-time set).</summary>
        private void ResolveActionYields(ActionYield[] yields, Float3 spawnPos, List<int> contributors)
        {
            if (yields == null) return;
            float now                = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;
            int   primaryContributor = contributors.Count > 0 ? contributors[0] : 0;
            foreach (var yield in yields)
                DeliverYield(yield, yield.amount, spawnPos, primaryContributor, now);
        }

        /// <summary>Delivers <paramref name="amount"/> of a single yield — item → primary contributor
        /// (or ground), world object → that many spawns.</summary>
        private void DeliverYield(ActionYield yield, float amount, Float3 spawnPos, int primaryContributor, float now)
        {
            if (amount <= 0f) return;
            if (yield.isWorldObject)
            {
                for (int i = 0; i < amount; i++) SpawnProcessable(yield.worldObjectDefId, spawnPos.ToVector3());
                return;
            }

            var itemDef = DefRegistry.Instance?.Get(yield.itemDefId);
            var inst    = new Instance { defId = yield.itemDefId, quantity = amount };
            if (itemDef != null && itemDef.perishable && itemDef.effectiveLifespan > 0f)
                inst.InitConditionTriple(now, itemDef.effectiveLifespan);
            if (itemDef != null && itemDef.isDurabilityTool && itemDef.maxDurability > 0f)
                inst.durability = itemDef.maxDurability;

            var invSync = DreamerInventorySync.GetForSlot(primaryContributor);
            if (invSync != null)
            {
                invSync.TryDeliverItem(inst, now);
                Debug.Log($"[MapEntitySync] Yield: {amount}× defId={yield.itemDefId} → slot {primaryContributor}.");
            }
            else
            {
                Debug.LogWarning($"[MapEntitySync] No DreamerInventorySync for slot {primaryContributor}; item dropped.");
                PlaceItem(yield.itemDefId, amount, spawnPos.ToVector3());
            }
        }

        private void RemoveWorldObject(MapEntityLayer layer, int id) =>
            layer.worldObjects.items.RemoveAll(o => o.id == id);

        // ── Host API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures an Instance about to live in the world carries an accrual array matching its
        /// Def's world actions: one fresh <see cref="ActionAccrualState"/> per action for a world
        /// object, or null for a plain ground item (Def has no world actions).
        ///
        /// Must run on every InWorld placement whose source did not already carry accrual. A
        /// picked-up world object loses its accrual when it enters a container as a plain
        /// inventory item, so a later drop MUST rebuild it — otherwise the ground instance reports
        /// ActionCount 0, never enters the processable registry, and becomes non-interactable
        /// (can't be processed or re-picked-up). Carry drop already preserves accrual and does not
        /// need this (TDD §1.12).
        /// </summary>
        public static void InitWorldAccrual(Instance inst)
        {
            if (inst?.location == null) return;
            var def = DefRegistry.Instance?.Get(inst.defId);
            if (def == null || !def.HasWorldActions())
            {
                inst.location.accrual = null;
                return;
            }
            int count   = def.actions?.Length ?? 0;
            var accrual = new ActionAccrualState[count];
            for (int i = 0; i < count; i++) accrual[i] = new ActionAccrualState();
            inst.location.accrual = accrual;
        }

        // ── Ground snap ──────────────────────────────────────────────────────────

        // Per-Def base offset from PrefabFootOffset — renderer bounds are not free and every
        // instance of a Def resolves to the same number.
        private readonly Dictionary<int, float> _footOffsetByDef = new Dictionary<int, float>();

        /// <summary>
        /// Host: resolve a world placement onto the ground (see <see cref="GroundSnap"/>). Every
        /// path that puts an Instance InWorld goes through this — yields, drops, unequips, carry
        /// drops — so a single toggle governs them all.
        ///
        /// Called once, at placement: the resolved position is what gets stored, synced and saved.
        /// Nothing re-snaps afterwards, so save/revert reproduce the placement exactly.
        /// </summary>
        /// <param name="defId">Def of the thing being placed — supplies the prefab base offset.</param>
        /// <param name="ignoreRoot">Existing visual of the object being placed, if it already has
        /// one (a carried object being dropped stands in its own probe's way).</param>
        public Vector3 ResolveGroundPosition(Vector3 position, int defId, Transform ignoreRoot = null)
        {
            if (!_snapToGround) return position;

            float footOffset = _alignToPrefabBase ? FootOffsetForDef(defId) : 0f;

            return GroundSnap.TryResolve(position, _groundMask, _probeAbove, _probeBelow,
                       footOffset, _groundClearance, ignoreRoot, out var grounded)
                ? grounded
                : position;
        }

        private float FootOffsetForDef(int defId)
        {
            if (_footOffsetByDef.TryGetValue(defId, out var cached)) return cached;

            var def    = DefRegistry.Instance?.Get(defId);
            var prefab = def?.prefab != null
                ? def.prefab
                : (def != null && def.HasWorldActions() ? _processablePrefab : _groundItemPrefab);

            float offset = GroundSnap.PrefabFootOffset(prefab);
            _footOffsetByDef[defId] = offset;
            return offset;
        }

        /// <summary>Host: place a plain ground item at the given world position.</summary>
        public void PlaceItem(int defId, float quantity, Vector3 position)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var world = RuntimeDataManager.Instance?.WorldState;
            if (layer == null || world == null) return;

            float now  = world.clock?.totalInGameMinutes ?? 0f;
            var   inst = new Instance { defId = defId, quantity = quantity };
            var   def  = DefRegistry.Instance?.Get(defId);
            if (def != null && def.perishable && def.effectiveLifespan > 0f)
                inst.InitConditionTriple(now, def.effectiveLifespan);
            if (def != null && def.isDurabilityTool && def.maxDurability > 0f)
                inst.durability = def.maxDurability;

            position      = ResolveGroundPosition(position, defId);
            inst.id       = layer.nextInstanceId++;
            inst.location = new InstanceLocation { kind = LocationKind.InWorld, position = position.ToFloat3() };
            InitWorldAccrual(inst); // world-action Defs become actionable; plain items stay accrual-null
            layer.worldObjects.items.Add(inst);
            ForcePush();
            Debug.Log($"[MapEntitySync] PlaceItem defId={defId} qty={quantity} at {position}.");
        }

        /// <summary>
        /// Host: spawn an actionable world object at the given position (TDD §5.6.7, 0.2.8a2).
        /// Creates an InWorld Instance with accrual[] matching the Def's actions array.
        /// </summary>
        public void SpawnProcessable(int defId, Vector3 position)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null) return;

            var def = DefRegistry.Instance?.Get(defId);
            if (def == null)
            {
                Debug.LogWarning($"[MapEntitySync] SpawnProcessable: no def for id={defId}.");
                return;
            }

            int actionCount = def.actions?.Length ?? 0;
            var accrual     = new ActionAccrualState[actionCount];
            for (int i = 0; i < actionCount; i++)
                accrual[i] = new ActionAccrualState();

            // The source object's own position is the spawn position, and an authored trunk whose
            // base is sunk into the terrain would otherwise put its fallen form underground.
            position = ResolveGroundPosition(position, defId);

            var obj = new Instance
            {
                id       = layer.nextInstanceId++,
                defId    = defId,
                quantity = 1f,
                location = new InstanceLocation
                {
                    kind     = LocationKind.InWorld,
                    position = position.ToFloat3(),
                    accrual  = accrual,
                },
            };
            layer.worldObjects.items.Add(obj);
            ForcePush();
            Debug.Log($"[MapEntitySync] Spawned world object id={obj.id} def={defId} at {position}.");
        }

        /// <summary>
        /// Host: find a world object by id, materialising an authored node's delta on FIRST interaction
        /// (0.2.9f). A registered authored id not yet in the layer becomes an InWorld Instance
        /// (authored = true, position + Def read from its scene Interactable, accrual per its actions);
        /// pristine authored nodes are never materialised, so they cost nothing and send no traffic.
        /// Returns null if the id is neither a live instance nor a registered authored node.
        /// </summary>
        private Instance ResolveWorldObject(int id)
        {
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null) return null;

            var obj = layer.worldObjects.items.Find(p => p.id == id);
            if (obj != null) return obj;

            if (_authoredById.TryGetValue(id, out var it) && it != null && it.def != null)
            {
                var inst = new Instance
                {
                    id       = id,
                    defId    = it.def.defId,
                    quantity = 1f,
                    authored = true,
                    location = new InstanceLocation
                    {
                        kind     = LocationKind.InWorld,
                        position = it.transform.position.ToFloat3(),
                    },
                };
                InitWorldAccrual(inst);
                layer.worldObjects.items.Add(inst);
                Debug.Log($"[MapEntitySync] Materialised authored node id={id} (def={it.def.defId}) on first interaction.");
                return inst;
            }
            return null;
        }

        /// <summary>
        /// Host: register a dreamer as a contributor to a world-object action (TDD §5.5.5, 0.2.8b4).
        /// </summary>
        public void StartContributor(int instanceId, int actionIndex, int dreamerSlot, float committedMinutes = -1f)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (layer == null || clock == null) return;

            var obj = ResolveWorldObject(instanceId);
            if (obj == null)
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: world object {instanceId} not found.");
                return;
            }

            var accrual = obj.location.accrual;
            if (accrual == null || actionIndex < 0 || actionIndex >= accrual.Length)
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: actionIndex {actionIndex} out of range.");
                return;
            }

            var def       = DefRegistry.Instance?.Get(obj.defId);
            var actionDef = def?.actions[actionIndex];
            var state     = accrual[actionIndex];

            // Instant actions (timeRequired <= 0) resolve immediately by outcome and must NOT
            // open a labor-accrual segment. Any caller that reaches StartContributor with an
            // instant action (e.g. the dev Gather "Go" button, which routes every action through
            // RequestGatherServerRpc) is redirected to the instant dispatch path. Without this,
            // an instant ToInventory pickup runs through the timed-completion path, whose yield
            // resolution ignores the "deliver the object itself" case (empty yields) — so the
            // action completes into nothing and the object stays in the world. DispatchWorldAction
            // only calls StartContributor for timeRequired > 0, so there is no recursion.
            if (actionDef != null && actionDef.timeRequired <= 0f)
            {
                DispatchWorldAction(instanceId, actionIndex, dreamerSlot);
                return;
            }

            // A station-craft action is recipe-driven — it must be committed via StartStationCraft,
            // which sets the recipe + requiredLabor. Reject a generic gather start on it so its
            // labor/output never fall through to the def action's fixed values (0.2.10c).
            if (actionDef != null && actionDef.actionId == CraftActionId)
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: '{CraftActionId}' action must be started via StartStationCraft (recipe required).");
                return;
            }

            if (state.isComplete)
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: action {actionIndex} already complete.");
                return;
            }

            if (actionDef != null && actionDef.prerequisite != null)
            {
                // prerequisite is a verb (ActionDef); find the sibling ItemAction that binds it.
                int prereqIdx = Array.FindIndex(def.actions, ia => ia != null && ia.action == actionDef.prerequisite);
                if (prereqIdx >= 0 && prereqIdx < accrual.Length && !accrual[prereqIdx].isComplete)
                {
                    Debug.LogWarning($"[MapEntitySync] StartContributor: prerequisite '{actionDef.prerequisite.actionId}' not met.");
                    return;
                }
            }

            // Tool check
            if (actionDef != null && actionDef.allowedToolCategoryIds?.Length > 0)
            {
                bool hasTool = false;
                var  dreamer = RuntimeDataManager.Instance?.GetDreamer(dreamerSlot);
                if (dreamer != null)
                {
                    foreach (var (_, container) in dreamer.equipSlots.AllOccupied())
                    {
                        if (container.contents == null) continue;
                        foreach (var item in container.contents.items)
                        {
                            if (!DefRegistry.Instance.TryGetDef(item.defId, out var iDef)) continue;
                            foreach (var cat in actionDef.allowedToolCategoryIds)
                                if (iDef.toolCategoryId == cat) { hasTool = true; break; }
                            if (hasTool) break;
                        }
                        if (hasTool) break;
                    }
                }
                if (!hasTool && actionDef.toolRequired)
                {
                    Debug.LogWarning($"[MapEntitySync] StartContributor: no matching tool for '{actionDef.actionId}'.");
                    return;
                }
            }

            if (actionDef != null && !actionDef.coop && state.HasActiveContributors
                && !state.contributors.Contains(dreamerSlot))
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: action '{actionDef?.actionId}' is single-occupancy.");
                return;
            }

            // Single active slot (§5.5, 0.2.11a3): committed labor is exclusive. Checked BEFORE the
            // contributor is added and before any cost is debited, so a rejected start changes nothing.
            var slotHolder = RuntimeDataManager.Instance?.GetDreamer(dreamerSlot);
            if (IsSlotBlocked(slotHolder, instanceId, actionIndex))
            {
                Debug.LogWarning($"[MapEntitySync] StartContributor: slot {dreamerSlot} is already committed to " +
                                 $"{slotHolder.CurrentTaskType()} — one active action at a time.");
                return;
            }

            // ── Trivial verbs against the bracket (§5.5.5, 0.2.11d2/d5/d6) ──────────
            //
            // The object-side twin of the Trivial consumable path. A verb authored Trivial —
            // sharpen, mend — resolves NOW: no world-clock advance, no accrual segment, its
            // timeRequired debited from the bracket. Short bracket and not critical → fall through
            // and run it as an ordinary Active commitment at full duration (d6), which is the
            // forgiving answer §5.5.15 #6 settles on.
            if (actionDef.actionClass == ActionClass.Trivial)
            {
                bool covered = slotHolder != null && slotHolder.trivialBracket >= actionDef.timeRequired;
                if (covered || (slotHolder != null && actionDef.allowsCriticalBypass))
                {
                    slotHolder.trivialBracket = Mathf.Max(0f, slotHolder.trivialBracket - actionDef.timeRequired);
                    Debug.Log($"[MapEntitySync] Slot {dreamerSlot} performed Trivial '{actionDef.actionId}' instantly " +
                              $"({actionDef.timeRequired:F0}m off the bracket, {slotHolder.trivialBracket:F0}m left)" +
                              (covered ? "." : " — critical bypass (d5)."));

                    // Instant means the whole labor lands at once: seed the contributor so the
                    // completion path sees them as the worker, then complete on the same timestamp
                    // so the clock never moves.
                    state.AddContributor(dreamerSlot, clock.totalInGameMinutes);
                    state.accumulatedLabor = actionDef.timeRequired;
                    TriggerActionComplete(obj, actionIndex, clock.totalInGameMinutes);
                    ForcePush();
                    return;
                }

                Debug.Log($"[MapEntitySync] Slot {dreamerSlot}: bracket too short for Trivial " +
                          $"'{actionDef.actionId}' — running it as an Active action instead (d6).");
            }

            // Commitment length (§5.5.3, c1/c2/c3). A negative request means "commit the maximum",
            // which is the hold-interact gesture; an explicit request is capped to the same ceiling,
            // so over-commitment is impossible by construction rather than by a later check.
            float maxCommit = MaxCommitFor(instanceId, actionIndex, dreamerSlot);
            if (maxCommit <= 0f)
            {
                Debug.Log($"[MapEntitySync] StartContributor: slot {dreamerSlot} has nothing to commit " +
                          $"(no labor left, or day pool exhausted).");
                return;
            }
            float committed = committedMinutes <= 0f ? maxCommit : Mathf.Min(committedMinutes, maxCommit);

            state.AddContributor(dreamerSlot, clock.totalInGameMinutes);

            // Up-front cost: debit the committed minutes now; the unserved share is refunded when
            // the commitment ends (stop, expiry, or early co-op completion).
            DebitCommitmentCost(dreamerSlot, actionDef, committed);

            // Proportional rewards start delivering from 0 (seed once, on the first contributor).
            if (actionDef.yieldModel == YieldModel.Proportional
                && !_proportionalDelivered.ContainsKey((instanceId, actionIndex)))
                _proportionalDelivered[(instanceId, actionIndex)] = 0f;

            OccupySlotWithWorldAction(slotHolder, ActionSlotKind.WorldAction, TaskType.Gathering,
                instanceId, actionIndex, actionDef.actionClass, 0, clock.totalInGameMinutes, committed);

            Debug.Log($"[MapEntitySync] Slot {dreamerSlot} started gathering world object {instanceId} action {actionIndex}.");
        }

        /// <summary>
        /// Puts object-bound work into the dreamer's single action slot (§5.5, 0.2.11a3).
        ///
        /// The record is <c>externallyResolved</c>: SimResolver must never time it out, because its
        /// progress is piecewise-linear labor accrual on the Instance, not elapsed duration. The
        /// stop / complete paths (StopContributor, TriggerActionComplete, TriggerStationCraftComplete)
        /// are what clear it.
        ///
        /// Re-entrant by design: a dreamer already bound to THIS instance+action keeps their existing
        /// record, so resuming or re-confirming does not stack duplicate slot entries.
        /// </summary>
        private static void OccupySlotWithWorldAction(Game.Simulation.DreamerRecord dreamer,
            ActionSlotKind kind, TaskType taskType, int instanceId, int actionIndex,
            ActionClass actionClass, int recipeId, float now, float committedMinutes)
        {
            if (dreamer == null) return;
            dreamer.actionQueue ??= new List<ActionRecord>();
            if (dreamer.IndexOfWorldAction(instanceId, actionIndex) >= 0) return;

            dreamer.actionQueue.Insert(0, new ActionRecord
            {
                kind               = kind,
                taskType           = taskType,
                actionClass        = actionClass,
                instanceId         = instanceId,
                actionIndex        = actionIndex,
                recipeId           = recipeId,
                externallyResolved = true,
                started            = true,
                startTime          = now,
                committedMinutes   = committedMinutes,
                commitStart        = now,
            });
        }

        /// <summary>
        /// True when this dreamer already holds the single active slot with work OTHER than the
        /// given instance+action (§5.5, 0.2.11a3). The presence rule makes committed labor
        /// exclusive: you cannot fell a tree and craft at a station in the same minute.
        /// </summary>
        private static bool IsSlotBlocked(Game.Simulation.DreamerRecord dreamer, int instanceId, int actionIndex)
        {
            var active = dreamer?.ActiveAction();
            return active != null && !active.BindsWorldAction(instanceId, actionIndex);
        }

        /// <summary>Host: remove a dreamer as a contributor from a world-object action.</summary>
        public void StopContributor(int instanceId, int actionIndex, int dreamerSlot)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (layer == null || clock == null) return;

            var obj = layer.worldObjects.items.Find(p => p.id == instanceId);
            if (obj == null) return;
            var accrual = obj.location.accrual;
            if (accrual == null || actionIndex < 0 || actionIndex >= accrual.Length) return;

            // Refund the unserved share of the commitment and vacate the slot. The accrual segment
            // closes at `now`, so the labor already done stays on the object — cancel is never
            // punitive and never loses a chunk (§5.5.4, c4).
            float now = clock.totalInGameMinutes;
            var   def = DefRegistry.Instance?.Get(obj.defId);
            var   ia  = def?.actions != null && actionIndex < def.actions.Length ? def.actions[actionIndex] : null;

            // The work may already be finished — a commitment that expires exactly as the labor
            // threshold is met (the normal case after a skip) must complete, not abandon.
            if (TryCompleteInsteadOfStopping(obj, actionIndex, now)) return;

            accrual[actionIndex].RemoveContributor(dreamerSlot, now);
            SettleCommitment(dreamerSlot, instanceId, actionIndex, ia, now);

            Debug.Log($"[MapEntitySync] Slot {dreamerSlot} stopped gathering world object {instanceId} action {actionIndex}.");
        }

        // ── Station crafting (object-bound §5.6.5 task, TDD §5.7.1, 0.2.10c) ──────
        //
        // A station craft reuses the §5.5.5 accrual engine (piecewise-linear labor, co-op = faster,
        // re-projection on contributor change) but is recipe-driven: the labor threshold and the
        // output come from the recipe (stored on Instance.craft), not the station Def's action. Commit
        // consumes the crafter's materials + reserves their committed labor; assist joins a coop craft;
        // leave refunds the unused share and leaves the partial on the station, resumable by anyone
        // (c2). Completion rolls the tier (§5.7.3) and resets the action so the station is reusable —
        // the station is NOT removed.

        /// <summary>
        /// Applies a station-craft commitment's costs, signed by <paramref name="sign"/> (-1 = debit
        /// on commit, +1 × unused fraction = refund on leave/complete). Station costs are
        /// recipe-driven (requiredLabor / energyPerMinute) rather than ItemAction effects, so they
        /// need their own helper — but the shape mirrors DebitCommitmentCost exactly.
        /// </summary>
        private void ApplyStationCommitmentCost(Game.Simulation.DreamerRecord crafter, RecipeDef recipe,
                                                float committed, float sign)
        {
            if (crafter == null || recipe == null || committed <= 0f || sign == 0f) return;
            var cfg = GetNeedsConfig();
            SimResolver.ApplyActionEffect(crafter, ResourceStat.TimePool, sign * committed, cfg);
            if (recipe.energyPerMinute > 0f)
                SimResolver.ApplyActionEffect(crafter, ResourceStat.Energy, sign * recipe.energyPerMinute * committed, cfg);
            if (sign < 0f && crafter.timePool < 0f) ApplyExhaustion(crafter, cfg);
        }

        /// <summary>
        /// Refunds a station crafter's unserved share and vacates their slot. The station-craft twin
        /// of <see cref="SettleCommitment"/> — same single-exit-path reasoning, different cost source.
        /// </summary>
        private void SettleStationCommitment(int slot, int instanceId, int actionIndex, RecipeDef recipe, float now)
        {
            var crafter = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (crafter == null) return;

            int idx = crafter.IndexOfWorldAction(instanceId, actionIndex);
            if (idx < 0) return;

            var commitment = crafter.actionQueue[idx];
            ApplyStationCommitmentCost(crafter, recipe, commitment.committedMinutes, UnusedFractionFor(commitment, now));
            crafter.RemoveAt(idx);
        }

        /// <summary>Index of the station's craft action (the ItemAction bound to the `craft` verb), or -1.</summary>
        private static int FindCraftActionIndex(Def def)
        {
            if (def?.actions == null) return -1;
            for (int i = 0; i < def.actions.Length; i++)
                if (def.actions[i]?.actionId == CraftActionId) return i;
            return -1;
        }

        /// <summary>
        /// Host: commit (or assist / resume) a station craft (TDD §5.7.1, 0.2.10c). New craft →
        /// validate the station matches the recipe, gate on tool, consume the committing crafter's
        /// materials, and set Instance.craft. Assist/resume → same recipe on an in-progress craft
        /// (coop only for a second live contributor). Then reserve the contributor's committed labor
        /// + energy and open their accrual segment.
        /// </summary>
        public void StartStationCraft(int instanceId, int recipeId, int dreamerSlot, float committedMinutes = -1f)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var world = RuntimeDataManager.Instance?.WorldState;
            var clock = world?.clock;
            if (layer == null || world == null || clock == null) return;

            // ResolveWorldObject (not Find) so an AUTHORED in-scene station materialises on its first
            // craft (0.2.9f) — a station's first interaction is usually the craft itself.
            var obj = ResolveWorldObject(instanceId);
            if (obj == null) { Debug.LogWarning($"[MapEntitySync] StartStationCraft: station {instanceId} not found."); return; }

            var recipe = RecipeRegistry.Instance?.Get(recipeId);
            if (recipe == null || recipe.IsHandCraft)
            {
                Debug.LogWarning($"[MapEntitySync] StartStationCraft: recipe {recipeId} is not a station recipe.");
                return;
            }
            if (obj.defId != recipe.requiredStationDefId)
            {
                Debug.LogWarning($"[MapEntitySync] StartStationCraft: station def {obj.defId} ≠ recipe station {recipe.requiredStationDefId}.");
                return;
            }

            var def      = DefRegistry.Instance?.Get(obj.defId);
            int craftIdx = FindCraftActionIndex(def);
            if (craftIdx < 0 || obj.location.accrual == null || craftIdx >= obj.location.accrual.Length)
            {
                Debug.LogWarning($"[MapEntitySync] StartStationCraft: station def {obj.defId} has no '{CraftActionId}' action.");
                return;
            }
            var state   = obj.location.accrual[craftIdx];
            if (state.isComplete) return;

            var crafter = RuntimeDataManager.Instance?.GetDreamer(dreamerSlot);
            var invSync = DreamerInventorySync.GetForSlot(dreamerSlot);
            if (crafter == null || invSync == null) return;
            if (state.contributors.Contains(dreamerSlot)) return; // already contributing

            float now = clock.totalInGameMinutes;

            // Single active slot (§5.5, 0.2.11a3): checked before materials are consumed and before
            // any cost is reserved, so a rejected commit leaves the crafter untouched.
            if (IsSlotBlocked(crafter, instanceId, craftIdx))
            {
                Debug.LogWarning($"[MapEntitySync] StartStationCraft: slot {dreamerSlot} is already committed to " +
                                 $"{crafter.CurrentTaskType()} — one active action at a time.");
                return;
            }

            if (obj.craft == null)
            {
                // ── New craft ──
                if (recipe.requiredToolCategoryId != 0 && !invSync.CrafterHasTool(recipe.requiredToolCategoryId))
                { Debug.LogWarning($"[MapEntitySync] StartStationCraft: no tool of category {recipe.requiredToolCategoryId}."); return; }
                if (crafter.timePool < recipe.requiredLabor)
                { Debug.Log($"[MapEntitySync] StartStationCraft: pool exhausted for slot {dreamerSlot}."); return; }
                if (!invSync.CrafterCanAfford(recipe.inputs))
                { Debug.Log($"[MapEntitySync] StartStationCraft: missing materials for '{recipe.displayName}'."); return; }

                var reserved = invSync.CrafterConsumeInputs(recipe.inputs);
                obj.craft = new CraftRecord
                {
                    recipeId           = recipe.recipeId,
                    crafterSlot        = dreamerSlot,
                    startTime          = now,
                    craftCounter       = world.craftCounter++,   // RNG-as-state seed uniqueness (§5.7.3 b2)
                    outputAmount       = Mathf.Max(1, recipe.outputAmount),
                    rollGranularity    = (int)recipe.rollGranularity,
                    isStation          = true,
                    stationActionIndex = craftIdx,
                    requiredLabor      = recipe.requiredLabor,
                    reservedInputs     = reserved,
                };
                Debug.Log($"[MapEntitySync] Station {instanceId} started craft '{recipe.displayName}' (labor {recipe.requiredLabor:F0}, counter {obj.craft.craftCounter}).");
            }
            else
            {
                // ── Assist / resume ──
                if (obj.craft.recipeId != recipeId)
                { Debug.LogWarning($"[MapEntitySync] StartStationCraft: station busy with a different craft."); return; }
                if (!recipe.coop && state.HasActiveContributors)
                { Debug.LogWarning($"[MapEntitySync] StartStationCraft: '{recipe.displayName}' is single-crafter (locked)."); return; }
                if (crafter.timePool < recipe.requiredLabor)
                { Debug.Log($"[MapEntitySync] StartStationCraft: pool exhausted for slot {dreamerSlot}."); return; }
            }

            // Commitment length (§5.5.3, c3) — capped the same way a gather is: the lesser of the
            // labor the craft still needs and this crafter's day pool. A negative request means max.
            float maxCommit = MaxCommitFor(instanceId, craftIdx, dreamerSlot);
            if (maxCommit <= 0f)
            {
                Debug.Log($"[MapEntitySync] StartStationCraft: slot {dreamerSlot} has nothing to commit.");
                return;
            }
            float committed = committedMinutes <= 0f ? maxCommit : Mathf.Min(committedMinutes, maxCommit);

            state.AddContributor(dreamerSlot, now);

            // Reserve this contributor's committed labor (pool) + energy up front; refunded pro-rata
            // on leave / early completion (co-op).
            ApplyStationCommitmentCost(crafter, recipe, committed, -1f);

            // Object-bound handle: a station craft rides the same single slot as gathering, tagged
            // StationCraft so the completion/cancel paths can tell them apart (0.2.11a3).
            OccupySlotWithWorldAction(crafter, ActionSlotKind.StationCraft, TaskType.Crafting,
                instanceId, craftIdx, recipe.actionClass, recipe.recipeId, now, committed);

            ForcePush();
        }

        /// <summary>
        /// Host: a contributor leaves a station craft (TDD §5.5.4, 0.2.10c). Refunds their unused
        /// labor/energy share and closes their segment; the partial persists on the station,
        /// resumable by anyone (c2). If the last contributor leaves with materials already consumed,
        /// the craft remains staged on the station (Instance.craft) until finished or explicitly
        /// abandoned.
        /// </summary>
        public void StopStationCraft(int instanceId, int dreamerSlot)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            var clock = RuntimeDataManager.Instance?.WorldState?.clock;
            if (layer == null || clock == null) return;

            var obj = layer.worldObjects.items.Find(p => p.id == instanceId);
            if (obj?.craft == null || !obj.craft.isStation) return;

            int craftIdx = obj.craft.stationActionIndex;
            if (obj.location.accrual == null || craftIdx < 0 || craftIdx >= obj.location.accrual.Length) return;
            var state = obj.location.accrual[craftIdx];
            if (!state.contributors.Contains(dreamerSlot)) return;

            float now    = clock.totalInGameMinutes;
            var   recipe = RecipeRegistry.Instance?.Get(obj.craft.recipeId);

            // Same rule as a gather: a craft whose labor is already served completes rather than
            // being abandoned at the threshold.
            if (TryCompleteInsteadOfStopping(obj, craftIdx, now)) return;

            state.RemoveContributor(dreamerSlot, now);
            SettleStationCommitment(dreamerSlot, instanceId, craftIdx, recipe, now);

            ForcePush();
            Debug.Log($"[MapEntitySync] Slot {dreamerSlot} left station craft {instanceId} (partial persists, resumable).");
        }

        /// <summary>
        /// Host: a station craft reached its requiredLabor. Refund each contributor's unused share,
        /// roll the tier(s) at completion (§5.7.3) and deliver to the committing crafter (overflow →
        /// station location), then reset the action so the station is reusable (the station is NOT
        /// removed). Skip/revert re-derive the identical output from the saved craft record.
        /// </summary>
        private void TriggerStationCraftComplete(Instance obj, int actionIdx, float clockNow)
        {
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null || obj?.craft == null || !layer.worldObjects.items.Contains(obj)) return;

            var craft  = obj.craft;
            var recipe = RecipeRegistry.Instance?.Get(craft.recipeId);
            var state  = obj.location.accrual[actionIdx];
            var contributors = new List<int>(state.contributors);

            foreach (int slot in contributors)
                SettleStationCommitment(slot, obj.id, actionIdx, recipe, clockNow);

            // Output materialises AT the station for collection (§5.6.5 / c1 "collect at the station"),
            // not auto-delivered — so it is unambiguous who owns it and either crafter can pick it up.
            var outputs = CraftResolver.BuildOutputs(recipe, craft, clockNow);
            foreach (var outInst in outputs)
                PlaceItem(outInst.defId, outInst.quantity, obj.location.position.ToVector3());

            // Reset so the station is reusable — clear the craft + accrual, keep the station.
            obj.craft                       = null;
            obj.location.accrual[actionIdx] = new ActionAccrualState();
            ForcePush();
            Debug.Log($"[MapEntitySync] Station {obj.id} completed craft '{recipe?.displayName}' → {outputs.Count} stack(s).");
        }

        /// <summary>
        /// Host: every object-bound task the given dreamer is currently contributing to (0.2.10) —
        /// gathering and station crafting — so the owning client's Actions HUD can list the world
        /// actions the player triggered, alongside eating (action queue) and hand crafts. Returns
        /// (instanceId, defId, actionIndex, recipeId [0 = gather], threshold = labor-to-complete);
        /// progress is computed client-side from the synced accrual labor / threshold.
        /// </summary>
        public List<(int instanceId, int defId, int actionIndex, int recipeId, float threshold)>
            GetActiveTasksFor(int dreamerSlot)
        {
            var result = new List<(int, int, int, int, float)>();
            var layer  = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null) return result;

            foreach (var obj in layer.worldObjects.items)
            {
                if (obj.location.accrual == null) continue;
                var def = DefRegistry.Instance?.Get(obj.defId);
                if (def == null) continue;

                int len = Mathf.Min(obj.location.accrual.Length, def.actions.Length);
                for (int i = 0; i < len; i++)
                {
                    var st = obj.location.accrual[i];
                    if (st.isComplete || !st.contributors.Contains(dreamerSlot)) continue;

                    bool  isCraft   = obj.craft != null && obj.craft.isStation && obj.craft.stationActionIndex == i;
                    float threshold = isCraft ? obj.craft.requiredLabor : (def.actions[i]?.timeRequired ?? 0f);
                    result.Add((obj.id, obj.defId, i, isCraft ? obj.craft.recipeId : 0, threshold));
                }
            }
            return result;
        }

        // ── Client API ───────────────────────────────────────────────────────────

        public List<(int id, int defId, Vector3 pos, int actionCount, float[] labors, bool[] completes)>
            GetNearbyProcessables(Vector3 origin, float range)
        {
            var result = new List<(int, int, Vector3, int, float[], bool[])>();
            foreach (var e in _currentProcessables)
            {
                var pos = new Vector3(e.x, e.y, e.z);
                if (Vector3.Distance(origin, pos) <= range)
                    result.Add((e.id, e.defId, pos, e.actionCount, e.labors, e.completes));
            }
            return result;
        }

        /// <summary>
        /// Returns the current labor and completion state for one action on a world object.
        /// Called client-side by InteractableDetector to render progress.
        /// </summary>
        public bool TryGetProcessableActionState(int instanceId, int actionIndex,
                                                 out float labor, out bool complete)
        {
            labor    = 0f;
            complete = false;
            foreach (var e in _currentProcessables)
            {
                if (e.id != instanceId) continue;
                if (actionIndex < 0 || actionIndex >= e.actionCount) return false;
                labor    = e.labors[actionIndex];
                complete = e.completes[actionIndex];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Routes a world action dispatch (TDD §1.12, 0.2.9c3 / 0.2.9e3): instant actions
        /// (timeRequired == 0) execute by outcome; timed actions delegate to StartContributor.
        /// </summary>
        public void DispatchWorldAction(int instanceId, int actionIndex, int dreamerSlot, float committedMinutes = -1f)
        {
            if (!IsServer) return;
            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null) return;

            var obj = ResolveWorldObject(instanceId);   // materialises an authored node on first interaction (0.2.9f)
            if (obj == null)
            {
                Debug.LogWarning($"[MapEntitySync] DispatchWorldAction: instance {instanceId} not found.");
                return;
            }

            var def       = DefRegistry.Instance?.Get(obj.defId);
            var actionDef = def?.actions != null && actionIndex >= 0 && actionIndex < def.actions.Length
                                ? def.actions[actionIndex] : null;
            if (actionDef == null)
            {
                Debug.LogWarning($"[MapEntitySync] DispatchWorldAction: no action at index {actionIndex} on def {obj.defId}.");
                return;
            }

            if (actionDef.timeRequired <= 0f)
            {
                switch (actionDef.outcome)
                {
                    case ActionOutcome.ToInventory:
                        InstantPickup(obj, dreamerSlot);
                        break;
                    case ActionOutcome.Carry:
                        ToggleCarry(obj, dreamerSlot);
                        break;
                    default:
                        Debug.LogWarning($"[MapEntitySync] DispatchWorldAction: instant outcome {actionDef.outcome} not yet implemented.");
                        break;
                }
            }
            else
            {
                StartContributor(instanceId, actionIndex, dreamerSlot, committedMinutes);
            }
        }

        /// <summary>
        /// Instant pickup (ActionOutcome.ToInventory) — the single, common pickup for every item
        /// (TDD §1.12): the shared `pickup` ActionDef on every inventory-capable Def routes here.
        /// A container auto-equips into its slot; any other item flips InWorld → InContainer by
        /// delivering the SAME Instance to the dreamer's inventory. The Def is unchanged, so a
        /// re-dropped object stays interactable.
        /// </summary>
        private void InstantPickup(Instance obj, int dreamerSlot)
        {
            if (obj.location.accrual != null)
                foreach (var state in obj.location.accrual)
                    if (state.HasActiveContributors)
                    {
                        Debug.LogWarning($"[MapEntitySync] Instant pickup rejected: object {obj.id} has active contributors.");
                        return;
                    }

            var layer = RuntimeDataManager.Instance?.CurrentMapLayer;
            if (layer == null) return;
            var dis = DreamerInventorySync.GetForSlot(dreamerSlot);
            if (dis == null) return;

            float now      = RuntimeDataManager.Instance?.WorldState?.clock?.totalInGameMinutes ?? 0f;
            var   savedLoc = obj.location;
            var   so       = DefRegistry.Instance?.Get(obj.defId);

            layer.worldObjects.items.Remove(obj);

            bool delivered;
            if (so != null && so.isContainer)
            {
                // Container pickup = auto-equip into its slot (subsumes the old EquipContainerServerRpc).
                delivered = dis.TryEquipContainerFromWorld(obj, now);
            }
            else
            {
                obj.location = new InstanceLocation { kind = LocationKind.InContainer };
                delivered    = dis.TryDeliverItem(obj, now);
            }

            if (!delivered)
            {
                obj.location = savedLoc;
                layer.worldObjects.items.Add(obj);
                Debug.Log($"[MapEntitySync] Instant pickup failed: no room/slot for dreamer {dreamerSlot} (def={obj.defId}).");
                return;
            }

            ForcePush();
            Debug.Log($"[MapEntitySync] Instant pickup: object {obj.id} (def={obj.defId}) → dreamer {dreamerSlot}.");
        }

        /// <summary>
        /// Carry toggle (ActionOutcome.Carry): InWorld → CarriedBy(slot) → InWorld
        /// (TDD §1.12, 0.2.9e3). Accrual is preserved across the flip. A carried object
        /// follows the dreamer in the sync (ResolvePosition) and stays interactable; a second
        /// carry dispatch drops it at the dreamer's current position. No hand-attach visual yet
        /// (presentation deferred) — dormant until a Carry ActionDef is authored.
        /// </summary>
        private void ToggleCarry(Instance obj, int dreamerSlot)
        {
            if (obj.location.kind == LocationKind.CarriedBy && obj.location.dreamerSlot == dreamerSlot)
            {
                var pos = RuntimeDataManager.Instance?.GetDreamerPosition(dreamerSlot)
                          ?? obj.location.position.ToVector3();

                // The carried object's visual is at the dreamer, directly in the probe's path.
                _worldVisuals.TryGetValue(obj.id, out var carriedVisual);
                pos = ResolveGroundPosition(pos, obj.defId,
                          carriedVisual != null ? carriedVisual.transform : null);

                obj.location = new InstanceLocation
                {
                    kind     = LocationKind.InWorld,
                    position = pos.ToFloat3(),
                    accrual  = obj.location.accrual,
                };
                Debug.Log($"[MapEntitySync] Dropped carried object {obj.id} for dreamer {dreamerSlot}.");
            }
            else if (obj.location.kind == LocationKind.InWorld)
            {
                obj.location = new InstanceLocation
                {
                    kind        = LocationKind.CarriedBy,
                    dreamerSlot = dreamerSlot,
                    accrual     = obj.location.accrual,
                };
                Debug.Log($"[MapEntitySync] Dreamer {dreamerSlot} now carrying object {obj.id}.");
            }
            ForcePush();
        }

        // ── Equality guard ───────────────────────────────────────────────────────

        private static bool PayloadEquals(WorldObjectPayload a, WorldObjectPayload b)
        {
            int ca = a.Ids?.Length ?? 0;
            int cb = b.Ids?.Length ?? 0;
            if (ca != cb) return false;
            if (ca == 0) return true;

            for (int i = 0; i < ca; i++)
            {
                if (a.Ids[i]             != b.Ids[i]
                    || a.DefIds[i]       != b.DefIds[i]
                    || a.Quantities[i]   != b.Quantities[i]
                    || a.Xs[i]           != b.Xs[i]
                    || a.Ys[i]           != b.Ys[i]
                    || a.Zs[i]           != b.Zs[i]
                    || a.ActionCounts[i] != b.ActionCounts[i]
                    || a.Flags[i]        != b.Flags[i])
                    return false;
            }

            int la = a.Labors?.Length ?? 0;
            int lb = b.Labors?.Length ?? 0;
            if (la != lb) return false;
            for (int i = 0; i < la; i++)
                if (Math.Abs(a.Labors[i] - b.Labors[i]) > 0.001f || a.Completes[i] != b.Completes[i])
                    return false;
            return true;
        }
    }
}
