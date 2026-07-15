using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Procedurally generates a single office-tower floor on a 2D grid
    /// laid out on the XZ plane (Y = 0 at floor level). Produces a
    /// deterministic layout from a given seed so the same floor number
    /// always generates the same layout within a run.
    ///
    /// <para>Grid layout (example for 6x6):</para>
    /// <list type="bullet">
    ///   <item>Stairwell at (0, 0) — bottom-left, normal entry.</item>
    ///   <item>TeaRoom at (gridWidth/2, 0) — bottom-center, near stairs.</item>
    ///   <item>Fire-escape stairwell at (gridWidth-1, gridHeight-1) — top-right, diagonal extraction.</item>
    ///   <item>Remaining cells: 60% Office, 30% Hallway, 10% Conference.</item>
    /// </list>
    /// </summary>
    public class FloorGenerator : MonoBehaviour
    {
        // ---- Inspector configuration ---------------------------------------------

        /// <summary>
        /// Prefab array indexed by room type:
        /// [0] Office, [1] Hallway, [2] TeaRoom, [3] Stairwell, [4] Conference.
        /// Assign in the Unity Editor.
        /// </summary>
        [SerializeField] private GameObject[] roomPrefabs;

        /// <summary>Number of grid columns (X axis).</summary>
        [SerializeField] private int gridWidth = 6;

        /// <summary>Number of grid rows (Z axis).</summary>
        [SerializeField] private int gridHeight = 6;

        /// <summary>
        /// World-space size of a single tile in units (X = width, Y = depth).
        /// Default: 25x25, giving a 150x150 total floor for a 6x6 grid.
        /// </summary>
        [SerializeField] private Vector2 tileSize = new Vector2(25f, 25f);

        // ---- Generated state ----------------------------------------------------

        /// <summary>2D array of generated rooms. Null cells are empty.</summary>
        private RoomModule[,] _grid;

        /// <summary>Parent transform under which all room instances are organised.</summary>
        private Transform _roomContainer;

        // ---- Public accessors ----------------------------------------------------

        /// <summary>The generated room grid. Read-only after <see cref="GenerateFloor"/> completes.</summary>
        public RoomModule[,] Grid => _grid;

        /// <summary>Number of columns in the grid.</summary>
        public int GridWidth => gridWidth;

        /// <summary>Number of rows in the grid.</summary>
        public int GridHeight => gridHeight;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            GameObject container = new GameObject("RoomContainer");
            container.transform.SetParent(transform);
            container.transform.localPosition = Vector3.zero;
            _roomContainer = container.transform;
        }

        // ---- Public API ---------------------------------------------------------

        /// <summary>
        /// Generate a complete floor layout from the given seed.
        /// Destroys any previously generated rooms and creates a fresh grid.
        /// </summary>
        /// <param name="seed">Deterministic seed passed to UnityEngine.Random.InitState.</param>
        public void GenerateFloor(int seed)
        {
            // Clear any previous generation.
            ClearGrid();

            // Initialise random state for deterministic generation.
            Random.InitState(seed);

            _grid = new RoomModule[gridWidth, gridHeight];

            // 1. Place the normal-entry stairwell at bottom-left.
            PlaceRoom(GetPrefabByType(RoomType.Stairwell), 0, 0, false);

            // 2. Place the tea-room at bottom-center, near the stairs.
            PlaceRoom(GetPrefabByType(RoomType.TeaRoom), gridWidth / 2, 0, false);

            // 3. Place the fire-escape stairwell at top-right (diagonal).
            PlaceRoom(GetPrefabByType(RoomType.Stairwell), gridWidth - 1, gridHeight - 1, true);

            // 4. Fill remaining empty cells with weighted random rooms.
            FillRemainingCells();

            // 5. Compute adjacency connections between neighbours.
            UpdateConnections();
        }

        // ---- Private placement logic --------------------------------------------

        /// <summary>
        /// Instantiate a room prefab at the given grid coordinates and record it
        /// in the grid.
        /// </summary>
        /// <param name="prefab">The room prefab to instantiate.</param>
        /// <param name="x">Grid column.</param>
        /// <param name="y">Grid row.</param>
        /// <param name="isExtraction">If true, marks this room as an extraction point (fire escape).</param>
        /// <returns>The RoomModule component on the instantiated GameObject, or null if placement failed.</returns>
        private RoomModule PlaceRoom(GameObject prefab, int x, int y, bool isExtraction = false)
        {
            if (prefab == null)
            {
                Debug.LogError($"[FloorGenerator] Null prefab for room at ({x}, {y}).");
                return null;
            }

            Vector3 worldPos = GridToWorld(x, y);
            GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity, _roomContainer);
            instance.name = $"{prefab.name}_{x}_{y}";

            RoomModule module = instance.GetComponent<RoomModule>();
            if (module == null)
            {
                Debug.LogError($"[FloorGenerator] Prefab '{prefab.name}' is missing a RoomModule component.");
                return null;
            }

            module.gridPosition = new Vector2Int(x, y);
            module.isExtractionPoint = isExtraction;
            if (isExtraction)
            {
                module.roomType = RoomType.Stairwell;
            }

            // Disable all colliders on room tiles so the player can move freely.
            // Walls are visual-only at this prototype stage.
            foreach (var col in instance.GetComponentsInChildren<Collider>())
            {
                col.isTrigger = true;
            }

            _grid[x, y] = module;
            return module;
        }

        /// <summary>
        /// Fill every empty cell in the grid with a randomly chosen room type
        /// weighted at 60% Office, 30% Hallway, 10% Conference.
        /// </summary>
        private void FillRemainingCells()
        {
            // Build weighted pool: Office=6, Hallway=3, Conference=1 (sum=10).
            // Using simple threshold selection for clarity rather than a weighted list.
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (_grid[x, y] != null)
                        continue;

                    RoomType type = RollRoomType();
                    PlaceRoom(GetPrefabByType(type), x, y, false);
                }
            }
        }

        /// <summary>
        /// Weighted random roll for a non-special room type.
        /// </summary>
        /// <returns>Office (60%), Hallway (30%), or Conference (10%).</returns>
        private static RoomType RollRoomType()
        {
            float roll = Random.value; // [0, 1)

            if (roll < 0.60f)
                return RoomType.Office;

            if (roll < 0.90f) // 0.60 + 0.30
                return RoomType.Hallway;

            return RoomType.ConferenceRoom;
        }

        /// <summary>
        /// Scan every cell in the grid and set the connection flags
        /// (North/South/East/West) based on whether the adjacent cell
        /// contains a non-null room.
        /// </summary>
        private void UpdateConnections()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    RoomModule room = _grid[x, y];
                    if (room == null)
                        continue;

                    // North: y + 1 (positive Z in world space).
                    room.connectionNorth = (y + 1 < gridHeight) && _grid[x, y + 1] != null;

                    // South: y - 1 (negative Z in world space).
                    room.connectionSouth = (y - 1 >= 0) && _grid[x, y - 1] != null;

                    // East: x + 1 (positive X in world space).
                    room.connectionEast = (x + 1 < gridWidth) && _grid[x + 1, y] != null;

                    // West: x - 1 (negative X in world space).
                    room.connectionWest = (x - 1 >= 0) && _grid[x - 1, y] != null;
                }
            }
        }

        // ---- Grid utility methods -----------------------------------------------

        /// <summary>
        /// Convert a grid coordinate to a world-space position on the XZ plane.
        /// The origin (0,0) maps to a corner of the floor; the room center is
        /// offset by half a tile.
        /// </summary>
        /// <param name="x">Grid column.</param>
        /// <param name="y">Grid row.</param>
        /// <returns>World position with Y = 0 (floor level).</returns>
        private Vector3 GridToWorld(int x, int y)
        {
            float worldX = (x + 0.5f) * tileSize.x;
            float worldZ = (y + 0.5f) * tileSize.y;
            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>
        /// Look up the prefab for a given room type from the inspector-assigned array.
        /// </summary>
        /// <param name="type">The room type to look up.</param>
        /// <returns>The GameObject prefab, or null if the array is misconfigured.</returns>
        private GameObject GetPrefabByType(RoomType type)
        {
            int index = (int)type;

            if (roomPrefabs == null || roomPrefabs.Length == 0)
            {
                Debug.LogError($"[FloorGenerator] roomPrefabs array is empty. Cannot get prefab for {type}.");
                return null;
            }

            if (index < 0 || index >= roomPrefabs.Length)
            {
                Debug.LogError($"[FloorGenerator] Room type index {index} ({type}) is out of range (array length {roomPrefabs.Length}).");
                return null;
            }

            return roomPrefabs[index];
        }

        /// <summary>
        /// Destroy all previously generated room instances and clear the grid.
        /// </summary>
        private void ClearGrid()
        {
            if (_grid != null)
            {
                for (int x = 0; x < _grid.GetLength(0); x++)
                {
                    for (int y = 0; y < _grid.GetLength(1); y++)
                    {
                        if (_grid[x, y] != null)
                        {
                            if (Application.isPlaying)
                                Destroy(_grid[x, y].gameObject);
                            else
                                DestroyImmediate(_grid[x, y].gameObject);
                        }
                    }
                }
            }

            _grid = null;

            // Also clean up any leftover children in the container.
            if (_roomContainer != null)
            {
                for (int i = _roomContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = _roomContainer.GetChild(i);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Draw the floor footprint.
            Gizmos.color = new Color(0.3f, 0.3f, 0.8f, 0.3f);
            Vector3 center = new Vector3(
                gridWidth * tileSize.x * 0.5f,
                0f,
                gridHeight * tileSize.y * 0.5f);
            Vector3 size = new Vector3(gridWidth * tileSize.x, 0.05f, gridHeight * tileSize.y);
            Gizmos.DrawCube(center, size);

            // Draw grid cell outlines.
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            for (int x = 0; x <= gridWidth; x++)
            {
                float worldX = x * tileSize.x;
                Vector3 start = new Vector3(worldX, 0f, 0f);
                Vector3 end = new Vector3(worldX, 0f, gridHeight * tileSize.y);
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= gridHeight; y++)
            {
                float worldZ = y * tileSize.y;
                Vector3 start = new Vector3(0f, 0f, worldZ);
                Vector3 end = new Vector3(gridWidth * tileSize.x, 0f, worldZ);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
