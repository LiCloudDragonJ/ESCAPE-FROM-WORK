using UnityEngine;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// PPT怨灵Boss — 41F. Skeleton — fields reserved for future implementation.
    /// </summary>
#pragma warning disable CS0414
    public class PPTBoss : EnemyBase
    {
        [Header("PPT Boss")]
        [SerializeField] private int cloneCount = 2;
        [SerializeField] private float cloneLifetime = 10f;
        [SerializeField] private float barrageSpread = 20f;

        protected override void PatrolBehavior() { }
        protected override void PerformAttack()
        {
            // Phase 1: Enhanced PPT barrages + summon 2 slide clones.
            // Clones fire reduced-damage PPT projectiles.
        }
    }
}
