using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Client-authoritative movement for the owned dreamer (0.0.2, reworked for the FP build Group C).
    ///
    /// c1: CharacterController movement, <b>body-relative to facing</b> — input is projected onto this
    ///     transform's forward/right, which <see cref="FirstPersonLook"/> yaws. That is also what makes
    ///     free-look work for free (d2): free-look moves camera yaw only, so the body's heading — and
    ///     therefore the walk direction — is unchanged.
    /// c2: jump — an impulse into the same vertical velocity that gravity already integrates.
    /// c3: the movement-drain seam (0.2.3e) is untouched. The host samples this transform's
    ///     displacement through the RDM registry; it reads the result, not the driver.
    ///
    /// Enabled only for the owner by <see cref="FirstPersonRig"/> (the single ownership check); the
    /// Start() gate below is a fallback for a dreamer prefab that has no rig yet. The
    /// owner-authoritative NetworkTransform replicates the resulting motion to every other client.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DreamerMovementController : MonoBehaviour
    {
        [Header("Gait speeds (m/s)")]
        [Tooltip("Hold Left Alt. Must stay below MovementConfig.runSpeedThreshold so the host buckets " +
                 "it as a walk for movement drain.")]
        [SerializeField] private float _walkSpeed   = 2.5f;
        [SerializeField] private float _runSpeed    = 5f;
        [Tooltip("Hold Shift. Must stay above MovementConfig.sprintSpeedThreshold.")]
        [SerializeField] private float _sprintSpeed = 8f;

        [Header("Vertical")]
        [SerializeField] private float _gravity     = -20f;
        [Tooltip("Peak height of a jump in metres. Jump is free (no energy cost) for now — the host's " +
                 "movement drain measures horizontal displacement only.")]
        [SerializeField] private float _jumpHeight  = 1.1f;

        private CharacterController _cc;
        private NetworkObject       _netObj;
        private InputAction         _moveAction;
        private InputAction         _jumpAction;
        private float               _verticalVelocity;

        private void Awake()
        {
            _cc     = GetComponent<CharacterController>();
            _netObj = GetComponent<NetworkObject>();

            // Inline actions — no asset reference required.
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Up",    "<Keyboard>/upArrow")
                .With("Down",  "<Keyboard>/s")
                .With("Down",  "<Keyboard>/downArrow")
                .With("Left",  "<Keyboard>/a")
                .With("Left",  "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _jumpAction = new InputAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void Start()
        {
            // Fallback gate — FirstPersonRig is the authority, but a rig-less prefab must still not
            // let a non-owner drive the transform.
            if (_netObj != null && !_netObj.IsOwner)
            {
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
            _jumpAction?.Dispose();
        }

        private void Update()
        {
            var  input  = _moveAction.ReadValue<Vector2>();
            bool walk   = Keyboard.current?.leftAltKey.isPressed ?? false;
            bool sprint = Keyboard.current?.shiftKey.isPressed   ?? false;
            float speed = sprint ? _sprintSpeed : walk ? _walkSpeed : _runSpeed;

            // c1 — body-relative. transform.forward/right carry the yaw FirstPersonLook applied
            // earlier this frame (it runs at execution order -100).
            var planar = transform.right * input.x + transform.forward * input.y;
            if (planar.sqrMagnitude > 1f) planar.Normalize();
            var motion = planar * speed;

            // c2 — grounded check + jump impulse. v = sqrt(2 * g * h) for the requested peak height.
            if (_cc.isGrounded)
            {
                _verticalVelocity = -2f;        // small constant keeps the controller grounded
                if (_jumpAction.WasPressedThisFrame())
                    _verticalVelocity = Mathf.Sqrt(2f * _jumpHeight * -_gravity);
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            motion.y = _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }
    }
}
