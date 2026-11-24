using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Post-generation checks that need real walk distance instead of straight lines:
    /// how far a team must travel from its spawn before it can extract. Uses a 1 m
    /// walkable grid with door openings stamped in, so it follows rooms and corridors.
    /// </summary>
    public static class FloorValidator
    {
        public const float Cell = 1f;

        public struct Grid
        {
            public int W, H;
            public bool[] Walkable;
            public float[] Distance;

            public bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
            public int Index(int x, int y) { return y * W + x; }
            public bool IsWalkable(int x, int y) { return Inside(x, y) && Walkable[y * W + x]; }
            public Vector2 Center(int x, int y) { return new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell); }
            public int X(float wx) { return Mathf.FloorToInt(wx / Cell); }
            public int Y(float wz) { return Mathf.FloorToInt(wz / Cell); }
        }

        public static Grid BuildGrid(FloorPlan plan)
        {
            Grid g = new Grid();
            g.W = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.W / Cell));
            g.H = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.H / Cell));
            g.Walkable = new bool[g.W * g.H];
            g.Distance = new float[g.W * g.H];
            for (int i = 0; i < g.Distance.Length; i++) g.Distance[i] = -1f;

            for (int i = 0; i < plan.Corridors.Count; i++) Stamp(g, plan.Corridors[i].Rect);
            for (int i = 0; i < plan.Rooms.Count; i++) Stamp(g, plan.Rooms[i].Rect);
            for (int i = 0; i < plan.Stairs.Count; i++) Stamp(g, plan.Stairs[i]);

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room r = plan.Rooms[i];
                if (r.Door.Width > 0f) StampDoor(g, r.Door.Center, 1.4f);
                if (r.ExtraDoors == null) continue;
                for (int k = 0; k < r.ExtraDoors.Count; k++) StampDoor(g, r.ExtraDoors[k].Center, 1.2f);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) StampDoor(g, plan.LoopDoors[i].Center, 1.2f);
            return g;
        }

        static void Stamp(Grid g, RectF r)
        {
            int x0 = Mathf.Max(0, g.X(r.X));
            int x1 = Mathf.Min(g.W - 1, g.X(r.XMax - 0.01f));
            int y0 = Mathf.Max(0, g.Y(r.Y));
            int y1 = Mathf.Min(g.H - 1, g.Y(r.YMax - 0.01f));
            for (int y = y0; y <= y1; y++)
            {
                int row = y * g.W;
                for (int x = x0; x <= x1; x++) g.Walkable[row + x] = true;
            }
        }

        static void StampDoor(Grid g, Vector2 center, float size)
        {
            float half = size * 0.5f;
            int x0 = g.X(center.x - half);
            int x1 = g.X(center.x + half);
            int y0 = g.Y(center.y - half);
            int y1 = g.Y(center.y + half);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (g.Inside(x, y)) g.Walkable[y * g.W + x] = true;
                }
            }
        }

        /// <summary>Breadth-first walk distances (in metres) from one point; -1 = unreachable.</summary>
        public static float[] Flood(Grid g, Vector2 from)
        {
            for (int i = 0; i < g.Distance.Length; i++) g.Distance[i] = -1f;
            int sx = g.X(from.x);
            int sy = g.Y(from.y);
            if (!g.IsWalkable(sx, sy))
            {
                float best = float.MaxValue;
                for (int y = 0; y < g.H; y++)
                {
                    for (int x = 0; x < g.W; x++)
                    {
                        if (!g.IsWalkable(x, y)) continue;
                        float d = (g.Center(x, y) - from).sqrMagnitude;
                        if (d < best) { best = d; sx = x; sy = y; }
                    }
                }
                if (!g.IsWalkable(sx, sy)) return g.Distance;
            }

            Queue<int> queue = new Queue<int>(4096);
            int start = g.Index(sx, sy);
            g.Distance[start] = 0f;
            queue.Enqueue(start);
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % g.W;
                int cy = cur / g.W;
                float d = g.Distance[cur] + Cell;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + dx[k];
                    int ny = cy + dy[k];
                    if (!g.IsWalkable(nx, ny)) continue;
                    int ni = ny * g.W + nx;
                    if (g.Distance[ni] >= 0f) continue;
                    g.Distance[ni] = d;
                    queue.Enqueue(ni);
                }
            }
            return g.Distance;
        }

        public static float WalkDistance(Grid g, Vector2 from, Vector2 to)
        {
            float[] dist = Flood(g, from);
            int x = g.X(to.x);
            int y = g.Y(to.y);
            return g.Inside(x, y) ? dist[y * g.W + x] : -1f;
        }

        /// <summary>Straight-line test on the walkable grid: any wall between the two points blocks sight.</summary>
        public static bool HasLineOfSight(Grid g, Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
            for (int i = 1; i < steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                if (!g.IsWalkable(g.X(p.x), g.Y(p.y))) return false;
            }
            return true;
        }

        public struct SpawnAudit
        {
            public int SpawnIndex;
            public int FarExtractions;
            public float NearestFarDistance;
            public float RequiredDistance;
        }

        /// <summary>Required travel: at least 60% of the floor diagonal.</summary>
        public static float RequiredExtractionDistance(FloorPlan plan)
        {
            float diag = Mathf.Sqrt(plan.Bounds.W * plan.Bounds.W + plan.Bounds.H * plan.Bounds.H);
            return diag * 0.6f;
        }

        public static List<SpawnAudit> AuditSpawns(FloorPlan plan, Grid g)
        {
            List<SpawnAudit> list = new List<SpawnAudit>();
            float required = RequiredExtractionDistance(plan);
            for (int s = 0; s < plan.SpawnZones.Count; s++)
            {
                Vector2 spawn = plan.SpawnZones[s].Center;
                float[] dist = Flood(g, spawn);
                SpawnAudit audit = new SpawnAudit();
                audit.SpawnIndex = s;
                audit.RequiredDistance = required;
                audit.NearestFarDistance = float.MaxValue;
                for (int e = 0; e < plan.Extractions.Count; e++)
                {
                    Vector2 c = plan.Extractions[e].Center;
                    int x = g.X(c.x);
                    int y = g.Y(c.y);
                    float d = g.Inside(x, y) ? dist[y * g.W + x] : -1f;
                    if (d < 0f) continue;
                    // Extraction pads sit in corridors/fire stairs; take the nearest walkable
                    // cell if the centre itself is not on the grid.
                    if (d >= required)
                    {
                        audit.FarExtractions++;
                        if (d < audit.NearestFarDistance) audit.NearestFarDistance = d;
                    }
                }
                if (audit.NearestFarDistance == float.MaxValue) audit.NearestFarDistance = -1f;
                list.Add(audit);
            }
            return list;
        }
    }
}
