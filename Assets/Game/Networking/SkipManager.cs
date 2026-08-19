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

            _pendingInterrupt          = null;
            _scheduledInterruptAtStep  = debugInterruptAtStep;
            _skipProgress.Value        = 0f;
            _skipStopCause.Value       = (int)SkipStopCause.None;
            _skipStopDreamerSlot.Value = -1;
            _isSkipping.Value          = true;

            RuntimeDataManager.Instance?.WorldState?.clock?.Pause();
            StartCoroutine(RunSkipCoroutine(minutes));
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
                if (dreamer?.task == null || dreamer.task.type != TaskType.Sleeping) continue;

                float actualSlept = dreamer.task.elapsedMinutes;
                dreamer.task = new DreamerTask();

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

            GUI.color = savedColor;
        }
    }
}
