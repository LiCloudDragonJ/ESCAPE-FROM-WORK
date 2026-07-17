using UnityEngine;
using EscapeFromWork.Data;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// Manages enemy population for a single floor. Supports two spawn modes:
    /// 1. Prefab-based (existing) — picks randomly from enemyPrefabs[].
    /// 2. Data-driven (new) — picks from enemyDataPool[], creating a runtime
    ///    GameObject with the appropriate EnemyBase subclass.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // ---- Prefab-based spawning ------------------------------------------------

        [SerializeField] private GameObject[] enemyPrefabs;

        // ---- Data-driven spawning -------------------------------------------------

        [Tooltip("EnemyData SOs for data-driven spawning. Each entry defines one enemy type.")]
        [SerializeField] private EnemyData[] enemyDataPool;

        [Tooltip("Fallback prefab used when spawning from data without a specific prefab.")]
        [SerializeField] private GameObject defaultEnemyPrefab;

        // ---- Count config ---------------------------------------------------------

        [SerializeField] private int minEnemies = 5;
        [SerializeField] private int maxEnemies = 12;

        // ---- Spawn zones ----------------------------------------------------------

        [SerializeField] private Transform[] spawnZones;

        // ---- Private state --------------------------------------------------------

        private Transform _enemyContainer;

        // ---- Unity lifecycle ------------------------------------------------------

        private void Awake()
        {
            GameObject container = new GameObject("EnemyContainer");
            container.transform.SetParent(transform);
            _enemyContainer = container.transform;
        }

        // ---- Public API -----------------------------------------------------------

        /// <summary>
        /// Spawn enemies for the current floor. Uses data-driven spawning if
        /// enemyDataPool is populated, falling back to prefab-based spawning.
        /// </summary>
        public void SpawnFloorEnemies()
        {
            if (spawnZones == null || spawnZones.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn zones assigned.");
                return;
            }

            // Data-driven mode: spawn from EnemyData SO references.
            if (enemyDataPool != null && enemyDataPool.Length > 0)
            {
                SpawnFromData();
                return;
            }

            // Prefab-based mode: spawn from prefab references.
            SpawnFromPrefabs();
        }

        /// <summary>
        /// Spawn enemies from EnemyData SO pool. Creates a runtime GameObject
        /// for each enemy, attaches the correct component, and initializes it.
        /// </summary>
        private void SpawnFromData()
        {
            int count = Random.Range(minEnemies, maxEnemies + 1);

            for (int i = 0; i < count; i++)
            {
                EnemyData data = enemyDataPool[Random.Range(0, enemyDataPool.Length)];
                if (data == null) continue;

                Transform zone = spawnZones[Random.Range(0, spawnZones.Length)];
                if (zone == null) continue;

                Vector3 pos = zone.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

                // Try prefab first, fall back to default.
                GameObject prefab = data.Prefab != null ? data.Prefab : defaultEnemyPrefab;
                if (prefab == null)
                {
                    Debug.LogWarning($"[EnemySpawner] No prefab for {data.EnemyName}");
                    continue;
                }

                GameObject enemy = Instantiate(prefab, pos, Quaternion.identity, _enemyContainer);
                enemy.tag = "Enemy";
                enemy.name = $"{data.EnemyName}_{i}";

                // Initialize EnemyBase with data.
                var enemyBase = enemy.GetComponent<EnemyBase>();
                if (enemyBase != null)
                    enemyBase.InitializeFromData(data);
            }

            Debug.Log($"[EnemySpawner] Spawned {count} enemies from data pool ({enemyDataPool.Length} types).");
        }

        private void SpawnFromPrefabs()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No enemy prefabs assigned.");
                return;
            }

            int count = Random.Range(minEnemies, maxEnemies + 1);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                if (prefab == null) continue;

                Transform zone = spawnZones[Random.Range(0, spawnZones.Length)];
                if (zone == null) continue;

                Vector3 pos = zone.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                GameObject enemy = Instantiate(prefab, pos, Quaternion.identity, _enemyContainer);
                enemy.tag = "Enemy";
                enemy.name = $"{prefab.name}_{i}";
            }

            Debug.Log($"[EnemySpawner] Spawned {count} enemies from prefab pool.");
        }

        /// <summary>
        /// Count how many living enemies are currently in the scene.
        /// </summary>
        /// <returns>The number of active GameObjects tagged "Enemy".</returns>
        public int CountLivingEnemies()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            return enemies.Length;
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (spawnZones == null)
                return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            foreach (Transform zone in spawnZones)
            {
                if (zone != null)
                {
                    Gizmos.DrawSphere(zone.position, 0.5f);
                }
            }
        }
    }
}
