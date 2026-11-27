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

    /// <summary>
    /// One box of greybox geometry (center + size, world space). Yaw rotates it about +Y so a
    /// 45° chamfer wall can be a box instead of a hand-built triangle; 0 means axis-aligned and
    /// every legacy caller keeps working unchanged.
    /// </summary>
    public struct BoxSpec
    {
        public Vector3 Center;
        public Vector3 Size;
        /// <summary>Rotation about +Y in radians (local +X is the long axis).</summary>
        public float Yaw;

        public BoxSpec(Vector3 center, Vector3 size) { Center = center; Size = size; Yaw = 0f; }
        public BoxSpec(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            Center = new Vector3(cx, cy, cz);
            Size = new Vector3(sx, sy, sz);
            Yaw = 0f;
        }

        public BoxSpec(Vector3 center, Vector3 size, float yaw)
        {
            Center = center;
            Size = size;
            Yaw = yaw;
        }

        public float Volume { get { return Size.x * Size.y * Size.z; } }

        /// <summary>XZ footprint of a possibly rotated box (AABB, conservative).</summary>
        public RectF Footprint
        {
            get
            {
                if (Mathf.Abs(Yaw) < 0.0001f)
                    return new RectF(Center.x - Size.x * 0.5f, Center.z - Size.z * 0.5f, Size.x, Size.z);
                float c = Mathf.Abs(Mathf.Cos(Yaw)), s = Mathf.Abs(Mathf.Sin(Yaw));
                float w = Size.x * c + Size.z * s;
                float d = Size.x * s + Size.z * c;
                return new RectF(Center.x - w * 0.5f, Center.z - d * 0.5f, w, d);
            }
        }
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

            public void AddBox(Vector3 center, Vector3 size) { AddBoxRotated(center, size, 0f); }

            /// <summary>
            /// Axis box with a rotation about +Y (chamfer walls). Rotation is orientation
            /// preserving, so the same clockwise winding stays correct as seen from outside.
            /// </summary>
            public void AddBoxRotated(Vector3 center, Vector3 size, float yawRadians)
            {
                float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
                float y0 = center.y - size.y * 0.5f, y1 = center.y + size.y * 0.5f;
                float z0 = center.z - size.z * 0.5f, z1 = center.z + size.z * 0.5f;

                // Winding must be clockwise as seen from OUTSIDE: with the old order the four
                // side faces pointed inward, so a box only showed its far (inner) faces —
                // the "only the left and back faces exist" artifact.
                Face(Rp(x0, y1, z1, center, yawRadians), Rp(x0, y0, z1, center, yawRadians),
                     Rp(x1, y0, z1, center, yawRadians), Rp(x1, y1, z1, center, yawRadians), Rn(0f, 0f, 1f, yawRadians));
                Face(Rp(x1, y1, z0, center, yawRadians), Rp(x1, y0, z0, center, yawRadians),
                     Rp(x0, y0, z0, center, yawRadians), Rp(x0, y1, z0, center, yawRadians), Rn(0f, 0f, -1f, yawRadians));
                Face(Rp(x1, y1, z1, center, yawRadians), Rp(x1, y0, z1, center, yawRadians),
                     Rp(x1, y0, z0, center, yawRadians), Rp(x1, y1, z0, center, yawRadians), Rn(1f, 0f, 0f, yawRadians));
                Face(Rp(x0, y1, z0, center, yawRadians), Rp(x0, y0, z0, center, yawRadians),
                     Rp(x0, y0, z1, center, yawRadians), Rp(x0, y1, z1, center, yawRadians), Rn(-1f, 0f, 0f, yawRadians));
                Face(Rp(x0, y1, z1, center, yawRadians), Rp(x1, y1, z1, center, yawRadians),
                     Rp(x1, y1, z0, center, yawRadians), Rp(x0, y1, z0, center, yawRadians), Rn(0f, 1f, 0f, yawRadians));
                Face(Rp(x0, y0, z0, center, yawRadians), Rp(x1, y0, z0, center, yawRadians),
                     Rp(x1, y0, z1, center, yawRadians), Rp(x0, y0, z1, center, yawRadians), Rn(0f, -1f, 0f, yawRadians));
            }

            static Vector3 Rp(float x, float y, float z, Vector3 center, float yawRadians)
            {
                if (Mathf.Abs(yawRadians) < 0.00001f) return new Vector3(x, y, z);
                float c = Mathf.Cos(yawRadians), s = Mathf.Sin(yawRadians);
                float dx = x - center.x, dz = z - center.z;
                return new Vector3(center.x + dx * c + dz * s, y, center.z - dx * s + dz * c);
            }

            static Vector3 Rn(float x, float y, float z, float yawRadians)
            {
                if (Mathf.Abs(yawRadians) < 0.00001f) return new Vector3(x, y, z);
                float c = Mathf.Cos(yawRadians), s = Mathf.Sin(yawRadians);
                return new Vector3(x * c + z * s, y, -x * s + z * c);
            }

            /// <summary>
            /// One triangle with an explicit facing. The vertex order is corrected against the
            /// normal before it is stored, so a polygon triangulation can never reintroduce the
            /// "back faces only" bug by getting its winding backwards.
            /// </summary>
            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f)
                {
                    Vector3 swap = b; b = c; c = swap;
                }
                int i = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Normals.Add(n); Normals.Add(n); Normals.Add(n);
                Uvs.Add(WorldUv(a, n)); Uvs.Add(WorldUv(b, n)); Uvs.Add(WorldUv(c, n));
                Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            }

            /// <summary>
            /// Extrude a floor outline into a slab (top face up, bottom face down, side faces
            /// outward). Used for the floor plate and the suspended ceiling, which now follow the
            /// non-rectangular building envelope instead of a bounding rectangle.
            /// </summary>
            public void AddPrism(Vector2[] outline, List<int> triangleIndices, float y0, float y1)
            {
                if (outline == null || outline.Length < 3) return;
                for (int i = 0; i + 2 < triangleIndices.Count; i += 3)
                {
                    Vector2 a = outline[triangleIndices[i]];
                    Vector2 b = outline[triangleIndices[i + 1]];
                    Vector2 c = outline[triangleIndices[i + 2]];
                    AddTriangle(new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), new Vector3(c.x, y1, c.y), Vector3.up);
                    AddTriangle(new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), new Vector3(c.x, y0, c.y), Vector3.down);
                }
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i];
                    Vector2 b = outline[(i + 1) % outline.Length];
                    Vector2 d = b - a;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    Vector2 outward = new Vector2(d.y, -d.x).normalized;
                    Vector3 n = new Vector3(outward.x, 0f, outward.y);
                    Vector3 p0 = new Vector3(a.x, y0, a.y), p1 = new Vector3(b.x, y0, b.y);
                    Vector3 p2 = new Vector3(b.x, y1, b.y), p3 = new Vector3(a.x, y1, a.y);
                    AddTriangle(p0, p1, p2, n);
                    AddTriangle(p0, p2, p3, n);
                }
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

        /// <summary>
        /// Floor plate + suspended ceiling. Both follow the building envelope when there is one,
        /// so the plate has chamfered corners and edge notches instead of being a rectangle.
        /// Shared by the one-shot bake and the streamed always-loaded shell.
        /// </summary>
        public static void AddPlateBoxes(FloorPlan plan, MeshAccum floor, MeshAccum ceiling)
        {
            if (plan.Envelope != null && plan.Envelope.Outline != null && plan.Envelope.Outline.Length >= 3)
            {
                List<int> slabTris = FloorEnvelope.Triangulate(plan.Envelope.Outline);
                floor.AddPrism(plan.Envelope.Outline, slabTris, -0.2f, 0f);
                ceiling.AddPrism(plan.Envelope.Outline, slabTris, CeilingY, CeilingY + 0.06f);
                return;
            }
            float width = plan.Bounds.W, depth = plan.Bounds.H;
            floor.AddBox(new Vector3(width * 0.5f, -0.1f, depth * 0.5f), new Vector3(width, 0.2f, depth));
            ceiling.AddBox(new Vector3(width * 0.5f, CeilingY + 0.03f, depth * 0.5f), new Vector3(width, 0.06f, depth));
        }

        public static GameObject Build(FloorPlan plan, FloorMaterials mats, Transform parent)
        {
            return Build(plan, mats, parent, null);
        }

        /// <summary>Single-shot build; prop names present in the catalog become prefab instances.</summary>
        public static GameObject Build(FloorPlan plan, FloorMaterials mats, Transform parent, PropCatalog catalog)
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
            // The plate follows the building envelope: a chamfered / notched outline instead of
            // the bounding rectangle, so the floor reads as a non-rectangular plate in space.
            AddPlateBoxes(plan, floor, ceiling);

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

            // ---- solid mass in the non-walkable parts of the footprint ----------------
            boxes.Clear();
            CollectVoidBoxes(plan, boxes);
            MeshAccum solidMass = new MeshAccum();
            AddBoxes(solidMass, boxes);

            // ---- 45° chamfer walls closing the cut room corners ----------------------
            boxes.Clear();
            CollectChamferBoxes(plan, boxes);
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
                float fallback = p.Spec.Size == PropSize.Large ? 1.05f : p.Spec.Size == PropSize.Medium ? 0.75f : 0.5f;
                float h = p.Spec.Height > 0.01f ? p.Spec.Height : fallback;
                if (p.Spec.Name == "隔断墙")
                {
                    sightBreak.AddBox(Center(p.Rect, CeilingY * 0.5f), new Vector3(p.Rect.W, CeilingY, p.Rect.H));
                    continue;
                }
                if (PropSpawner.TrySpawn(catalog, p, root.transform)) continue;
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
            Emit(root, "SolidMass", solidMass, mats.Wall);

            AddLights(root, plan);
            SignageRenderer.Build(plan, plan.Signs, root.transform);
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
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec b = boxes[i];
                acc.AddBoxRotated(b.Center, b.Size, b.Yaw);
            }
        }

        /// <summary>Floor slab and ceiling plates for a horizontal rect (used by chunk builds).</summary>
        public static void AddSlabBoxes(MeshAccum floor, MeshAccum ceiling, RectF rect)
        {
            floor.AddBox(new Vector3(rect.Center.x, -0.1f, rect.Center.y), new Vector3(rect.W, 0.2f, rect.H));
            ceiling.AddBox(new Vector3(rect.Center.x, CeilingY + 0.03f, rect.Center.y), new Vector3(rect.W, 0.06f, rect.H));
        }

        /// <summary>
        /// Solid structural mass filling every part of the footprint that is not walkable
        /// (shafts, plant rooms, the gaps between BSP rectangles). Without it the plan's holes
        /// look like open hall with free-standing wall fragments standing in the dark.
        /// </summary>
        public static void CollectVoidBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            const float cell = 0.2f;
            float width = plan.Bounds.W, depth = plan.Bounds.H;
            int cols = Mathf.Max(1, Mathf.CeilToInt(width / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(depth / cell));

            // The walkable mask is rasterised straight from the rects. Anything that is not inside
            // a room, a corridor, a stair, the core or a spawn pad becomes solid mass, so a crack
            // left by the BSP layout can never stay an unwalled floor slot you can walk into and
            // get stuck in (the "无意义空腔 / 死胡同" report). Rasterising keeps this O(area) instead
            // of O(cells × rects); at 0.2 m the leftover sliver at a rect border is invisible.
            bool[] walkable = new bool[cols * rows];
            for (int i = 0; i < plan.Rooms.Count; i++) Rasterise(walkable, cols, rows, cell, plan.Rooms[i].Rect, 0f);
            for (int i = 0; i < plan.Corridors.Count; i++) Rasterise(walkable, cols, rows, cell, plan.Corridors[i].Rect, 0f);
            for (int i = 0; i < plan.Stairs.Count; i++) Rasterise(walkable, cols, rows, cell, plan.Stairs[i], 0f);
            if (plan.Core.W > 0f) Rasterise(walkable, cols, rows, cell, plan.Core, 0f);
            for (int i = 0; i < plan.SpawnZones.Count; i++) Rasterise(walkable, cols, rows, cell, plan.SpawnZones[i], 0f);

            // Outside the envelope is open air, not hidden structure: only fill cells that are
            // actually inside the floor plate, otherwise the notches get bricked back up.
            bool[] inside = new bool[cols * rows];
            if (plan.Envelope != null && plan.Envelope.Pieces != null)
            {
                for (int k = 0; k < plan.Envelope.Pieces.Count; k++)
                {
                    Vector2[] piece = plan.Envelope.Pieces[k];
                    RectF pb = FloorEnvelope.BoundsOf(piece);
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(pb.X / cell));
                    int x1 = Mathf.Min(cols, Mathf.CeilToInt(pb.XMax / cell));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(pb.Y / cell));
                    int y1 = Mathf.Min(rows, Mathf.CeilToInt(pb.YMax / cell));
                    for (int y = y0; y < y1; y++)
                    {
                        for (int x = x0; x < x1; x++)
                        {
                            Vector2 c = new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
                            if (FloorEnvelope.Contains(piece, c)) inside[y * cols + x] = true;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < inside.Length; i++) inside[i] = true;
            }

            bool[] solid = new bool[cols * rows];
            for (int i = 0; i < solid.Length; i++) solid[i] = inside[i] && !walkable[i];

            // Merge equal horizontal runs downwards: a shaft becomes a few tall boxes.
            Dictionary<long, int[]> open = new Dictionary<long, int[]>(256);
            for (int j = 0; j < rows; j++)
            {
                HashSet<long> seen = new HashSet<long>();
                int i2 = 0;
                while (i2 < cols)
                {
                    if (!solid[j * cols + i2]) { i2++; continue; }
                    int x0 = i2;
                    while (i2 < cols && solid[j * cols + i2]) i2++;
                    long key = ((long)x0 << 20) | (long)i2;
                    seen.Add(key);
                    int[] span;
                    if (open.TryGetValue(key, out span)) span[2] = j + 1;
                    else open[key] = new int[] { x0, i2, j, j + 1 };
                }
                List<long> finished = new List<long>();
                foreach (KeyValuePair<long, int[]> pair in open)
                {
                    if (seen.Contains(pair.Key)) continue;
                    finished.Add(pair.Key);
                    EmitVoid(boxes, pair.Value, cell, width, depth);
                }
                for (int k = 0; k < finished.Count; k++) open.Remove(finished[k]);
            }
            foreach (KeyValuePair<long, int[]> pair in open) EmitVoid(boxes, pair.Value, cell, width, depth);
        }

        static void Rasterise(bool[] grid, int cols, int rows, float cell, RectF rect, float inflate)
        {
            RectF r = inflate > 0f ? rect.Inflate(inflate) : rect;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.X / cell));
            int x1 = Mathf.Min(cols, Mathf.CeilToInt(r.XMax / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.Y / cell));
            int y1 = Mathf.Min(rows, Mathf.CeilToInt(r.YMax / cell));
            for (int y = y0; y < y1; y++)
            {
                float cz = (y + 0.5f) * cell;
                if (cz < r.Y || cz > r.YMax) continue;
                for (int x = x0; x < x1; x++)
                {
                    float cx = (x + 0.5f) * cell;
                    if (cx < r.X || cx > r.XMax) continue;
                    grid[y * cols + x] = true;
                }
            }
        }

        static void EmitVoid(List<BoxSpec> boxes, int[] span, float cell, float width, float depth)
        {
            float x0 = span[0] * cell;
            float x1 = Mathf.Min(span[1] * cell, width);
            float z0 = span[2] * cell;
            float z1 = Mathf.Min(span[3] * cell, depth);
            if (x1 - x0 < 0.05f || z1 - z0 < 0.05f) return;
            boxes.Add(new BoxSpec((x0 + x1) * 0.5f, CeilingY * 0.5f, (z0 + z1) * 0.5f, x1 - x0, CeilingY, z1 - z0));
        }

        /// <summary>Outer boundary walls (always loaded, they sit outside the chunk lattice).</summary>
        public static void CollectShellBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            float body = WallBodyThickness(plan);
            if (plan.Envelope != null && plan.Envelope.Outline != null)
            {
                // Exterior walls follow every outline edge, including the 45° chamfers and the
                // sides of the notches. Each edge becomes one rotated box centered on the edge.
                Vector2[] outline = plan.Envelope.Outline;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i];
                    Vector2 b = outline[(i + 1) % outline.Length];
                    Vector2 d = b - a;
                    float len = d.magnitude;
                    if (len < 0.05f) continue;
                    float yaw = Mathf.Atan2(-(b.y - a.y), b.x - a.x);
                    Vector2 mid = (a + b) * 0.5f;
                    boxes.Add(new BoxSpec(new Vector3(mid.x, CeilingY * 0.5f, mid.y),
                        new Vector3(len + body, CeilingY, body), yaw));
                }
                return;
            }

            float width = plan.Bounds.W, depth = plan.Bounds.H;
            // Centred on the footprint edge so the wall body also covers the WallInset ring on the
            // inside; the outer face sticks out by half the body, which nobody sees.
            float half = body * 0.5f;
            AddAxisBox(boxes, 0f, -half, width, body, 0f, CeilingY, true);
            AddAxisBox(boxes, 0f, depth - half, width, body, 0f, CeilingY, true);
            AddAxisBox(boxes, -half, 0f, body, depth, 0f, CeilingY, false);
            AddAxisBox(boxes, width - half, 0f, body, depth, 0f, CeilingY, false);
        }

        /// <summary>
        /// Interior walls merged from rectangle outlines. Every room outline is inflated by
        /// WallInset so it lands exactly on its BSP leaf edge: two neighbouring rooms then share
        /// one boundary line, and the shared run merges into a single 0.2 m partition instead of
        /// two leaves 0.4 m apart with a hollow between them (the "floating L-shaped paper wall"
        /// look, and the reason you could stare into the void through the seam).
        /// Doorways are subtracted from the merged runs and capped by a lintel, so an opening is
        /// a real 2.1 m hole in a continuous wall and never a floating sliver.
        /// </summary>
        public static void CollectWallBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            float inset = Mathf.Max(0f, plan.Spec.WallInset);
            Dictionary<int, List<Vector2>> horizontal = new Dictionary<int, List<Vector2>>();
            Dictionary<int, List<Vector2>> vertical = new Dictionary<int, List<Vector2>>();

            for (int i = 0; i < plan.Rooms.Count; i++) AddEdges(horizontal, vertical, plan.Rooms[i].Rect.Inflate(inset));
            for (int i = 0; i < plan.Corridors.Count; i++) AddEdges(horizontal, vertical, plan.Corridors[i].Rect);
            for (int i = 0; i < plan.Stairs.Count; i++) AddEdges(horizontal, vertical, plan.Stairs[i]);
            AddEdges(horizontal, vertical, plan.Core);
            // 出生区只是地面标记（10×10 地贴），不是房间：给它砌墙会在房间中间凭空冒出一个封闭盒子

            CombineAll(horizontal);
            CombineAll(vertical);

            // Rooms are inset by WallInset from their BSP leaf while the wall centreline sits on
            // the leaf edge, so a 0.2 m wall leaves a WallInset - WallThickness/2 = 0.1 m strip of
            // bare floor along both sides of every partition. That strip had neither wall nor
            // solid mass and read as a walk-in crack ("无意义空腔"). Widen the wall body to cover
            // the whole gap on both sides instead of chasing it with solid mass.
            float body = WallBodyThickness(plan);

            List<BoxSpec> lintels = new List<BoxSpec>();
            List<Door> doors = CollectDoors(plan);
            for (int i = 0; i < doors.Count; i++) CutDoor(horizontal, vertical, doors[i], lintels, body);

            EmitLines(boxes, horizontal, true, plan, body);
            EmitLines(boxes, vertical, false, plan, body);
            MergeLintels(lintels);
            boxes.AddRange(lintels);

            // 大厅挡墙垛：垂直于长轴的 0.4m 墙垛，交替从两侧伸出（见 FacilityGrammar.AddHallBreaks）。
            // 它们不是房间边界，不能走 AddEdges（那会被当成墙线去挖门洞），直接作为独立盒子并进墙体。
            for (int i = 0; i < plan.HallBreaks.Count; i++)
            {
                RectF b = plan.HallBreaks[i];
                boxes.Add(new BoxSpec(new Vector3(b.Center.x, CeilingY * 0.5f, b.Center.y),
                    new Vector3(b.W, CeilingY, b.H), 0f));
            }
        }

        static void MergeLintels(List<BoxSpec> lintels)
        {
            // Two door records may describe neighbouring cuts in the same shared wall.
            // Their headers are one architectural beam, not two overlapping solids.
            for (int i = lintels.Count - 1; i >= 0; i--)
            {
                BoxSpec a = lintels[i];
                bool horizontal = a.Size.x >= a.Size.z;
                float line = horizontal ? a.Center.z : a.Center.x;
                float a0 = horizontal ? a.Center.x - a.Size.x * 0.5f : a.Center.z - a.Size.z * 0.5f;
                float a1 = horizontal ? a.Center.x + a.Size.x * 0.5f : a.Center.z + a.Size.z * 0.5f;
                for (int j = 0; j < i; j++)
                {
                    BoxSpec b = lintels[j];
                    bool otherHorizontal = b.Size.x >= b.Size.z;
                    if (horizontal != otherHorizontal) continue;
                    float otherLine = horizontal ? b.Center.z : b.Center.x;
                    if (Mathf.Abs(line - otherLine) > 0.02f || Mathf.Abs(a.Center.y - b.Center.y) > 0.01f) continue;
                    if (Mathf.Abs(horizontal ? a.Size.z - b.Size.z : a.Size.x - b.Size.x) > 0.01f) continue;
                    float b0 = horizontal ? b.Center.x - b.Size.x * 0.5f : b.Center.z - b.Size.z * 0.5f;
                    float b1 = horizontal ? b.Center.x + b.Size.x * 0.5f : b.Center.z + b.Size.z * 0.5f;
                    if (b0 > a1 + 0.01f || a0 > b1 + 0.01f) continue;
                    float lo = Mathf.Min(a0, b0), hi = Mathf.Max(a1, b1);
                    Vector3 center = b.Center, size = b.Size;
                    if (horizontal) { center.x = (lo + hi) * 0.5f; size.x = hi - lo; }
                    else { center.z = (lo + hi) * 0.5f; size.z = hi - lo; }
                    lintels[j] = new BoxSpec(center, size);
                    lintels.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 45° chamfer walls: one rotated box per cut room corner, sealing the triangle off from
        /// the room. The two legs of the triangle are the room's own walls, so this single
        /// diagonal closes the pocket; the cut region is excluded from walkable cells and prop
        /// placement through <see cref="RoomShape"/>.
        /// </summary>
        public static void CollectChamferBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            float body = WallBodyThickness(plan);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.Cuts == null) continue;
                for (int c = 0; c < room.Cuts.Count; c++)
                {
                    CornerCut cut = room.Cuts[c];
                    if (cut.Size < 0.4f) continue;
                    Vector2 center;
                    float length, yaw;
                    RoomShape.CutWall(room.Rect, cut, out center, out length, out yaw);
                    boxes.Add(new BoxSpec(new Vector3(center.x, CeilingY * 0.5f, center.y),
                        new Vector3(length + body, CeilingY, body), yaw));
                }
            }
        }

        const float LineGrid = 0.05f;

        static float SnapLine(float value) { return Mathf.Round(value / LineGrid) * LineGrid; }

        static int LineKey(float value) { return Mathf.RoundToInt(SnapLine(value) / LineGrid); }

        static void AddEdges(Dictionary<int, List<Vector2>> horizontal, Dictionary<int, List<Vector2>> vertical, RectF r)
        {
            if (r.W <= 0.05f || r.H <= 0.05f) return;
            PushSpan(vertical, LineKey(r.X), r.Y, r.YMax);
            PushSpan(vertical, LineKey(r.XMax), r.Y, r.YMax);
            PushSpan(horizontal, LineKey(r.Y), r.X, r.XMax);
            PushSpan(horizontal, LineKey(r.YMax), r.X, r.XMax);
        }

        static void PushSpan(Dictionary<int, List<Vector2>> lines, int key, float a, float b)
        {
            if (b - a < 0.05f) return;
            List<Vector2> spans;
            if (!lines.TryGetValue(key, out spans))
            {
                spans = new List<Vector2>();
                lines[key] = spans;
            }
            spans.Add(new Vector2(Mathf.Min(a, b), Mathf.Max(a, b)));
        }

        static void CombineAll(Dictionary<int, List<Vector2>> lines)
        {
            foreach (KeyValuePair<int, List<Vector2>> pair in lines)
            {
                List<Vector2> spans = pair.Value;
                if (spans.Count == 0) continue;
                spans.Sort((x, y) => x.x.CompareTo(y.x));
                List<Vector2> merged = new List<Vector2>(spans.Count);
                Vector2 current = spans[0];
                for (int i = 1; i < spans.Count; i++)
                {
                    if (spans[i].x <= current.y + 0.01f) current.y = Mathf.Max(current.y, spans[i].y);
                    else
                    {
                        merged.Add(current);
                        current = spans[i];
                    }
                }
                merged.Add(current);
                spans.Clear();
                spans.AddRange(merged);
            }
        }

        static void CutDoor(Dictionary<int, List<Vector2>> horizontal, Dictionary<int, List<Vector2>> vertical, Door door, List<BoxSpec> lintels, float thickness)
        {
            bool isHorizontal = door.Side == DoorSide.North || door.Side == DoorSide.South;
            Dictionary<int, List<Vector2>> lines = isHorizontal ? horizontal : vertical;
            float coordinate = isHorizontal ? door.Center.y : door.Center.x;
            float along = isHorizontal ? door.Center.x : door.Center.y;
            float half = Mathf.Max(0.45f, door.Width * 0.5f + 0.04f);

            // 门中心可能落在房间内缩线与叶子边界线之间：只认“真正盖住门”的那条墙线，
            // 否则会选中旁边 0.1 m 外的墙垛，切不到该切的墙，门洞就成了实墙。
            List<Vector2> target = null;
            float line = SnapLine(coordinate);
            float bestDelta = 0.61f;
            foreach (KeyValuePair<int, List<Vector2>> pair in lines)
            {
                float delta = Mathf.Abs(pair.Key * LineGrid - coordinate);
                if (delta > 0.6f || delta >= bestDelta) continue;
                if (!CoversSpan(pair.Value, along)) continue;
                bestDelta = delta;
                line = pair.Key * LineGrid;
                target = pair.Value;
            }
            if (target == null) return;

            // 门洞两侧必须还有墙，否则会留下一片悬空的门楣（看起来像 L 型纸片墙）
            bool keepSides = SideWallLength(target, along - half, along + half) >= 0.6f;
            CutSpan(target, along - half, along + half);
            if (!keepSides)
            {
                RemoveSpanCovering(target, along - half, along + half);
                return;
            }

            // 门楣必须和墙面完全共面（同厚度、同中心线），任何凸出都会变成"悬空纸片"
            float y = (DoorHeight + CeilingY) * 0.5f;
            lintels.Add(isHorizontal
                ? new BoxSpec(along, y, line, half * 2f, CeilingY - DoorHeight, thickness)
                : new BoxSpec(line, y, along, thickness, CeilingY - DoorHeight, half * 2f));
        }

        static bool CoversSpan(List<Vector2> spans, float along)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                if (along >= spans[i].x - 0.05f && along <= spans[i].y + 0.05f) return true;
            }
            return false;
        }

        static void RemoveSpanCovering(List<Vector2> spans, float a, float b)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                if (b <= spans[i].x + 0.01f || a >= spans[i].y - 0.01f) continue;
                spans.RemoveAt(i);
            }
        }

        /// <summary>Total wall length left beside an opening (walls that would remain on either side).</summary>
        static float SideWallLength(List<Vector2> spans, float a, float b)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                Vector2 span = spans[i];
                if (b <= span.x + 0.01f || a >= span.y - 0.01f) continue;
                float left = Mathf.Max(0f, a - span.x);
                float right = Mathf.Max(0f, span.y - b);
                return left + right;
            }
            return 0f;
        }

        static void CutSpan(List<Vector2> spans, float a, float b)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                Vector2 span = spans[i];
                if (b <= span.x + 0.01f || a >= span.y - 0.01f) continue;
                spans.RemoveAt(i);
                if (a > span.x + 0.01f) spans.Add(new Vector2(span.x, a));
                if (b < span.y - 0.01f) spans.Add(new Vector2(b, span.y));
            }
        }

        static float WallBodyThickness(FloorPlan plan)
        {
            return Mathf.Max(WallThickness, plan.Spec.WallInset * 2f);
        }

        static void EmitLines(List<BoxSpec> boxes, Dictionary<int, List<Vector2>> lines, bool horizontal, FloorPlan plan, float thickness)
        {
            RectF bounds = plan.Bounds;
            float limit = horizontal ? bounds.H : bounds.W;
            EnvelopeShape envelope = plan.Envelope;
            List<Vector2> clipped = new List<Vector2>();
            foreach (KeyValuePair<int, List<Vector2>> pair in lines)
            {
                float line = pair.Key * LineGrid;
                if (line <= 0.06f || line >= limit - 0.06f) continue; // 外墙由外壳负责
                List<Vector2> spans = pair.Value;
                for (int i = 0; i < spans.Count; i++)
                {
                    float a = spans[i].x, b = spans[i].y;
                    if (b - a < 0.06f) continue;
                    if (envelope == null)
                    {
                        AddWallRun(boxes, horizontal, line, a, b, thickness);
                        continue;
                    }
                    // Interior partitions are still axis aligned, but the floor is not: keep only
                    // the parts of the run that are inside the envelope.
                    clipped.Clear();
                    ClipRunToEnvelope(envelope, horizontal, line, a, b, clipped);
                    for (int k = 0; k < clipped.Count; k++)
                        AddWallRun(boxes, horizontal, line, clipped[k].x, clipped[k].y, thickness);
                }
            }
        }

        static void AddWallRun(List<BoxSpec> boxes, bool horizontal, float line, float a, float b, float thickness)
        {
            float len = b - a;
            if (len < 0.06f) return;
            float mid = (a + b) * 0.5f;
            boxes.Add(horizontal
                ? new BoxSpec(mid, CeilingY * 0.5f, line, len + 0.02f, CeilingY, thickness)
                : new BoxSpec(line, CeilingY * 0.5f, mid, thickness, CeilingY, len + 0.02f));
        }

        static void ClipRunToEnvelope(EnvelopeShape envelope, bool horizontal, float line, float a, float b, List<Vector2> intervals)
        {
            const float step = 0.25f;
            bool open = false;
            float start = 0f;
            for (float t = a; t <= b + 0.001f; t += step)
            {
                float tt = Mathf.Min(t, b);
                Vector2 p = horizontal ? new Vector2(tt, line) : new Vector2(line, tt);
                bool inside = FloorEnvelope.Contains(envelope.Outline, p);
                if (inside && !open) { open = true; start = tt; }
                else if (!inside && open)
                {
                    open = false;
                    if (tt - start > 0.2f) intervals.Add(new Vector2(start, tt));
                }
                if (tt >= b) break;
            }
            if (open && b - start > 0.2f) intervals.Add(new Vector2(start, b));
        }

        /// <summary>Elevator bank strip inside the core (north side, 4 m opening).</summary>
        public static void CollectCoreBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            RectF coreRect = plan.Core;
            float bankDepth = 1.4f;
            float bankZ = coreRect.YMax - bankDepth * 0.5f;
            float bankHalf = (coreRect.W - 4f) * 0.5f;
            RectF left = new RectF(coreRect.X, coreRect.YMax - bankDepth, bankHalf, bankDepth);
            RectF right = new RectF(coreRect.XMax - bankHalf, coreRect.YMax - bankDepth, bankHalf, bankDepth);
            if (!CoreBankIntersected(plan, left))
                boxes.Add(new BoxSpec(coreRect.X + bankHalf * 0.5f, CeilingY * 0.5f, bankZ,
                    bankHalf, CeilingY, bankDepth));
            if (!CoreBankIntersected(plan, right))
                boxes.Add(new BoxSpec(coreRect.XMax - bankHalf * 0.5f, CeilingY * 0.5f, bankZ,
                    bankHalf, CeilingY, bankDepth));
        }

        static bool CoreBankIntersected(FloorPlan plan, RectF bank)
        {
            for (int i = 0; i < plan.Corridors.Count; i++)
                if (plan.Corridors[i].Rect.Inflate(0.02f).Overlaps(bank)) return true;
            return false;
        }

        /// <summary>
        /// Structural columns on the 8 m grid. Columns that land on a wall line are dropped:
        /// a 0.6 m box half-buried in a 0.2 m wall reads as a thin slab glued to the wall
        /// (the "L 型纸片墙" artifact) instead of a freestanding pillar.
        /// </summary>
        public static void CollectColumnBoxes(FloorPlan plan, List<BoxSpec> boxes)
        {
            List<BoxSpec> walls = new List<BoxSpec>();
            CollectShellBoxes(plan, walls);
            CollectWallBoxes(plan, walls);

            const float half = 0.3f;
            for (int i = 0; i < plan.Columns.Count; i++)
            {
                Vector2 c = plan.Columns[i];
                bool blocked = false;
                for (int w = 0; w < walls.Count && !blocked; w++)
                {
                    BoxSpec box = walls[w];
                    float bx = box.Size.x * 0.5f + half + 0.05f;
                    float bz = box.Size.z * 0.5f + half + 0.05f;
                    blocked = Mathf.Abs(box.Center.x - c.x) < bx && Mathf.Abs(box.Center.z - c.y) < bz;
                }
                if (blocked) continue;
                // Only structural bays that actually read as structure: big open floors and
                // corridors. A 0.6 m pillar dropped into the middle of a 20 m² room is neither
                // believable nor good cover — it just eats the floor.
                if (!StandingInBigVolume(plan, c)) continue;
                boxes.Add(new BoxSpec(c.x, CeilingY * 0.5f, c.y, half * 2f, CeilingY, half * 2f));
            }
        }

        /// <summary>Corridor bays always stay; room bays only in open-plan rooms (≥120 m²).</summary>
        static bool StandingInBigVolume(FloorPlan plan, Vector2 c)
        {
            float bestRoom = 0f;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                if (r.Contains(c) && r.Area > bestRoom) bestRoom = r.Area;
            }
            if (bestRoom > 0f) return bestRoom >= 120f;
            for (int i = 0; i < plan.Corridors.Count; i++)
                if (plan.Corridors[i].Rect.Contains(c)) return true;
            return false;
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
            // 房间：3.6 × 3.0 m 网格铺 600 × 1200 灯盘，贴近真实吊顶的排布
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                if (r.W < 2.4f || r.H < 2.4f) continue;
                int cols = Mathf.Max(1, Mathf.FloorToInt((r.W - 1.2f) / 3.6f) + 1);
                int rows = Mathf.Max(1, Mathf.FloorToInt((r.H - 1.2f) / 3.0f) + 1);
                for (int cx = 0; cx < cols; cx++)
                {
                    for (int cz = 0; cz < rows; cz++)
                    {
                        float x = r.X + r.W * (cx + 0.5f) / cols;
                        float z = r.Y + r.H * (cz + 0.5f) / rows;
                        bool alongX = (cx + cz) % 2 == 0;
                        boxes.Add(new BoxSpec(x, CeilingY - 0.03f, z,
                            alongX ? 1.2f : 0.6f, 0.04f, alongX ? 0.6f : 1.2f));
                    }
                }
            }
            // 走廊：沿长轴每 6 m 一盏
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF c = plan.Corridors[i].Rect;
                bool horizontal = c.W >= c.H;
                float length = Mathf.Max(c.W, c.H);
                int count = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
                for (int k = 0; k < count; k++)
                {
                    float t = (k + 0.5f) / count;
                    float x = horizontal ? c.X + c.W * t : c.X + c.W * 0.5f;
                    float z = horizontal ? c.Y + c.H * 0.5f : c.Y + c.H * t;
                    boxes.Add(new BoxSpec(x, CeilingY - 0.03f, z,
                        horizontal ? 0.6f : 1.2f, 0.04f, horizontal ? 1.2f : 0.6f));
                }
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
        public const int LightBudget = 72;

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
            // 走廊内部的横墙门 / 门斗侧门：走廊矩形四边默认全是实墙，这些是唯一的穿墙通道
            for (int i = 0; i < plan.CorridorOpenings.Count; i++) Push(doors, seen, plan.CorridorOpenings[i]);
            return doors;
        }

        static void Push(List<Door> doors, HashSet<string> seen, Door d)
        {
            if (d.Width <= 0.05f) return;
            string key = Mathf.RoundToInt(d.Center.x * 10f) + ":" + Mathf.RoundToInt(d.Center.y * 10f);
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
                // Hung 1 m under the ceiling: a light flush against the slab burns a hard
                // hotspot into the acoustic tile right above it.
                go.transform.position = new Vector3(positions[i].x, CeilingY - 1.0f, positions[i].y);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 16f;
                l.intensity = 5.5f;
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
