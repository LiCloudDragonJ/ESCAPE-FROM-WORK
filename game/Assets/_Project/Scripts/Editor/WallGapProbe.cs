using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Diagnostic: how many interior walls stand as near-parallel pairs with a sliver between them?
    /// Prints gap pairs per raid floor so we can decide the merge tolerance before rewriting walls.
    /// </summary>
    public static class WallGapProbe
    {
        [MenuItem("Escape From Work/Debug/Wall Gap Probe")]
        public static void Run()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            for (int f = 0; f < raid.FloorCount; f++)
            {
                FloorPlan plan = raid.Floors[f].Plan;
                List<BoxSpec> boxes = new List<BoxSpec>();
                FloorGeometry.CollectWallBoxes(plan, boxes);
                FloorGeometry.FillWallSlivers(plan, boxes);
                int horizontal = 0, vertical = 0, pairs = 0;
                var report = new StringBuilder();
                var histogram = new SortedDictionary<int, int>();
                for (int i = 0; i < boxes.Count; i++)
                {
                    BoxSpec a = boxes[i];
                    bool ah = Mathf.Abs(a.Size.x) >= Mathf.Abs(a.Size.z);
                    if (ah) horizontal++; else vertical++;
                }
                for (int i = 0; i < boxes.Count; i++)
                {
                    BoxSpec a = boxes[i];
                    bool ah = Mathf.Abs(a.Size.x) >= Mathf.Abs(a.Size.z);
                    float lineA = ah ? a.Center.z : a.Center.x;
                    float a0 = ah ? a.Center.x - a.Size.x * 0.5f : a.Center.z - a.Size.z * 0.5f;
                    float a1 = ah ? a.Center.x + a.Size.x * 0.5f : a.Center.z + a.Size.z * 0.5f;
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        BoxSpec b = boxes[j];
                        bool bh = Mathf.Abs(b.Size.x) >= Mathf.Abs(b.Size.z);
                        if (ah != bh) continue;
                        float lineB = bh ? b.Center.z : b.Center.x;
                        float gap = Mathf.Abs(lineA - lineB);
                        if (gap < 0.06f || gap > 3.0f) continue;
                        if (Mathf.Abs(a.Center.y - b.Center.y) > 0.1f) continue;
                        float b0 = bh ? b.Center.x - b.Size.x * 0.5f : b.Center.z - b.Size.z * 0.5f;
                        float b1 = bh ? b.Center.x + b.Size.x * 0.5f : b.Center.z + b.Size.z * 0.5f;
                        float overlap = Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
                        if (overlap < 2.0f) continue;
                        pairs++;
                        int bucket = Mathf.RoundToInt(gap * 10f);
                        histogram.TryGetValue(bucket, out int seen);
                        histogram[bucket] = seen + 1;
                        if (gap <= 1.65f && report.Length < 2600)
                        {
                            float midLine = (lineA + lineB) * 0.5f;
                            float mu = (Mathf.Max(a0, b0) + Mathf.Min(a1, b1)) * 0.5f;
                            Vector2 mid = ah ? new Vector2(mu, midLine) : new Vector2(midLine, mu);
                            bool open = false;
                            for (int r = 0; r < plan.Rooms.Count && !open; r++) open = plan.Rooms[r].Rect.Contains(mid);
                            for (int c = 0; c < plan.Corridors.Count && !open; c++) open = plan.Corridors[c].Rect.Contains(mid);
                            report.AppendLine(string.Format(
                                "  F{0} {1} gap={2:0.00} overlap={3:0.0} at ({4:0.0},{5:0.0}) vs ({6:0.0},{7:0.0}) sz=({8:0.0}x{9:0.0})/ ({10:0.0}x{11:0.0}) rooms={12}/{13} openMid={14} brk={15}/{16}{17}",
                                f, ah ? "H" : "V", gap, overlap,
                                a.Center.x, a.Center.z, b.Center.x, b.Center.z,
                                a.Size.x, a.Size.z, b.Size.x, b.Size.z,
                                RoomLabel(plan, a), RoomLabel(plan, b), open,
                                NearBreak(plan, a.Center), NearBreak(plan, b.Center),
                                gap <= 1.45f
                                    ? "\n      A " + CorridorEdges(plan, ah, lineA) + "\n      B " + CorridorEdges(plan, ah, lineB)
                                    : string.Empty));
                        }
                    }
                }
                foreach (KeyValuePair<int, int> kv in histogram)
                    report.AppendLine(string.Format("  bucket {0:0.0}m -> {1}", kv.Key * 0.1f, kv.Value));
                Debug.Log(string.Format("[WallGap] F{0}: {1} wall boxes ({2} H / {3} V), {4} near-parallel sliver pairs\n{5}",
                    f, boxes.Count, horizontal, vertical, pairs, report));
            }
        }
        static string CorridorEdges(FloorPlan plan, bool horizontal, float line)
        {
            var sb = new StringBuilder();
            int shown = 0;
            for (int c = 0; c < plan.Corridors.Count && shown < 4; c++)
            {
                RectF r = plan.Corridors[c].Rect;
                float a = horizontal ? r.Y : r.X;
                float b = horizontal ? r.YMax : r.XMax;
                if (Mathf.Abs(a - line) > 0.2f && Mathf.Abs(b - line) > 0.2f) continue;
                sb.Append('c').Append(c).Append(":(").Append(r.X.ToString("0.0")).Append(',').Append(r.Y.ToString("0.0"))
                  .Append(' ').Append(r.W.ToString("0.0")).Append('x').Append(r.H.ToString("0.0")).Append(") ");
                shown++;
            }
            return sb.ToString();
        }

        static string RoomLabel(FloorPlan plan, BoxSpec box)
        {
            bool horizontal = Mathf.Abs(box.Size.x) >= Mathf.Abs(box.Size.z);
            float line = horizontal ? box.Center.z : box.Center.x;
            float inset = plan.Spec.WallInset;
            var tag = new StringBuilder();
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                RectF rr = plan.Rooms[r].Rect;
                float a = horizontal ? rr.Y : rr.X;
                float b = horizontal ? rr.YMax : rr.XMax;
                bool near = Mathf.Abs(a - (line - inset)) < 0.16f || Mathf.Abs(b - (line + inset)) < 0.16f;
                if (!near) continue;
                if (tag.Length > 0) tag.Append(',');
                tag.Append('r').Append(r);
                if (tag.Length > 18) break;
            }
            for (int c = 0; c < plan.Corridors.Count; c++)
            {
                RectF cr = plan.Corridors[c].Rect;
                float a = horizontal ? cr.Y : cr.X;
                float b = horizontal ? cr.YMax : cr.XMax;
                if (Mathf.Abs(a - line) < 0.16f || Mathf.Abs(b - line) < 0.16f
                    || Mathf.Abs(a - (line - inset)) < 0.16f || Mathf.Abs(b - (line + inset)) < 0.16f)
                {
                    if (tag.Length > 0) tag.Append(',');
                    tag.Append('c').Append(c);
                }
                if (tag.Length > 30) break;
            }
            for (int s = 0; s < plan.Stairs.Count; s++)
            {
                RectF sr = plan.Stairs[s];
                float a = horizontal ? sr.Y : sr.X;
                float b = horizontal ? sr.YMax : sr.XMax;
                if (Mathf.Abs(a - line) < 0.16f || Mathf.Abs(b - line) < 0.16f)
                {
                    if (tag.Length > 0) tag.Append(',');
                    tag.Append('s').Append(s);
                }
            }
            if (plan.Core.W > 0f)
            {
                float a = horizontal ? plan.Core.Y : plan.Core.X;
                float b = horizontal ? plan.Core.YMax : plan.Core.XMax;
                if (Mathf.Abs(a - line) < 0.16f || Mathf.Abs(b - line) < 0.16f)
                {
                    if (tag.Length > 0) tag.Append(',');
                    tag.Append("core");
                }
            }
            return tag.Length == 0 ? "-" : tag.ToString();
        }

        static string NearBreak(FloorPlan plan, Vector3 point)
        {
            for (int i = 0; i < plan.HallBreaks.Count; i++)
            {
                RectF br = plan.HallBreaks[i].Inflate(0.45f);
                if (br.Contains(new Vector2(point.x, point.z))) return "brk";
            }
            return "-";
        }
    }
}
