using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 精英保安队长 — Shield + baton. Has charge attack and frontal damage block.
    /// Found in Security Dept and lower floors. Slow turn speed.
    /// </summary>
    public class EliteCaptain : EnemyBase
    {
        [Header("Elite Captain")]
        [SerializeField] private float patrolRadius = 4f;
        [SerializeField] private float chargeSpeed = 8f;
        [SerializeField] private float shieldBlockChance = 0.5f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private bool _isWaiting;
        private float _waitEndTime;
        private bool _isCharging;
        private readonly Collider[] _detectionResults = new Collider[16];

        protected override void Awake() { base.Awake(); _homePosition = transform.position; PickNewWaypoint(); }

        protected override void PatrolBehavior()
        {
            if (TryDetectPlayer()) return;
            if (_isWaiting) { if (Time.time >= _waitEndTime) PickNewWaypoint(); return; }
            MoveToward(_currentWaypoint);
            Vector3 to = _currentWaypoint - transform.position; to.y = 0f;
            if (to.sqrMagnitude <= 0.3f * 0.3f) { _isWaiting = true; _waitEndTime = Time.time + Random.Range(1f, 2f); }
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;
            float dist = DistanceToTarget();

            if (dist > AttackRange * 2f && !_isCharging)
            {
                // Charge at player.
                _isCharging = true;
            }

            if (_isCharging && dist <= AttackRange)
            {
                _isCharging = false;
                IDamageable dmg = _target.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    float d = data != null ? data.AttackDamage : 25f;
                    dmg.TakeDamage(d * 1.5f, gameObject); // charge bonus
                }
            }
            else if (dist <= AttackRange)
            {
                IDamageable dmg = _target.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    float d = data != null ? data.AttackDamage : 25f;
                    dmg.TakeDamage(d, gameObject);
                }
            }
        }

        /// <summary>
        /// Override chase to use charge speed when charging.
        /// </summary>
        protected override void ChaseBehavior()
        {
            if (_target == null) { _state = EnemyState.Patrol; return; }
            float dist = DistanceToTarget();
            float speed = _isCharging ? chargeSpeed : MoveSpeed;

            if (dist <= AttackRange) _state = EnemyState.Attack;
            else
            {
                Vector3 dir = _target.position - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) transform.position += dir.normalized * speed * Time.deltaTime;
            }
        }

        public override void TakeDamage(float amount, GameObject source)
        {
            // Frontal shield block: check if attacker is in front.
            if (source != null)
            {
                Vector3 toAttacker = (source.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toAttacker);
                if (dot > 0.3f && Random.value < shieldBlockChance)
                    return; // blocked!
            }
            base.TakeDamage(amount, source);
        }

        private bool TryDetectPlayer()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectionResults);
            for (int i = 0; i < n; i++)
                if (_detectionResults[i].CompareTag("Player")) { SetTarget(_detectionResults[i].transform); return true; }
            return false;
        }

        private void PickNewWaypoint() { _isWaiting = false; _currentWaypoint = _homePosition + new Vector3(Random.Range(-1f,1f)*patrolRadius,0,Random.Range(-1f,1f)*patrolRadius); }
    }
}
