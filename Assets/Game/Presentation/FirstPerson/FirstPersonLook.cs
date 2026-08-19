using Game.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// FP build, Groups B and D — mouselook, cursor mode, and free-look for the owned dreamer.
    ///
    /// b1: yaw rotates the <b>body</b> (this transform); pitch is held here and applied to the camera
    ///     by <see cref="FirstPersonRig"/> — the camera is never parented, so it owns no rotation state.
    /// b2: cursor is locked/hidden and look is live during gameplay; any <see cref="UiFocus"/> claim
    ///     (inventory, pause menu, action menu, dream-flow panels) frees the cursor and suspends look.
    ///     Tab toggles a manual free-cursor claim for the always-on dev HUD.
    /// d1: middle-mouse toggles free-look — mouse X drives a camera-only yaw offset clamped to ±90°,
    ///     the body keeps its heading (so movement, which is body-relative, is unaffected — d2).
    /// d3: toggling off eases the offset back to body-forward.
    ///
    /// Owner-gated by <see cref="FirstPersonRig"/> (the single ownership check). Runs before the motor
    /// so movement uses this frame's heading.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class FirstPersonLook : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("Degrees of rotation per unit of mouse delta. Mouse delta is already frame-relative, " +
                 "so this is deliberately NOT scaled by deltaTime.")]
        [SerializeField] private float _sensitivity = 0.12f;
        [SerializeField] private bool  _invertY;

        [Header("Clamps")]
        [Tooltip("Pitch limit above/below the horizon, in degrees.")]
        [SerializeField] private float _pitchClamp = 85f;
        [Tooltip("Free-look yaw limit either side of the body's heading — a 180° arc in total (d1).")]
        [SerializeField] private float _freeLookClamp = 90f;
        [Tooltip("Seconds to ease the camera back to body-forward when free-look is toggled off (d3). " +
                 "0 = snap.")]
        [SerializeField] private float _freeLookReturnSeconds = 0.12f;

        [Header("Cursor")]
        [Tooltip("Toggles a manual free-cursor claim so the dev HUD stays clickable. Remove once the " +
                 "dev HUD is replaced by real UI.")]
        [SerializeField] private Key _freeCursorKey = Key.Tab;

        private float _bodyYaw;         // authoritative heading — written to transform.rotation
        private float _pitch;           // camera-only
        private float _freeYawOffset;   // camera-only, non-zero during (and briefly after) free-look
        private bool  _freeLook;
        private float _returnVelocity;  // SmoothDamp state for the d3 ease-back

        /// <summary>World rotation the camera should adopt this frame (applied by FirstPersonRig).</summary>
        public Quaternion CameraRotation => Quaternion.Euler(_pitch, _bodyYaw + _freeYawOffset, 0f);

        /// <summary>True while free-look is engaged (d1) — the camera is off the body's heading.</summary>
        public bool IsFreeLooking => _freeLook;

        private void OnEnable()
        {
            // Adopt the body's authored heading rather than snapping it to zero on spawn/respawn.
            _bodyYaw       = transform.eulerAngles.y;
            _pitch         = 0f;
            _freeYawOffset = 0f;
            _freeLook      = false;
        }

        private void OnDisable()
        {
            _devCursorClaim = false;
            UiFocus.Release(this);

            // Only hand the cursor back if this component was the one holding it. A remote dreamer's
            // look component is disabled by the ownership gate the moment it spawns; without this
            // guard that disable would unlock the cursor the local owner's rig had just locked.
            if (_heldCursor)
            {
                _heldCursor = false;
                ReleaseCursor();
            }
        }

        private void Update()
        {
            bool uiOpen = HandleCursorMode();

            // b2 — look is suspended, not merely ignored: no delta is accumulated while a panel is up,
            // so the view does not jump when the panel closes.
            if (uiOpen)
            {
                // A menu opened mid-free-look keeps the free-look view; only a released free-look
                // is easing back, and that ease should still finish while the menu is up.
                if (!_freeLook) EaseFreeLookHome();
                return;
            }

            HandleFreeLookToggle();

            Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            float   yawDelta   = delta.x * _sensitivity;
            float   pitchDelta = delta.y * _sensitivity * (_invertY ? 1f : -1f);

            _pitch = Mathf.Clamp(_pitch + pitchDelta, -_pitchClamp, _pitchClamp);

            if (_freeLook)
            {
                // d1/d2 — camera yaw only. The body keeps its heading, so WASD still walks the
                // original course.
                _freeYawOffset = Mathf.Clamp(_freeYawOffset + yawDelta, -_freeLookClamp, _freeLookClamp);
            }
            else
            {
                _bodyYaw += yawDelta;
                EaseFreeLookHome();
            }

            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }

        /// <summary>
        /// Applies b2's cursor split and returns true when a UI panel owns the mouse.
        /// This is the single writer of <see cref="Cursor"/> state in the project.
        /// </summary>
        private bool HandleCursorMode()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[_freeCursorKey].wasPressedThisFrame)
            {
                _devCursorClaim = !_devCursorClaim;
                UiFocus.Set(this, _devCursorClaim);
            }

            bool uiOpen = UiFocus.AnyOpen;

            var wanted = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != wanted) Cursor.lockState = wanted;
            if (Cursor.visible   != uiOpen) Cursor.visible   = uiOpen;
            _heldCursor = true;

            return uiOpen;
        }

        // Our own Tab claim, tracked separately from UiFocus.AnyOpen so Tab keeps toggling
        // predictably while another panel also holds the cursor.
        private bool _devCursorClaim;
        private bool _heldCursor;

        private void HandleFreeLookToggle()
        {
            if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
            {
                _freeLook = !_freeLook;
                if (!_freeLook) _returnVelocity = 0f;   // start the ease-back clean
            }
        }

        /// <summary>d3 — return camera yaw to body-forward once free-look is released.</summary>
        private void EaseFreeLookHome()
        {
            if (Mathf.Approximately(_freeYawOffset, 0f)) { _freeYawOffset = 0f; return; }

            if (_freeLookReturnSeconds <= 0f)
            {
                _freeYawOffset = 0f;
                return;
            }

            _freeYawOffset = Mathf.SmoothDamp(
                _freeYawOffset, 0f, ref _returnVelocity, _freeLookReturnSeconds);

            if (Mathf.Abs(_freeYawOffset) < 0.05f) _freeYawOffset = 0f;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
}
