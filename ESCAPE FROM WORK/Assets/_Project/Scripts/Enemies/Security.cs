using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 保安 — Armored guard with flashlight blind + melee.
    /// 50% damage reduction from armor.
    /// </summary>
    public class Security : EnemyBase
    {
        [Header("Security")]
        [SerializeField] private float patrolRadius = 6f;
        [SerializeField] private float blindDuration = 3f;
        [SerializeField] private float blindRange = 10f;
        [SerializeField] private float defenseMultiplier = 0.5f;

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
            if (to.sqrMagnitude <= 0.3f * 0.3f) { _isWaiting = true; _waitEndTime = Time.time + Random.Range(1f, 3f); }
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;
            float dist = DistanceToTarget();

            // Flashlight blind at range.
            if (dist <= blindRange)
            {
                var fx = _target.GetComponent<StatusEffectManager>();
                fx?.Apply(StatusEffectType.Blind, blindDuration, 1f, gameObject);
            }

            // Melee at close range.
            if (dist <= AttackRange)
            {
                float dmg = data != null ? data.AttackDamage : 15f;
                _target.GetComponent<IDamageable>()?.TakeDamage(dmg, gameObject);
            }
        }

        public override void TakeDamage(float amount, GameObject source)
        {
            base.TakeDamage(amount * defenseMultiplier, source);
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
