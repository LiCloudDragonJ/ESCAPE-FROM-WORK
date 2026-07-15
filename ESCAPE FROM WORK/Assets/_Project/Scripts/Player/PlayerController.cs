using UnityEngine;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Rigidbody-based top-down player movement on the XZ plane.
    /// Reads move/aim/dodge input via UnityEngine.Input and applies
    /// physics-driven motion via a 3D Rigidbody. Designed for an orthographic
    /// camera looking down from above (ground plane at Y = 0).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        // ---- Serialized fields ---------------------------------------------------

        [Header("Movement")]
        [Tooltip("Base movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 8f;

        [Header("Dodge")]
        [Tooltip("Speed in units per second during a dodge.")]
        [SerializeField] private float dodgeSpeed = 18f;

        [Tooltip("How long a single dodge lasts in seconds.")]
        [SerializeField] private float dodgeDuration = 0.2f;

        [Tooltip("Minimum time in seconds between consecutive dodges.")]
        [SerializeField] private float dodgeCooldown = 0.8f;

        [Tooltip("Speed multiplier applied while auto-aim is locking (0-1). Reduces dodge distance when an enemy is targeted.")]
        [Range(0f, 1f)]
        [SerializeField] private float dodgeDistancePenalty = 0.75f;

        [Header("Aim")]
        [Tooltip("Reference to the PlayerAim component that controls targeting logic.")]
        [SerializeField] private PlayerAim playerAim;

        // ---- Private state -------------------------------------------------------

        private Rigidbody _rb;

        private Vector2 _moveInput;
        private Vector3 _aimDirection = Vector3.forward;

        private bool _isDodging;
        private float _lastDodgeTime = float.MinValue;

        private Coroutine _dodgeCoroutine;

        /// <summary>
        /// Cached reference to the main camera. Populated on Awake.
        /// </summary>
        private Camera _mainCamera;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// The current world-space direction the player is aiming toward.
        /// </summary>
        public Vector3 AimDirection => _aimDirection;

        /// <summary>
        /// Whether the player is currently executing a dodge.
        /// </summary>
        public bool IsDodging => _isDodging;

        /// <summary>
        /// The player's current world position (read from the Transform).
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// The Rigidbody driving player movement.
        /// </summary>
        public Rigidbody Rigidbody => _rb;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _mainCamera = Camera.main;
        }

        private void FixedUpdate()
        {
            if (_isDodging)
                return;

            Vector3 velocity = new Vector3(_moveInput.x, 0f, _moveInput.y) * moveSpeed;
            _rb.velocity = new Vector3(velocity.x, _rb.velocity.y, velocity.z);
        }

        // ---- Input polling (Update) ---------------------------------------------

        private void Update()
        {
            // Movement input (WASD / Arrow keys / left stick).
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Aim input.
            UpdateAimInput();

            // Dodge input.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryDodge();
            }
        }

        /// <summary>
        /// Reads aim input from mouse or gamepad right stick.
        /// Mouse: screen-to-world raycast against the ground plane (Y = 0).
        /// Gamepad: right-stick direction treated as a world-space offset from the player.
        /// </summary>
        private void UpdateAimInput()
        {
            // Mouse — screen-to-world raycast against the ground plane (MVP: keyboard+mouse only).
            if (_mainCamera == null)
                return;

            Vector3 screenPos = Input.mousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                Vector3 direction = (worldPoint - transform.position).normalized;
                _aimDirection = direction;
            }
        }

        /// <summary>
        /// Attempts a dodge in the current movement direction, or backward from the
        /// aim direction if the player is standing still.
        /// Applies <see cref="dodgeDistancePenalty"/> when auto-aim has a lock.
        /// </summary>
        private void TryDodge()
        {
            if (_isDodging)
                return;

            if (Time.time - _lastDodgeTime < dodgeCooldown)
                return;

            // Determine dodge direction: movement input direction, or backward
            // from aim direction if not moving.
            Vector3 dodgeDir;

            if (_moveInput.sqrMagnitude > 0.01f)
            {
                dodgeDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
            }
            else
            {
                // Dodge backward — opposite of current aim direction projected
                // onto the XZ plane.
                Vector3 aimXZ = new Vector3(_aimDirection.x, 0f, _aimDirection.z);
                if (aimXZ.sqrMagnitude < 0.01f)
                    aimXZ = Vector3.back;

                dodgeDir = -aimXZ.normalized;
            }

            // Apply auto-aim distance penalty when a lock is established.
            if (playerAim != null && playerAim.IsLockEstablished())
            {
                dodgeDir *= dodgeDistancePenalty;
            }

            _lastDodgeTime = Time.time;

            if (_dodgeCoroutine != null)
                StopCoroutine(_dodgeCoroutine);

            _dodgeCoroutine = StartCoroutine(DodgeRoutine(dodgeDir));
        }

        // ---- Dodge coroutine ----------------------------------------------------

        private System.Collections.IEnumerator DodgeRoutine(Vector3 direction)
        {
            _isDodging = true;

            float elapsed = 0f;

            while (elapsed < dodgeDuration)
            {
                _rb.velocity = direction * dodgeSpeed;
                elapsed += Time.deltaTime;
                yield return null;
            }

            _rb.velocity = Vector3.zero;
            _isDodging = false;
            _dodgeCoroutine = null;
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawRay(origin, _aimDirection * 2f);

            // Draw a small sphere at the aim endpoint.
            Gizmos.DrawWireSphere(origin + _aimDirection * 2f, 0.15f);
        }
    }
}
