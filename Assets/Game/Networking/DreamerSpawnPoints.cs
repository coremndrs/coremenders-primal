using System.Collections.Generic;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Scene-side resolver for authored dreamer spawn locations. One of these lives in the Action
    /// scene; the <see cref="DreamerSpawnPoint"/> markers scattered around the terrain are the data.
    ///
    /// <para><b>Why this exists.</b> Spawn position previously came only from
    /// <c>DreamerRecord.position</c> — i.e. from <c>Templates/dreamer_&lt;slot&gt;.json</c> for a new
    /// game, or from the save for a loaded one. Editing JSON to test a different part of the map is
    /// a bad loop. Now a fresh session spawns at an authored marker you can drag in the scene view,
    /// and the templates are only the fallback when no marker exists.</para>
    ///
    /// <para><b>What it does not do.</b> It does not own position — it only decides where a body is
    /// <i>placed at spawn</i>. The moment a dreamer exists, its position is authoritative state
    /// owned by the RDM and the save, exactly as before. <see cref="Policy.NewGameOnly"/> (the
    /// default) therefore keeps saved games landing where the player left them; the other policies
    /// exist for testing.</para>
    ///
    /// Presentation-free, simulation-free, never serialized: purely a spawn-time lookup.
    /// </summary>
    public sealed class DreamerSpawnPoints : MonoBehaviour
    {
        /// <summary>When authored markers win over the position carried in the DreamerRecord.</summary>
        public enum Policy
        {
            /// <summary>Only when the session started from the templates (New Game). Shipping default.</summary>
            NewGameOnly = 0,

            /// <summary>Every spawn, including one restored from a save. Testing only — a loaded
            /// game will not put the dreamers back where they were saved.</summary>
            Always = 1,

            /// <summary>Never — spawn straight from the DreamerRecord, the pre-marker behaviour.</summary>
            Disabled = 2,
        }

        public static DreamerSpawnPoints Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [Header("Policy")]
        [Tooltip("NewGameOnly: markers place the dreamers only on a fresh session (New Game). " +
                 "Always: markers win even when loading a save — testing only. " +
                 "Disabled: ignore markers entirely and spawn from the DreamerRecord.")]
        [SerializeField] private Policy _policy = Policy.NewGameOnly;

        [Tooltip("Id of the marker to use (DreamerSpawnPoint.Id, which defaults to its GameObject " +
                 "name). Leave blank to use the first marker found. This is the dial to turn when " +
                 "testing: author several markers, type one id here.")]
        [SerializeField] private string _activePointId = "";

        [Tooltip("Leave empty to auto-collect every DreamerSpawnPoint in the scene on Awake. " +
                 "Assign explicitly only to restrict the set.")]
        [SerializeField] private List<DreamerSpawnPoint> _points = new();

        [Header("Placement")]
        [Tooltip("Metres to offset the second dreamer sideways when both slots share one marker, " +
                 "so they do not spawn inside each other.")]
        [SerializeField] private float _slotSpacing = 1.5f;

        [Header("Ground snap")]
        [Tooltip("Drop the marker position onto whatever is below it. Keeps markers valid while the " +
                 "terrain is still being sculpted — height no longer has to be re-authored.")]
        [SerializeField] private bool _snapToGround = true;

        [Tooltip("Layers treated as ground. Dreamers are skipped regardless.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("Start the downward probe this far above the marker, so a marker left slightly " +
                 "under the terrain surface still resolves.")]
        [SerializeField] private float _probeAbove = 50f;

        [Tooltip("How far below the marker to look for ground before giving up.")]
        [SerializeField] private float _probeBelow = 200f;

        [Tooltip("Extra clearance above the ground hit, on top of the capsule's own skin width.")]
        [SerializeField] private float _groundClearance = 0.05f;

        private bool _warnedMissingId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[DreamerSpawnPoints] A second manager exists on '{name}'; " +
                                 "ignoring it. Keep exactly one per scene.");
                return;
            }
            Instance = this;

            _points.RemoveAll(p => p == null);
            if (_points.Count == 0)
                _points.AddRange(FindObjectsByType<DreamerSpawnPoint>(FindObjectsSortMode.None));

            // Scene order is not stable across loads; sort so "the first marker" means the same
            // marker every run.
            _points.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Query ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves where slot <paramref name="slot"/> should be placed. Returns false when the
        /// caller should fall back to <c>DreamerRecord.position</c> — no markers, or the policy
        /// says the record wins.
        /// </summary>
        /// <param name="slot">Dreamer slot (0 = host, 1 = joiner).</param>
        /// <param name="sessionFromTemplate">
        /// True when this session's dreamer positions came from the templates (New Game) rather
        /// than from a save. Drives <see cref="Policy.NewGameOnly"/>.
        /// </param>
        /// <param name="dreamerPrefab">
        /// Prefab about to be instantiated — its CharacterController supplies the capsule offset
        /// used by the ground snap. May be null.
        /// </param>
        public bool TryGetSpawnPose(int slot, bool sessionFromTemplate, GameObject dreamerPrefab,
            out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (_policy == Policy.Disabled) return false;
            if (_policy == Policy.NewGameOnly && !sessionFromTemplate) return false;

            var point = ResolvePoint(slot);
            if (point == null) return false;

            var t = point.transform;
            position = t.position;
            rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f); // yaw only — never tip the capsule

            // A shared marker serves both dreamers; step the second one sideways.
            if (point.Slot < 0 && slot > 0)
                position += t.right * (slot * _slotSpacing);

            if (_snapToGround) position = DropToGround(position, dreamerPrefab);
            return true;
        }

        private DreamerSpawnPoint ResolvePoint(int slot)
        {
            if (_points.Count == 0) return null;

            bool filtered = !string.IsNullOrWhiteSpace(_activePointId);
            if (filtered && !AnyMatchesActiveId())
            {
                if (!_warnedMissingId)
                {
                    _warnedMissingId = true;
                    Debug.LogWarning($"[DreamerSpawnPoints] No spawn point with id " +
                                     $"'{_activePointId}'; using the first available marker.");
                }
                filtered = false;
            }

            // A marker reserved for this slot wins over a shared one.
            DreamerSpawnPoint shared = null;
            foreach (var p in _points)
            {
                if (p == null) continue;
                if (filtered && p.Id != _activePointId) continue;
                if (p.Slot == slot) return p;
                if (p.Slot < 0 && shared == null) shared = p;
            }
            return shared;
        }

        private bool AnyMatchesActiveId()
        {
            foreach (var p in _points)
                if (p != null && p.Id == _activePointId) return true;
            return false;
        }

        // ── Ground snap ──────────────────────────────────────────────────────────

        /// <summary>
        /// Stands the capsule's feet on the surface at the marker, via the shared
        /// <see cref="GroundSnap"/> resolve. Returns the input unchanged when nothing is hit — an
        /// authored height over a hole is a deliberate enough choice to respect.
        /// </summary>
        private Vector3 DropToGround(Vector3 position, GameObject dreamerPrefab)
        {
            float feetOffset = 0f, skin = 0f;
            if (dreamerPrefab != null && dreamerPrefab.TryGetComponent<CharacterController>(out var cc))
            {
                skin       = cc.skinWidth;
                feetOffset = cc.center.y - cc.height * 0.5f; // local y of the capsule's base
            }

            return GroundSnap.TryResolve(position, _groundMask, _probeAbove, _probeBelow,
                       feetOffset, skin + _groundClearance, null, out var grounded)
                ? grounded
                : position;
        }
    }
}
