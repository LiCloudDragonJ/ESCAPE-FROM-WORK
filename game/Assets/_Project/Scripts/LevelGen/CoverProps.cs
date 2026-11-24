using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum PropSize { Large, Medium, Small }

    public struct PropSpec
    {
        public string Name;
        public float Width;      // 沿墙方向
        public float Depth;      // 垂直墙面，占走廊宽度
        public PropSize Size;
        public bool BlocksSight; // 站着也挡视线
        public bool Vaultable;   // 可以翻越
    }

    public struct PropPlacement
    {
        public RectF Rect;
        public int CorridorIndex;
        public bool FarSide;     // true = 走廊长轴的另一侧
        public PropSpec Spec;
    }

    /// <summary>走廊掩体目录：大中小三档，每档多种办公用品。</summary>
    public static class CoverCatalog
    {
        public static readonly PropSpec[] Large =
        {
            new PropSpec { Name = "双柜组", Width = 1.8f, Depth = 0.9f, Size = PropSize.Large, BlocksSight = true, Vaultable = false },
            new PropSpec { Name = "纸箱堆", Width = 1.4f, Depth = 0.9f, Size = PropSize.Large, BlocksSight = true, Vaultable = true },
            new PropSpec { Name = "会议桌", Width = 2.2f, Depth = 0.9f, Size = PropSize.Large, BlocksSight = false, Vaultable = true },
        };

        public static readonly PropSpec[] Medium =
        {
            new PropSpec { Name = "办公桌", Width = 1.4f, Depth = 0.6f, Size = PropSize.Medium, BlocksSight = false, Vaultable = true },
            new PropSpec { Name = "打印台", Width = 1.2f, Depth = 0.6f, Size = PropSize.Medium, BlocksSight = false, Vaultable = false },
            new PropSpec { Name = "沙发", Width = 1.8f, Depth = 0.6f, Size = PropSize.Medium, BlocksSight = false, Vaultable = true },
        };

        public static readonly PropSpec[] Small =
        {
            new PropSpec { Name = "折叠椅", Width = 0.5f, Depth = 0.45f, Size = PropSize.Small, BlocksSight = false, Vaultable = true },
            new PropSpec { Name = "饮水机", Width = 0.5f, Depth = 0.45f, Size = PropSize.Small, BlocksSight = false, Vaultable = false },
            new PropSpec { Name = "小纸箱", Width = 0.6f, Depth = 0.45f, Size = PropSize.Small, BlocksSight = false, Vaultable = true },
            new PropSpec { Name = "垃圾桶", Width = 0.4f, Depth = 0.4f, Size = PropSize.Small, BlocksSight = false, Vaultable = false },
        };

        public static PropSpec[] Of(PropSize size)
        {
            return size == PropSize.Large ? Large : size == PropSize.Medium ? Medium : Small;
        }
    }

    /// <summary>
    /// 走廊掩体布点：两侧都要刷、大中小混着刷、只贴墙、避开门口和交叉口。
    /// </summary>
    public static class CoverPlacer
    {
        public static void Place(FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            plan.Props.Clear();
            if (!spec.PlaceCoverProps) return;

            List<Vector2> doorPts = new List<Vector2>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) doorPts.Add(plan.Rooms[i].Door.Center);
                List<Door> extra = plan.Rooms[i].ExtraDoors;
                if (extra == null) continue;
                for (int k = 0; k < extra.Count; k++) doorPts.Add(extra[k].Center);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) doorPts.Add(plan.LoopDoors[i].Center);

            int[] used = new int[3];

            for (int ci = 0; ci < plan.Corridors.Count; ci++)
            {
                RectF r = plan.Corridors[ci].Rect;
                bool horiz = r.W >= r.H;
                float width = horiz ? r.H : r.W;
                float length = horiz ? r.W : r.H;
                if (width < 3.0f || length < 6f) continue;

                bool wide = width >= 4.4f;
                float band = wide ? 0.9f : 0.6f;
                PropSize[] allowed = wide
                    ? new[] { PropSize.Large, PropSize.Medium, PropSize.Small }
                    : new[] { PropSize.Medium, PropSize.Small };

                List<Vector2> blocked = new List<Vector2>(); // (start, end) 沿长轴

                // 与其他走廊的交叉口
                for (int j = 0; j < plan.Corridors.Count; j++)
                {
                    if (j == ci) continue;
                    RectF o = plan.Corridors[j].Rect;
                    if (!o.Overlaps(r)) continue;
                    float a = horiz ? Mathf.Max(o.X, r.X) : Mathf.Max(o.Y, r.Y);
                    float b = horiz ? Mathf.Min(o.XMax, r.XMax) : Mathf.Min(o.YMax, r.YMax);
                    float origin = horiz ? r.X : r.Y;
                    blocked.Add(new Vector2(a - origin - 1.2f, b - origin + 1.2f));
                }

                // 门洞两侧各 1.2m
                float axisOrigin = horiz ? r.X : r.Y;
                float sideMin = horiz ? r.Y : r.X;
                float sideMax = horiz ? r.YMax : r.XMax;
                for (int d = 0; d < doorPts.Count; d++)
                {
                    Vector2 p = doorPts[d];
                    float along = horiz ? p.x : p.y;
                    float across = horiz ? p.y : p.x;
                    if (across < sideMin - 0.6f || across > sideMax + 0.6f) continue;
                    if (along < axisOrigin - 0.6f || along > axisOrigin + length + 0.6f) continue;
                    blocked.Add(new Vector2(along - axisOrigin - 1.2f, along - axisOrigin + 1.2f));
                }

                float cursor = 1.4f;
                bool farFirst = rng.Next(2) == 0;
                int placedHere = 0;
                int distinctHere = 0;
                bool[] sizeHere = new bool[3];

                while (cursor < length - 2.0f)
                {
                    float groupLen = 1.6f + (float)rng.NextDouble() * 1.8f;
                    if (Blocked(blocked, cursor - 0.3f, cursor + groupLen + 0.3f))
                    {
                        cursor += 1.0f;
                        continue;
                    }

                    // 每组固定两侧都刷；远端沿长轴错开 0–1.4m，避免看起来是对称贴图
                    float farOffset = (float)rng.NextDouble() * 1.4f;

                    for (int s = 0; s < 2; s++)
                    {
                        bool far = (s == 0) == farFirst;
                        float along = cursor + (far ? farOffset : 0f);

                        PropSpec ps = PickBalanced(allowed, used, rng);
                        if (placedHere >= 3 && distinctHere < 2 && sizeHere[(int)ps.Size])
                            ps = PickDifferent(allowed, ps.Size, rng);

                        float depth = Mathf.Min(ps.Depth, band);
                        float w = ps.Width;
                        if (along + w > length - 1.2f) w = length - 1.2f - along;
                        if (w < 0.4f) continue;

                        // 远端错开之后要重新检查门口净空，否则会堵住别的门
                        if (Blocked(blocked, along - 0.15f, along + w + 0.15f)) continue;

                        RectF pr = horiz
                            ? new RectF(r.X + along, far ? r.YMax - depth - 0.02f : r.Y + 0.02f, w, depth)
                            : new RectF(far ? r.XMax - depth - 0.02f : r.X + 0.02f, r.Y + along, depth, w);

                        // 两条走廊在拐角相交时，各自的掩体可能叠在一起
                        if (OverlapsAny(plan.Props, pr)) continue;

                        plan.Props.Add(new PropPlacement
                        {
                            Rect = pr,
                            CorridorIndex = ci,
                            FarSide = far,
                            Spec = ps,
                        });
                        used[(int)ps.Size]++;
                        if (!sizeHere[(int)ps.Size]) { sizeHere[(int)ps.Size] = true; distinctHere++; }
                        placedHere++;
                    }

                    cursor += groupLen + 0.4f;
                }
            }
        }

        /// <summary>长走廊每隔 26–32m 砌一道隔断墙断掉直线视线；只占一侧，永远留出通行净宽。</summary>
        public static void AddSightBreaks(FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            if (!spec.PlaceCoverProps) return;

            List<Vector2> doors = DoorPoints(plan);

            for (int ci = 0; ci < plan.Corridors.Count; ci++)
            {
                RectF r = plan.Corridors[ci].Rect;
                bool horiz = r.W >= r.H;
                float width = horiz ? r.H : r.W;
                float length = horiz ? r.W : r.H;
                if (length < 34f) continue;
                if (width < 3.4f) continue;

                float depth = width - spec.CorridorClearLane;
                if (depth < 0.9f) continue;
                if (depth > 2.6f) depth = 2.6f;

                bool farFirst = rng.Next(2) == 0;
                float pos = 16f + (float)rng.NextDouble() * 4f;
                int index = 0;
                int placed = 0;

                while (pos < length - 16f)
                {
                    if (TryPlaceSightBreak(plan, ci, r, horiz, length, pos, depth, ((index % 2) == 0) == farFirst, doors))
                        placed++;

                    index++;
                    pos += 26f + (float)rng.NextDouble() * 6f;
                }

                // 兜底：节奏被门口/交叉口挤掉时，至少在中段补一道
                if (placed == 0)
                {
                    float mid = length * 0.5f;
                    for (int k = 0; k < 8; k++)
                        if (TryPlaceSightBreak(plan, ci, r, horiz, length, mid + k * 2f, depth, farFirst, doors)) break;
                }
            }
        }

        static bool TryPlaceSightBreak(FloorPlan plan, int ci, RectF r, bool horiz, float length,
            float pos, float depth, bool far, List<Vector2> doors)
        {
            if (pos < 2f || pos > length - 2.5f) return false;
            float w = 0.5f;
            RectF pr = horiz
                ? new RectF(r.X + pos, far ? r.YMax - depth - 0.02f : r.Y + 0.02f, w, depth)
                : new RectF(far ? r.XMax - depth - 0.02f : r.X + 0.02f, r.Y + pos, depth, w);

            // 门口 1.3m 内不砌（比收尾的 1.05m 清空更保守，避免刚放上就被拔掉）
            RectF pad = pr.Inflate(1.3f);
            for (int i = 0; i < doors.Count; i++)
                if (pad.Contains(doors[i])) return false;

            if (OverlapsAny(plan.Props, pr))
            {
                // 同一截走廊上的家具让位
                plan.Props.RemoveAll(p => p.CorridorIndex == ci && p.Rect.Inflate(0.06f).Overlaps(pr));
                if (OverlapsAny(plan.Props, pr)) return false;
            }

            // 对侧同跨的家具也要清掉，否则净宽不够
            float origin = horiz ? r.X : r.Y;
            float a = origin + pos - 0.9f;
            float b = origin + pos + w + 0.9f;
            plan.Props.RemoveAll(p => p.CorridorIndex == ci && p.FarSide != far && AlongOverlaps(p.Rect, horiz, a, b));

            plan.Props.Add(new PropPlacement
            {
                Rect = pr,
                CorridorIndex = ci,
                FarSide = far,
                Spec = new PropSpec { Name = "隔断墙", Width = w, Depth = depth, Size = PropSize.Large, BlocksSight = true, Vaultable = false },
            });
            return true;
        }

        static bool AlongOverlaps(RectF p, bool horiz, float a, float b)
        {
            float s = horiz ? p.X : p.Y;
            float e = horiz ? p.XMax : p.YMax;
            return e > a && s < b;
        }

        static List<Vector2> DoorPoints(FloorPlan plan)
        {
            List<Vector2> doors = new List<Vector2>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) doors.Add(plan.Rooms[i].Door.Center);
                List<Door> extra = plan.Rooms[i].ExtraDoors;
                if (extra == null) continue;
                for (int k = 0; k < extra.Count; k++) doors.Add(extra[k].Center);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) doors.Add(plan.LoopDoors[i].Center);
            return doors;
        }

        /// <summary>收尾：任何门口 1.05m 以内的掩体一律拔掉（门可能开在别的走廊上，扫描阶段看不全）。</summary>
        public static void DropPropsNearDoors(FloorPlan plan)
        {
            List<Vector2> doors = new List<Vector2>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) doors.Add(plan.Rooms[i].Door.Center);
                List<Door> extra = plan.Rooms[i].ExtraDoors;
                if (extra == null) continue;
                for (int k = 0; k < extra.Count; k++) doors.Add(extra[k].Center);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) doors.Add(plan.LoopDoors[i].Center);

            plan.Props.RemoveAll(p =>
            {
                RectF pr = p.Rect;
                for (int d = 0; d < doors.Count; d++)
                {
                    Vector2 c = doors[d];
                    float dx = Mathf.Max(pr.X - c.x, 0f, c.x - pr.XMax);
                    float dy = Mathf.Max(pr.Y - c.y, 0f, c.y - pr.YMax);
                    if (dx * dx + dy * dy < 1.05f * 1.05f) return true;
                }
                return false;
            });
        }

        static bool OverlapsAny(List<PropPlacement> placed, RectF r)
        {
            RectF g = r.Inflate(0.04f);
            for (int i = 0; i < placed.Count; i++)
                if (g.Overlaps(placed[i].Rect)) return true;
            return false;
        }

        static bool Blocked(List<Vector2> spans, float a, float b)
        {
            for (int i = 0; i < spans.Count; i++)
                if (b > spans[i].x && a < spans[i].y) return true;
            return false;
        }

        static PropSpec PickDifferent(PropSize[] allowed, PropSize avoid, System.Random rng)
        {
            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == avoid) continue;
                PropSpec[] pool = CoverCatalog.Of(allowed[i]);
                return pool[rng.Next(pool.Length)];
            }
            PropSpec[] fallback = CoverCatalog.Of(avoid);
            return fallback[rng.Next(fallback.Length)];
        }

        static PropSpec PickBalanced(PropSize[] allowed, int[] used, System.Random rng)
        {
            int total = used[0] + used[1] + used[2];
            if (total > 8)
            {
                // 某档占比过低就强制补一个，保证大中小都出现
                for (int i = 0; i < allowed.Length; i++)
                {
                    int idx = (int)allowed[i];
                    if (used[idx] * 100 < total * 14)
                    {
                        PropSpec[] pool = CoverCatalog.Of(allowed[i]);
                        return pool[rng.Next(pool.Length)];
                    }
                }
            }

            float roll = (float)rng.NextDouble();
            PropSize size = allowed.Length == 3
                ? (roll < 0.3f ? PropSize.Large : roll < 0.72f ? PropSize.Medium : PropSize.Small)
                : (roll < 0.55f ? PropSize.Medium : PropSize.Small);
            if (size == PropSize.Large && allowed.Length < 3) size = PropSize.Medium;
            PropSpec[] p = CoverCatalog.Of(size);
            return p[rng.Next(p.Length)];
        }
    }
}
