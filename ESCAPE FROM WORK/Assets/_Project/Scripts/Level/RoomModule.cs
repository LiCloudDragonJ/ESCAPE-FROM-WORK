using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Classification of room function within the office tower.
    /// Determines enemy density, loot distribution, and extraction rules.
    ///
    /// <para>Organised by spatial zone:
    ///   Public → SemiPublic → Workspace → Secure → Facility → Core</para>
    /// </summary>
    public enum RoomType
    {
        // ── Public zone (entry side, Y=72-80) ──────────────────────────
        /// <summary>Front desk / lobby — the player's entry point.</summary>
        Reception,

        /// <summary>Visitor waiting area — sofas, coffee tables, magazines.</summary>
        WaitingArea,

        // ── Semi-public zone (Y=60-72) ──────────────────────────────────
        /// <summary>Large meeting space — glass walls, conference table cover.</summary>
        ConferenceRoom,

        /// <summary>Break area with vending machines — moderate enemies, food/utility loot.</summary>
        TeaRoom,

        // ── Workspace zone (Y=28-60) ────────────────────────────────────
        /// <summary>Open-plan cubicle farm — desk sea with low partitions.</summary>
        OpenOffice,

        /// <summary>Single-person office — supervisor/manager desk + filing cabinet.</summary>
        PrivateOffice,

        /// <summary>Corner / large executive office — big desk, wine cabinet, safe.</summary>
        ExecutiveOffice,

        // ── Secure / back-of-house zone (Y=0-28) ────────────────────────
        /// <summary>IT infrastructure — server racks, UPS, patch panels. High-value electronic loot.</summary>
        ServerRoom,

        /// <summary>Dense filing-cabinet maze — HR/finance records.</summary>
        ArchiveRoom,

        /// <summary>General storage — random supplies, janitorial equipment.</summary>
        StorageRoom,

        /// <summary>High-security vault — top-tier loot, requires key/password.</summary>
        VaultRoom,

        // ── Facility / transit ──────────────────────────────────────────
        /// <summary>Fire-escape stairwell — may serve as extraction point.</summary>
        Stairwell,

        /// <summary>Elevator lobby — stable extraction point near core筒.</summary>
        ElevatorLobby,

        /// <summary>Corridor connecting rooms — low enemies, minimal loot.</summary>
        Hallway,

        /// <summary>Restroom — tight quarters, ambush risk.</summary>
        Restroom,

        // ── Core筒 interior (fixed per floor, no room allocation) ───────
        /// <summary>Core筒 internal staircase.</summary>
        CoreStairs,

        /// <summary>Core筒 elevator shaft.</summary>
        CoreElevator,

        /// <summary>Core筒 restroom.</summary>
        CoreRestroom,

        /// <summary>Core筒 mechanical / riser closet.</summary>
        CoreMechanical,

        // ── Legacy compatibility (map to new types) ─────────────────────
        /// <summary>[Legacy] Maps to <see cref="OpenOffice"/>.</summary>
        Office = OpenOffice,
    }

    /// <summary>
    /// Spatial zone within a floor. Determines the player's exploration
    /// progression from entry (Public) to deepest loot (Secure).
    /// </summary>
    public enum ZoneLevel
    {
        /// <summary>Entry area — reception, waiting.</summary>
        Public,

        /// <summary>Near-entry — meeting rooms, tea rooms.</summary>
        SemiPublic,

        /// <summary>Main floor body — open offices, private offices.</summary>
        Workspace,

        /// <summary>Deepest area — executive suites, vaults, server rooms.</summary>
        Secure,

        /// <summary>Fixed facility — stairs, elevators, restrooms, core筒.</summary>
        Facility,
    }

    /// <summary>
    /// Which side of a room the door is on.
    /// </summary>
    public enum DoorDirection
    {
        North,  // +Z wall
        South,  // -Z wall
        East,   // +X wall
        West,   // -X wall
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
