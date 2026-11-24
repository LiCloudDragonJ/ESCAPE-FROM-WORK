using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// PPT怨灵 — Ranged enemy. Fires projectiles that blind on hit.
    /// </summary>
    public class PPTWraith : EnemyBase
    {
        [Header("PPT Wraith")]
        [SerializeField] private float patrolRadius = 4f;
        [SerializeField] private float blindDuration = 1.5f;
        [SerializeField] private float preferredDistance = 8f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private float _waitEndTime;
        private bool _isWaiting;
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
            if (dist > AttackRange) return;

            if (dist < preferredDistance * 0.5f)
                transform.position += (transform.position - _target.position).normalized * MoveSpeed * Time.deltaTime;

            float dmg = data != null ? data.AttackDamage : 15f;
            _target.GetComponent<IDamageable>()?.TakeDamage(dmg, gameObject);

            // Blind the player on hit.
            var fx = _target.GetComponent<StatusEffectManager>();
            fx?.Apply(StatusEffectType.Blind, blindDuration, 1f, gameObject);
        }

        private bool TryDetectPlayer()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectionResults);
            for (int i = 0; i < n; i++)
                if (_detectionResults[i].CompareTag("Player"))
                { SetTarget(_detectionResults[i].transform); return true; }
            return false;
        }

        private void PickNewWaypoint()
        {
            _isWaiting = false;
            _currentWaypoint = _homePosition + new Vector3(Random.Range(-1f, 1f) * patrolRadius, 0, Random.Range(-1f, 1f) * patrolRadius);
        }
    }
}
