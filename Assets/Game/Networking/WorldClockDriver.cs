using System.Collections.Generic;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-only tick driver — the engine boundary between Unity's Time.deltaTime and the
    /// pure-C# SimResolver (TDD §4.1, §E2).
    ///
    /// Accumulates real seconds and fires SimResolver.Step() once per IG minute (6 real
    /// seconds by default). Never calls clock.Advance directly — Step() owns clock mutation.
    ///
    /// tick == jump holds by construction: real-time and Skip both call the same Step().
    /// After each tick, CheckpointCleaner removes any checkpoint files orphaned by expired
    /// protection buffs (0.1.5b).
    ///
    /// 0.2.3e: Computes per-dreamer distance moved since last tick and passes it to Step
    /// for movement-pool drain. Distance is zeroed after the first tick in each Update frame
    /// so multi-tick frames don't double-count the same displacement.
    /// </summary>
    public class WorldClockDriver : MonoBehaviour
    {
        [SerializeField] private NeedsConfig    _needsConfig    = new NeedsConfig();
        [SerializeField] private BuffConfig     _buffConfig     = new BuffConfig();
        [Tooltip("Movement drain tuning. Both rates default to 0 (inert) until set in Inspector.")]
        [SerializeField] private MovementConfig _movementConfig = new MovementConfig();
        [Tooltip("Bucket thresholds for item spoilage display (0.2.4c).")]
        [SerializeField] private SpoilageConfig _spoilageConfig = new SpoilageConfig();

        /// <summary>Exposes configs so SkipManager and other systems can use the same values without duplication.</summary>
        public NeedsConfig    NeedsConfig    => _needsConfig;
        public BuffConfig     BuffConfig     => _buffConfig;
        public SpoilageConfig SpoilageConfig => _spoilageConfig;

        private float _pendingRealSeconds;

        // Reused per-tick output lists; avoid per-step allocation.
        private readonly List<SleepEndedResult>  _sleepEnded   = new List<SleepEndedResult>();
        private readonly List<ActionEndedResult> _actionsEnded = new List<ActionEndedResult>();

        // Per-dreamer position cache for movement drain (indexed by slot 0/1).
        private readonly Vector3?[] _lastPositions       = new Vector3?[2];
        private readonly float[]    _walkDistanceMoved   = new float[2];
        private readonly float[]    _runDistanceMoved    = new float[2];
        private readonly float[]    _sprintDistanceMoved = new float[2];

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            var world = RuntimeDataManager.Instance?.WorldState;
            if (world?.clock == null || world.clock.isPaused) return;

            // Accumulate per-dreamer movement since the last frame, bucketed by gait.
            // Instantaneous speed (dist / deltaTime) determines walk vs sprint.
            // Both arrays are zeroed inside the tick loop after each Step() consumes them.
            if (world.dreamers != null)
            {
                foreach (var dreamer in world.dreamers)
                {
                    if (dreamer == null) continue;
                    int slot = dreamer.slot;
                    if (slot < 0 || slot >= _walkDistanceMoved.Length) continue;
                    var curPos = RuntimeDataManager.Instance.GetDreamerPosition(slot);
                    if (curPos.HasValue && _lastPositions[slot].HasValue)
                    {
                        // Horizontal displacement only. Jumping and falling are vertical and must not
                        // register as travel — "jump is free (no energy cost)" (FP build c2/c3) — and
                        // gait bucketing on a slope should reflect ground speed, not slope speed.
                        var delta   = curPos.Value - _lastPositions[slot].Value;
                        delta.y     = 0f;
                        float dist  = delta.magnitude;
                        float speed = Time.deltaTime > 0f ? dist / Time.deltaTime : 0f;
                        if (speed >= _movementConfig.sprintSpeedThreshold)
                            _sprintDistanceMoved[slot] += dist;
                        else if (speed >= _movementConfig.runSpeedThreshold)
                            _runDistanceMoved[slot] += dist;
                        else
                            _walkDistanceMoved[slot] += dist;
                    }
                    _lastPositions[slot] = curPos;
                }
            }

            _pendingRealSeconds += Time.deltaTime;

            float secondsPerMinute = world.clock.realSecondsPerInGameMinute;
            while (_pendingRealSeconds >= secondsPerMinute)
            {
                _pendingRealSeconds -= secondsPerMinute;
                _sleepEnded.Clear();
                _actionsEnded.Clear();

                SimResolver.Step(world, _needsConfig, _buffConfig,
                    _sleepEnded, _actionsEnded,
                    _walkDistanceMoved, _runDistanceMoved, _sprintDistanceMoved, _movementConfig);

                // Zero all three buckets after the first tick — same movement must not count twice
                _walkDistanceMoved[0]   = 0f; _walkDistanceMoved[1]   = 0f;
                _runDistanceMoved[0]    = 0f; _runDistanceMoved[1]    = 0f;
                _sprintDistanceMoved[0] = 0f; _sprintDistanceMoved[1] = 0f;

                if (_sleepEnded.Count > 0)
                    GameFlowManager.Instance?.HandleSleepWake(_sleepEnded);

                if (_actionsEnded.Count > 0)
                    HandleActionsEnded(_actionsEnded);

                // 0.1.7a: notify DreamFlowManager of any dreamer that just became incapacitated.
                DreamFlowManager.Instance?.CheckForNewlyDown(world);
                CheckpointCleaner.CleanOrphans(world);
            }
        }

        private void HandleActionsEnded(List<ActionEndedResult> results)
        {
            foreach (var r in results)
            {
                switch (r.EndEffect)
                {
                    case ActionEndEffect.DreamSave:
                        GameFlowManager.Instance?.UseDebugSaveConsumable(r.DreamerSlot);
                        Debug.Log($"[WorldClockDriver] Action end-payout DreamSave for slot {r.DreamerSlot}.");
                        break;
                }
            }
        }
    }
}
