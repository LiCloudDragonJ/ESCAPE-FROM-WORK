using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Interface for any entity that can receive damage in the game world.
    /// Implemented by both the player (<see cref="Player.PlayerCombat"/>) and
    /// all enemy types (see EnemyBase in Task 6).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Apply damage to this entity.
        /// </summary>
        /// <param name="amount">Raw damage before any resistance calculations.</param>
        /// <param name="source">The GameObject that originated the damage (projectile, melee swing, etc.).</param>
        void TakeDamage(float amount, GameObject source);
    }
}
