using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeFromWork.Gameplay
{
    /// First-person controller for M0: walk / sprint / crouch / jump, stairs and slopes via CharacterController.
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 4.2f;
        public float sprintSpeed = 7.0f;
        public float crouchSpeed = 1.8f;
        public float groundAcceleration = 30f;
        public float airAcceleration = 6f;

        [Header("Jump")]
        public float jumpHeight = 1.05f;
        public float gravity = -24f;
        public float coyoteTime = 0.12f;

        [Header("Crouch")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float standEye = 1.62f;
        public float crouchEye = 1.04f;
        public float crouchLerp = 12f;

        [Header("Look")]
        public float lookSensitivity = 0.09f;
        public float gamepadLookSpeed = 140f;
        public float maxPitch = 85f;

        public Transform cameraRig;

        CharacterController controller;
        PlayerInputActions input;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedAt = -99f;
        float eyeHeight;
        float pitch;
        bool cursorLocked;

        public float CurrentSpeed => new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

        /// CharacterController.isGrounded can miss contact on frames with a tiny delta; probe down as a fallback.
        public bool IsGrounded
        {
            get
            {
                if (controller.isGrounded) return true;
                Vector3 origin = transform.position + Vector3.up * 0.06f;
                return Physics.Raycast(origin, Vector3.down, out _, 0.18f, ~0, QueryTriggerInteraction.Ignore);
            }
        }
        public float EyeHeight => eyeHeight;
        public float Pitch => pitch;
        public int Ticks { get; private set; }
        public bool CursorLocked => cursorLocked;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
            eyeHeight = standEye;
            input = new PlayerInputActions();
            input.LoadOverrides();
        }

        void OnEnable()
        {
            input.Enable();
            LockCursor(true);
        }

        void OnDisable()
        {
            input.Disable();
        }

        void OnDestroy()
        {
            input.Dispose();
        }

        void Update()
        {
            Ticks++;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (!cursorLocked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
                return;
            }

            UpdateLook();
            UpdateCrouch();
            UpdateMove();
        }

        public void LockCursor(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void BindCamera(Transform rig)
        {
            cameraRig = rig;
            if (cameraRig == null) return;
            Vector3 p = cameraRig.localPosition;
            p.y = eyeHeight;
            cameraRig.localPosition = p;
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateLook()
        {
            Vector2 look = input.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0.0001f) return;

            bool fromMouse = input.Look.activeControl != null && input.Look.activeControl.device is Mouse;
            Vector2 delta = fromMouse ? look * lookSensitivity : look * gamepadLookSpeed * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f, Space.World);
            pitch = PlayerMath.ClampPitch(pitch - delta.y, maxPitch);
            if (cameraRig != null) cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateCrouch()
        {
            bool wantsCrouch = input.Crouch.IsPressed();
            bool crouched = controller.height < standHeight - 0.05f;
            bool stayCrouched = !wantsCrouch && !crouched && !CanStand();

            float targetHeight = (wantsCrouch || stayCrouched) ? crouchHeight : standHeight;
            float targetEye = targetHeight < standHeight - 0.05f ? crouchEye : standEye;

            controller.height = PlayerMath.Damp(controller.height, targetHeight, crouchLerp, Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            eyeHeight = PlayerMath.Damp(eyeHeight, targetEye, crouchLerp, Time.deltaTime);

            if (cameraRig != null)
            {
                Vector3 p = cameraRig.localPosition;
                p.y = eyeHeight;
                cameraRig.localPosition = p;
            }
        }

        bool CanStand()
        {
            Vector3 origin = transform.position + Vector3.up * (controller.height + 0.03f);
            float distance = Mathf.Max(0.02f, standHeight - controller.height - 0.06f);
            return !Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        void UpdateMove()
        {
            Vector2 move = input.Move.ReadValue<Vector2>();
            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool grounded = IsGrounded;
            bool crouched = controller.height < standHeight - 0.05f;
            bool sprinting = input.Sprint.IsPressed() && !crouched && move.y > 0.1f;
            float speed = crouched ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);
            float acceleration = grounded ? groundAcceleration : airAcceleration;

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish * speed, acceleration * Time.deltaTime);

            if (grounded) lastGroundedAt = Time.time;
            if (input.Jump.WasPressedThisFrame() && Time.time - lastGroundedAt <= coyoteTime)
            {
                verticalVelocity = PlayerMath.JumpVelocity(jumpHeight, gravity);
                lastGroundedAt = -99f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
