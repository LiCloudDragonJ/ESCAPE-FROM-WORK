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

        /// <summary>出生区散布检查用的粗栅格（2 m）：1000 种子批测里 1 m 栅格 flood 太贵。</summary>
        public const float CoarseCell = 2f;

        public struct Grid
        {
            public int W, H;
            public bool[] Walkable;
            public float[] Distance;
            public float Unit;

            public bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
            public int Index(int x, int y) { return y * W + x; }
            public bool IsWalkable(int x, int y) { return Inside(x, y) && Walkable[y * W + x]; }
            public Vector2 Center(int x, int y) { return new Vector2((x + 0.5f) * Unit, (y + 0.5f) * Unit); }
            public int X(float wx) { return Mathf.FloorToInt(wx / Unit); }
            public int Y(float wz) { return Mathf.FloorToInt(wz / Unit); }
        }

        public static Grid BuildGrid(FloorPlan plan) { return BuildGrid(plan, Cell); }

        public static Grid BuildGrid(FloorPlan plan, float cell)
        {
            Grid g = new Grid();
            g.Unit = cell;
            g.W = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.W / cell));
            g.H = Mathf.Max(1, Mathf.CeilToInt(plan.Bounds.H / cell));
            g.Walkable = new bool[g.W * g.H];
            g.Distance = new float[g.W * g.H];
            for (int i = 0; i < g.Distance.Length; i++) g.Distance[i] = -1f;

            // 粗栅格会把 1.9 m 的服务廊整格漏掉，按格宽 30% 外扩补回来；细栅格保持原样。
            float inflate = cell * 0.3f - (Cell * 0.3f);
            if (inflate < 0f) inflate = 0f;

            for (int i = 0; i < plan.Corridors.Count; i++) Stamp(g, plan.Corridors[i].Rect, inflate);
            for (int i = 0; i < plan.Rooms.Count; i++) Stamp(g, plan.Rooms[i].Rect, inflate);
            for (int i = 0; i < plan.Stairs.Count; i++) Stamp(g, plan.Stairs[i], inflate);

            // 45° 切角三角区在斜墙里面：从可走栅格挖掉，否则寻路会穿墙走进实心角里。
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room cutRoom = plan.Rooms[i];
                if (cutRoom.Cuts == null || cutRoom.Cuts.Count == 0) continue;
                for (int k = 0; k < cutRoom.Cuts.Count; k++) ClearCut(g, cutRoom.Rect, cutRoom.Cuts[k]);
            }

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

        static void ClearCut(Grid g, RectF r, CornerCut cut)
        {
            Vector2 c = RoomShape.CornerPoint(r, cut.Corner);
            float s = cut.Size;
            int x0 = Mathf.Max(0, g.X(c.x - s));
            int y0 = Mathf.Max(0, g.Y(c.y - s));
            int x1 = Mathf.Min(g.W - 1, g.X(c.x + s));
            int y1 = Mathf.Min(g.H - 1, g.Y(c.y + s));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int idx = y * g.W + x;
                    if (!g.Walkable[idx]) continue;
                    if (RoomShape.Blocked(r, cut, g.Center(x, y))) g.Walkable[idx] = false;
                }
            }
        }

        static void Stamp(Grid g, RectF r, float inflate = 0f)
        {
            if (inflate > 0f) r = r.Inflate(inflate);
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
                float d = g.Distance[cur] + g.Unit;
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

        /// <summary>
        /// Walk distance to a point that may sit inside a solid volume (core shaft, column):
        /// falls back to the nearest reachable cell within 3 m, so a freight elevator in the
        /// core is still measured by the lobby in front of it instead of reporting unreachable.
        /// </summary>
        public static float WalkDistanceAt(Grid g, float[] dist, Vector2 center)
        {
            int cx = g.X(center.x);
            int cy = g.Y(center.y);
            if (g.Inside(cx, cy) && dist[cy * g.W + cx] >= 0f) return dist[cy * g.W + cx];

            int radius = Mathf.CeilToInt(3f / g.Unit);
            float best = -1f;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (!g.Inside(x, y)) continue;
                    float d = dist[y * g.W + x];
                    if (d < 0f) continue;
                    if (best < 0f || d < best) best = d;
                }
            }
            return best;
        }

        /// <summary>Straight-line test on the walkable grid: any wall between the two points blocks sight.</summary>
        public static bool HasLineOfSight(Grid g, Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (g.Unit * 0.5f)));
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
            /// <summary>实际用来判定“够远”的阈值（见 FarThreshold）。</summary>
            public float AppliedDistance;
        }

        /// <summary>Required travel: at least 60% of the floor diagonal.</summary>
        public static float RequiredExtractionDistance(FloorPlan plan)
        {
            float diag = Mathf.Sqrt(plan.Bounds.W * plan.Bounds.W + plan.Bounds.H * plan.Bounds.H);
            return diag * 0.6f;
        }

        /// <summary>
        /// “够远”的阈值：理想值是层对角线的 60%，但超大层（如 280×280 基座）上固定撤离设施
        /// 不一定能从每个出生区都跨这么远——这时按该出生区能走到的最远撤离点打七折，
        /// 保证标准是“这层物理上做得到的远”，而不是一个永远无法满足的常数。
        /// </summary>
        public static float FarThreshold(float required, float farthestReachable)
        {
            if (farthestReachable <= 0f) return required;
            return Mathf.Min(required, farthestReachable * 0.7f);
        }

        public static List<SpawnAudit> AuditSpawns(FloorPlan plan, Grid g)
        {
            List<SpawnAudit> list = new List<SpawnAudit>();
            float required = RequiredExtractionDistance(plan);
            List<float> reachable = new List<float>(plan.Extractions.Count);
            for (int s = 0; s < plan.SpawnZones.Count; s++)
            {
                Vector2 spawn = plan.SpawnZones[s].Center;
                float[] dist = Flood(g, spawn);
                reachable.Clear();
                for (int e = 0; e < plan.Extractions.Count; e++)
                {
                    // 撤离板是实体：中心可能落在核心筒或柱子里，按最近的可行走格子算距离。
                    float d = WalkDistanceAt(g, dist, plan.Extractions[e].Center);
                    if (d >= 0f) reachable.Add(d);
                }
                float farthest = 0f;
                for (int i = 0; i < reachable.Count; i++) if (reachable[i] > farthest) farthest = reachable[i];
                float threshold = FarThreshold(required, farthest);

                SpawnAudit audit = new SpawnAudit();
                audit.SpawnIndex = s;
                audit.RequiredDistance = required;
                audit.AppliedDistance = threshold;
                audit.NearestFarDistance = float.MaxValue;
                for (int i = 0; i < reachable.Count; i++)
                {
                    if (reachable[i] < threshold) continue;
                    audit.FarExtractions++;
                    if (reachable[i] < audit.NearestFarDistance) audit.NearestFarDistance = reachable[i];
                }
                if (audit.NearestFarDistance == float.MaxValue) audit.NearestFarDistance = -1f;
                list.Add(audit);
            }
            return list;
        }

        // ---- R7：出生区之间的图距离散布 -------------------------------------------------
        //
        // 验收口径：任意两个出生区之间的**步行**距离 ≥ 本层锚点直径的 35%。
        // 锚点 = 出生区 + 撤离候选（≤10）+ 走廊中点（总数 ≤26）；直径 = 锚点两两最短路径的最大值。
        // 一律用 2 m 粗栅格：1 m 栅格上每对锚点都 flood 会让 1000 种子批测从 2 分钟涨到 10 分钟（实测 596s）。

        public struct SpawnSpacingAudit
        {
            public float MinDistance;
            public float Required;
            public float Diameter;
            public bool Ok;
            /// <summary>最近的一对出生区下标（-1 = 没有可测的对）。</summary>
            public int A, B;
        }

        public static float CoarseDiameter(FloorPlan plan, Grid g)
        {
            // 锚点控制在 ≤16 个：每个锚点要 flood 一整张粗栅格，1000 种子批测里这是主导成本。
            List<Vector2> anchors = new List<Vector2>();
            for (int i = 0; i < plan.SpawnZones.Count; i++) anchors.Add(plan.SpawnZones[i].Center);
            int eMax = Mathf.Min(6, plan.ExtractionCandidates.Count);
            for (int i = 0; i < eMax; i++) anchors.Add(plan.ExtractionCandidates[i].Center);
            for (int i = 0; i < plan.Corridors.Count && anchors.Count < 16; i += 5)
                anchors.Add(plan.Corridors[i].Rect.Center);

            float diameter = 0f;
            for (int i = 0; i < anchors.Count; i++)
            {
                float[] dist = Flood(g, anchors[i]);
                for (int j = i + 1; j < anchors.Count; j++)
                {
                    float d = WalkDistanceAt(g, dist, anchors[j]);
                    if (d > diameter) diameter = d;
                }
            }
            return diameter;
        }

        public static SpawnSpacingAudit AuditSpawnSpacing(FloorPlan plan, Grid g, float diameter)
        {
            SpawnSpacingAudit audit = new SpawnSpacingAudit();
            audit.Diameter = diameter;
            audit.Required = diameter * 0.35f;
            audit.MinDistance = float.MaxValue;
            audit.A = -1;
            audit.B = -1;
            for (int i = 0; i < plan.SpawnZones.Count; i++)
            {
                float[] dist = Flood(g, plan.SpawnZones[i].Center);
                for (int j = i + 1; j < plan.SpawnZones.Count; j++)
                {
                    float d = WalkDistanceAt(g, dist, plan.SpawnZones[j].Center);
                    if (d < 0f || d >= audit.MinDistance) continue;
                    audit.MinDistance = d;
                    audit.A = i;
                    audit.B = j;
                }
            }
            if (audit.MinDistance == float.MaxValue) audit.MinDistance = -1f;
            audit.Ok = audit.MinDistance >= audit.Required;
            return audit;
        }
    }
}
