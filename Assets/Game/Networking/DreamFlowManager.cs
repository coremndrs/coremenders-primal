using System;
using System.Collections.Generic;
using Game.Persistence;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-authoritative orchestrator for the GDD §18 dream flow (TDD §0.1.7).
    ///
    /// Flow:
    ///   Stage 7 sets dreamer.isIncapacitated = true →
    ///   WorldClockDriver calls CheckForNewlyDown() →
    ///   DreamFlowManager enters RescueWindow phase (real-time countdown) →
    ///   Partner uses debug input / rescue action within window → RescueSucceeded() (no revert) -OR-
    ///   Window expires / no partner → BeginWakeUpVote() (0.1.7b) →
    ///   ConsensusVoteComponent reaches consensus → RevertToCheckpoint() →
    ///   ApplyNightmare() (0.1.7c) →
    ///   Back to None phase.
    ///
    /// Game Over path: BeginWakeUpVote() finds no valid checkpoints → GameOver phase.
    ///
    /// 0.1.7a — RescueWindow, rescue interaction, skip-block.
    /// 0.1.7b — Wake Up vote via ConsensusVoteComponent, revert.
    /// 0.1.7c — Nightmare escalation, Game Over.
    /// </summary>
    public class DreamFlowManager : NetworkBehaviour
    {
        public static DreamFlowManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // ── Inspector wiring ─────────────────────────────────────────────────────

        [SerializeField] private ConsensusVoteComponent _voteComponent;

        // ── Server-authoritative NetworkVariables ─────────────────────────────────

        // Current phase, readable by all clients for UI.
        private readonly NetworkVariable<int> _phase = new NetworkVariable<int>(
            (int)DreamFlowPhase.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Which dreamer slot is down (-1 if none).
        private readonly NetworkVariable<int> _downSlot = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Remaining real-time seconds in the rescue window.
        private readonly NetworkVariable<float> _rescueSecondsLeft = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Host-side tracking ────────────────────────────────────────────────────

        // Slots already entered into the flow; prevents double-notification on repeated ticks.
        private readonly HashSet<int>  _trackedDownSlots     = new HashSet<int>();

        // Host-local ordered checkpoint IDs for the current Wake Up vote.
        // Maps vote index → checkpoint ID; never crosses the wire.
        private readonly List<string>  _pendingCheckpointIds = new List<string>();

        // ── Public queries ────────────────────────────────────────────────────────

        public DreamFlowPhase Phase         => (DreamFlowPhase)_phase.Value;
        public int            DownSlot      => _downSlot.Value;
        public float          RescueSecondsLeft => _rescueSecondsLeft.Value;
        public bool           IsAnyDreamerDown  => _phase.Value != (int)DreamFlowPhase.None;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _voteComponent != null)
            {
                _voteComponent.OnConsensusReached -= OnVoteConsensus;
                _voteComponent.OnConsensusReached += OnVoteConsensus;
            }
        }

        public override void OnNetworkDespawn()
        {
            UiFocus.Release(this);
            if (_voteComponent != null)
                _voteComponent.OnConsensusReached -= OnVoteConsensus;
            base.OnNetworkDespawn();
        }

        // ── Tick hook (called by WorldClockDriver after each SimResolver.Step()) ──

        /// <summary>
        /// Idempotent. Checks if any dreamer just became incapacitated this tick
        /// and starts the dream flow for each newly-downed dreamer (one at a time —
        /// simultaneous double-down is not in scope for Cluster 1).
        /// </summary>
        public void CheckForNewlyDown(WorldState world)
        {
            if (!IsServer) return;
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer == null || !dreamer.isIncapacitated) continue;
                if (_trackedDownSlots.Contains(dreamer.slot)) continue;
                OnDreamerDown(dreamer.slot);
            }
        }

        // ── 0.1.7a: enter dream flow ─────────────────────────────────────────────

        private void OnDreamerDown(int slot)
        {
            _trackedDownSlots.Add(slot);

            var config = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();

            _downSlot.Value           = slot;
            _rescueSecondsLeft.Value  = config.rescueWindowSeconds;
            _phase.Value              = (int)DreamFlowPhase.RescueWindow;

            Debug.Log($"[DreamFlowManager] Dreamer {slot} is down. Rescue window: {config.rescueWindowSeconds}s.");
        }

        // ── Update: rescue window countdown ──────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;
            if (_phase.Value != (int)DreamFlowPhase.RescueWindow) return;

            _rescueSecondsLeft.Value -= Time.deltaTime;
            if (_rescueSecondsLeft.Value <= 0f)
                OnRescueWindowExpired();
        }

        // ── 0.1.7a: rescue interaction ───────────────────────────────────────────

        /// <summary>
        /// Called by the rescuing client (owner of the partner dreamer) to attempt a rescue.
        /// Server validates: rescue window is open, rescuer is alive and in range.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TryRescueRpc(RpcParams rpcParams = default)
        {
            if (_phase.Value != (int)DreamFlowPhase.RescueWindow) return;

            ulong callerId = rpcParams.Receive.SenderClientId;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(callerId, out int rescuerSlot)) return;

            int downedSlot = _downSlot.Value;
            if (rescuerSlot == downedSlot)
            {
                Debug.Log("[DreamFlowManager] Rescue rejected — rescuer is the downed dreamer.");
                return;
            }

            var rdm     = RuntimeDataManager.Instance;
            var rescuer = rdm.GetDreamer(rescuerSlot);
            if (rescuer == null || rescuer.isIncapacitated)
            {
                Debug.Log("[DreamFlowManager] Rescue rejected — rescuer is incapacitated.");
                return;
            }

            var config       = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();
            var rescuerPos   = rdm.GetDreamerPosition(rescuerSlot);
            var downedPos    = rdm.GetDreamerPosition(downedSlot);
            if (rescuerPos.HasValue && downedPos.HasValue)
            {
                float dist = Vector3.Distance(rescuerPos.Value, downedPos.Value);
                if (dist > config.rescueRangeUnits)
                {
                    Debug.Log($"[DreamFlowManager] Rescue rejected — rescuer too far ({dist:F1} > {config.rescueRangeUnits}).");
                    return;
                }
            }

            ExecuteRescue(downedSlot, config);
        }

        /// <summary>
        /// Debug-only server shortcut: skips range check, used when the host presses the
        /// debug rescue key or a dev button (a2 input is Manual).
        /// </summary>
        public void DebugForceRescue()
        {
            if (!IsServer || _phase.Value != (int)DreamFlowPhase.RescueWindow) return;
            var config = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();
            ExecuteRescue(_downSlot.Value, config);
        }

        private void ExecuteRescue(int downedSlot, BuffConfig config)
        {
            var dreamer = RuntimeDataManager.Instance.GetDreamer(downedSlot);
            if (dreamer == null) return;

            // Restore Vitality to a safe level and clear the killing afflictions.
            dreamer.vitality         = config.rescueVitalityRestore;
            dreamer.isIncapacitated  = false;
            // Clear afflictions that drove them down (they can still persist if severe,
            // but at least one tick cycle will follow before they can go down again).
            dreamer.afflictions.isStarving    = false;
            dreamer.afflictions.isDehydrated  = false;
            dreamer.afflictions.isHypothermic = false;

            // Force-push all sync components so clients see the recovery.
            foreach (var s in FindObjectsByType<DreamerNeedsSync>(FindObjectsSortMode.None))
                s.ForcePush();

            ExitDreamFlow();
            Debug.Log($"[DreamFlowManager] Rescue succeeded for dreamer {downedSlot}. No revert, no Nightmare.");
        }

        private void OnRescueWindowExpired()
        {
            Debug.Log($"[DreamFlowManager] Rescue window expired for dreamer {_downSlot.Value}. Beginning Wake Up vote.");
            BeginWakeUpVote();
        }

        // ── 0.1.7b: Wake Up vote ─────────────────────────────────────────────────

        private void BeginWakeUpVote()
        {
            int downedSlot = _downSlot.Value;
            var dreamer    = RuntimeDataManager.Instance.GetDreamer(downedSlot);
            if (dreamer == null) { ExitDreamFlow(); return; }

            // Collect the downed dreamer's valid checkpoints.
            var validIds = new List<string>(dreamer.ValidCheckpoints());

            if (validIds.Count == 0)
            {
                Debug.Log($"[DreamFlowManager] Dreamer {downedSlot} has no valid checkpoints — Game Over.");
                TriggerGameOver();
                return;
            }

            // Pause the world for the vote duration.
            FindFirstObjectByType<WorldClockSync>()?.SetPaused(true);
            _phase.Value = (int)DreamFlowPhase.WakeUpVote;

            // Cache the ordered ID list host-locally; the vote component only sees numeric display data.
            _pendingCheckpointIds.Clear();
            _pendingCheckpointIds.AddRange(validIds);

            int count       = Math.Min(validIds.Count, 4);
            var voteOptions = new VoteOptionData[count];
            for (int i = 0; i < count; i++)
            {
                float capturedAt = 0f;
                byte  coverage   = (byte)CheckpointCoverage.Unknown;
                var   meta       = SaveSystem.NamedCheckpointExists(validIds[i])
                    ? TryLoadMeta(validIds[i]) : null;
                if (meta != null)
                {
                    capturedAt = meta.capturedAtInGameMinutes;
                    coverage   = (byte)CheckpointMeta.ParseCoverage(meta.coverageLabel);
                }
                voteOptions[i] = new VoteOptionData { CapturedAtMinutes = capturedAt, Coverage = coverage };
            }

            if (_voteComponent != null)
                _voteComponent.Activate(voteOptions);
            else
                Debug.LogError("[DreamFlowManager] No ConsensusVoteComponent wired — Wake Up vote cannot proceed.");
        }

        private void OnVoteConsensus(int chosenIndex)
        {
            if (chosenIndex < 0 || chosenIndex >= _pendingCheckpointIds.Count)
            {
                Debug.LogError($"[DreamFlowManager] Vote consensus index {chosenIndex} out of range (have {_pendingCheckpointIds.Count}).");
                return;
            }
            string checkpointId = _pendingCheckpointIds[chosenIndex];
            Debug.Log($"[DreamFlowManager] Wake Up consensus: index {chosenIndex} → checkpoint '{checkpointId}'.");
            RevertToCheckpoint(checkpointId);
        }

        // ── Revert + nightmare (0.1.7b + 0.1.7c + 0.1.7d) ───────────────────────

        private void RevertToCheckpoint(string checkpointId)
        {
            int downedSlot = _downSlot.Value;

            // c3: discard checkpoints newer than the target before rehydrating so the
            // player cannot re-enter a future timeline branch after waking up.
            var  meta          = TryLoadMeta(checkpointId);
            float targetMinutes = meta?.capturedAtInGameMinutes ?? 0f;
            SaveSystem.DiscardCheckpointsNewerThan(targetMinutes);

            // Rehydrate from checkpoint (LoadGame path: Populate + position snap).
            GameFlowManager.Instance?.RevertToNamedCheckpoint(checkpointId);

            // d2/d3: apply nightmare scoped to this checkpoint, then re-save so subsequent
            // reverts to the same point see the baked nightmare and escalate correctly.
            var dreamer = RuntimeDataManager.Instance.GetDreamer(downedSlot);
            if (dreamer != null)
            {
                ApplyNightmare(dreamer, checkpointId);
                GameFlowManager.Instance?.ResaveNamedCheckpoint(checkpointId, meta?.coverageLabel ?? "Shared");
            }

            // Force-push all sync components so clients converge on the reverted + nightmare state.
            foreach (var s in FindObjectsByType<DreamerNeedsSync>(FindObjectsSortMode.None))   s.ForcePush();
            foreach (var s in FindObjectsByType<DreamerTaskSync>(FindObjectsSortMode.None))    s.ForcePush();
            foreach (var s in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))    s.ForcePush();
            FindFirstObjectByType<WorldClockSync>()?.ForcePush();

            // Resume the world clock (was paused for the vote).
            FindFirstObjectByType<WorldClockSync>()?.SetPaused(false);

            ExitDreamFlow();
        }

        // ── 0.1.7c: Nightmare escalation (refined in 0.1.7d d3) ─────────────────

        /// <summary>
        /// Applies (or escalates) a nightmare buff scoped to <paramref name="originCheckpointId"/>.
        /// Only the nightmare buff that originated from THIS checkpoint is replaced — nightmares
        /// from other checkpoints (carried over from prior reverts) are preserved (d3).
        /// The buff's checkpointId is set to originCheckpointId so the next revert to the same
        /// checkpoint can identify and replace it again (d1).
        /// </summary>
        private void ApplyNightmare(DreamerRecord dreamer, string originCheckpointId)
        {
            var config = FindFirstObjectByType<WorldClockDriver>()?.BuffConfig ?? new BuffConfig();

            // Derive escalation from the checkpoint-specific nightmare in the rehydrated state —
            // NOT from dreamer.nightmareTier (which is a global highest-tier field and would
            // wrongly skip to tier 2 on a first-ever revert to a new checkpoint).
            bool hadTier2 = dreamer.buffs?.Exists(b =>
                b.defId == "nightmare_tier2" && b.checkpointId == originCheckpointId) ?? false;
            bool hadTier1 = dreamer.buffs?.Exists(b =>
                b.defId == "nightmare_tier1" && b.checkpointId == originCheckpointId) ?? false;
            int  priorTier = hadTier2 ? 2 : (hadTier1 ? 1 : 0);
            int  newTier   = Math.Min(priorTier + 1, 2);

            // Replace this checkpoint's nightmare (preserve nightmares from other checkpoints).
            dreamer.buffs?.RemoveAll(b =>
                (b.defId == "nightmare_tier1" || b.defId == "nightmare_tier2") &&
                b.checkpointId == originCheckpointId);

            string defId    = newTier == 1 ? "nightmare_tier1" : "nightmare_tier2";
            float  duration = newTier == 1
                ? config.nightmareTier1DurationMinutes
                : config.nightmareTier2DurationMinutes;

            dreamer.buffs ??= new List<BuffInstance>();
            dreamer.buffs.Add(new BuffInstance
            {
                defId            = defId,
                remainingMinutes = duration,  // always a fresh full duration — timers never accumulate
                checkpointId     = originCheckpointId,
                coverage         = BuffCoverage.None,
            });

            // Keep the global field as the highest tier across all active nightmares.
            dreamer.nightmareTier = HighestActiveNightmareTier(dreamer.buffs);

            Debug.Log($"[DreamFlowManager] Nightmare tier {newTier} applied to dreamer {dreamer.slot} " +
                      $"(checkpoint '{originCheckpointId}' prior tier {priorTier} → {newTier}).");
        }

        private static int HighestActiveNightmareTier(List<BuffInstance> buffs)
        {
            if (buffs == null) return 0;
            int highest = 0;
            foreach (var b in buffs)
            {
                if      (b.defId == "nightmare_tier2" && highest < 2) highest = 2;
                else if (b.defId == "nightmare_tier1" && highest < 1) highest = 1;
            }
            return highest;
        }

        // ── 0.1.7c: Game Over ────────────────────────────────────────────────────

        private void TriggerGameOver()
        {
            _phase.Value = (int)DreamFlowPhase.GameOver;
            GameFlowManager.Instance?.LockSaveForGameOver();
            Debug.Log("[DreamFlowManager] Game Over — save locked.");
        }

        // ── Clean-up helper ───────────────────────────────────────────────────────

        private void ExitDreamFlow()
        {
            _trackedDownSlots.Clear();
            _pendingCheckpointIds.Clear();
            _downSlot.Value          = -1;
            _rescueSecondsLeft.Value = 0f;
            _phase.Value             = (int)DreamFlowPhase.None;

            _voteComponent?.Deactivate();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static CheckpointMeta TryLoadMeta(string id)
        {
            try { return SaveSystem.LoadCheckpointMeta(id); }
            catch { return null; }
        }

        // ── Debug OnGUI ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned) return;

            var phase = (DreamFlowPhase)_phase.Value;

            // The rescue overlay carries a clickable button; gameplay runs with the cursor locked
            // (FP build b2), so it has to claim the mouse while it is up.
            UiFocus.Set(this, phase == DreamFlowPhase.RescueWindow && IsServer);

            if (phase == DreamFlowPhase.None) return;

            var labelStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            switch (phase)
            {
                case DreamFlowPhase.RescueWindow:
                {
                    // Rescue window countdown overlay.
                    float w = 360f, h = 100f;
                    float x = Screen.width / 2f - w / 2f, y = 20f;
                    GUI.Box(new Rect(x, y, w, h), "");
                    GUI.Label(new Rect(x, y + 8f, w, 24f),
                        $"Dreamer {_downSlot.Value} is DOWN — rescue in {_rescueSecondsLeft.Value:F0}s",
                        labelStyle);

                    // Debug rescue button (host-only; real input wired manually).
                    if (IsServer)
                    {
                        if (GUI.Button(new Rect(x + w / 2f - 80f, y + 56f, 160f, 30f),
                                "DEBUG: Rescue", btnStyle))
                            DebugForceRescue();
                    }
                    break;
                }

                case DreamFlowPhase.GameOver:
                {
                    float w = 360f, h = 80f;
                    float x = Screen.width / 2f - w / 2f, y = Screen.height / 2f - h / 2f;
                    GUI.Box(new Rect(x, y, w, h), "");
                    GUI.Label(new Rect(x, y + 28f, w, 24f), "GAME OVER — No valid checkpoints.", labelStyle);
                    break;
                }
            }
        }
    }
}
