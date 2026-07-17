using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 午睡魔 — Dormant until player approaches, then bursts into high-speed charge.
    /// </summary>
    public class NapDemon : EnemyBase
    {
        [Header("Nap Demon")]
        [SerializeField] private float wakeRadius = 5f;
        [SerializeField] private float chargeSpeed = 8f;

        private bool _isAwake;
        private readonly Collider[] _detectionResults = new Collider[16];

        protected override void PatrolBehavior()
        {
            if (!_isAwake)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && Vector3.Distance(transform.position, player.transform.position) <= wakeRadius)
                {
                    _isAwake = true;
                    SetTarget(player.transform);
                }
                return;
            }
            if (_target == null) return;
            MoveToward(_target.position);
        }

        /// <summary>
        /// Override chase to use burst charge speed.
        /// </summary>
        protected override void ChaseBehavior()
        {
            if (_target == null) { _state = EnemyState.Patrol; return; }
            float dist = DistanceToTarget();
            if (dist <= AttackRange) _state = EnemyState.Attack;
            else
            {
                Vector3 dir = _target.position - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.position += dir.normalized * chargeSpeed * Time.deltaTime;
            }
        }

        protected override void PerformAttack()
        {
            if (_target == null || DistanceToTarget() > AttackRange) return;
            float dmg = data != null ? data.AttackDamage : 25f;
            _target.GetComponent<IDamageable>()?.TakeDamage(dmg, gameObject);
        }
    }
}
