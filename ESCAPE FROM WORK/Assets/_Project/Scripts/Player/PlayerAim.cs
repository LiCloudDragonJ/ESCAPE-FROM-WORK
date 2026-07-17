using UnityEngine;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Dual-mode aiming system for the player. Operates in two modes:
    /// <list type="bullet">
    ///   <item><b>Auto-aim</b> (default): finds the nearest enemy within
    ///     <see cref="autoAimRadius"/> and locks onto their Transform.</item>
    ///   <item><b>Manual aim</b>: toggled by pressing Left Shift. Freely aim
    ///     at the mouse world position on the ground plane (Y = 0).</item>
    /// </list>
    /// Auto-aim lock-in has a configurable delay (<see cref="autoAimLockDelay"/>)
    /// before it is considered "established," which gates accuracy-sensitive
    /// mechanics like dodge-penalty distance.
    /// </summary>
    public class PlayerAim : MonoBehaviour
    {
        // ---- Serialized fields ---------------------------------------------------

        [Header("Auto-Aim")]
        [Tooltip("Radius of the auto-aim search sphere (3D, centered on player).")]
        [SerializeField] private float autoAimRadius = 35f;

        [Tooltip("Seconds the crosshair must linger on a target before the lock is considered established.")]
        [SerializeField] private float autoAimLockDelay = 0.15f;

        [Header("References")]
        [Tooltip("The PlayerController whose aim input this component augments.")]
        [SerializeField] private PlayerController playerController;

        // ---- Private state -------------------------------------------------------

        private Camera _mainCamera;

        private bool _isManualAim;
        private Transform _lockTarget;
        private float _lockTimer;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// True when manual-aim mode is toggled on (Left Shift).
        /// When false, auto-aim is active.
        /// </summary>
        public bool IsManualAim => _isManualAim;

        /// <summary>
        /// True when the player is NOT in manual-aim mode AND has a valid lock target.
        /// </summary>
        public bool IsAutoAiming => !_isManualAim && _lockTarget != null;

        /// <summary>
        /// The Transform of the currently locked-on enemy, or null.
        /// </summary>
        public Transform LockTarget => _lockTarget;

        /// <summary>
        /// The world-space point the player is currently aiming at.
        /// Auto-aim: the lock target's position. Manual: the mouse-to-ground hit point.
        /// </summary>
        public Vector3 AimPoint { get; private set; }

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            _mainCamera = Camera.main;

            // Initialize AimPoint to a forward projection so that GetAimDirection()
            // in PlayerCombat never reads Vector3.zero on the first frame (before
            // the first UpdateAutoAim / UpdateManualAim call populates it).
            AimPoint = transform.position + Vector3.forward * 10f;
        }

        private void Update()
        {
            PollManualAimInput();

            if (_isManualAim)
            {
                // Manual mode: aim at the mouse world position on the ground plane.
                UpdateManualAim();
                _lockTarget = null;
                _lockTimer = 0f;
            }
            else
            {
                // Auto-aim: find the nearest enemy within radius.
                UpdateAutoAim();
            }
        }

        // ---- Manual aim ---------------------------------------------------------

        /// <summary>
        /// Reads the mouse screen position and projects it onto the Y = 0 plane
        /// to produce the manual AimPoint.
        /// </summary>
        private void UpdateManualAim()
        {
            if (_mainCamera == null)
                return;

            Vector2 screenPos = Input.mousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                AimPoint = ray.GetPoint(enter);
            }
        }

        // ---- Auto-aim -----------------------------------------------------------

        /// <summary>
        /// Scans for the nearest enemy within <see cref="autoAimRadius"/> using
        /// <see cref="Physics.OverlapSphere"/>. If a target is found, locks onto
        /// it and increments the lock timer toward <see cref="autoAimLockDelay"/>.
        /// </summary>
        private void UpdateAutoAim()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, autoAimRadius);

            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                if (!col.CompareTag("Enemy"))
                    continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = col.transform;
                }
            }

            if (nearest != null)
            {
                // Lock onto this target.
                if (_lockTarget == nearest)
                {
                    // Same target — advance the lock timer.
                    _lockTimer += Time.deltaTime;
                }
                else
                {
                    // New target — reset the lock timer.
                    _lockTarget = nearest;
                    _lockTimer = 0f;
                }

                AimPoint = _lockTarget.position;
            }
            else
            {
                // No target in range — fall back to the controller's raw aim direction
                // projected to a point on the ground plane.
                _lockTarget = null;
                _lockTimer = 0f;

                Vector3 aimDir = playerController != null
                    ? playerController.AimDirection
                    : transform.forward;

                AimPoint = transform.position + new Vector3(aimDir.x, 0f, aimDir.z).normalized * 10f;
            }
        }

        // ---- Input polling ------------------------------------------------------

        /// <summary>
        /// Manual aim: hold RMB to enter free-aim mode, release to return to auto-aim.
        /// While active, stamina drains at 8/s (enforced by PlayerCombat).
        /// Called from <see cref="Update"/>.
        /// </summary>
        private void PollManualAimInput()
        {
            // RMB hold = manual aim (GDD: combat-system.md §1)
            _isManualAim = Input.GetMouseButton(1);
        }

        // ---- Public methods -----------------------------------------------------

        /// <summary>
        /// Returns true when the auto-aim lock timer has exceeded <see cref="autoAimLockDelay"/>,
        /// meaning the player has been tracking the same target long enough for accuracy
        /// penalties (e.g., dodge distance reduction) to apply.
        /// </summary>
        public bool IsLockEstablished()
        {
            return IsAutoAiming && _lockTimer >= autoAimLockDelay;
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Auto-aim radius.
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, autoAimRadius);

            // Aim point.
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AimPoint, 0.25f);

            // Line from player to aim point.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, AimPoint);

            // Lock target highlight.
            if (_lockTarget != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(_lockTarget.position, Vector3.one * 0.5f);
            }
        }
    }
}
