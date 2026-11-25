using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EscapeFromWork.LevelGen
{
    [System.Serializable]
    public class FloorMaterials
    {
        public Material Wall;
        public Material Floor;
        public Material Ceiling;
        public Material Column;
        public Material Core;
        public Material Stair;
        public Material PropLarge;
        public Material PropMedium;
        public Material PropSmall;
        public Material SightBreak;
        public Material LightPanel;
        public Material Extraction;
        public Material Spawn;
    }

    /// <summary>One axis-aligned box of greybox geometry (center + size, world space).</summary>
    public struct BoxSpec
    {
        public Vector3 Center;
        public Vector3 Size;

        public BoxSpec(Vector3 center, Vector3 size) { Center = center; Size = size; }
        public BoxSpec(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            Center = new Vector3(cx, cy, cz);
            Size = new Vector3(sx, sy, sz);
        }

        public float Volume { get { return Size.x * Size.y * Size.z; } }
    }

    /// <summary>
    /// Turns a FloorPlan into an enclosed 3D floor: slab, suspended ceiling, walls with
    /// door openings, columns, cover props, extraction pads, lights, plus a runtime NavMesh.
    /// All boxes are merged per material so one floor is a handful of draw calls.
    /// The Collect* helpers are the single source of truth for individual boxes; both the
    /// whole-floor bake and the chunked streaming build consume them.
    /// </summary>
    public static class FloorGeometry
    {
        public const float FloorToFloor = 4.2f;
        public const float CeilingY = 3.37f;
        public const float WallThickness = 0.2f;
        public const float DoorHeight = 2.1f;
        public const float EyeHeight = 1.62f;

        const float MinSegment = 0.12f;

        public class MeshAccum
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public void AddBox(Vector3 center, Vector3 size)
            {
                float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
                float y0 = center.y - size.y * 0.5f, y1 = center.y + size.y * 0.5f;
                float z0 = center.z - size.z * 0.5f, z1 = center.z + size.z * 0.5f;

                Face(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.back);
                Face(new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), Vector3.forward);
                Face(new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), Vector3.left);
                Face(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), Vector3.right);
                Face(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.up);
                Face(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down);
            }

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int i = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                for (int k = 0; k < 4; k++) Normals.Add(n);
                Uvs.Add(WorldUv(a, n)); Uvs.Add(WorldUv(b, n));
                Uvs.Add(WorldUv(c, n)); Uvs.Add(WorldUv(d, n));
                Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
                Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
            }

            /// <summary>
            /// Planar world-space UVs: one texture unit per metre, projected on the two
            /// axes that face the quad normal. Materials scale this with _BaseMap_ST, so
            /// a floor reads as 2 m carpet tiles and a wall as 1.5 m brush strokes
            /// without any per-face uv authoring.
            /// </summary>
            public static Vector2 WorldUv(Vector3 p, Vector3 n)
            {
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
                if (ax >= az) return new Vector2(p.z, p.y);
                return new Vector2(p.x, p.y);
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
                if (Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static GameObject Build(FloorPlan plan, FloorMaterials mats, Transform parent)
        {
            if (mats == null) mats = FallbackMaterials();

            GameObject root = new GameObject("Floor_" + plan.Spec.Seed);
            if (parent != null) root.transform.SetParent(parent, false);

            MeshAccum wall = new MeshAccum(), floor = new MeshAccum(), ceiling = new MeshAccum();
            MeshAccum column = new MeshAccum(), core = new MeshAccum(), stair = new MeshAccum();
            MeshAccum large = new MeshAccum(), medium = new MeshAccum(), small = new MeshAccum();
            MeshAccum sightBreak = new MeshAccum(), panel = new MeshAccum();
            MeshAccum extraction = new MeshAccum(), spawn = new MeshAccum();

            FloorSpec spec = plan.Spec;
            float width = plan.Bounds.W, depth = plan.Bounds.H;

            // ---- floor slab and suspended ceiling: one continuous plate over the whole
            // footprint. Per-room plates would leave 0.2 m slivers where rooms are inset
            // from their BSP leaf, and the sky shows through them.
            floor.AddBox(new Vector3(width * 0.5f, -0.1f, depth * 0.5f), new Vector3(width, 0.2f, depth));
            ceiling.AddBox(new Vector3(width * 0.5f, CeilingY + 0.03f, depth * 0.5f), new Vector3(width, 0.06f, depth));

            List<BoxSpec> boxes = new List<BoxSpec>();
            CollectPanelBoxes(plan, boxes);
            AddBoxes(panel, boxes);

            boxes.Clear();
            CollectStairBoxes(plan, boxes);
            AddBoxes(stair, boxes);

            // ---- outer shell and interior walls with door openings --------------------
            boxes.Clear();
            CollectShellBoxes(plan, boxes);
            CollectWallBoxes(plan, boxes);
            AddBoxes(wall, boxes);

            // ---- elevator bank inside the core (north strip, 4 m opening in the middle) ----
            boxes.Clear();
            CollectCoreBoxes(plan, boxes);
            AddBoxes(core, boxes);

            // ---- columns -------------------------------------------------------------
            boxes.Clear();
            CollectColumnBoxes(plan, boxes);
            AddBoxes(column, boxes);

            // ---- props ---------------------------------------------------------------
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                float h = p.Spec.Size == PropSize.Large ? 1.05f : p.Spec.Size == PropSize.Medium ? 0.75f : 0.5f;
                if (p.Spec.Name == "隔断墙")
                {
                    sightBreak.AddBox(Center(p.Rect, CeilingY * 0.5f), new Vector3(p.Rect.W, CeilingY, p.Rect.H));
                    continue;
                }
                Vector3 size = new Vector3(p.Rect.W, h, p.Rect.H);
                MeshAccum target = p.Spec.Size == PropSize.Large ? large : p.Spec.Size == PropSize.Medium ? medium : small;
                target.AddBox(Center(p.Rect, h * 0.5f), size);
            }

            // ---- extraction pads and spawn pads --------------------------------------
            for (int i = 0; i < plan.Extractions.Count; i++)
            {
                ExtractionPoint e = plan.Extractions[i];
                extraction.AddBox(new Vector3(e.Center.x, 0.03f, e.Center.y), new Vector3(1.4f, 0.06f, 1.4f));
                extraction.AddBox(new Vector3(e.Rect.X + 0.1f, 1.1f, e.Rect.Y + 0.1f), new Vector3(0.16f, 2.2f, 0.16f));
            }
            for (int i = 0; i < plan.SpawnZones.Count; i++)
            {
                RectF r = plan.SpawnZones[i];
                spawn.AddBox(new Vector3(r.Center.x, 0.03f, r.Center.y), new Vector3(r.W - 0.6f, 0.06f, r.H - 0.6f));
            }

            // ---- commit merged meshes ------------------------------------------------
            Emit(root, "Walls", wall, mats.Wall);
            Emit(root, "Floor", floor, mats.Floor);
            Emit(root, "Ceiling", ceiling, mats.Ceiling);
            Emit(root, "Columns", column, mats.Column);
            Emit(root, "Core", core, mats.Core);
            Emit(root, "Stairs", stair, mats.Stair);
            Emit(root, "Props_Large", large, mats.PropLarge);
            Emit(root, "Props_Medium", medium, mats.PropMedium);
            Emit(root, "Props_Small", small, mats.PropSmall);
            Emit(root, "SightBreaks", sightBreak, mats.SightBreak);
            Emit(root, "LightPanels", panel, mats.LightPanel);
            Emit(root, "Extractions", extraction, mats.Extraction);
            Emit(root, "SpawnPads", spawn, mats.Spawn);

            AddLights(root, plan);
            return root;
        }

        public static NavMeshData BuildNavMesh(FloorPlan plan)
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            for (int i = 0; i < plan.Rooms.Count; i++) AddSource(sources, plan.Rooms[i].Rect);
            for (int i = 0; i < plan.Corridors.Count; i++) AddSource(sources, plan.Corridors[i].Rect);
            for (int i = 0; i < plan.Stairs.Count; i++) AddSource(sources, plan.Stairs[i]);
            AddSource(sources, plan.Core);
            for (int i = 0; i < plan.SpawnZones.Count; i++) AddSource(sources, plan.SpawnZones[i]);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentSlope = 45f;
            settings.agentClimb = 0.4f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.15f;

            Bounds bounds = new Bounds(new Vector3(plan.Bounds.W * 0.5f, 0f, plan.Bounds.H * 0.5f),
                new Vector3(plan.Bounds.W + 4f, 8f, plan.Bounds.H + 4f));
            return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        }

        static void AddSource(List<NavMeshBuildSource> sources, RectF r)
        {
            if (r.W < 0.4f || r.H < 0.4f) return;
            NavMeshBuildSource s = new NavMeshBuildSource();
            s.shape = NavMeshBuildSourceShape.Box;
            s.size = new Vector3(r.W, 0.2f, r.H);
            s.transform = Matrix4x4.TRS(new Vector3(r.X + r.W * 0.5f, -0.1f, r.Y + r.H * 0.5f), Quaternion.identity, Vector3.one);
            s.area = 0;
            sources.Add(s);
        }

        // ---- geometry collectors (single source of truth for bake + streaming) ------
        public static void AddBoxes(MeshAccum acc, List<BoxSpec> boxes)
        {
            for (int i = 0; i < boxes.Count; i++) acc.AddBox(boxes[i].Center, boxes[i].Size);
        }

        /// <summary>Floor slab and ceiling plates for a horizontal rect (used by chunk builds).</summary>
        public static void AddSlabBoxes(MeshAccum floor, MeshAccum ceiling, RectF rect)
        {
            floor.AddBox(new Vector3(rect.Center.x, -0.1f, rect.Center.y), new Vector3(rect.W, 0.2f, rect.H));
            ceiling.AddBox(new Vector3(rect.Center.x, CeilingY + 0.03f, rect.Center.y), new Vector3(rect.W, 0.06f, rect.H));
        }

        /// <summary>Outer boundary walls (always loaded, they sit outside the chunk lattice).</summary>
        public static void CollectShellBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            float width = plan.Bounds.W, depth = plan.Bounds.H;
            AddAxisBox(boxes, 0f, -WallThickness * 0.5f, width, WallThickness, 0f, CeilingY, true);
            AddAxisBox(boxes, 0f, depth + WallThickness * 0.5f, width, WallThickness, 0f, CeilingY, true);
            AddAxisBox(boxes, -WallThickness * 0.5f, 0f, WallThickness, depth, 0f, CeilingY, false);
            AddAxisBox(boxes, width + WallThickness * 0.5f, 0f, WallThickness, depth, 0f, CeilingY, false);
        }

        /// <summary>
        /// Interior walls, generated from walkability rather than from room outlines.
        /// Rooms sit inset from their BSP leaf, so per-room edge walls left 0.2–0.4 m slivers
        /// between neighbours that showed straight into the unbuilt void. Building walls on the
        /// boundary between walkable and non-walkable cells seals every seam; a doorway is a
        /// hole in that boundary with a lintel over it, not a gap cut out of a long box.
        /// </summary>
        public static void CollectWallBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            const float cell = 0.25f;
            int cols = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.W / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.H / cell));
            bool[] walk = new bool[cols * rows];

            for (int i = 0; i < plan.Rooms.Count; i++) Stamp(walk, cols, rows, cell, plan.Rooms[i].Rect);
            for (int i = 0; i < plan.Corridors.Count; i++) Stamp(walk, cols, rows, cell, plan.Corridors[i].Rect);
            for (int i = 0; i < plan.Stairs.Count; i++) Stamp(walk, cols, rows, cell, plan.Stairs[i]);
            Stamp(walk, cols, rows, cell, plan.Core);
            for (int i = 0; i < plan.SpawnZones.Count; i++) Stamp(walk, cols, rows, cell, plan.SpawnZones[i]);

            List<Door> doors = CollectDoors(plan);
            for (int i = 0; i < doors.Count; i++) StampDoor(walk, cols, rows, cell, doors[i]);

            // Horizontal boundaries: a wall wherever the cells above and below disagree.
            for (int j = 1; j < rows; j++)
            {
                int start = -1;
                for (int i = 0; i <= cols; i++)
                {
                    bool solid = i < cols && walk[i + (j - 1) * cols] != walk[i + j * cols];
                    if (solid && start < 0) start = i;
                    else if (!solid && start >= 0)
                    {
                        AddWallRun(boxes, true, start * cell, i * cell, j * cell);
                        start = -1;
                    }
                }
            }

            // Vertical boundaries.
            for (int i = 1; i < cols; i++)
            {
                int start = -1;
                for (int j = 0; j <= rows; j++)
                {
                    bool solid = j < rows && walk[(i - 1) + j * cols] != walk[i + j * cols];
                    if (solid && start < 0) start = j;
                    else if (!solid && start >= 0)
                    {
                        AddWallRun(boxes, false, start * cell, j * cell, i * cell);
                        start = -1;
                    }
                }
            }

            // Lintel over every doorway so an opening is 2.1 m tall instead of floor to ceiling.
            for (int i = 0; i < doors.Count; i++)
            {
                Door d = doors[i];
                bool horizontal = d.Side == DoorSide.North || d.Side == DoorSide.South;
                float width = Mathf.Max(0.6f, d.Width);
                float y = (DoorHeight + CeilingY) * 0.5f;
                boxes.Add(horizontal
                    ? new BoxSpec(d.Center.x, y, d.Center.y, width, CeilingY - DoorHeight, WallThickness)
                    : new BoxSpec(d.Center.x, y, d.Center.y, WallThickness, CeilingY - DoorHeight, width));
            }
        }

        static void AddWallRun(List<BoxSpec> boxes, bool horizontal, float a, float b, float line)
        {
            float len = b - a;
            if (len < 0.05f) return;
            float mid = (a + b) * 0.5f;
            boxes.Add(horizontal
                ? new BoxSpec(mid, CeilingY * 0.5f, line, len + 0.02f, CeilingY, WallThickness)
                : new BoxSpec(line, CeilingY * 0.5f, mid, WallThickness, CeilingY, len + 0.02f));
        }

        static void Stamp(bool[] walk, int cols, int rows, float cell, RectF r)
        {
            if (r.W <= 0.05f || r.H <= 0.05f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.X / cell));
            int x1 = Mathf.Min(cols - 1, Mathf.FloorToInt((r.XMax - 0.001f) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.Y / cell));
            int y1 = Mathf.Min(rows - 1, Mathf.FloorToInt((r.YMax - 0.001f) / cell));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) walk[x + y * cols] = true;
        }

        static void StampDoor(bool[] walk, int cols, int rows, float cell, Door d)
        {
            bool horizontal = d.Side == DoorSide.North || d.Side == DoorSide.South;
            float halfWidth = Mathf.Max(0.55f, d.Width * 0.5f + 0.08f);
            float halfDepth = 0.7f; // bridges the inset on both sides of the wall line
            RectF area = horizontal
                ? new RectF(d.Center.x - halfWidth, d.Center.y - halfDepth, halfWidth * 2f, halfDepth * 2f)
                : new RectF(d.Center.x - halfDepth, d.Center.y - halfWidth, halfDepth * 2f, halfWidth * 2f);
            Stamp(walk, cols, rows, cell, area);
        }

        /// <summary>Elevator bank strip inside the core (north side, 4 m opening).</summary>
        public static void CollectCoreBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            RectF coreRect = plan.Core;
            float bankDepth = 1.4f;
            float bankZ = coreRect.YMax - bankDepth * 0.5f;
            float bankHalf = (coreRect.W - 4f) * 0.5f;
            boxes.Add(new BoxSpec(coreRect.X + bankHalf * 0.5f, CeilingY * 0.5f, bankZ,
                bankHalf, CeilingY, bankDepth));
            boxes.Add(new BoxSpec(coreRect.XMax - bankHalf * 0.5f, CeilingY * 0.5f, bankZ,
                bankHalf, CeilingY, bankDepth));
        }

        public static void CollectColumnBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            for (int i = 0; i < plan.Columns.Count; i++)
            {
                Vector2 c = plan.Columns[i];
                boxes.Add(new BoxSpec(c.x, CeilingY * 0.5f, c.y, 0.6f, CeilingY, 0.6f));
            }
        }

        public static void CollectStairBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            for (int i = 0; i < plan.Stairs.Count; i++)
            {
                RectF r = plan.Stairs[i];
                boxes.Add(new BoxSpec(Center(r, 0.02f), new Vector3(r.W - 0.4f, 0.04f, r.H - 0.4f)));
            }
        }

        public static void CollectPanelBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                boxes.Add(new BoxSpec(r.Center.x, CeilingY - 0.03f, r.Center.y, 1.2f, 0.04f, 0.6f));
            }
        }

        /// <summary>All candidate light positions before the global light budget is applied.</summary>
        public static void CollectLightPositions(FloorPlan plan, List<Vector2> positions)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                if (r.Area >= 40f) positions.Add(r.Center);
            }
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF c = plan.Corridors[i].Rect;
                bool horizontal = c.W >= c.H;
                float length = Mathf.Max(c.W, c.H);
                int count = Mathf.Max(1, Mathf.RoundToInt(length / 13f));
                for (int k = 0; k < count; k++)
                {
                    float t = count == 1 ? 0.5f : (k + 0.5f) / count;
                    if (horizontal) positions.Add(new Vector2(c.X + c.W * t, c.Y + c.H * 0.5f));
                    else positions.Add(new Vector2(c.X + c.W * 0.5f, c.Y + c.H * t));
                }
            }
            for (int i = 0; i < plan.SpawnZones.Count; i++) positions.Add(plan.SpawnZones[i].Center);
        }

        /// <summary>Global light budget (per-floor) sampled evenly over every candidate position.</summary>
        public const int LightBudget = 48;

        public static List<Vector2> SampleLights(List<Vector2> positions, int budget)
        {
            List<Vector2> result = new List<Vector2>();
            int step = Mathf.Max(1, positions.Count / Mathf.Max(1, budget));
            for (int i = 0, n = 0; i < positions.Count && n < budget; i += step, n++) result.Add(positions[i]);
            return result;
        }

        // ---- walls with door openings ----------------------------------------------
        static void AddSegment(List<BoxSpec> boxes, bool horizontal, float from, float to, RectF edge)
        {
            float mid = (from + to) * 0.5f;
            if (horizontal) AddAxisBox(boxes, mid - (to - from) * 0.5f, edge.Y, to - from, WallThickness, 0f, CeilingY, true, edge.Y);
            else AddAxisBox(boxes, edge.X, mid - (to - from) * 0.5f, WallThickness, to - from, 0f, CeilingY, false, edge.X);
        }

        static void AddAxisBox(List<BoxSpec> boxes, float x, float z, float width, float depth, float y0, float y1, bool horizontal)
        {
            boxes.Add(new BoxSpec(x + width * 0.5f, (y0 + y1) * 0.5f, z + depth * 0.5f, width, y1 - y0, depth));
        }

        static void AddAxisBox(List<BoxSpec> boxes, float x, float z, float width, float depth, float y0, float y1, bool horizontal, float line)
        {
            float cx = x + width * 0.5f;
            float cz = z + depth * 0.5f;
            if (horizontal) cz = line;
            else cx = line;
            boxes.Add(new BoxSpec(cx, (y0 + y1) * 0.5f, cz, width, y1 - y0, depth));
        }

        static void AddEdge(Dictionary<string, RectF> edges, float x0, float y0, float x1, float y1)
        {
            bool horizontal = Mathf.Abs(y1 - y0) < 0.01f;
            string key = horizontal
                ? "H:" + Mathf.RoundToInt(y0 * 10f) + ":" + Mathf.RoundToInt(Mathf.Min(x0, x1) * 10f) + ":" + Mathf.RoundToInt(Mathf.Max(x0, x1) * 10f)
                : "V:" + Mathf.RoundToInt(x0 * 10f) + ":" + Mathf.RoundToInt(Mathf.Min(y0, y1) * 10f) + ":" + Mathf.RoundToInt(Mathf.Max(y0, y1) * 10f);
            if (edges.ContainsKey(key)) return;
            edges[key] = horizontal
                ? new RectF(Mathf.Min(x0, x1), y0, Mathf.Abs(x1 - x0), 0.01f)
                : new RectF(x0, Mathf.Min(y0, y1), 0.01f, Mathf.Abs(y1 - y0));
        }

        static List<Door> CollectDoors(FloorPlan plan)
        {
            List<Door> doors = new List<Door>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor) Push(doors, seen, room.Door);
                if (room.ExtraDoors != null)
                    for (int j = 0; j < room.ExtraDoors.Count; j++) Push(doors, seen, room.ExtraDoors[j]);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) Push(doors, seen, plan.LoopDoors[i]);
            return doors;
        }

        static void Push(List<Door> doors, HashSet<string> seen, Door d)
        {
            if (d.Width <= 0.05f) return;
            string key = Mathf.RoundToInt(d.Center.x * 10f) + ":" + Mathf.RoundToInt(d.Center.y * 10f) + ":" + (int)d.Side;
            if (!seen.Add(key)) return;
            doors.Add(d);
        }

        static void AddLights(GameObject root, FloorPlan plan)
        {
            GameObject lights = new GameObject("Lights");
            lights.transform.SetParent(root.transform, false);

            List<Vector2> candidates = new List<Vector2>();
            CollectLightPositions(plan, candidates);
            List<Vector2> positions = SampleLights(candidates, LightBudget);

            for (int i = 0; i < positions.Count; i++)
            {
                GameObject go = new GameObject("Light_" + i);
                go.transform.SetParent(lights.transform, false);
                go.transform.position = new Vector3(positions[i].x, CeilingY - 0.3f, positions[i].y);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 18f;
                l.intensity = 9f;
                l.color = new Color(1f, 0.95f, 0.87f);
                l.shadows = LightShadows.None;
            }
        }

        public static void Emit(GameObject root, string name, MeshAccum acc, Material material, bool collider = true)
        {
            if (acc.Vertices.Count == 0 || material == null) return;
            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = acc.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
            {
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            go.isStatic = true;
        }

        public static Vector3 Center(RectF r, float y)
        {
            return new Vector3(r.X + r.W * 0.5f, y, r.Y + r.H * 0.5f);
        }

        public static FloorMaterials FallbackMaterials()
        {
            if (_fallback != null) return _fallback;
            _fallback = MakeFallbackMaterials();
            return _fallback;
        }

        static FloorMaterials _fallback;

        static FloorMaterials MakeFallbackMaterials()
        {
            FloorMaterials mats = new FloorMaterials();
            mats.Wall = MakeMaterial("Wall", new Color(0.78f, 0.77f, 0.74f));
            mats.Floor = MakeMaterial("Floor", new Color(0.46f, 0.47f, 0.50f));
            mats.Ceiling = MakeMaterial("Ceiling", new Color(0.86f, 0.86f, 0.84f));
            mats.Column = MakeMaterial("Column", new Color(0.70f, 0.70f, 0.68f));
            mats.Core = MakeMaterial("Core", new Color(0.55f, 0.57f, 0.60f));
            mats.Stair = MakeMaterial("Stair", new Color(0.38f, 0.40f, 0.43f));
            mats.PropLarge = MakeMaterial("PropLarge", new Color(0.55f, 0.42f, 0.30f));
            mats.PropMedium = MakeMaterial("PropMedium", new Color(0.62f, 0.55f, 0.46f));
            mats.PropSmall = MakeMaterial("PropSmall", new Color(0.45f, 0.55f, 0.52f));
            mats.SightBreak = MakeMaterial("SightBreak", new Color(0.72f, 0.50f, 0.38f));
            mats.LightPanel = MakeMaterial("LightPanel", new Color(0.95f, 0.93f, 0.86f));
            mats.Extraction = MakeMaterial("Extraction", new Color(0.95f, 0.65f, 0.25f));
            mats.Spawn = MakeMaterial("Spawn", new Color(0.35f, 0.65f, 0.45f));
            return mats;
        }

        static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            return m;
        }
    }
}
