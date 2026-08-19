using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Per-dreamer component that resolves what the owner is looking at and presents that
    /// Interactable's world-context actions (TDD §1.12, 0.2.9c2; retargeted by the FP build Group E).
    ///
    /// e1: targeting is a <b>center-screen cast from the camera</b>, capped at
    ///     <see cref="_interactRange"/>. The old distance-based proximity query is gone — you interact
    ///     with what you are looking at, not with whatever happens to be nearest. What sits behind this
    ///     is unchanged: the same DispatchWorldActionServerRpc / StopWorldActionServerRpc on
    ///     DreamerTaskSync, driven by the same Def action list.
    /// e2: the cast is a SphereCast of <see cref="_aimAssistRadius"/> by default — forgiving enough
    ///     that small or low-lying pickups do not need pixel-accurate aim — and when several
    ///     Interactables are hit, the one <b>nearest to screen center</b> (smallest angle off the
    ///     camera's forward axis) wins, not the nearest in depth.
    /// e3: holding <see cref="_precisionKey"/> narrows the probe to <see cref="_precisionRadius"/>,
    ///     letting you single out one small item from a cluster. Releasing restores the wide sphere.
    ///
    /// The action list is a menu you <b>open</b> with <see cref="_actionMenuKey"/>: gameplay runs with
    /// the cursor locked, so the buttons are only clickable while the menu holds a
    /// <see cref="UiFocus"/> claim. While it is open the target is frozen, so the menu cannot change
    /// under the player's hand.
    ///
    /// Owner-only. The collider/layer mask and final production UI are wired manually.
    /// </summary>
    [RequireComponent(typeof(DreamerTaskSync))]
    public class InteractableDetector : NetworkBehaviour
    {
        [Header("Probe")]
        [SerializeField] private float     _interactRange = 3.5f;
        [SerializeField] private LayerMask _interactLayer = ~0;
        [Tooltip("Camera to cast from. Leave empty to use Camera.main — which is the owner's " +
                 "first-person camera once FirstPersonRig has claimed it.")]
        [SerializeField] private Camera    _overrideCamera;

        [Tooltip("Radius of the default center-screen SphereCast (e2). Wider = more forgiving aim on " +
                 "small or low-lying objects. 0 turns the default probe into a hairline ray.")]
        [SerializeField] private float _aimAssistRadius = 0.4f;

        [Tooltip("Radius used while the precision key is held (e3). Near-zero = a thin ray for picking " +
                 "one item out of a cluster.")]
        [SerializeField] private float   _precisionRadius = 0.02f;
        [SerializeField] private KeyCode _precisionKey    = KeyCode.LeftControl;

        [Header("Action menu")]
        [Tooltip("Opens/closes the targeted object's action menu. Opening frees the cursor and " +
                 "suspends mouselook; the target is frozen while the menu is open.")]
        [SerializeField] private KeyCode _actionMenuKey = KeyCode.E;

        [Header("Debug")]
        [Tooltip("Draw a small owner-only readout of what the interaction probe is resolving each " +
                 "frame (camera status / hit count / target). Turn off once interaction is confirmed.")]
        [SerializeField] private bool _showProbeDebug = true;

        private DreamerTaskSync _taskSync;
        private Interactable    _target;
        private Interactable    _menuTarget;      // the object the action menu was opened on
        private bool            _menuOpen;        // authoritative — survives _menuTarget being destroyed
        private string          _probeDebug = "(idle)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { /* no statics; hook satisfies the project pattern */ }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _taskSync = GetComponent<DreamerTaskSync>();
        }

        public override void OnNetworkDespawn()
        {
            CloseActionMenu();
            _target = null;
            base.OnNetworkDespawn();
        }

        private void OnDisable() => CloseActionMenu();

        private void Update()
        {
            if (!IsOwner) { _target = null; CloseActionMenu(); return; }

            // Menu open → freeze the target. The cursor is free and mouselook is suspended, so the
            // probe would only be noise; freezing also guarantees the buttons under the cursor stay
            // bound to the object the player opened them on.
            //
            // _menuOpen is tracked separately from _menuTarget because the target can be *destroyed*
            // while the menu is up (a node depleted by the other dreamer). A destroyed Interactable
            // compares equal to null, so keying off _menuTarget alone would silently skip this block
            // and strand the UiFocus claim — cursor free forever.
            if (_menuOpen)
            {
                if (_menuTarget == null || _menuTarget.def == null)
                {
                    CloseActionMenu();
                }
                else
                {
                    _target = _menuTarget;
                    if (Input.GetKeyDown(_actionMenuKey)) CloseActionMenu();
                    return;
                }
            }

            var cam = _overrideCamera != null ? _overrideCamera : Camera.main;
            if (cam == null)
            {
                _target     = null;
                _probeDebug = "no camera";
                return;
            }

            bool  precision = Input.GetKey(_precisionKey);
            float radius    = precision ? _precisionRadius : _aimAssistRadius;

            _target     = ProbeForInteractable(cam, radius, out string dbg);
            _probeDebug = precision ? $"[precision] {dbg}" : dbg;

            if (_target != null && Input.GetKeyDown(_actionMenuKey))
                OpenActionMenu(_target);
        }

        /// <summary>
        /// Center-screen cast against the interactable layer (e1/e2/e3).
        ///
        /// Uses ...All + filter rather than a single-hit cast so the probe is NOT blocked by the
        /// dreamer's own capsule or other non-interactable geometry in front of the target — with
        /// _interactLayer = ~0 the first raw hit is often self or ground, which resolves to no
        /// Interactable and would otherwise null the target.
        ///
        /// Disambiguation is <b>angular</b>, not by depth: among everything the sphere sweeps up, the
        /// winner is whichever Interactable sits closest to the crosshair. Depth-nearest would let a
        /// pebble at the edge of the sphere steal the target from the object dead center.
        /// </summary>
        private Interactable ProbeForInteractable(Camera cam, float radius, out string debug)
        {
            Vector3 origin  = cam.transform.position;
            Vector3 forward = cam.transform.forward;
            var     ray     = new Ray(origin, forward);

            RaycastHit[] hits = radius > 0f
                ? Physics.SphereCastAll(ray, radius, _interactRange, _interactLayer, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(ray, _interactRange, _interactLayer, QueryTriggerInteraction.Collide);

            Interactable best      = null;
            float        bestAngle = float.MaxValue;
            foreach (var h in hits)
            {
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it == null || it.def == null) continue;

                // A SphereCast that starts already overlapping a collider reports distance 0 and an
                // undefined point; fall back to the collider's center so it still gets an angle.
                Vector3 point = h.distance > 0f ? h.point : h.collider.bounds.center;
                float   angle = Vector3.Angle(forward, point - origin);
                if (angle < bestAngle) { bestAngle = angle; best = it; }
            }

            debug = $"r={radius:0.##} hits={hits.Length}, target=" +
                    (best != null ? $"{best.def.displayName} ({bestAngle:0.#}° off center)" : "none");
            return best;
        }

        // ── Action menu open/close (UiFocus claim) ──────────────────────────────

        private void OpenActionMenu(Interactable target)
        {
            _menuTarget = target;
            _menuOpen   = true;
            UiFocus.Set(this, true);
        }

        private void CloseActionMenu()
        {
            if (!_menuOpen) return;
            _menuTarget = null;
            _menuOpen   = false;
            UiFocus.Release(this);
        }

        private void OnGUI()
        {
            if (!IsOwner) return;

            bool menuOpen = _menuOpen && _menuTarget != null;

            // Crosshair — center-screen targeting needs a center to aim. Hidden while the cursor is
            // free, since the mouse pointer is then the aiming device.
            if (!UiFocus.AnyOpen)
                GUI.Label(new Rect(Screen.width * 0.5f - 4f, Screen.height * 0.5f - 10f, 12f, 20f), "+");

            if (_showProbeDebug)
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height - 174f, 400f, 20f),
                          $"[probe] {_probeDebug}");

            if (_target == null || _target.def == null) return;

            float x = Screen.width * 0.5f - 85f;
            float y = Screen.height - 150f;

            if (!menuOpen)
            {
                // Look-at prompt only — the buttons below need a cursor, so they wait for the menu.
                GUI.Label(new Rect(x, y, 300f, 20f),
                          $"[{_actionMenuKey}] {_target.def.displayName}");
                return;
            }

            GUI.Label(new Rect(x, y, 300f, 20f), $"── {_target.def.displayName} ──  [{_actionMenuKey}] close");
            y += 22f;

            var mapSync = MapEntitySync.Instance;
            foreach (var (idx, action) in _target.WorldActions())
            {
                // Progress label for timed actions
                string progressLabel = "";
                if (action.timeRequired > 0f && mapSync != null)
                {
                    if (mapSync.TryGetProcessableActionState(_target.instanceId, idx,
                            out float labor, out bool complete))
                    {
                        if (complete)
                            progressLabel = " [done]";
                        else
                            progressLabel = $" {Mathf.Clamp01(labor / action.timeRequired) * 100f:F0}%";
                    }
                }

                string label = action.DisplayLabel + progressLabel;
                bool   done  = progressLabel == " [done]";
                GUI.enabled = !done;

                float bw = action.timeRequired > 0f ? 100f : 160f;
                if (GUI.Button(new Rect(x, y, bw, 22f), label))
                    _taskSync?.DispatchWorldActionServerRpc(_target.instanceId, idx);

                if (!done && action.timeRequired > 0f)
                    if (GUI.Button(new Rect(x + 104f, y, 56f, 22f), "Stop"))
                        _taskSync?.StopWorldActionServerRpc(_target.instanceId, idx);

                GUI.enabled = true;
                y += 26f;
            }
        }

        /// <summary>The Interactable currently targeted by the owning dreamer's center-screen cast
        /// (null if none).</summary>
        public Interactable CurrentTarget => IsOwner ? _target : null;
    }
}
