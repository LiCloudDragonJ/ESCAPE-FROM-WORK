using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Enemies;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Orchestrates the lifecycle of a single floor during a raid:
    /// generation, enemy spawning, clearance tracking, and extraction.
    ///
    /// <para>A new instance is created per floor entry. The static
    /// <see cref="Instance"/> singleton is valid only for the current raid;
    /// it is set when the player enters a floor and cleared on extraction
    /// or death.</para>
    /// </summary>
    public class FloorManager : MonoBehaviour
    {
        // ---- Singleton ----------------------------------------------------------

        /// <summary>
        /// The active FloorManager for the current raid. Valid only while
        /// the player is on a floor (<see cref="Core.GameState.InRaid"/>).
        /// Null during base-building and main-menu states.
        /// </summary>
        public static FloorManager Instance { get; private set; }

        // ---- Inspector references -----------------------------------------------

        /// <summary>Generates the procedural room layout for this floor.</summary>
        [SerializeField] private FloorGenerator floorGenerator;

        /// <summary>Spawns enemies into the generated rooms.</summary>
        [SerializeField] private EnemySpawner enemySpawner;

        // ---- Public state -------------------------------------------------------

        /// <summary>Which floor of the tower this is (1 = top, 50 = ground/lobby).</summary>
        public int floorNumber;

        /// <summary>
        /// Design-time data for this floor (enemy composition, loot tables,
        /// boss presence, etc.). Placeholder — define FloorTemplateData as a
        /// ScriptableObject when the content pipeline is ready.
        /// </summary>
        // TODO: Replace with actual FloorTemplateData ScriptableObject reference.

        /// <summary>Per-floor persistent state (cleared, looted, visit count).</summary>
        public FloorState State { get; private set; }

        /// <summary>
        /// True when all enemies on this floor have been eliminated.
        /// Shortcut for <c>State.isCleared</c>.
        /// </summary>
        public bool IsSafe => State != null && State.isCleared;

        // ---- Private state ------------------------------------------------------

        /// <summary>Number of living enemies remaining on this floor.</summary>
        private int _enemiesRemaining;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            // FloorManager instances are created per-floor; the most recent
            // one becomes the singleton.
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---- Public API ---------------------------------------------------------

        /// <summary>
        /// Initialise the floor: generate the procedural layout, spawn enemies,
        /// count the enemy population, and record the entry in floor state.
        /// </summary>
        /// <param name="floorNum">Floor number (1-50).</param>
        /// <param name="seed">
        /// Deterministic seed for the procedural generation. Typically derived
        /// from the floor number or a run seed so that the same floor always
        /// generates the same layout within a run.
        /// </param>
        public void InitializeFloor(int floorNum, int seed)
        {
            floorNumber = Mathf.Clamp(floorNum, 1, 50);

            // Load or create persistent floor state.
            State = FloorState.LoadOrCreate(floorNumber);
            State.RecordEntry();

            // Generate the room grid.
            if (floorGenerator != null)
            {
                floorGenerator.GenerateFloor(seed);
            }
            else
            {
                Debug.LogWarning("[FloorManager] No FloorGenerator assigned — floor will have no rooms.");
            }

            // Spawn enemies into the generated rooms.
            if (enemySpawner != null)
            {
                // Collect spawn zones from all generated rooms.
                GatherSpawnZones();
                enemySpawner.SpawnFloorEnemies();
            }
            else
            {
                Debug.LogWarning("[FloorManager] No EnemySpawner assigned — floor will have no enemies.");
            }

            // Count living enemies after spawning.
            _enemiesRemaining = CountLivingEnemies();

            Debug.Log($"[FloorManager] Floor {floorNumber} initialised. " +
                      $"Rooms: {CountRooms()}, Enemies: {_enemiesRemaining}, " +
                      $"Safe: {State.isCleared}");
        }

        /// <summary>
        /// Called when an enemy on this floor is killed.
        /// Decrements the remaining count; when it reaches zero the floor
        /// is marked as cleared and the GameManager is notified.
        /// </summary>
        public void OnEnemyKilled()
        {
            if (IsSafe)
                return;

            _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);

            // Reconcile with actual scene state to catch edge cases
            // (e.g. enemies killed through non-standard means).
            if (_enemiesRemaining <= 0)
            {
                _enemiesRemaining = CountLivingEnemies();
            }

            if (_enemiesRemaining <= 0)
            {
                State.MarkCleared();
                Debug.Log($"[FloorManager] Floor {floorNumber} cleared!");

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnFloorCleared(floorNumber);
                }
            }
        }

        /// <summary>
        /// Trigger extraction from the current floor.
        /// Saves the floor state and delegates to
        /// <see cref="GameManager.ExtractFromFloor"/>.
        /// </summary>
        /// <param name="useFireEscape">
        /// True if extracting via the fire-escape stairwell (diagonal exit);
        /// false for the normal stairwell entry point. Affects narrative
        /// and may affect the next floor's entry point.
        /// </param>
        public void Extract(bool useFireEscape)
        {
            // Persist floor state.
            State.Save();

            Debug.Log($"[FloorManager] Extracting from floor {floorNumber} " +
                      $"via {(useFireEscape ? "fire escape" : "normal stairs")}.");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ExtractFromFloor();
            }
            else
            {
                Debug.LogError("[FloorManager] Cannot extract — GameManager.Instance is null.");
            }
        }

        // ---- Internal helpers ---------------------------------------------------

        /// <summary>
        /// Collect enemy spawn-zone transforms from all generated rooms
        /// for future integration with a per-room spawn-zone system.
        /// Currently logs the count; the EnemySpawner uses its own
        /// inspector-assigned spawn zones.
        /// </summary>
        private void GatherSpawnZones()
        {
            if (floorGenerator == null || enemySpawner == null)
                return;

            RoomModule[,] grid = floorGenerator.Grid;
            if (grid == null)
                return;

            int totalZones = 0;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    RoomModule room = grid[x, y];
                    if (room != null && room.enemySpawnZones != null)
                        totalZones += room.enemySpawnZones.Length;
                }
            }

            if (totalZones > 0)
            {
                Debug.Log($"[FloorManager] Gathered {totalZones} spawn zones across {CountRooms()} rooms.");
            }
        }

        /// <summary>
        /// Count living enemies currently in the scene.
        /// Delegates to <see cref="EnemySpawner.CountLivingEnemies"/> when
        /// available; otherwise falls back to a tag-based lookup.
        /// </summary>
        /// <returns>Number of active GameObjects tagged "Enemy".</returns>
        private int CountLivingEnemies()
        {
            if (enemySpawner != null)
                return enemySpawner.CountLivingEnemies();

            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            return enemies.Length;
        }

        /// <summary>
        /// Count the number of rooms in the generated grid.
        /// </summary>
        /// <returns>Number of non-null cells in the grid.</returns>
        private int CountRooms()
        {
            if (floorGenerator == null || floorGenerator.Grid == null)
                return 0;

            int count = 0;
            RoomModule[,] grid = floorGenerator.Grid;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] != null)
                        count++;
                }
            }

            return count;
        }
    }
}
