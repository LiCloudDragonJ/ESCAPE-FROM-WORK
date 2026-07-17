using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 邮件幽灵 — Fast swarm enemy. Rushes toward player and explodes on death.
    /// Appears in groups, low HP, high threat from AOE death explosion.
    /// </summary>
    public class MailGhost : EnemyBase
    {
        [Header("Mail Ghost")]
        [SerializeField] private float explosionDamage = 20f;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float patrolRadius = 6f;
        [SerializeField] private float waypointReachThreshold = 0.3f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private readonly Collider[] _detectionResults = new Collider[16];
        private readonly Collider[] _explosionResults = new Collider[32];

        protected override void Awake()
        {
            base.Awake();
            _homePosition = transform.position;
            PickNewWaypoint();
        }

        protected override void PatrolBehavior()
        {
            if (TryDetectPlayer()) return;

            // Fast, erratic patrol — drift toward waypoint at full speed.
            MoveToward(_currentWaypoint);
            Vector3 toWaypoint = _currentWaypoint - transform.position;
            toWaypoint.y = 0f;
            if (toWaypoint.sqrMagnitude <= waypointReachThreshold * waypointReachThreshold)
                PickNewWaypoint();
        }

        /// <summary>
        /// Rush toward the player. On contact, the MailGhost explodes via Die().
        /// </summary>
        protected override void PerformAttack()
        {
            if (_target == null) return;

            MoveToward(_target.position);

            // If close enough, trigger death explosion.
            float dist = DistanceToTarget();
            if (dist <= AttackRange)
            {
                Explode();
                Die();
            }
        }

        /// <summary>
        /// AOE damage to all IDamageable within explosionRadius.
        /// </summary>
        private void Explode()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, _explosionResults);
            for (int i = 0; i < hitCount; i++)
            {
                IDamageable dmg = _explosionResults[i].GetComponent<IDamageable>();
                if (dmg != null)
                    dmg.TakeDamage(explosionDamage, gameObject);
            }
            // TODO: Spawn explosion VFX.
        }

        protected override void Die()
        {
            base.Die();
        }

        private bool TryDetectPlayer()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectionResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_detectionResults[i].CompareTag("Player"))
                {
                    SetTarget(_detectionResults[i].transform);
                    return true;
                }
            }
            return false;
        }

        private void PickNewWaypoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            _currentWaypoint = new Vector3(_homePosition.x + randomCircle.x, transform.position.y, _homePosition.z + randomCircle.y);
        }
    }
}
