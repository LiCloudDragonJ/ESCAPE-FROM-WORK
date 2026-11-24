using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// CEO Boss — Three variants accessed via SO config:
    ///   Avatar (50F tutorial): HP 200, Phase 0 only, Layoff Notice AOE
    ///   SemiBody (1F): HP 1200, Phase 0+1
    ///   TrueBody (50F return): HP 2000, Phase 0+1+2
    ///
    /// Phase 0: Layoff Notice — Telegraphed circular AOE, 2s windup, dodgeable
    /// Phase 1: Brainwash — Mind control (reverses player input) — TODO
    /// Phase 2: Overtime Loop — Map-wide slow + DOT zone — TODO
    /// </summary>
#pragma warning disable CS0414 // Phase 1/2 fields reserved
    public class CEOBoss : EnemyBase
    {
        [Header("Boss Config")]
        [Tooltip("Which CEO variant this is. 0=Avatar, 1=SemiBody, 2=TrueBody")]
        [SerializeField] private int bossVariant;

        [Header("Phase 0: Layoff Notice")]
        [SerializeField] private float layoffRadius = 6f;
        [SerializeField] private float layoffWindup = 2f;
        [SerializeField] private float layoffDamage = 30f;
        [SerializeField] private float layoffCooldown = 5f;
        [SerializeField] private GameObject warningZonePrefab;

        [Header("Phase 1: Brainwash (future)")]
        [SerializeField] private float brainwashDuration = 5f;
        [SerializeField] private float brainwashCooldown = 15f;

        [Header("Phase 2: Overtime Loop (future)")]
        [SerializeField] private float overtimeSlowAmount = 0.5f;
        [SerializeField] private float overtimeDamagePerSec = 5f;

        [Header("Movement")]
        [SerializeField] private float patrolRadius = 4f;
        [SerializeField] private float preferredDistance = 6f;

        // ---- State ----
        private int _currentPhase;
        private float _nextLayoffTime;
        private float _nextBrainwashTime;
        private bool _isCasting;
        private Vector3 _currentLayoffTarget;
        private GameObject _activeWarningZone;
        private float _layoffCastStartTime;

        private Vector3 _homePosition;
        private Vector3 _currentWaypoint;
        private readonly Collider[] _detectionResults = new Collider[16];
        private readonly Collider[] _aoeResults = new Collider[32];

        // ---- Properties ----
        private float EffectiveLayoffCooldown => layoffCooldown / (1f + _currentPhase * 0.5f);

        // ---- Unity lifecycle ----

        protected override void Awake()
        {
            base.Awake();
            _homePosition = transform.position;
            _currentPhase = 0;
            PickNewWaypoint();
        }

        // ---- Patrol (pre-combat) ----

        protected override void PatrolBehavior()
        {
            // CEO Avatar: aggressive detection. TrueBody: even wider.
            if (TryDetectPlayer()) return;

            MoveToward(_currentWaypoint);
            Vector3 to = _currentWaypoint - transform.position; to.y = 0f;
            if (to.sqrMagnitude <= 0.3f * 0.3f) PickNewWaypoint();
        }

        // ---- Phase management ----

        public override void TakeDamage(float amount, GameObject source)
        {
            base.TakeDamage(amount, source);
            float hp = _currentHealth / MaxHealth;

            // Phase transitions based on variant.
            if (bossVariant >= 1 && hp <= 0.5f && _currentPhase == 0)
                EnterPhase(1);
            if (bossVariant >= 2 && hp <= 0.25f && _currentPhase == 1)
                EnterPhase(2);
        }

        private void EnterPhase(int phase)
        {
            _currentPhase = phase;
            CancelCurrentCast();
            // TODO: Phase transition VFX (screen flash, model change).
            Debug.Log($"[CEOBoss] Entering Phase {phase}");
        }

        // ---- Attack behavior ----

        protected override void PerformAttack()
        {
            if (_target == null) return;

            // Maintain preferred distance — strafe if too close.
            float dist = DistanceToTarget();
            if (dist < preferredDistance * 0.5f && !_isCasting)
                StrafeAway();

            switch (_currentPhase)
            {
                case 0: Phase0_Attack(); break;
                case 1: Phase1_Attack(); break;
                case 2: Phase2_Attack(); break;
            }
        }

        // ---- Phase 0: Layoff Notice ----

        private void Phase0_Attack()
        {
            if (Time.time < _nextLayoffTime) return;

            if (!_isCasting)
            {
                // Begin casting Layoff Notice at player's current position.
                _isCasting = true;
                _currentLayoffTarget = _target.position;
                _currentLayoffTarget.y = 0f;
                _layoffCastStartTime = Time.time;

                // Spawn warning zone indicator at target position.
                if (warningZonePrefab != null)
                {
                    _activeWarningZone = Instantiate(warningZonePrefab,
                        _currentLayoffTarget, Quaternion.identity);
                    _activeWarningZone.transform.localScale =
                        Vector3.one * (layoffRadius * 2f);
                }
            }

            // Check windup completion.
            if (Time.time - _layoffCastStartTime >= layoffWindup)
            {
                ExecuteLayoff();
            }
        }

        private void ExecuteLayoff()
        {
            // AOE damage at the target position.
            int hitCount = Physics.OverlapSphereNonAlloc(
                _currentLayoffTarget, layoffRadius, _aoeResults);

            for (int i = 0; i < hitCount; i++)
            {
                IDamageable dmg = _aoeResults[i].GetComponent<IDamageable>();
                if (dmg != null)
                    dmg.TakeDamage(layoffDamage, gameObject);
            }

            // Cleanup warning zone.
            if (_activeWarningZone != null)
            {
                Destroy(_activeWarningZone);
                _activeWarningZone = null;
            }

            _isCasting = false;
            _nextLayoffTime = Time.time + EffectiveLayoffCooldown;
        }

        // ---- Phase 1: Brainwash (stub) ----

        private void Phase1_Attack()
        {
            // Phase 0 attacks still available.
            Phase0_Attack();

            if (Time.time < _nextBrainwashTime || _isCasting) return;

            // TODO: Apply mind control — reverse player input for brainwashDuration.
            // var player = _target.GetComponent<PlayerController>();
            // player.ApplyMindControl(brainwashDuration);
            _nextBrainwashTime = Time.time + brainwashCooldown;
        }

        // ---- Phase 2: Overtime Loop (stub) ----

        private void Phase2_Attack()
        {
            Phase0_Attack();
            Phase1_Attack();

            // TODO: Map-wide slow aura + DOT.
            // GameManager.ApplyOvertimeLoop(overtimeSlowAmount, overtimeDamagePerSec);
        }

        // ---- Helpers ----

        private void StrafeAway()
        {
            if (_target == null) return;
            Vector3 away = (transform.position - _target.position).normalized;
            transform.position += away * MoveSpeed * Time.deltaTime;
        }

        private void CancelCurrentCast()
        {
            if (_activeWarningZone != null)
            {
                Destroy(_activeWarningZone);
                _activeWarningZone = null;
            }
            _isCasting = false;
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
            _currentWaypoint = _homePosition +
                new Vector3(Random.Range(-1f, 1f) * patrolRadius, 0,
                            Random.Range(-1f, 1f) * patrolRadius);
        }

        // ---- Death (Avatar variant) ----

        protected override void Die()
        {
            CancelCurrentCast();

            if (bossVariant == 0)
            {
                // Avatar defeated: trigger escape sequence.
                // TODO: Play CEO taunt dialogue ("你会回来的").
                // TODO: Activate escape route (fire escape door unlocks).
                Debug.Log("[CEOBoss] Avatar defeated — escape route opening.");
            }

            base.Die();
        }

        // ---- Gizmos ----

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, DetectionRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, layoffRadius);
        }
    }
}
