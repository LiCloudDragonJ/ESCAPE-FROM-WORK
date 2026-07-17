using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 饮水机漏电丧尸 — Melee enemy. Electric shock slows on hit.
    /// </summary>
    public class LeakyWaterZombie : EnemyBase
    {
        [Header("Leaky Water Zombie")]
        [SerializeField] private float patrolRadius = 4f;
        [SerializeField] private float slowDuration = 2f;
        [SerializeField] private float slowAmount = 0.3f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private bool _isWaiting; private float _waitEndTime;
        private readonly Collider[] _detectionResults = new Collider[16];

        protected override void Awake() { base.Awake(); _homePosition = transform.position; PickNewWaypoint(); }

        protected override void PatrolBehavior()
        {
            if (TryDetectPlayer()) return;
            if (_isWaiting) { if (Time.time >= _waitEndTime) PickNewWaypoint(); return; }
            MoveToward(_currentWaypoint);
            Vector3 to = _currentWaypoint - transform.position; to.y = 0f;
            if (to.sqrMagnitude <= 0.3f * 0.3f) { _isWaiting = true; _waitEndTime = Time.time + Random.Range(0.5f, 2f); }
        }

        protected override void PerformAttack()
        {
            if (_target == null || DistanceToTarget() > AttackRange) return;

            float dmg = data != null ? data.AttackDamage : 20f;
            _target.GetComponent<IDamageable>()?.TakeDamage(dmg, gameObject);

            // Electric shock slow.
            var fx = _target.GetComponent<StatusEffectManager>();
            fx?.Apply(StatusEffectType.Slow, slowDuration, slowAmount, gameObject);
        }

        private bool TryDetectPlayer()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectionResults);
            for (int i = 0; i < n; i++)
                if (_detectionResults[i].CompareTag("Player"))
                { SetTarget(_detectionResults[i].transform); return true; }
            return false;
        }

        private void PickNewWaypoint() { _isWaiting = false; _currentWaypoint = _homePosition + new Vector3(Random.Range(-1f,1f)*patrolRadius,0,Random.Range(-1f,1f)*patrolRadius); }
    }
}
