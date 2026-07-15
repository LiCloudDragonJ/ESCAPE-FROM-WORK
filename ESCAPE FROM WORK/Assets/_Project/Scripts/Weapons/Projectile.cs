using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Weapons
{
    /// <summary>
    /// 3D projectile that moves forward on the XZ plane at a constant speed,
    /// tracks distance traveled, damages enemies on contact, applies Type-C
    /// special effects, and self-destructs when its maximum range is exceeded
    /// or it hits a wall / obstacle.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        /// <summary>
        /// Forward speed in world units per second.
        /// </summary>
        [SerializeField] private float speed = 30f;

        // ---- Flight state --------------------------------------------------------

        private Vector3 _direction;
        private float _damage;
        private float _maxRange;
        private float _distanceTraveled;
        private bool _isTypeC;
        private string _specialEffect;

        private Rigidbody _rb;
        private bool _initialized;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Ensure the collider is set up as a trigger for OnTriggerEnter.
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (!_initialized)
                return;

            // Drive movement via velocity so physics collision detection is reliable.
            _rb.velocity = _direction * speed;

            float moveDelta = speed * Time.fixedDeltaTime;
            _distanceTraveled += moveDelta;

            if (_distanceTraveled >= _maxRange)
            {
                Destroy(gameObject);
            }
        }

        // ---- Initialization ------------------------------------------------------

        /// <summary>
        /// Configure the projectile after instantiation.
        /// </summary>
        /// <param name="dir">Normalized flight direction (XZ plane).</param>
        /// <param name="dmg">Damage applied to enemies on hit.</param>
        /// <param name="range">Maximum travel distance before the projectile expires.</param>
        /// <param name="isTypeC">True when fired from a Type-C special-effect weapon.</param>
        /// <param name="effect">Name of the special effect to apply (Blind, Root, Taunt).</param>
        public void Initialize(Vector3 dir, float dmg, float range, bool isTypeC, string effect)
        {
            _direction = dir.normalized;
            _damage = dmg;
            _maxRange = range;
            _isTypeC = isTypeC;
            _specialEffect = effect;
            _distanceTraveled = 0f;
            _initialized = true;
        }

        // ---- Collision -----------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized)
                return;

            // Destroy on environment collision (anything not Enemy, Player, Loot)
            if (!other.CompareTag("Enemy") && !other.CompareTag("Player") && !other.CompareTag("Loot"))
            {
                Destroy(gameObject);
                return;
            }

            // Damage enemies on contact.
            if (other.CompareTag("Enemy"))
            {
                IDamageable damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(_damage, gameObject);
                }

                // Apply Type-C special effect when applicable.
                if (_isTypeC && !string.IsNullOrEmpty(_specialEffect))
                {
                    ApplySpecialEffect(other.gameObject);
                }

                Destroy(gameObject);
            }
        }

        // ---- Special effects -----------------------------------------------------

        /// <summary>
        /// Apply a Type-C weapon's special status effect to the target enemy.
        /// </summary>
        /// <param name="target">The enemy GameObject that was hit.</param>
        private void ApplySpecialEffect(GameObject target)
        {
            switch (_specialEffect.ToLowerInvariant().Trim())
            {
                case "blind":
                    // TODO: Integrate with status-effect system when available.
                    Debug.Log($"[Projectile] Blind applied to {target.name}");
                    break;

                case "root":
                    // TODO: Integrate with status-effect system when available.
                    Debug.Log($"[Projectile] Root applied to {target.name}");
                    break;

                case "taunt":
                    // TODO: Integrate with status-effect system when available.
                    Debug.Log($"[Projectile] Taunt applied to {target.name}");
                    break;

                default:
                    Debug.Log($"[Projectile] Unknown special effect '{_specialEffect}' on {target.name}");
                    break;
            }
        }
    }
}
