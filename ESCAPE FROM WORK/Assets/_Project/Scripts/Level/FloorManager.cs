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

        /// <summary>Spawns enemies into the generated floor.</summary>
        [SerializeField] private EnemySpawner enemySpawner;

        // ---- Public state -------------------------------------------------------

        /// <summary>Which floor of the tower this is (50 = top, 1 = ground/lobby).</summary>
        public int floorNumber;

        /// <summary>The generated layout data for this floor.</summary>
        public FloorLayoutData Layout { get; private set; }

        /// <summary>Per-floor persistent state (cleared, looted, visit count).</summary>
        public FloorState State { get; private set; }

        /// <summary>
        /// True when all enemies on this floor have been eliminated.
        /// Shortcut for <c>State.isCleared</c>.
        /// </summary>
        public bool IsSafe => State != null && State.isCleared;

        // ---- Private state ------------------------------------------------------

        private int _enemiesRemaining;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            Instance = this;
            if (State == null)
                State = FloorState.LoadOrCreate(floorNumber);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---- Public API ---------------------------------------------------------

        /// <summary>
        /// Initialise the floor: generate the procedural layout via
        /// <see cref="FloorBuilder"/>, assemble GameObjects via
        /// <see cref="FloorAssembler"/>, spawn enemies, and record the entry.
        /// </summary>
        /// <param name="floorNum">Floor number (50 = top, 1 = ground).</param>
        /// <param name="seed">
        /// Deterministic seed. Typically derived from the run seed so the same
        /// floor generates the same layout within a run.
        /// </param>
        public void InitializeFloor(int floorNum, int seed)
        {
            floorNumber = Mathf.Clamp(floorNum, 1, 50);

            // Load or create persistent floor state.
            State = FloorState.LoadOrCreate(floorNumber);
            State.RecordEntry();

            // Generate layout data (pure data, no GameObjects).
            Layout = FloorBuilder.Build(floorNumber);

            // Assemble floor GameObjects from layout data.
            FloorAssembler.Assemble(Layout);

            // Spawn enemies. Collect spawn zones from the newly-built rooms.
            if (enemySpawner != null)
            {
                GatherSpawnZones();
                enemySpawner.SpawnFloorEnemies();
            }
            else
            {
                Debug.LogWarning("[FloorManager] No EnemySpawner assigned — floor will have no enemies.");
            }

            // Count living enemies after spawning.
            _enemiesRemaining = CountLivingEnemies();

            Debug.Log($"[FloorManager] Floor {floorNumber} [{Layout.archetype}] initialised. " +
                      $"Rooms: {Layout.rooms?.Count ?? 0}, Enemies: {_enemiesRemaining}, " +
                      $"Safe: {State.isCleared}, Entry: {Layout.entryPos}, Extract: {Layout.extractPos}");
        }

        /// <summary>
        /// Called when an enemy on this floor is killed.
        /// </summary>
        public void OnEnemyKilled()
        {
            if (IsSafe)
                return;

            _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);

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
        /// </summary>
        /// <param name="useFireEscape">
        /// True if extracting via a fire-escape stairwell;
        /// false for the elevator / normal stairwell.
        /// </param>
        public void Extract(bool useFireEscape)
        {
            if (State != null)
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

        private void GatherSpawnZones()
        {
            if (Layout?.rooms == null || enemySpawner == null)
                return;

            // Collect all enemy spawn zones from the newly-built rooms.
            // After FloorAssembler builds the floor, each stairwell/conference/etc.
            // room has its own area — for Phase 1, enemies spawn across the whole map.
            int totalZones = Layout.rooms.Count;
            if (totalZones > 0)
            {
                Debug.Log($"[FloorManager] Floor has {totalZones} rooms available for enemy spawn zones.");
            }
        }

        private int CountLivingEnemies()
        {
            if (enemySpawner != null)
                return enemySpawner.CountLivingEnemies();

            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            return enemies.Length;
        }
    }
}
