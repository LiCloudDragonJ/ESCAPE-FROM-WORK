using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeFromWork.Gameplay
{
    /// First-person controller: walk / sprint / crouch / jump, stairs and slopes via CharacterController.
    /// Feel pass: separate ground accel/decel, jump buffering, sprint FOV kick, head bob and landing dip.
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 4.4f;
        public float sprintSpeed = 7.6f;
        public float crouchSpeed = 1.9f;
        public float groundAcceleration = 40f;
        public float groundDeceleration = 48f;
        public float airAcceleration = 8f;
        public float airDrag = 2f;

        [Header("Jump")]
        public float jumpHeight = 1.0f;
        public float gravity = -26f;
        public float coyoteTime = 0.12f;
        public float jumpBuffer = 0.12f;

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

        [Header("Camera feel")]
        public bool bobEnabled = true;
        public float sprintFovKick = 7f;
        public float fovLerp = 7f;
        public float bobAmplitudeWalk = 0.026f;
        public float bobAmplitudeSprint = 0.04f;
        public float bobStride = 1.7f;
        public float bobLateral = 0.35f;
        public float landDipScale = 0.014f;
        public float landDipMax = 0.14f;
        public float landDipRecover = 9f;
        public float recoilRecover = 16f;

        public Transform cameraRig;

        CharacterController controller;
        PlayerInputActions input;
        Camera viewCamera;
        float baseFov = 78f;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedAt = -99f;
        float lastJumpPressedAt = -99f;
        float eyeHeight;
        float pitch;
        float bobPhase;
        float landDip;
        float recoilPitch;
        float recoilYaw;
        bool wasGrounded;
        bool cursorLocked;

        public float CurrentSpeed => new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
        public float FallSpeed => verticalVelocity;
        public bool IsSprinting { get; private set; }
        public bool IsCrouched => controller.height < standHeight - 0.05f;

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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
                if (keyboard.f1Key.wasPressedThisFrame) bobEnabled = !bobEnabled;
            }
            if (!cursorLocked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
                return;
            }

            UpdateLook();
            UpdateCrouch();
            UpdateMove();
            UpdateCamera();
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
            viewCamera = cameraRig.GetComponent<Camera>();
            if (viewCamera != null) baseFov = viewCamera.fieldOfView;
            Vector3 p = cameraRig.localPosition;
            p.y = eyeHeight;
            cameraRig.localPosition = p;
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// Drops the player onto a spawn point (used when the floor is regenerated).
        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            landDip = 0f;
            bobPhase = 0f;
            pitch = 0f;
            recoilPitch = 0f;
            recoilYaw = 0f;
            if (cameraRig != null) cameraRig.localRotation = Quaternion.identity;
        }

        /// Adds a camera kick; the offset decays back to zero on its own.
        public void AddRecoil(float pitchKick, float yawKick)
        {
            recoilPitch = Mathf.Clamp(recoilPitch - pitchKick, -25f, 25f);
            recoilYaw = Mathf.Clamp(recoilYaw + yawKick, -12f, 12f);
        }

        void ApplyCameraRotation()
        {
            if (cameraRig == null) return;
            cameraRig.localRotation = Quaternion.Euler(pitch + recoilPitch, recoilYaw, 0f);
        }

        void UpdateLook()
        {
            Vector2 look = input.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0.0001f) return;

            bool fromMouse = input.Look.activeControl != null && input.Look.activeControl.device is Mouse;
            Vector2 delta = fromMouse ? look * lookSensitivity : look * gamepadLookSpeed * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f, Space.World);
            pitch = PlayerMath.ClampPitch(pitch - delta.y, maxPitch);
            ApplyCameraRotation();
        }

        void UpdateCrouch()
        {
            bool wantsCrouch = input.Crouch.IsPressed();
            bool stayCrouched = !wantsCrouch && IsCrouched && !CanStand();

            float targetHeight = (wantsCrouch || stayCrouched) ? crouchHeight : standHeight;
            float targetEye = targetHeight < standHeight - 0.05f ? crouchEye : standEye;

            controller.height = PlayerMath.Damp(controller.height, targetHeight, crouchLerp, Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            eyeHeight = PlayerMath.Damp(eyeHeight, targetEye, crouchLerp, Time.deltaTime);
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
            bool crouched = IsCrouched;
            IsSprinting = input.Sprint.IsPressed() && !crouched && move.y > 0.1f;
            float speed = crouched ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            bool hasInput = wish.sqrMagnitude > 0.0004f;
            float accel = grounded
                ? (hasInput ? groundAcceleration : groundDeceleration)
                : (hasInput ? airAcceleration : airDrag);
            Vector3 target = wish * speed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * Time.deltaTime);

            if (grounded) lastGroundedAt = Time.time;
            if (input.Jump.WasPressedThisFrame()) lastJumpPressedAt = Time.time;

            bool bufferedJump = Time.time - lastJumpPressedAt <= jumpBuffer;
            if (bufferedJump && Time.time - lastGroundedAt <= coyoteTime)
            {
                verticalVelocity = PlayerMath.JumpVelocity(jumpHeight, gravity);
                lastGroundedAt = -99f;
                lastJumpPressedAt = -99f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            float impactSpeed = verticalVelocity;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

            // Landing dip: use the fall speed from before the ground clamp.
            if (grounded && !wasGrounded)
                landDip = Mathf.Max(landDip, PlayerMath.LandingDip(impactSpeed, landDipScale, landDipMax));
            wasGrounded = grounded;

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            float planar = new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
            if (grounded && planar > 0.2f)
                bobPhase += planar / Mathf.Max(0.5f, bobStride) * Mathf.PI * 2f * Time.deltaTime;
            landDip = PlayerMath.Damp(landDip, 0f, landDipRecover, Time.deltaTime);
        }

        void UpdateCamera()
        {
            if (cameraRig == null) return;

            recoilPitch = PlayerMath.Damp(recoilPitch, 0f, recoilRecover, Time.deltaTime);
            recoilYaw = PlayerMath.Damp(recoilYaw, 0f, recoilRecover, Time.deltaTime);
            ApplyCameraRotation();

            float speed01 = Mathf.InverseLerp(walkSpeed, sprintSpeed, CurrentSpeed);
            float amplitude = Mathf.Lerp(bobAmplitudeWalk, bobAmplitudeSprint, speed01) * (IsCrouched ? 0.6f : 1f);
            float vertical = bobEnabled ? PlayerMath.BobOffset(bobPhase, amplitude) : 0f;
            float lateral = bobEnabled ? Mathf.Sin(bobPhase * 0.5f) * amplitude * bobLateral : 0f;

            Vector3 p = cameraRig.localPosition;
            p.x = lateral;
            p.y = eyeHeight + vertical - landDip;
            cameraRig.localPosition = p;

            if (viewCamera != null)
            {
                float targetFov = baseFov + (IsSprinting ? sprintFovKick : 0f);
                viewCamera.fieldOfView = PlayerMath.Damp(viewCamera.fieldOfView, targetFov, fovLerp, Time.deltaTime);
            }
        }
    }
}
