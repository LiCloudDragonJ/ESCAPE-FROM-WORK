using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// 2.5D camera: follows the target with an offset, looks at the target,
    /// clamps to map bounds. Based on BENGLAOTOU CameraFollow25D approach.
    ///
    /// Offset-based positioning (e.g. (0, 10, -10) = behind + above) with
    /// natural LookRotation tilt. No manual pitch/FOV math needed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        public Transform Target { get => target; set => target = value; }

        [Header("Offset")]
        [Tooltip("Camera offset from target. (0, 10, -10) = 10 units above, 10 behind.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);

        [Header("Height Lock")]
        [Tooltip("Lock camera Y to a fixed world height regardless of target elevation.")]
        [SerializeField] private bool lockHeight = true;

        [Tooltip("Fixed world-space Y. Defaults to offset.y if 0.")]
        [SerializeField] private float fixedHeight = 10f;

        [Header("Look")]
        [Tooltip("Fixed pitch angle from horizontal. 90=straight down. 45=classic 2.5D.")]
        [Range(30f, 90f)]
        [SerializeField] private float pitchAngle = 45f;

        [Header("Movement")]
        [Tooltip("How fast the camera catches up. Higher = snappier.")]
        [SerializeField] private float followSpeed = 10f;

        [Header("Map Bounds")]
        [SerializeField] private Vector2 boundsMin = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 boundsMax = new Vector2(120f, 120f);

        private Camera _cam;

        // ---- Unity lifecycle ----

        private void Awake()
        {
            _cam = GetComponent<Camera>();

            // 2.5D perspective.
            _cam.orthographic = false;
            _cam.fieldOfView = 60f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 500f;

            // Default fixed height from offset if not set explicitly.
            if (lockHeight && Mathf.Approximately(fixedHeight, 0f))
                fixedHeight = offset.y;

            // Lock initial rotation.
            _cam.transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (_cam == null || target == null)
                return;

            // --- Position ---
            Vector3 desired = lockHeight
                ? new Vector3(target.position.x + offset.x, fixedHeight, target.position.z + offset.z)
                : target.position + offset;

            // Clamp to map bounds.
            desired = ClampToBounds(desired);

            // Exponential smoothing (frame-rate independent).
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position, desired,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );

            // --- Rotation: fixed angle, no jitter ---
            _cam.transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

        // ---- Internal ----

        private Vector3 ClampToBounds(Vector3 pos)
        {
            // Visible ground extent (approximate for perspective).
            float camY = lockHeight ? fixedHeight : pos.y;
            float halfH = camY * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;

            pos.x = Mathf.Clamp(pos.x, boundsMin.x + halfW, boundsMax.x - halfW);
            pos.z = Mathf.Clamp(pos.z, boundsMin.y + halfH, boundsMax.y - halfH);
            return pos;
        }

        // ---- Public ----

        /// <summary>Immediately snap to target — call after scene transitions.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Vector3 pos = lockHeight
                ? new Vector3(target.position.x + offset.x, fixedHeight, target.position.z + offset.z)
                : target.position + offset;

            _cam.transform.position = ClampToBounds(pos);
        }
    }
}
