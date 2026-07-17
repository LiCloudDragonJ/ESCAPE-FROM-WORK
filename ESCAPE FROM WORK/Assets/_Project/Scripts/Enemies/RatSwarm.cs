using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 茶水间老鼠群 — Tiny fast swarm enemy. Low HP, high speed,
    /// uses collision-based melee damage. Typically spawned in groups of 5.
    /// </summary>
    public class RatSwarm : EnemyBase
    {
        [Header("Rat Swarm")]
        [SerializeField] private float patrolRadius = 2f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private readonly Collider[] _detectionResults = new Collider[16];

        protected override void Awake() { base.Awake(); _homePosition = transform.position; PickNewWaypoint(); }

        protected override void PatrolBehavior()
        {
            if (TryDetectPlayer()) return;
            MoveToward(_currentWaypoint);
            Vector3 to = _currentWaypoint - transform.position; to.y = 0f;
            if (to.sqrMagnitude <= 0.2f * 0.2f) PickNewWaypoint();
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;
            if (DistanceToTarget() > AttackRange) return;

            IDamageable dmg = _target.GetComponent<IDamageable>();
            if (dmg != null)
            {
                float d = data != null ? data.AttackDamage : 5f;
                dmg.TakeDamage(d, gameObject);
            }
        }

        private bool TryDetectPlayer()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectionResults);
            for (int i = 0; i < n; i++)
                if (_detectionResults[i].CompareTag("Player")) { SetTarget(_detectionResults[i].transform); return true; }
            return false;
        }

        private void PickNewWaypoint() { _currentWaypoint = _homePosition + new Vector3(Random.Range(-1f,1f)*patrolRadius,0,Random.Range(-1f,1f)*patrolRadius); }
    }
}
