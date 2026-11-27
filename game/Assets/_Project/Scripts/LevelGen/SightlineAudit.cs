using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// R2 视线审计：拿<b>真实墙盒</b>（内墙 + 外壳 + 斜墙 + 核心筒，全部来自 FloorGeometry 的收集器）
    /// 在 1.62m 眼高做 2.5D 占用栅格，再从采样点沿 32 个方向打射线，量出最长的可通视距离。
    ///
    /// 为什么不直接用 FloorValidator：那是 1m 网格的"能不能走过去"近似，门洞和 0.2m 厚的墙在
    /// 它眼里都不存在，拿它量视线会得出"一条走廊 4.8m 宽一路通到底"的假象（文档明确写了它不是证据）。
    ///
    /// 栅格保守化：每个墙盒按 AABB 外扩半格光栅化，宁可把视线量短，也不会漏掉一堵薄墙。
    /// 门洞是墙盒里真的被挖掉的部分，所以门就是门——射线能穿过去，这也是这套审计的意义。
    /// </summary>
    public static class SightlineAudit
    {
        public const float EyeHeight = 1.62f;
        /// <summary>占用栅格边长。墙厚 0.2–0.4m，0.25m 能保证最薄的墙也至少糊住一格。</summary>
        public const float Cell = 0.25f;
        public const int Directions = 32;
        /// <summary>射线步进：比格子小，才不会从格子缝里漏过去。</summary>
        public const float Step = 0.18f;
        public const float MaxRay = 60f;

        public struct Report
        {
            public float CorridorMax;
            public float RoomMax;
            public float GlobalMax;
            public Vector2 From;
            public Vector2 To;
            public int Samples;
        }

        public static Report Run(FloorPlan plan) { return Run(plan, null, null); }

        /// <summary>Optional world-space filter used by raid slices to exclude sealed floor areas.</summary>
        public static Report Run(FloorPlan plan, Func<Vector2, bool> includePoint) { return Run(plan, includePoint, null); }

        /// <summary>
        /// visiblePoint 可选：把世界坐标里“不可通视”的区域当实墙。切开楼层时封死区只是本局不可进入，
        /// 几何上并没有墙；视线审计必须按同一口径把封死区当墙，否则会量出穿越封死区的一整段假通视
        /// （实测 raid 某层量到 60m，就是射线飞过了封死条）。
        /// </summary>
        public static Report Run(FloorPlan plan, Func<Vector2, bool> includePoint, Func<Vector2, bool> visiblePoint)
        {
            bool[] solid;
            int cols, rows;
            BuildOccupancy(plan, out solid, out cols, out rows);

            Report report;
            report.CorridorMax = 0f;
            report.RoomMax = 0f;
            report.GlobalMax = 0f;
            report.From = Vector2.zero;
            report.To = Vector2.zero;
            report.Samples = 0;

            List<Vector2> points = new List<Vector2>();
            SampleCorridors(plan, points);
            SampleRooms(plan, points);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (includePoint != null && !includePoint(p)) continue;
                if (!Walkable(solid, cols, rows, p)) continue;
                report.Samples++;
                bool corridor = InCorridor(plan, p);
                for (int d = 0; d < Directions; d++)
                {
                    float angle = d * Mathf.PI * 2f / Directions;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float dist = RayLength(solid, cols, rows, p, dir, visiblePoint);
                    if (dist > report.GlobalMax)
                    {
                        report.GlobalMax = dist;
                        report.From = p;
                        report.To = p + dir * dist;
                    }
                    if (corridor && dist > report.CorridorMax)
                    {
                        report.CorridorMax = dist;
                        report.From = p;
                        report.To = p + dir * dist;
                    }
                    if (!corridor && dist > report.RoomMax) report.RoomMax = dist;
                }
            }
            return report;
        }

        // ------------------------------------------------------------------ 占用栅格

        static void BuildOccupancy(FloorPlan plan, out bool[] solid, out int cols, out int rows)
        {
            RectF b = plan.Bounds;
            cols = Mathf.CeilToInt((b.W + 2f) / Cell);
            rows = Mathf.CeilToInt((b.H + 2f) / Cell);
            solid = new bool[cols * rows];

            List<BoxSpec> boxes = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, boxes);
            FloorGeometry.CollectShellBoxes(plan, boxes);
            FloorGeometry.CollectChamferBoxes(plan, boxes);
            FloorGeometry.CollectCoreBoxes(plan, boxes);

            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec box = boxes[i];
                // 墙盒的 Center.y 在 1.7m 左右（整面墙的中心），不能拿它比眼高：
                // 要看这堵墙是否"遮住了眼高这一层"，门楣（底在 2.1m）就不该算。
                float bottom = box.Center.y - box.Size.y * 0.5f;
                float top = box.Center.y + box.Size.y * 0.5f;
                if (top <= 0.25f) continue;
                if (bottom >= EyeHeight) continue;
                Rasterise(solid, cols, rows, box.Footprint);
            }
        }

        static void Rasterise(bool[] solid, int cols, int rows, RectF r)
        {
            float x0 = r.X - Cell * 0.5f, x1 = r.XMax + Cell * 0.5f;
            float y0 = r.Y - Cell * 0.5f, y1 = r.YMax + Cell * 0.5f;
            int cx0 = Mathf.Clamp(Mathf.FloorToInt(x0 / Cell), 0, cols - 1);
            int cx1 = Mathf.Clamp(Mathf.CeilToInt(x1 / Cell), 0, cols - 1);
            int cy0 = Mathf.Clamp(Mathf.FloorToInt(y0 / Cell), 0, rows - 1);
            int cy1 = Mathf.Clamp(Mathf.CeilToInt(y1 / Cell), 0, rows - 1);
            for (int y = cy0; y <= cy1; y++)
                for (int x = cx0; x <= cx1; x++)
                    solid[y * cols + x] = true;
        }

        static bool SolidAt(bool[] solid, int cols, int rows, float x, float y)
        {
            if (x < 0f || y < 0f) return true;
            int cx = Mathf.FloorToInt(x / Cell);
            int cy = Mathf.FloorToInt(y / Cell);
            if (cx < 0 || cy < 0 || cx >= cols || cy >= rows) return true;
            return solid[cy * cols + cx];
        }

        static bool Walkable(bool[] solid, int cols, int rows, Vector2 p)
        {
            return !SolidAt(solid, cols, rows, p.x, p.y);
        }

        static float RayLength(bool[] solid, int cols, int rows, Vector2 from, Vector2 dir, Func<Vector2, bool> visiblePoint)
        {
            float t = 0f;
            while (t < MaxRay)
            {
                t += Step;
                Vector2 p = new Vector2(from.x + dir.x * t, from.y + dir.y * t);
                if (SolidAt(solid, cols, rows, p.x, p.y)) return t - Step;
                if (visiblePoint != null && !visiblePoint(p)) return t - Step;
            }
            return MaxRay;
        }

        // ------------------------------------------------------------------ 采样

        static void SampleCorridors(FloorPlan plan, List<Vector2> points)
        {
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                bool vertical = r.H >= r.W;
                float band = vertical ? r.W : r.H;
                float start = vertical ? r.Y : r.X;
                float length = vertical ? r.H : r.W;
                float[] across = { 0.25f, 0.5f, 0.75f };

                for (int a = 0; a < across.Length; a++)
                {
                    float lateral = (vertical ? r.X : r.Y) + band * across[a];
                    for (float s = 0.9f; s < length - 0.4f; s += 1f)
                    {
                        points.Add(vertical ? new Vector2(lateral, start + s) : new Vector2(start + s, lateral));
                    }
                }
            }
        }

        static void SampleRooms(FloorPlan plan, List<Vector2> points)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                RectF r = room.Rect;
                if (room.Outline != null && !FloorEnvelope.Contains(room.Outline, r.Center)) continue;
                for (float x = r.X + 1f; x < r.XMax - 0.5f; x += 4f)
                {
                    for (float y = r.Y + 1f; y < r.YMax - 0.5f; y += 4f)
                    {
                        Vector2 p = new Vector2(x, y);
                        if (room.Outline != null && !FloorEnvelope.Contains(room.Outline, p)) continue;
                        if (RoomShape.BlockedInFloor(plan, p)) continue;
                        points.Add(p);
                    }
                }
            }
        }

        static bool InCorridor(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Corridors.Count; i++)
                if (plan.Corridors[i].Rect.Contains(p)) return true;
            return false;
        }
    }
}
