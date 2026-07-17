using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 会议恶魔 — Tanky slow-field caster. Slows player within field radius.
    /// </summary>
    public class MeetingDemon : EnemyBase
    {
        [Header("Meeting Demon")]
        [SerializeField] private float patrolRadius = 3f;
        [SerializeField] private float slowAmount = 0.5f;
        [SerializeField] private float slowDuration = 3f;
        [SerializeField] private float fieldRadius = 5f;
        [SerializeField] private float fieldCooldown = 6f;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private bool _isWaiting; private float _waitEndTime;
        private float _nextFieldTime;
        private readonly Collider[] _detectionResults = new Collider[16];
        private readonly Collider[] _fieldResults = new Collider[32];

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
            if (Time.time < _nextFieldTime) return;
            _nextFieldTime = Time.time + fieldCooldown;

            // Slow-field: apply Slow to player within field radius.
            int n = Physics.OverlapSphereNonAlloc(transform.position, fieldRadius, _fieldResults);
            for (int i = 0; i < n; i++)
            {
                if (_fieldResults[i].CompareTag("Player"))
                {
                    var fx = _fieldResults[i].GetComponent<StatusEffectManager>();
                    fx?.Apply(StatusEffectType.Slow, slowDuration, slowAmount, gameObject);

                    float dmg = (data != null ? data.AttackDamage : 18f) * 0.5f;
                    _fieldResults[i].GetComponent<IDamageable>()?.TakeDamage(dmg, gameObject);
                }
            }
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
