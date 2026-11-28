using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Reports pairs of rooms whose rects face each other across a narrow gap. "Narrow gap" here is
    /// the strip the user sees between two parallel wall faces: it is walkable-open in neither room,
    /// so FloorGeometry bricks it with solid mass and it reads as a meaningless slot from above.
    /// </summary>
    public static class RoomGapProbe
    {
        [MenuItem("Escape From Work/Debug/Room Gap Probe")]
        public static void Run()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            StringBuilder report = new StringBuilder();
            report.AppendLine("[RoomGap] pairs of rooms facing across 0.10-3.00 m, per floor");

            for (int f = 0; f < raid.FloorCount; f++)
            {
                FloorPlan plan = raid.Floors[f].Plan;
                int pairs = 0;
                Dictionary<int, int> buckets = new Dictionary<int, int>();
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    for (int j = i + 1; j < plan.Rooms.Count; j++)
                    {
                        RectF a = plan.Rooms[i].Rect;
                        RectF b = plan.Rooms[j].Rect;
                        if (!Gap(a, b, out bool alongX, out float gap, out float midX, out float midZ)) continue;
                        if (gap < 0.10f || gap > 3.00f) continue;
                        int key = Mathf.RoundToInt(gap / 0.10f);
                        buckets.TryGetValue(key, out int count);
                        buckets[key] = count + 1;
                        pairs++;
                        if (pairs <= 24)
                            report.AppendLine(string.Format(
                                "  F{0} gap={1:0.00} {2} mid=({3:0.0},{4:0.0}) rooms {5}/{6}",
                                f, gap, alongX ? "X" : "Z", midX, midZ, i, j));
                    }
                }

                report.AppendLine(string.Format("[RoomGap] F{0}: {1} room count, {2} narrow facing pairs",
                    f, plan.Rooms.Count, pairs));
                report.AppendLine(string.Format("[RoomGap] F{0}: hallBreaks={1}", f, plan.HallBreaks.Count));
                int narrow = 0;
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    RectF cr = plan.Corridors[c].Rect;
                    if (Mathf.Min(cr.W, cr.H) >= 1.2f) continue;
                    narrow++;
                    if (narrow <= 10)
                        report.AppendLine(string.Format("  narrow corridor ({0:0.0},{1:0.0} {2:0.0}x{3:0.0}) service={4}",
                            cr.X, cr.Y, cr.W, cr.H, plan.Corridors[c].Service));
                }
                report.AppendLine(string.Format("[RoomGap] F{0}: corridors={1} narrow(<1.2)={2}", f, plan.Corridors.Count, narrow));
                for (int i = 0; i < plan.HallBreaks.Count && i < 12; i++)
                    report.AppendLine(string.Format("  break {0} rect=({1:0.0},{2:0.0} {3:0.0}x{4:0.0})",
                        i, plan.HallBreaks[i].X, plan.HallBreaks[i].Y, plan.HallBreaks[i].W, plan.HallBreaks[i].H));
                List<int> keys = new List<int>(buckets.Keys);
                keys.Sort();
                for (int k = 0; k < keys.Count; k++)
                    report.AppendLine(string.Format("  bucket {0:0.0}m -> {1}", keys[k] * 0.1f, buckets[keys[k]]));
            }

            string path = "docs/3d-rebuild/room-gap.txt";
            Directory.CreateDirectory("docs/3d-rebuild");
            File.WriteAllText(path, report.ToString());
            Debug.Log("[RoomGap] written -> " + Path.GetFullPath(path));
        }

        static bool Gap(RectF a, RectF b, out bool alongX, out float gap, out float midX, out float midZ)
        {
            alongX = false;
            gap = 0f;
            midX = 0f;
            midZ = 0f;

            // Vertical faces (gap along X), require Z overlap long enough to read as a slot.
            float zFrom = Mathf.Max(a.Y, b.Y);
            float zTo = Mathf.Min(a.YMax, b.YMax);
            if (zTo - zFrom > 1.5f)
            {
                if (a.XMax <= b.X && b.X - a.XMax <= 3.0f)
                {
                    alongX = true;
                    gap = b.X - a.XMax;
                    midX = (a.XMax + b.X) * 0.5f;
                    midZ = (zFrom + zTo) * 0.5f;
                    return true;
                }
                if (b.XMax <= a.X && a.X - b.XMax <= 3.0f)
                {
                    alongX = true;
                    gap = a.X - b.XMax;
                    midX = (b.XMax + a.X) * 0.5f;
                    midZ = (zFrom + zTo) * 0.5f;
                    return true;
                }
            }

            // Horizontal faces (gap along Z), require X overlap.
            float xFrom = Mathf.Max(a.X, b.X);
            float xTo = Mathf.Min(a.XMax, b.XMax);
            if (xTo - xFrom > 1.5f)
            {
                if (a.YMax <= b.Y && b.Y - a.YMax <= 3.0f)
                {
                    gap = b.Y - a.YMax;
                    midX = (xFrom + xTo) * 0.5f;
                    midZ = (a.YMax + b.Y) * 0.5f;
                    return true;
                }
                if (b.YMax <= a.Y && a.Y - b.YMax <= 3.0f)
                {
                    gap = a.Y - b.YMax;
                    midX = (xFrom + xTo) * 0.5f;
                    midZ = (b.YMax + a.Y) * 0.5f;
                    return true;
                }
            }

            return false;
        }
    }
}
