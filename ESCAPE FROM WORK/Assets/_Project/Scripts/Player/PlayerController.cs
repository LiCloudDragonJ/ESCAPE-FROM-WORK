using UnityEngine;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Third-person player controller: camera-relative movement, crouch, dodge.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float crouchSpeed = 2.5f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 10f;
        [SerializeField] private float dodgeDuration = 0.2f;
        [SerializeField] private float dodgeCooldown = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float dodgeDistancePenalty = 0.75f;

        [Header("Aim")]
        [SerializeField] private PlayerAim playerAim;

        // ---- Private state ----
        private Rigidbody _rb;
        private Vector2 _moveInput;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _isDodging;
        private float _lastDodgeTime = float.MinValue;
        private Coroutine _dodgeCoroutine;
        private Camera _mainCamera;
        private Vector3 _originalScale;
        private float _originalY;

        // ---- Public properties ----
        public Vector3 AimDirection => _aimDirection;
        public bool IsDodging => _isDodging;
        public Vector3 Position => transform.position;
        public Rigidbody Rigidbody => _rb;
        public bool IsCrouching { get; private set; }
        public float HeightMultiplier => IsCrouching ? 0.55f : 1f;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _mainCamera = Camera.main;
            _originalScale = transform.localScale;
            _originalY = transform.position.y;
        }

        private void FixedUpdate()
        {
            if (_isDodging) return;

            Camera cam = Camera.main;
            Vector3 moveDir = Vector3.zero;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
                Vector3 rgt = cam.transform.right;   rgt.y = 0; rgt.Normalize();
                moveDir = (fwd * _moveInput.y + rgt * _moveInput.x).normalized;
            }
            else
            {
                moveDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
            }

            float spd = IsCrouching ? crouchSpeed : moveSpeed;
            _rb.velocity = moveDir * spd;

            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
        }

        // ---- Input polling (Update) ---------------------------------------------

        private void Update()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            UpdateAimInput();

            if (Input.GetKeyDown(KeyCode.Space))
                TryDodge();

            if (Input.GetKeyDown(KeyCode.LeftControl))
                ToggleCrouch();
        }

        void ToggleCrouch()
        {
            IsCrouching = !IsCrouching;
            if (IsCrouching)
            {
                transform.localScale = new Vector3(_originalScale.x, _originalScale.y * 0.55f, _originalScale.z);
                Vector3 p = transform.position;
                p.y = _originalY * 0.55f;
                transform.position = p;
            }
            else
            {
                transform.localScale = _originalScale;
                Vector3 p = transform.position;
                p.y = _originalY;
                transform.position = p;
            }
        }

        /// <summary>
        /// Sets _aimDirection from screen center (third-person crosshair).
        /// </summary>
        private void UpdateAimInput()
        {
            if (_mainCamera == null) return;

            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 dir = (hitPoint - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                    _aimDirection = dir.normalized;
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
