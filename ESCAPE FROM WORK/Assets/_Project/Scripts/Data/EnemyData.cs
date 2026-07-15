using UnityEngine;

namespace EscapeFromWork.Data
{
    /// <summary>
    /// Data definition for an enemy type that can spawn during a raid.
    /// Create instances via Assets > Create > Data > Enemy.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Enemy", fileName = "NewEnemy")]
    public class EnemyData : ScriptableObject
    {
        // ---- Identity -----------------------------------------------------------

        /// <summary>Display name shown in HUD and bestiary.</summary>
        [SerializeField] private string enemyName = "New Enemy";

        /// <summary>Lore entry displayed in the bestiary after first encounter.</summary>
        [SerializeField] [TextArea(3, 8)] private string backstory;

        // ---- Combat attributes --------------------------------------------------

        /// <summary>Total health pool.</summary>
        [SerializeField] [Range(1f, 10000f)] private float maxHealth = 100f;

        /// <summary>Movement speed in world units per second.</summary>
        [SerializeField] [Range(0.1f, 30f)] private float moveSpeed = 3f;

        /// <summary>Damage dealt per successful attack.</summary>
        [SerializeField] [Range(0f, 1000f)] private float attackDamage = 10f;

        /// <summary>Maximum distance at which this enemy can land an attack.</summary>
        [SerializeField] [Range(0.1f, 50f)] private float attackRange = 1.5f;

        /// <summary>Minimum time in seconds between consecutive attacks.</summary>
        [SerializeField] [Range(0.1f, 30f)] private float attackCooldown = 1.5f;

        // ---- Perception ---------------------------------------------------------

        /// <summary>Radius at which the enemy becomes aware of the player and begins pursuit.</summary>
        [SerializeField] [Range(0.1f, 100f)] private float detectionRange = 15f;

        /// <summary>
        /// Vision cone half-angle in degrees. 360 means the enemy is omniscient
        /// (no blind spot). 180 covers a full hemisphere.
        /// </summary>
        [SerializeField] [Range(1f, 360f)] private float detectionAngle = 120f;

        // ---- Loot ---------------------------------------------------------------

        /// <summary>
        /// The 工牌 (work badge) this enemy always drops on death. This is the
        /// player's primary means of progression and floor-unlock currency.
        /// </summary>
        [SerializeField] private ItemData guaranteedDrop;

        /// <summary>Pool of random items that may drop on death.</summary>
        [SerializeField] private ItemData[] possibleDrops = System.Array.Empty<ItemData>();

        /// <summary>
        /// Per-item drop probability: chance (0-1) that each entry in
        /// possibleDrops is included in the death loot.
        /// </summary>
        [SerializeField] [Range(0f, 1f)] private float dropChance = 0.3f;

        /// <summary>Minimum 回形针 (paperclips) dropped on death.</summary>
        [SerializeField] [Range(0, 10000)] private int minCurrencyDrop;

        /// <summary>Maximum 回形针 (paperclips) dropped on death.</summary>
        [SerializeField] [Range(0, 10000)] private int maxCurrencyDrop = 10;

        // ---- Presentation -------------------------------------------------------

        /// <summary>World-space prefab for spawning this enemy.</summary>
        [SerializeField] private GameObject prefab;

        // ---- Public properties --------------------------------------------------

        public string EnemyName => enemyName;
        public string Backstory => backstory;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float DetectionRange => detectionRange;
        public float DetectionAngle => detectionAngle;
        public ItemData GuaranteedDrop => guaranteedDrop;
        public ItemData[] PossibleDrops => possibleDrops;
        public float DropChance => dropChance;
        public int MinCurrencyDrop => minCurrencyDrop;
        public int MaxCurrencyDrop => maxCurrencyDrop;
        public GameObject Prefab => prefab;

        // ---- Convenience queries ------------------------------------------------

        /// <summary>True when this enemy has no visual blind spot (full 360 vision).</summary>
        public bool IsOmniscient => Mathf.Approximately(detectionAngle, 360f);

        /// <summary>True when this enemy guarantees at least one currency drop.</summary>
        public bool DropsCurrency => maxCurrencyDrop > 0;

        /// <summary>Number of items in the random loot pool.</summary>
        public int PossibleDropCount => possibleDrops?.Length ?? 0;

        // ---- Validation ---------------------------------------------------------

        private void OnValidate()
        {
            // Ensure min <= max for currency drop range.
            if (minCurrencyDrop > maxCurrencyDrop)
            {
                minCurrencyDrop = maxCurrencyDrop;
            }
        }
    }
}
