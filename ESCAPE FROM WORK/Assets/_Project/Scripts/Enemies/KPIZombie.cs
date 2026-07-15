using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// Standard KPI-obsessed office zombie. Wanders within a patrol radius,
    /// detects the player via proximity, and attacks with a melee swipe.
    /// </summary>
    public class KPIZombie : EnemyBase
    {
        // ---- Patrol -------------------------------------------------------------

        /// <summary>
        /// Maximum distance from the spawn / home point that this zombie will
        /// wander while patrolling.
        /// </summary>
        [SerializeField] private float patrolRadius = 5f;

        [Header("Patrol Tuning")]
        [Tooltip("Minimum seconds to wait after reaching a patrol waypoint before picking a new one.")]
        [SerializeField] private float minPatrolPause = 0.5f;

        [Tooltip("Maximum seconds to wait after reaching a patrol waypoint before picking a new one.")]
        [SerializeField] private float maxPatrolPause = 2f;

        [Tooltip("Distance threshold at which a patrol waypoint is considered reached.")]
        [SerializeField] private float waypointReachThreshold = 0.3f;

        // ---- Private patrol state -----------------------------------------------

        /// <summary>Home position around which patrol waypoints are generated.</summary>
        private Vector3 _homePosition;

        /// <summary>Current patrol destination on the XZ plane.</summary>
        private Vector3 _currentWaypoint;

        /// <summary>True while waiting at a waypoint before picking the next one.</summary>
        private bool _isWaitingAtWaypoint;

        /// <summary>Timestamp (Time.time) when the current waypoint wait ends.</summary>
        private float _waitEndTime;

        /// <summary>Cached collider array for player detection overlaps.</summary>
        private readonly Collider[] _detectionResults = new Collider[16];

        // ---- Unity lifecycle ----------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            _homePosition = transform.position;
            PickNewWaypoint();
        }

        // ---- Patrol -------------------------------------------------------------

        /// <summary>
        /// Per-frame patrol behaviour. Checks for the player inside
        /// <see cref="DetectionRange"/>; if found, transitions to Chase.
        /// Otherwise wanders toward random waypoints within
        /// <see cref="patrolRadius"/>.
        /// </summary>
        protected override void PatrolBehavior()
        {
            // -- Player detection --
            if (TryDetectPlayer())
                return;

            // -- Wander --
            if (_isWaitingAtWaypoint)
            {
                if (Time.time >= _waitEndTime)
                {
                    PickNewWaypoint();
                }
                return;
            }

            MoveToward(_currentWaypoint);

            // Check waypoint arrival (XZ-plane distance only).
            Vector3 toWaypoint = _currentWaypoint - transform.position;
            toWaypoint.y = 0f;
            if (toWaypoint.sqrMagnitude <= waypointReachThreshold * waypointReachThreshold)
            {
                StartWaypointWait();
            }
        }

        /// <summary>
        /// Scan for the player within <see cref="DetectionRange"/> using
        /// <see cref="Physics.OverlapSphere"/>. If the player is detected and this
        /// zombie is not blinded, sets the target and transitions to Chase.
        /// </summary>
        /// <returns>True when the player was detected.</returns>
        private bool TryDetectPlayer()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                DetectionRange,
                _detectionResults);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _detectionResults[i];
                if (col.CompareTag("Player"))
                {
                    SetTarget(col.transform);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pick a random walkable point within <see cref="patrolRadius"/> of
        /// <see cref="_homePosition"/> on the XZ plane.
        /// </summary>
        private void PickNewWaypoint()
        {
            _isWaitingAtWaypoint = false;

            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            _currentWaypoint = new Vector3(
                _homePosition.x + randomCircle.x,
                transform.position.y,
                _homePosition.z + randomCircle.y);
        }

        /// <summary>
        /// Begin waiting at the current waypoint for a randomised duration.
        /// </summary>
        private void StartWaypointWait()
        {
            _isWaitingAtWaypoint = true;
            _waitEndTime = Time.time + Random.Range(minPatrolPause, maxPatrolPause);
        }

        // ---- Attack -------------------------------------------------------------

        /// <summary>
        /// Melee swipe against the current target. Validates the target is still
        /// within <see cref="AttackRange"/> and that it implements
        /// <see cref="IDamageable"/> before applying damage.
        /// </summary>
        protected override void PerformAttack()
        {
            if (_target == null)
                return;

            float dist = DistanceToTarget();
            if (dist > AttackRange)
                return;

            // Apply damage through the IDamageable interface.
            IDamageable damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float dmg = data != null ? data.AttackDamage : 10f;
                damageable.TakeDamage(dmg, gameObject);
            }
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Patrol radius.
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Vector3 home = Application.isPlaying ? _homePosition : transform.position;
            Gizmos.DrawSphere(home, patrolRadius);

            // Detection radius.
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawSphere(transform.position, DetectionRange);

            // Attack range.
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, AttackRange);

            // Current waypoint.
            if (Application.isPlaying && _state == EnemyState.Patrol && !_isWaitingAtWaypoint)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_currentWaypoint, 0.2f);
                Gizmos.DrawLine(transform.position, _currentWaypoint);
            }
        }
    }
}
