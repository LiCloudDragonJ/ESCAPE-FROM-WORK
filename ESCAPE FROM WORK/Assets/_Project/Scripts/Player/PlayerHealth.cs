using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Player health component extracted from <see cref="PlayerCombat"/> for
    /// clarity and separation of concerns. Manages health pool, damage
    /// reception, and the death sequence (equipment drops, dog-tag spawning,
    /// <see cref="GameManager"/> notification).
    ///
    /// <para>Implements <see cref="IDamageable"/> so enemies and hazards can
    /// damage the player through the standard damage interface.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        // ---- Inspector fields --------------------------------------------------

        [Header("Health")]
        [Tooltip("Maximum health points.")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Death Drops")]
        [Tooltip("Dog-tag (工牌) prefab spawned at the player's death position.")]
        [SerializeField] private GameObject dogTagPrefab;

        [Header("References")]
        [Tooltip("Reference to the PlayerCombat component for equipment dropping on death.")]
        [SerializeField] private PlayerCombat playerCombat;

        // ---- Public properties -------------------------------------------------

        /// <summary>
        /// Maximum health pool. Read-only at runtime; configure via the inspector.
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// Current health value. Clamped to the range [0, maxHealth].
        /// </summary>
        public float CurrentHealth { get; private set; }

        /// <summary>
        /// True when the player has died (current health has reached zero).
        /// Damage is ignored while dead.
        /// </summary>
        public bool IsDead { get; private set; }

        // ---- Unity lifecycle ---------------------------------------------------

        private void Awake()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }

        // ---- Cover detection -----------------------------------------------------

        [Header("Cover")]
        [Tooltip("Distance in meters within which furniture provides cover.")]
        [SerializeField] private float coverRadius = 1f;

        [Tooltip("Damage multiplier when the player is in cover.")]
        [SerializeField] private float coverDamageMultiplier = 0.6f;

        /// <summary>
        /// Returns true when the player is within <see cref="coverRadius"/> of
        /// any collider tagged "Furniture". Distance-based (not raycast) per GDD MVP spec.
        /// </summary>
        public bool IsInCover()
        {
            Collider[] furniture = Physics.OverlapSphere(transform.position, coverRadius);
            foreach (var col in furniture)
            {
                if (col.CompareTag("Furniture"))
                    return true;
            }
            return false;
        }

        // ---- IDamageable -------------------------------------------------------

        /// <inheritdoc />
        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead)
                return;

            // Cover reduction: proximity to furniture-tagged objects reduces damage (GDD §8).
            float finalDamage = IsInCover() ? amount * coverDamageMultiplier : amount;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Restore health by the given amount. Health will not exceed
        /// <see cref="MaxHealth"/>. Does nothing if the player is dead.
        /// </summary>
        /// <param name="amount">Amount of health to restore.</param>
        public void Heal(float amount)
        {
            if (IsDead)
                return;

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        // ---- Death sequence ----------------------------------------------------

        /// <summary>
        /// Execute the player death sequence: drop equipment on dangerous floors,
        /// spawn a dog tag at the death location, build a <see cref="DeathContext"/>,
        /// and notify <see cref="GameManager.Instance"/>.
        /// </summary>
        private void Die()
        {
            if (IsDead)
                return;

            IsDead = true;
            CurrentHealth = 0f;

            int currentFloor = GameManager.Instance != null
                ? GameManager.Instance.CurrentFloorNumber
                : 0;

            bool isDangerousFloor = !IsSafeFloor(currentFloor);

            // Drop equipped weapons on dangerous floors (permadeath penalty).
            if (isDangerousFloor && playerCombat != null)
            {
                playerCombat.DropEquipment();
            }

            // Spawn dog tag (工牌) at the death location.
            if (dogTagPrefab != null)
            {
                Object.Instantiate(dogTagPrefab, transform.position, Quaternion.identity);
            }

            // Build death context and notify GameManager.
            DeathContext ctx = new DeathContext
            {
                floorNumber = currentFloor,
                isSafeFloor = !isDangerousFloor,
                characterName = gameObject.name,
                causeOfDeath = "Combat",
                lootValueReturned = 0
            };

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied(ctx);
            }
            else
            {
                Debug.LogError("[PlayerHealth] GameManager.Instance is null — death not reported.");
            }
        }

        /// <summary>
        /// Determines whether a floor number represents a safe floor where
        /// equipment is preserved on death. Safe floors occur every 5 levels
        /// (5, 10, 15, ..., 50). The lobby (floor 50) is always safe.
        /// </summary>
        /// <param name="floorNumber">The floor to check (1-50).</param>
        /// <returns>True when the floor is a safe zone.</returns>
        public static bool IsSafeFloor(int floorNumber)
        {
            // Every 5th floor and the starting lobby are safe rooms / rest stops.
            return floorNumber % 5 == 0 || floorNumber == 50;
        }
    }
}
