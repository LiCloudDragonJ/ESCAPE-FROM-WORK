using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Third-person over-the-shoulder camera with configurable smoothing.
    /// Mouse-driven yaw, fixed 15° pitch, cursor lock.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        public Transform Target { get => target; set => target = value; }

        [Header("Offset")]
        [Tooltip("Camera offset from target, relative to camera's own facing.")]
        [SerializeField] private Vector3 offset = new Vector3(0.5f, 1.8f, -5f);

        [Header("Mouse Look")]
        [Tooltip("Mouse sensitivity for horizontal camera rotation.")]
        [SerializeField] private float lookSensitivity = 3f;

        [Header("Smoothing")]
        [Tooltip("How quickly the camera catches up. Higher = snappier.")]
        [SerializeField] private float smoothSpeed = 8f;

        [Tooltip("Dead zone in meters — camera won't move if target displacement < this.")]
        [SerializeField] private float deadZone = 0.05f;

        [Tooltip("Maximum speed the camera can move (m/s). Prevents wild snaps.")]
        [SerializeField] private float maxSpeed = 30f;

        [Header("Pitch")]
        [Tooltip("Fixed camera pitch angle (negative = looking down).")]
        [SerializeField] private float pitchAngle = 15f;

        [Header("Map Bounds")]
        [SerializeField] private Vector2 boundsMin = new Vector2(-10f, -10f);
        [SerializeField] private Vector2 boundsMax = new Vector2(110f, 90f);

        // ── Private state ──────────────────────────────────────────────

        private Camera _cam;
        private float _yaw;
        private Vector3 _velocity; // for smooth damping

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = false;
            _cam.fieldOfView = 70f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 500f;
            _yaw = transform.eulerAngles.y;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Escape unlocks cursor for menus.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            // Click to re-lock.
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            if (_cam == null || target == null) return;

            // ── Mouse look: horizontal only ────────────────────────────
            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;

            // ── Crouch height adjustment ───────────────────────────────
            float heightAdjust = 0f;
            var pc = target?.GetComponent<EscapeFromWork.Player.PlayerController>();
            if (pc != null && pc.IsCrouching)
                heightAdjust = -0.8f;

            // ── Camera rotation ────────────────────────────────────────
            Quaternion rot = Quaternion.Euler(pitchAngle, _yaw, 0f);

            // ── Desired position: target + offset in camera-local space ─
            Vector3 desired = target.position
                            + rot * Vector3.right   * offset.x
                            + rot * Vector3.up      * (offset.y + heightAdjust)
                            + rot * Vector3.forward * offset.z;

            // Clamp to map bounds.
            desired.x = Mathf.Clamp(desired.x, boundsMin.x, boundsMax.x);
            desired.z = Mathf.Clamp(desired.z, boundsMin.y, boundsMax.y);

            // ── Dead zone ──────────────────────────────────────────────
            float displacement = Vector3.Distance(
                new Vector3(_cam.transform.position.x, 0, _cam.transform.position.z),
                new Vector3(desired.x, 0, desired.z));

            if (displacement < deadZone)
            {
                // Just update rotation, keep position.
                _cam.transform.rotation = rot;
                return;
            }

            // ── Smooth follow (spring-damper style) ────────────────────
            Vector3 newPos = Vector3.SmoothDamp(
                _cam.transform.position,
                desired,
                ref _velocity,
                1f / smoothSpeed, // smoothTime ≈ 1/speed
                maxSpeed,
                Time.deltaTime);

            _cam.transform.position = newPos;

            // Look at the target point (not at the target object — more stable).
            Vector3 lookTarget = target.position + Vector3.up * (offset.y + heightAdjust) * 0.5f;
            _cam.transform.rotation = rot;
        }

        /// <summary>
        /// Instantly snap the camera to its desired position. Used after teleports.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 desired = target.position
                            + _cam.transform.right   * offset.x
                            + _cam.transform.up      * offset.y
                            + _cam.transform.forward * offset.z;
            _cam.transform.position = desired;
            _velocity = Vector3.zero;
        }
    }
}
