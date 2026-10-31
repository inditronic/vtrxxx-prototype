using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghostclub
{
    /// <summary>
    /// Rock-solid First-Person Player Controller supporting:
    /// - WASD Keyboard (both New Input System Keyboard.current AND legacy Input)
    /// - Mouse Look (Mouse.current AND legacy Input.GetAxis)
    /// - PS5 / DualSense Controller (Left Stick, Right Stick, Cross jump, R2 sprint, Haptics)
    /// - Smooth Head Bobbing & Gravity
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GhostclubPlayerController : MonoBehaviour
    {
        [Header("Movement Dynamics")]
        public float walkSpeed = 5.0f;
        public float sprintSpeed = 9.0f;
        public float jumpForce = 5.2f;
        public float gravity = -18.0f;

        [Header("Look Controls")]
        public float mouseSensitivity = 1.8f;
        public float controllerLookSensitivity = 120.0f;
        public float lookXLimit = 85.0f;
        public float stickDeadzone = 0.12f;
        public Transform cameraTransform;

        [Header("Head Bob Dynamics")]
        public bool enableHeadBob = true;
        public float bobSpeed = 10f;
        public float bobAmount = 0.045f;

        private CharacterController _characterController;
        private Vector3 _velocity = Vector3.zero;
        private float _rotationX = 0f;
        private bool _cursorLocked = true;
        private float _defaultCameraY = 1.65f;
        private float _bobTimer = 0f;
        private float _hapticTimer = 0f;
        private int _startupFrames = 0;

        void Start()
        {
            _characterController = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }

            if (cameraTransform != null)
            {
                _defaultCameraY = cameraTransform.localPosition.y;
            }

            SetCursorLock(true);
        }

        void Update()
        {
            HandlePauseToggle();
            HandleLook();
            HandleMovement();
            HandleHapticDecay();
        }

        void HandlePauseToggle()
        {
            bool toggleRequested = false;

            if (Input.GetKeyDown(KeyCode.Escape))
                toggleRequested = true;

            // Click into screen to lock cursor if unlocked
            if (!_cursorLocked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                SetCursorLock(true);
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var kbd = Keyboard.current;
            if (kbd != null && kbd.escapeKey.wasPressedThisFrame)
                toggleRequested = true;

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame)
                    toggleRequested = true;
            }
#endif

            if (toggleRequested)
            {
                SetCursorLock(!_cursorLocked);
            }
        }

        void HandleLook()
        {
            if (_startupFrames < 6)
            {
                _startupFrames++;
                return;
            }

            float lookX = 0f;
            float lookY = 0f;

            // 1. Mouse Look (only when cursor is locked)
            if (_cursorLocked)
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue() * 0.1f * mouseSensitivity;
                    lookX += Mathf.Clamp(delta.x, -25f, 25f);
                    lookY += Mathf.Clamp(delta.y, -25f, 25f);
                }
#endif
                // Legacy fallback if delta is 0
                if (Mathf.Approximately(lookX, 0f) && Mathf.Approximately(lookY, 0f))
                {
                    lookX += Mathf.Clamp(Input.GetAxis("Mouse X") * mouseSensitivity, -25f, 25f);
                    lookY += Mathf.Clamp(Input.GetAxis("Mouse Y") * mouseSensitivity, -25f, 25f);
                }
            }

            // 2. PS5 / Gamepad Right Stick Look
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.magnitude > stickDeadzone)
                {
                    float factor = Time.deltaTime * controllerLookSensitivity;
                    lookX += Mathf.Sign(stick.x) * Mathf.Pow(Mathf.Abs(stick.x), 1.4f) * factor;
                    lookY += Mathf.Sign(stick.y) * Mathf.Pow(Mathf.Abs(stick.y), 1.4f) * factor;
                }
            }
#endif

            // Apply Pitch (X) and Yaw (Y)
            _rotationX -= lookY;
            _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(_rotationX, 0, 0);
            }
            transform.Rotate(Vector3.up * lookX);
        }

        void HandleMovement()
        {
            bool isGrounded = _characterController.isGrounded;
            if (isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            float inputX = 0f;
            float inputZ = 0f;
            bool sprintHeld = false;
            bool jumpPressed = false;

            // 1. New Input System Keyboard Reading (Guaranteed to work regardless of input settings)
#if ENABLE_INPUT_SYSTEM
            var kbd = Keyboard.current;
            if (kbd != null)
            {
                if (kbd.wKey.isPressed || kbd.upArrowKey.isPressed) inputZ += 1f;
                if (kbd.sKey.isPressed || kbd.downArrowKey.isPressed) inputZ -= 1f;
                if (kbd.dKey.isPressed || kbd.rightArrowKey.isPressed) inputX += 1f;
                if (kbd.aKey.isPressed || kbd.leftArrowKey.isPressed) inputX -= 1f;

                if (kbd.leftShiftKey.isPressed || kbd.rightShiftKey.isPressed) sprintHeld = true;
                if (kbd.spaceKey.wasPressedThisFrame) jumpPressed = true;
            }
#endif

            // 2. Legacy Keyboard Fallback
            if (Mathf.Approximately(inputX, 0f) && Mathf.Approximately(inputZ, 0f))
            {
                inputX = Input.GetAxisRaw("Horizontal");
                inputZ = Input.GetAxisRaw("Vertical");
                if (Input.GetKey(KeyCode.LeftShift)) sprintHeld = true;
                if (Input.GetButtonDown("Jump")) jumpPressed = true;
            }

            // 3. PS5 Controller Left Stick & Buttons
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 lStick = pad.leftStick.ReadValue();
                if (lStick.magnitude > stickDeadzone)
                {
                    inputX = lStick.x;
                    inputZ = lStick.y;
                }

                // PS5 Sprint: R2 Trigger or L3 click
                if (pad.rightTrigger.ReadValue() > 0.25f || pad.leftStickButton.isPressed)
                {
                    sprintHeld = true;
                }

                // PS5 Jump: Cross (X) button / ButtonSouth
                if (pad.buttonSouth.wasPressedThisFrame)
                {
                    jumpPressed = true;
                }
            }
#endif

            float currentSpeed = sprintHeld ? sprintSpeed : walkSpeed;
            Vector3 moveDirection = (transform.forward * inputZ + transform.right * inputX);
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            _characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

            // Jump
            if (jumpPressed && isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                TriggerHaptics(0.12f, 0.45f, 0.25f);
            }

            // Gravity
            _velocity.y += gravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);

            // Head Bob
            if (enableHeadBob && cameraTransform != null)
            {
                if (isGrounded && moveDirection.magnitude > 0.1f)
                {
                    _bobTimer += Time.deltaTime * (sprintHeld ? bobSpeed * 1.35f : bobSpeed);
                    float newY = _defaultCameraY + Mathf.Sin(_bobTimer) * bobAmount;
                    cameraTransform.localPosition = new Vector3(
                        cameraTransform.localPosition.x,
                        newY,
                        cameraTransform.localPosition.z
                    );
                }
                else
                {
                    _bobTimer = 0f;
                    cameraTransform.localPosition = new Vector3(
                        cameraTransform.localPosition.x,
                        Mathf.Lerp(cameraTransform.localPosition.y, _defaultCameraY, Time.deltaTime * 6f),
                        cameraTransform.localPosition.z
                    );
                }
            }
        }

        public void TriggerHaptics(float durationSeconds, float lowFreq, float highFreq)
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                pad.SetMotorSpeeds(lowFreq, highFreq);
                _hapticTimer = durationSeconds;
            }
#endif
        }

        void HandleHapticDecay()
        {
#if ENABLE_INPUT_SYSTEM
            if (_hapticTimer > 0)
            {
                _hapticTimer -= Time.deltaTime;
                if (_hapticTimer <= 0)
                {
                    var pad = Gamepad.current;
                    if (pad != null) pad.SetMotorSpeeds(0f, 0f);
                }
            }
#endif
        }

        public void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null) pad.SetMotorSpeeds(0f, 0f);
#endif
        }
    }
}
