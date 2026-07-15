using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Top-down camera: centers on target, but clamps at map edges so the camera
    /// never shows beyond the defined boundary. When player returns from the edge,
    /// smoothly resumes centering.
    /// </summary>
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        public Transform Target { get => target; set => target = value; }

        [Header("Movement")]
        [SerializeField] private float followSpeed = 8f;

        [Header("Map Bounds (world-space XZ)")]
        [Tooltip("Bottom-left corner of the playable area.")]
        [SerializeField] private Vector2 boundsMin = new Vector2(0f, 0f);
        [Tooltip("Top-right corner of the playable area.")]
        [SerializeField] private Vector2 boundsMax = new Vector2(100f, 100f);

        private Camera _cam;
        private float _camY;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _camY = transform.position.y;
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            // Visible half-extents (world units) based on orthographic size + aspect
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            // Desired: center on player
            float desiredX = Target.position.x;
            float desiredZ = Target.position.z;

            // Clamp so camera edges never go past map boundary
            float clampedX = Mathf.Clamp(desiredX, boundsMin.x + halfW, boundsMax.x - halfW);
            float clampedZ = Mathf.Clamp(desiredZ, boundsMin.y + halfH, boundsMax.y - halfH);

            Vector3 targetPos = new Vector3(clampedX, _camY, clampedZ);

            transform.position = Vector3.Lerp(
                transform.position, targetPos,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );
        }
    }
}
