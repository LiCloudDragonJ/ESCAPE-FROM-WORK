using UnityEngine;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// 精英保安队长Boss — 3F. Skeleton — fields reserved for future implementation.
    /// </summary>
#pragma warning disable CS0414
    public class SecurityBoss : EnemyBase
    {
        [Header("Security Boss")]
        [SerializeField] private int reinforcementCount = 3;
        [SerializeField] private GameObject reinforcementPrefab;
        [SerializeField] private float dodgeChance = 0.4f;

        protected override void PatrolBehavior() { }
        protected override void PerformAttack()
        {
            // Summon 3 Security guards as reinforcements.
            // Monitor prediction: has dodgeChance to dodge player's aim direction.
        }
    }
}
