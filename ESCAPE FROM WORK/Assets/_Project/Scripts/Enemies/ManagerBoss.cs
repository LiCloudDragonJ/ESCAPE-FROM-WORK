using UnityEngine;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 经理 — 21F. Skeleton — fields reserved for future implementation.
    /// </summary>
#pragma warning disable CS0414
    public class ManagerBoss : EnemyBase
    {
        [Header("Manager Boss")]
        [SerializeField] private float phase2DamageMult = 2f;
        [SerializeField] private float phase2SpeedMult = 0.5f;
        [SerializeField] private float phase2ScaleMult = 1.5f;

        private bool _isPhase2;

        protected override void PatrolBehavior() { }

        protected override void PerformAttack()
        {
            if (!_isPhase2) { /* Normal melee */ }
            else { /* Giant AOE smash */ }
        }

        public override void TakeDamage(float amount, GameObject source)
        {
            base.TakeDamage(amount, source);
            if (!_isPhase2 && _currentHealth / MaxHealth <= 0.5f)
                EnterPhase2();
        }

        private void EnterPhase2()
        {
            _isPhase2 = true;
            transform.localScale *= phase2ScaleMult;
        }
    }
}
