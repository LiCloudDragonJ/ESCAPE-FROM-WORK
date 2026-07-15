using UnityEngine;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// Manages enemy population for a single floor. Called by the level / floor
    /// manager when a new floor is entered. Spawns a random number of enemies
    /// (within configured bounds) at random spawn-zone positions.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // ---- Configuration ------------------------------------------------------

        /// <summary>
        /// Pool of enemy prefabs to choose from when spawning. Each spawned enemy
        /// is selected randomly from this array.
        /// </summary>
        [SerializeField] private GameObject[] enemyPrefabs;

        /// <summary>Minimum number of enemies to spawn per floor.</summary>
        [SerializeField] private int minEnemies = 5;

        /// <summary>Maximum number of enemies to spawn per floor.</summary>
        [SerializeField] private int maxEnemies = 12;

        /// <summary>
        /// Possible spawn locations. A random subset of these is used each time
        /// <see cref="SpawnFloorEnemies"/> is called.
        /// </summary>
        [SerializeField] private Transform[] spawnZones;

        // ---- Private state ------------------------------------------------------

        /// <summary>Parent Transform under which spawned enemies are organised in the hierarchy.</summary>
        private Transform _enemyContainer;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            // Create a container object to keep the Hierarchy tidy.
            GameObject container = new GameObject("EnemyContainer");
            container.transform.SetParent(transform);
            _enemyContainer = container.transform;
        }

        // ---- Public API ---------------------------------------------------------

        /// <summary>
        /// Spawn a random number of enemies (between <see cref="minEnemies"/> and
        /// <see cref="maxEnemies"/>) at random positions chosen from
        /// <see cref="spawnZones"/>.
        ///
        /// <para>Each spawned enemy is tagged "Enemy" so that
        /// <see cref="CountLivingEnemies"/> and other systems can find them.</para>
        /// </summary>
        public void SpawnFloorEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No enemy prefabs assigned — nothing to spawn.");
                return;
            }

            if (spawnZones == null || spawnZones.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn zones assigned — nothing to spawn.");
                return;
            }

            int count = Random.Range(minEnemies, maxEnemies + 1);

            for (int i = 0; i < count; i++)
            {
                // Pick a random prefab from the pool.
                GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                if (prefab == null)
                    continue;

                // Pick a random spawn zone.
                Transform zone = spawnZones[Random.Range(0, spawnZones.Length)];
                if (zone == null)
                    continue;

                Vector3 spawnPosition = zone.position;

                // Add slight random scatter so enemies don't stack exactly on top
                // of each other when multiple share a zone.
                spawnPosition += new Vector3(
                    Random.Range(-1f, 1f),
                    0f,
                    Random.Range(-1f, 1f));

                GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity, _enemyContainer);
                enemy.tag = "Enemy";
                enemy.name = $"{prefab.name}_{i}";
            }

            Debug.Log($"[EnemySpawner] Spawned {count} enemies across {spawnZones.Length} zones.");
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
