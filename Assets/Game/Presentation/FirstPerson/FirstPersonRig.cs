using Unity.Netcode;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// FP build, Group A — the single ownership gate and the camera rig.
    ///
    /// a1: exactly one `IsOwner` check governs camera, look, motor and self-body culling. A remote
    ///     dreamer gets no camera and no input; it is a plain replicated body driven by the existing
    ///     owner-authoritative NetworkTransform.
    /// a2: <see cref="_headTarget"/> is a head-height transform on the dreamer marking where the eyes
    ///     sit. The camera is <b>not</b> parented to it.
    /// a3: in LateUpdate — after CharacterController.Move has resolved this frame — the camera
    ///     position-follows the head target and takes its rotation from <see cref="FirstPersonLook"/>.
    ///     Following in LateUpdate rather than parenting is the anti-stutter pattern: a parented camera
    ///     inherits the body transform mid-frame and jitters against the controller's own resolution.
    ///
    /// Runs last (execution order 100) so the gate is applied after the components it governs have
    /// started, and so the camera follow sees final positions.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(FirstPersonLook))]
    [RequireComponent(typeof(SelfBodyVisibility))]
    [RequireComponent(typeof(DreamerMovementController))]
    public class FirstPersonRig : NetworkBehaviour
    {
        [Header("Head")]
        [Tooltip("Head-height transform on this dreamer that the camera position-follows (a2). " +
                 "If unset, a point _fallbackHeadHeight above the dreamer's origin is used.")]
        [SerializeField] private Transform _headTarget;
        [SerializeField] private float     _fallbackHeadHeight = 1.65f;

        [Header("Camera")]
        [Tooltip("Optional camera prefab to instantiate for the owner (must be tagged MainCamera). " +
                 "If unset, the rig takes over the existing scene camera instead — which is how the " +
                 "static scene camera is retired.")]
        [SerializeField] private GameObject _cameraPrefab;

        private FirstPersonLook          _look;
        private DreamerMovementController _motor;
        private SelfBodyVisibility        _selfBody;

        private Camera     _camera;
        private GameObject _spawnedCameraGo;      // non-null only when we instantiated the camera
        private Transform  _reparentedCamera;     // scene camera we detached, so we can leave it sane
        private Transform  _reparentedCameraOwner;

        /// <summary>The owner's first-person camera (null on non-owners).</summary>
        public Camera OwnerCamera => _camera;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _look     = GetComponent<FirstPersonLook>();
            _motor    = GetComponent<DreamerMovementController>();
            _selfBody = GetComponent<SelfBodyVisibility>();

            // ── a1: the one ownership check ──────────────────────────────────────
            bool owner = IsOwner;

            if (_look  != null) _look.enabled  = owner;
            if (_motor != null) _motor.enabled = owner;
            _selfBody?.ApplyOwnerState(owner);

            if (!owner)
            {
                enabled = false;   // no camera, no LateUpdate, no input on a remote body
                return;
            }

            AcquireCamera();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseCamera();
            base.OnNetworkDespawn();
        }

        // ── a3: camera follow ───────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_camera == null || _look == null) return;

            _camera.transform.SetPositionAndRotation(HeadPosition, _look.CameraRotation);
        }

        private Vector3 HeadPosition =>
            _headTarget != null
                ? _headTarget.position
                : transform.position + Vector3.up * _fallbackHeadHeight;

        // ── Camera acquisition ──────────────────────────────────────────────────

        private void AcquireCamera()
        {
            if (_cameraPrefab != null)
            {
                // Resolved BEFORE instantiating: the new camera is tagged MainCamera too, and
                // Camera.main picks an arbitrary one of the matching cameras.
                var existing = Camera.main;

                // Spawned unparented at scene root — the follow in LateUpdate is what moves it.
                _spawnedCameraGo = Instantiate(_cameraPrefab, HeadPosition, Quaternion.identity);
                _camera          = _spawnedCameraGo.GetComponentInChildren<Camera>();

                // Retire whatever camera the scene was using, so we don't end up with two cameras
                // and two AudioListeners fighting.
                if (existing != null && _camera != null && existing != _camera)
                    existing.gameObject.SetActive(false);
            }
            else
            {
                // No prefab authored — take over the scene camera. Detaching it is what turns the
                // "static scene camera" into the FP camera (a1).
                _camera = Camera.main;
                if (_camera != null && _camera.transform.parent != null)
                {
                    _reparentedCamera      = _camera.transform;
                    _reparentedCameraOwner = _camera.transform.parent;
                    _camera.transform.SetParent(null, worldPositionStays: true);
                }
            }

            if (_camera == null)
            {
                Debug.LogError("[FirstPersonRig] No camera available — assign a camera prefab on the " +
                               "dreamer, or leave a MainCamera-tagged camera in the scene.");
                return;
            }

            if (_headTarget == null)
                Debug.LogWarning($"[FirstPersonRig] No head target assigned on dreamer '{name}' — " +
                                 $"falling back to {_fallbackHeadHeight}m above the origin.");

            // Snap once so the first frame does not lerp in from wherever the camera was parked.
            _camera.transform.SetPositionAndRotation(HeadPosition, _look.CameraRotation);
        }

        private void ReleaseCamera()
        {
            if (_spawnedCameraGo != null)
            {
                Destroy(_spawnedCameraGo);
                _spawnedCameraGo = null;
            }
            else if (_reparentedCamera != null && _reparentedCameraOwner != null)
            {
                _reparentedCamera.SetParent(_reparentedCameraOwner, worldPositionStays: true);
                _reparentedCamera      = null;
                _reparentedCameraOwner = null;
            }

            _camera = null;
        }
    }
}
