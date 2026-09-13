using System;
using System.Collections;
using System.Collections.Generic;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-side Skip engine (TDD §4.2, Builds 0.1.3–0.1.4).
    ///
    /// 0.1.3 — chunked Step() coroutine; clock pause / resume; bulk ForcePush on complete.
    /// 0.1.4 — guard check (stage 11 equiv.) per tick; interrupt channel; force-wake sleeping
    ///          dreamers on early stop; stop reason synced to clients via NetworkVariables.
    ///
    /// Architecture invariants:
    ///   - Clock is paused at skip start so WorldClockDriver does not double-step.
    ///   - NeedsConfig is read from WorldClockDriver (single source of truth for rates).
    ///   - On completion every sync component is force-pushed so clients converge immediately.
    ///   - ForceWakeSleepers fires only on early-stop (guard or interrupt), not on normal completion.
    /// </summary>
    public class SkipManager : NetworkBehaviour
    {
        [Tooltip("Milliseconds of Step() work to run per frame. 33 ms ≈ 30 fps spinner.")]
        [SerializeField] private float _msBudgetPerFrame = 33f;

        // ── Server-authoritative NetworkVariables ─────────────────────────────────

        private readonly NetworkVariable<bool> _isSkipping = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _skipProgress = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Stop reason (valid after an early stop; 0 / -1 = completed normally)
        private readonly NetworkVariable<int> _skipStopCause = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _skipStopDreamerSlot = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Monotonic counter bumped each time a skip ends with a reason.
        // Declared AFTER _skipStopCause so NGO deserialises the cause first on the client —
        // the OnValueChanged callback is guaranteed to see the correct cause when it fires.
        private readonly NetworkVariable<int> _skipEndEvent = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Host-side interrupt channel (0.1.4c) ──────────────────────────────────

        private SkipStopReason _pendingInterrupt;
        private int            _scheduledInterruptAtStep = -1; // set by StartSkip overload; -1 = none

        // ── Events (0.1.4b — consumed by 0.1.6 checkpoint logic) ─────────────────

        public event Action<SleepCutShortResult> OnSleepCutShort;

        // ── Public state ─────────────────────────────────────────────────────────

        public bool  IsSkipping   => _isSkipping.Value;
        public float SkipProgress => _skipProgress.Value;

        // ── Client-side reason display ────────────────────────────────────────────

        private float _reasonDisplaySecondsLeft;
        private int   _lastHandledSkipEvent;
        private const float ReasonDisplayDuration = 3f;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Initialise before subscribing so we ignore events that already happened
            // (e.g. late-joining client receiving a non-zero initial _skipEndEvent value).
            _lastHandledSkipEvent = _skipEndEvent.Value;
            _skipEndEvent.OnValueChanged -= OnSkipEndEventChanged;
            _skipEndEvent.OnValueChanged += OnSkipEndEventChanged;
        }

        public override void OnNetworkDespawn()
        {
            StopAllCoroutines();
            _skipEndEvent.OnValueChanged -= OnSkipEndEventChanged;
            base.OnNetworkDespawn();
        }

        private void OnSkipEndEventChanged(int prev, int current)
        {
            if (current <= _lastHandledSkipEvent) return;
            _lastHandledSkipEvent = current;

            // _skipStopCause is declared before _skipEndEvent, so NGO has already
            // deserialised it by the time this callback fires on the client.
            if ((SkipStopCause)_skipStopCause.Value != SkipStopCause.None)
                _reasonDisplaySecondsLeft = ReasonDisplayDuration;
        }

        // ── Update ───────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_reasonDisplaySecondsLeft > 0f)
                _reasonDisplaySecondsLeft -= Time.deltaTime;

            // Dev interrupt: press F5 while a skip is running to inject a debug interrupt (0.1.4c).
            if (IsServer && _isSkipping.Value && Input.GetKeyDown(KeyCode.F5))
                EnqueueInterrupt("Debug-F5");
        }

        // ── Skip entry point ─────────────────────────────────────────────────────

        /// <summary>
        /// Host-only. Runs the world forward by <paramref name="minutes"/> IG minutes.
        /// Pass <paramref name="debugInterruptAtStep"/> (1-based tick index) to schedule a
        /// debug interrupt at that tick — useful when the skip would be instant and F5 cannot
        /// be pressed in time. -1 (default) = no scheduled interrupt.
        /// </summary>
        public void StartSkip(int minutes, int debugInterruptAtStep = -1)
        {
            if (!IsServer || _isSkipping.Value || minutes <= 0) return;
            // 0.1.7a: skip cannot begin while any dreamer is in the dream flow (rescue window
            // or Wake Up vote). The 30% guard prevents in-skip incapacitation; this guard prevents
            // initiating a skip when a dreamer is already down in real time.
            if (DreamFlowManager.Instance?.IsAnyDreamerDown ?? false)
            {
                Debug.Log("[SkipManager] Skip blocked — a dreamer is currently down.");
                return;
            }

            ClearSkipRequest();          // a skip is starting; any pending request is now moot
            _pendingInterrupt          = null;
            _scheduledInterruptAtStep  = debugInterruptAtStep;
            _skipProgress.Value        = 0f;
            _skipStopCause.Value       = (int)SkipStopCause.None;
            _skipStopDreamerSlot.Value = -1;
            _isSkipping.Value          = true;

            RuntimeDataManager.Instance?.WorldState?.clock?.Pause();
            StartCoroutine(RunSkipCoroutine(minutes));
        }

        // ── Commitment skip request (§5.5.3, 0.2.11c5/c6) ────────────────────────
        //
        // The wait-or-skip decision, attached to a commitment. A committed dreamer asks to skip
        // their remaining committed minutes rather than sit through them at 1×.
        //
        //   Solo  — proceeds immediately; there is nobody to ask.
        //   Co-op — surfaces to the partner as a pending request. Accept starts the skip; decline
        //           clears it and leaves the commitment completely intact (c6). A refusal must
        //           never lock the committer in: after a decline they can still wait, or cancel
        //           and keep their progress and their unspent time. That is the whole reason the
        //           request is separate from the commitment rather than part of it.
        //
        // Deliberately NOT reusing ConsensusVoteComponent: that instance is owned by DreamFlowManager
        // for the Wake Up vote, and a skip request arriving mid-rescue would fight it for the same
        // NetworkVariables. §4.5's "the skip-start vote reuses this component" is logged in TODO.md.

        private readonly NetworkVariable<int> _skipRequestSlot = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _skipRequestMinutes = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Slot of the dreamer whose skip request is pending, or -1 when none is.</summary>
        public int SkipRequestSlot    => _skipRequestSlot.Value;
        public int SkipRequestMinutes => _skipRequestMinutes.Value;
        public bool HasPendingSkipRequest => _skipRequestSlot.Value >= 0;

        /// <summary>
        /// Owner asks to skip the remainder of their commitment (c5). The default length is what
        /// they still owe on it; an idle dreamer may pass an explicit length instead.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestCommitmentSkipServerRpc(int minutesOverride = -1, RpcParams rpcParams = default)
        {
            if (!IsServer || _isSkipping.Value) return;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(rpcParams.Receive.SenderClientId, out int slot)) return;

            var world   = RuntimeDataManager.Instance?.WorldState;
            var dreamer = RuntimeDataManager.Instance?.GetDreamer(slot);
            if (world?.clock == null || dreamer == null) return;

            int minutes = minutesOverride > 0 ? minutesOverride : DefaultSkipMinutesFor(dreamer, world.clock.totalInGameMinutes);
            if (minutes <= 0)
            {
                Debug.Log($"[SkipManager] Slot {slot} skip request ignored — nothing committed to skip.");
                return;
            }

            // Solo: nobody to ask. ConnectedClientsList is the same test the Wake Up vote uses, so
            // solo behaves consistently across both consent flows.
            if (NetworkManager.Singleton.ConnectedClientsList.Count <= 1)
            {
                Debug.Log($"[SkipManager] Slot {slot} skipping {minutes} min of their commitment (solo).");
                StartSkip(minutes);
                return;
            }

            _skipRequestSlot.Value    = slot;
            _skipRequestMinutes.Value = minutes;
            Debug.Log($"[SkipManager] Slot {slot} requested a {minutes} min skip — awaiting partner.");
        }

        /// <summary>
        /// How long a skip request defaults to (c5): the minutes the dreamer still owes on their
        /// active work, whichever way that work is measured.
        ///
        /// Object-bound labor has a committed window; a needs task (sleep, rest) and a consumable
        /// run to a duration instead. Both are "the rest of what I signed up for", and a player
        /// asking to skip their eight-hour sleep is the single most common case there is — so the
        /// duration-driven form is not a fallback here, it is half the point.
        /// </summary>
        private static int DefaultSkipMinutesFor(DreamerRecord dreamer, float now)
        {
            var active = dreamer?.ActiveAction();
            if (active == null || !active.started) return 0;

            float remaining = active.committedMinutes > 0f
                ? active.CommittedRemainingAt(now)
                : Mathf.Max(0f, active.duration - active.ElapsedAt(now));
            return Mathf.CeilToInt(remaining);
        }

        /// <summary>Partner answers a pending request. Accept starts the skip; decline clears it and
        /// changes nothing else (c6).</summary>
        [Rpc(SendTo.Server)]
        public void AnswerSkipRequestServerRpc(bool accept, RpcParams rpcParams = default)
        {
            if (!IsServer || !HasPendingSkipRequest) return;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(rpcParams.Receive.SenderClientId, out int slot)) return;
            if (slot == _skipRequestSlot.Value) return; // the requester cannot answer their own request

            int minutes = _skipRequestMinutes.Value;
            ClearSkipRequest();

            if (!accept)
            {
                // Non-punitive by construction: nothing about the commitment is touched here.
                Debug.Log($"[SkipManager] Slot {slot} declined the skip request — commitment left intact.");
                return;
            }
            Debug.Log($"[SkipManager] Slot {slot} accepted — skipping {minutes} min.");
            StartSkip(minutes);
        }

        /// <summary>The requester takes their own request back — they decided to wait it out, or to
        /// cancel the commitment instead. Same no-op-on-the-commitment guarantee as a decline.</summary>
        [Rpc(SendTo.Server)]
        public void WithdrawSkipRequestServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || !HasPendingSkipRequest) return;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(rpcParams.Receive.SenderClientId, out int slot)) return;
            if (slot != _skipRequestSlot.Value) return;
            ClearSkipRequest();
            Debug.Log($"[SkipManager] Slot {slot} withdrew their skip request.");
        }

        private void ClearSkipRequest()
        {
            if (!IsServer) return;
            _skipRequestSlot.Value    = -1;
            _skipRequestMinutes.Value = 0;
        }

        /// <summary>
        /// Whether a dreamer is in a state the skip flow would actually accept (§5.5.3, 0.2.11e2).
        /// Idle or committed = available; a tripped need guard = not, because the skip would halt on
        /// its first tick. The in-combat term is a seam — there is no combat system yet.
        /// </summary>
        public static bool CanDreamerSkip(DreamerRecord dreamer, NeedsConfig config)
        {
            if (dreamer?.needs == null) return false;
            if (dreamer.isIncapacitated) return false;
            if (dreamer.needs.hunger < config.GetHungerGuard() * 100f) return false;
            if (dreamer.needs.thirst < config.GetThirstGuard() * 100f) return false;
            if (dreamer.needs.warmth < config.GetWarmthGuard() * 100f) return false;
            return true;
        }

        // ── Interrupt channel (0.1.4c) ───────────────────────────────────────────

        /// <summary>
        /// Host-only. Enqueues a typed interrupt; the running skip halts on the next committed tick.
        /// Weather events, predators, and other future systems push to this channel at runtime.
        /// For now, the only producer is the dev F5 shortcut (c2).
        /// </summary>
        public void EnqueueInterrupt(string note = "Debug")
        {
            if (!IsServer || !_isSkipping.Value) return;
            _pendingInterrupt = new SkipStopReason
            {
                Cause       = SkipStopCause.Interrupt,
                DreamerSlot = -1,
                Note        = note,
            };
        }

        // ── Coroutine ────────────────────────────────────────────────────────────

        private IEnumerator RunSkipCoroutine(int totalSteps)
        {
            var world      = RuntimeDataManager.Instance.WorldState;
            var driver     = FindFirstObjectByType<WorldClockDriver>();
            var config     = driver?.NeedsConfig ?? new NeedsConfig();
            var buffConfig = driver?.BuffConfig;

            int completed = 0;
            SkipStopReason stopReason = null;
            bool stopped = false;

            var sleepEnded   = new List<SleepEndedResult>();
            var actionsEnded = new List<ActionEndedResult>();

            while (completed < totalSteps && !stopped)
            {
                float frameStart = Time.realtimeSinceStartup;

                while (completed < totalSteps && !stopped &&
                       (Time.realtimeSinceStartup - frameStart) * 1000f < _msBudgetPerFrame)
                {
                    sleepEnded.Clear();
                    actionsEnded.Clear();
                    SimResolver.Step(world, config, buffConfig, sleepEnded, actionsEnded);
                    completed++;
                    ConvertIdleMinuteToBracket(world, config);

                    // Process planned sleep completions for this tick immediately so
                    // the checkpoint captures the tick's state and subsequent ticks
                    // correctly tick down the new protection buff.
                    if (sleepEnded.Count > 0)
                        GameFlowManager.Instance?.HandleSleepWake(sleepEnded);

                    // Process action end-effects (e.g. save-consumable payout) at the tick they
                    // complete, capturing the world state at the exact right moment.
                    if (actionsEnded.Count > 0)
                        HandleActionsEnded(actionsEnded);

                    // Fire the scheduled debug interrupt if this is the designated tick.
                    if (_scheduledInterruptAtStep > 0 && completed >= _scheduledInterruptAtStep)
                    {
                        _pendingInterrupt ??= new SkipStopReason
                        {
                            Cause = SkipStopCause.Interrupt, DreamerSlot = -1,
                            Note  = $"Scheduled-Debug-@{completed}",
                        };
                        _scheduledInterruptAtStep = -1;
                    }

                    // Stage 11 equivalent: check guards then interrupt after each committed tick.
                    stopReason = _pendingInterrupt ?? SimResolver.CheckSkipGuards(world, config);
                    if (stopReason != null) stopped = true;
                }

                if (!stopped)
                {
                    _skipProgress.Value = (float)completed / totalSteps;
                    yield return null;
                }
            }

            _skipProgress.Value = 1f;
            OnSkipComplete(stopReason);
        }

        // ── Idle-skip conversion (§5.5.5, 0.2.11d3) ──────────────────────────────

        /// <summary>
        /// Moves one minute from the general pool into the trivial bracket for every dreamer who
        /// passed this skip tick <b>uncommitted</b>. Called once per committed tick, so the
        /// conversion is exact regardless of chunk size.
        ///
        /// The rule it enforces: skipping is not free time. If your partner skips four hours while
        /// you stand around, those hours were still yours — they come back as bracket minutes you
        /// can spend on maintenance, not as general labor you could spend on felling. That is what
        /// stops "skip to refill the day" from being the dominant strategy.
        ///
        /// Committed = holding an Active-class slot entry. Sleeping is Active, so sleep does not
        /// convert (d4 makes that explicit); a dreamer in ghost mode or simply standing idle does.
        /// </summary>
        private static void ConvertIdleMinuteToBracket(WorldState world, NeedsConfig config)
        {
            if (world?.dreamers == null) return;
            foreach (var dreamer in world.dreamers)
            {
                if (dreamer == null) continue;
                var active = dreamer.ActiveAction();
                if (active != null && active.started && active.actionClass == ActionClass.Active) continue;

                // Never convert pool the dreamer does not have — an overdrawn dreamer would
                // otherwise mint bracket minutes out of their own exhaustion.
                if (dreamer.timePool < 1f) continue;

                dreamer.timePool     -= 1f;
                dreamer.trivialBracket += 1f;
            }
        }

        // ── Action end-effects during skip ───────────────────────────────────────

        private static void HandleActionsEnded(List<ActionEndedResult> results)
        {
            foreach (var r in results)
            {
                if (r.EndEffect == ActionEndEffect.DreamSave)
                {
                    GameFlowManager.Instance?.UseDebugSaveConsumable(r.DreamerSlot);
                    Debug.Log($"[SkipManager] Action end-payout DreamSave for slot {r.DreamerSlot} during skip.");
                }
            }
        }

        // ── Force-wake sleeping dreamers (0.1.4b) ────────────────────────────────

        private void ForceWakeSleepers(WorldState world)
        {
            var cutShortResults = new List<SleepEndedResult>();

            foreach (var dreamer in world.dreamers)
            {
                // 0.2.11a3: sleep lives in the single action slot now. Elapsed is derived from the
                // clock rather than an accumulated counter, so a skip that advanced the clock in
                // chunks still reports the exact minutes slept.
                var active = dreamer?.ActiveAction();
                if (active == null || active.taskType != TaskType.Sleeping) continue;

                float actualSlept = active.ElapsedAt(world.clock.totalInGameMinutes);
                dreamer.RemoveAt(0);

                OnSleepCutShort?.Invoke(new SleepCutShortResult
                {
                    DreamerSlot        = dreamer.slot,
                    ActualSleptMinutes = actualSlept,
                    WasCutShort        = true,
                });

                cutShortResults.Add(new SleepEndedResult
                {
                    DreamerSlot  = dreamer.slot,
                    SleptMinutes = actualSlept,
                    WasCutShort  = true,
                });
            }

            // Feed cut-short results into the unified wake resolver (0.1.6).
            // Called before ForcePush in OnSkipComplete, so the new buff is included
            // in the bulk sync that follows.
            if (cutShortResults.Count > 0)
                GameFlowManager.Instance?.HandleSleepWake(cutShortResults);
        }

        // ── Completion ───────────────────────────────────────────────────────────

        private void OnSkipComplete(SkipStopReason reason)
        {
            var world = RuntimeDataManager.Instance?.WorldState;

            // Force-wake any sleeping dreamers only on early stop (guard or interrupt).
            if (reason != null && world != null)
                ForceWakeSleepers(world);

            RuntimeDataManager.Instance?.WorldState?.clock?.Resume();

            foreach (var s in FindObjectsByType<DreamerNeedsSync>(FindObjectsSortMode.None))
                s.ForcePush();
            foreach (var s in FindObjectsByType<DreamerTaskSync>(FindObjectsSortMode.None))
                s.ForcePush();
            foreach (var s in FindObjectsByType<DreamerBuffSync>(FindObjectsSortMode.None))
                s.ForcePush();
            foreach (var s in FindObjectsByType<DreamerInventorySync>(FindObjectsSortMode.None))
            { s.ForcePush(); s.ForcePushAction(); }
            FindFirstObjectByType<WorldClockSync>()?.ForcePush();

            var worldForCleanup = RuntimeDataManager.Instance?.WorldState;
            if (worldForCleanup != null) CheckpointCleaner.CleanOrphans(worldForCleanup);

            // 0.2.11b4: an INTERRUPT breaks presence — combat, damage, a predator — so every
            // contributor is released with progress retained at the interrupt's timestamp. A GUARD
            // stop is not an interrupt: c7 says a guard-capped skip leaves the commitment
            // mid-progress to continue at 1×, so the commitment survives and only the skip ends.
            if (reason != null && reason.Cause == SkipStopCause.Interrupt)
                MapEntitySync.Instance?.ReleaseAllPresenceOnInterrupt();

            if (reason != null)
            {
                _skipStopCause.Value       = (int)reason.Cause;
                _skipStopDreamerSlot.Value = reason.DreamerSlot;
                _skipEndEvent.Value++;  // triggers display on all peers; cause already set above
                Debug.Log($"[SkipManager] Skip stopped early: {reason.Cause} (dreamer {reason.DreamerSlot}). {reason.Note}");
            }
            else
            {
                Debug.Log("[SkipManager] Skip complete (full duration).");
            }

            _isSkipping.Value = false;
        }

        // ── Client overlay ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned) return;

            var savedColor = GUI.color;
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18,
                alignment = TextAnchor.MiddleCenter,
            };

            if (_isSkipping.Value)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(0, Screen.height / 2f - 20f, Screen.width, 40f),
                    $"Skipping… {_skipProgress.Value * 100f:F0}%",
                    labelStyle);
            }
            else if (_reasonDisplaySecondsLeft > 0f)
            {
                var cause = (SkipStopCause)_skipStopCause.Value;
                if (cause != SkipStopCause.None)
                {
                    GUI.color = Color.white;
                    int slot = _skipStopDreamerSlot.Value;
                    string msg = cause switch
                    {
                        SkipStopCause.GuardHunger => $"Skip stopped — Dreamer {slot} hunger low",
                        SkipStopCause.GuardThirst => $"Skip stopped — Dreamer {slot} thirst low",
                        SkipStopCause.GuardWarmth => $"Skip stopped — Dreamer {slot} warmth low",
                        SkipStopCause.Interrupt   => "Skip interrupted",
                        _                         => "Skip stopped",
                    };
                    GUI.Label(
                        new Rect(0, Screen.height / 2f - 20f, Screen.width, 40f),
                        msg,
                        labelStyle);
                }
            }

            DrawSkipRequestPanel(labelStyle);

            GUI.color = savedColor;
        }

        /// <summary>
        /// The wait-or-skip panel (c5/c6). The requester sees "waiting"; the partner sees Accept /
        /// Decline. Both stay non-modal — the panel claims the cursor so its buttons are clickable,
        /// but nothing about the commitment is suspended while it is up (§5.5.3, b5).
        /// </summary>
        private void DrawSkipRequestPanel(GUIStyle labelStyle)
        {
            bool pending = HasPendingSkipRequest && !_isSkipping.Value;
            UiFocus.Set(this, pending);
            if (!pending) return;

            int  requester  = _skipRequestSlot.Value;
            // NOT RuntimeDataManager.GetOwner: the ownership map is host-side, so on a client it
            // reports "no owner" for every slot and the requester would be shown their partner's
            // Accept/Decline buttons — which the server then rejects, leaving them stuck.
            bool isMine     = DreamerNetworkAdapter.LocalSlot == requester;
            float hours     = _skipRequestMinutes.Value / 60f;

            float w = 420f, h = 108f;
            float x = Screen.width / 2f - w / 2f;
            float y = Screen.height * 0.28f;

            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x, y + 10f, w, 24f),
                $"Dreamer {requester} wants to skip {hours:F1}h", labelStyle);

            if (isMine)
            {
                GUI.Label(new Rect(x, y + 42f, w, 22f), "Waiting for your partner…", labelStyle);
                if (GUI.Button(new Rect(x + w / 2f - 70f, y + 70f, 140f, 26f), "Withdraw"))
                    WithdrawSkipRequestServerRpc();
                return;
            }

            if (GUI.Button(new Rect(x + 30f, y + 66f, 170f, 30f), "Skip together"))
                AnswerSkipRequestServerRpc(true);
            if (GUI.Button(new Rect(x + 220f, y + 66f, 170f, 30f), "Not now"))
                AnswerSkipRequestServerRpc(false);
        }
    }
}
