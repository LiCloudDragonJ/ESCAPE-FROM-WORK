using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Classification of room function within the office tower.
    /// Determines enemy density, loot distribution, and extraction rules.
    /// </summary>
    public enum RoomType
    {
        /// <summary>Cubicle farm — high enemy count, moderate loot.</summary>
        Office,

        /// <summary>Corridor connecting rooms — low enemies, minimal loot.</summary>
        Hallway,

        /// <summary>Break area with vending machines — moderate enemies, food/utility loot.</summary>
        TeaRoom,

        /// <summary>Vertical transit between floors — extraction point for normal exit.</summary>
        Stairwell,

        /// <summary>Large meeting space — low enemies, high-value executive loot.</summary>
        ConferenceRoom,

        /// <summary>IT infrastructure room — high-value electronic loot, moderate enemies.</summary>
        ServerRoom
    }

    /// <summary>
    /// Represents a single room tile on a procedurally generated floor.
    /// Stores grid position, connection flags to adjacent rooms, and
    /// spawn-point transforms used by the enemy and loot systems.
    ///
    /// <para>Attached to each room prefab instance by <see cref="FloorGenerator"/>.
    /// Prefabs are created in the Unity Editor; this component exposes
    /// the data the generation system needs to connect rooms.</para>
    /// </summary>
    public class RoomModule : MonoBehaviour
    {
        // ---- Room identity -------------------------------------------------------

        /// <summary>Functional type of this room.</summary>
        public RoomType roomType;

        /// <summary>Grid coordinate of this room (X = column, Y = row).</summary>
        public Vector2Int gridPosition;

        /// <summary>
        /// Footprint of this room in grid cells. Defaults to 1x1.
        /// Multi-cell rooms (e.g. 2x2 Conference) occupy a rectangular region.
        /// </summary>
        public Vector2Int gridSize = Vector2Int.one;

        // ---- Extraction ----------------------------------------------------------

        /// <summary>
        /// When true, this room serves as an extraction point.
        /// Fire-escape stairwells have this set; entering triggers an extraction.
        /// </summary>
        public bool isExtractionPoint;

        // ---- Connection flags ----------------------------------------------------

        /// <summary>True when a room exists in the grid cell above this one (positive Y).</summary>
        public bool connectionNorth;

        /// <summary>True when a room exists in the grid cell below this one (negative Y).</summary>
        public bool connectionSouth;

        /// <summary>True when a room exists in the grid cell to the right (positive X).</summary>
        public bool connectionEast;

        /// <summary>True when a room exists in the grid cell to the left (negative X).</summary>
        public bool connectionWest;

        // ---- Spawn-point transforms ----------------------------------------------

        /// <summary>
        /// Child Transforms marking where loot containers should be placed.
        /// Assigned in the prefab; <see cref="FloorGenerator"/> reads these
        /// and passes them to the loot spawning system.
        /// </summary>
        public Transform[] lootContainerSpawns;

        /// <summary>
        /// Child Transforms marking where enemies may spawn in this room.
        /// Assigned in the prefab; <see cref="Enemies.EnemySpawner"/> uses
        /// a random subset of these when populating the floor.
        /// </summary>
        public Transform[] enemySpawnZones;

        // ---- Editor helpers ------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Draw the connection direction arrows and grid bounds in the Scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            // Connection arrows.
            Gizmos.color = Color.green;
            if (connectionNorth) DrawArrow(center, Vector3.forward * 2f);
            if (connectionSouth) DrawArrow(center, Vector3.back * 2f);
            if (connectionEast)  DrawArrow(center, Vector3.right * 2f);
            if (connectionWest)  DrawArrow(center, Vector3.left * 2f);

            // Grid footprint.
            Gizmos.color = isExtractionPoint ? Color.red : Color.cyan;
            Vector3 size = new Vector3(gridSize.x * 0.9f, 0.1f, gridSize.y * 0.9f);
            Gizmos.DrawWireCube(center + Vector3.up * 0.05f, size);

            // Spawn zones.
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            if (enemySpawnZones != null)
            {
                foreach (Transform t in enemySpawnZones)
                {
                    if (t != null)
                        Gizmos.DrawSphere(t.position, 0.3f);
                }
            }
        }

        private static void DrawArrow(Vector3 origin, Vector3 offset)
        {
            Vector3 tip = origin + offset;
            Gizmos.DrawLine(origin, tip);
            Vector3 dir = offset.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.3f;
            Gizmos.DrawLine(tip, tip - dir * 0.5f + right);
            Gizmos.DrawLine(tip, tip - dir * 0.5f - right);
        }
#endif
    }
}
