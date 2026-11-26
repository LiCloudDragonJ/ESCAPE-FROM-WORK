using System.Collections.Generic;
using System.Text;
using EscapeFromWork.LevelGen;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// R2/R3 手工体检工具（不进游戏）：一次打印若干种子的走廊/房间/视线统计。
    /// 用 `Tuanjie.exe -batchmode -quit -projectPath game -executeMethod EscapeFromWork.EditorTools.R2Probe.Dump -logFile probe.log`
    /// 跑完在日志里 grep "R2PROBE"。
    /// </summary>
    public static class R2Probe
    {
        public static void Dump()
        {
            int[] seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < seeds.Length; i++)
            {
                int seed = seeds[i];
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seed));
                SightlineAudit.Report rep = SightlineAudit.Run(plan);

                int corridors = plan.Corridors.Count;
                int service = 0, vestibule = 0, main = 0, over32 = 0, over32Main = 0, svcMissing = 0;
                float longest = 0f, longestMain = 0f, corridorArea = 0f;
                string longestTag = "";
                StringBuilder missing = new StringBuilder();
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    Corridor cc = plan.Corridors[c];
                    if (cc.Service && !HasSideOpening(plan, cc))
                    {
                        svcMissing++;
                        if (svcMissing <= 4)
                            missing.AppendFormat(" [svc#{0} c=({1:F1},{2:F1}) s=({3:F1},{4:F1}) v={5}{6}{7}]",
                                c, cc.Rect.Center.x, cc.Rect.Center.y, cc.Rect.W, cc.Rect.H, cc.Vertical,
                                Neighbours(plan, cc), NearOpenings(plan, cc));
                    }
                    float len = Mathf.Max(cc.Rect.W, cc.Rect.H);
                    if (len > 32.5f)
                    {
                        over32++;
                        if (cc.Main) over32Main++;
                    }
                    if (len > longest)
                    {
                        longest = len;
                        longestTag = string.Format("{0}x{1}{2}{3}", cc.Rect.W, cc.Rect.H,
                            cc.Main ? " main" : "", cc.Service ? " svc" : cc.Vestibule ? " vest" : "");
                    }
                    if (cc.Service) service++;
                    if (cc.Vestibule) vestibule++;
                    if (cc.Main)
                    {
                        main++;
                        if (len > longestMain) longestMain = len;
                    }
                    corridorArea += cc.Rect.Area;
                }

                List<float> areas = new List<float>();
                float roomArea = 0f;
                for (int r = 0; r < plan.Rooms.Count; r++)
                {
                    areas.Add(plan.Rooms[r].Rect.Area);
                    roomArea += plan.Rooms[r].Rect.Area;
                }
                areas.Sort();
                float median = areas.Count == 0 ? 0f : areas[areas.Count / 2];
                int tiny = 0;
                for (int a = 0; a < areas.Count; a++) if (areas[a] < 56f) tiny++;
                int bigRooms = 0;
                StringBuilder big = new StringBuilder();
                for (int r = 0; r < plan.Rooms.Count; r++)
                {
                    RectF rr = plan.Rooms[r].Rect;
                    float side = Mathf.Max(rr.W, rr.H);
                    if (side <= 32f) continue;
                    bigRooms++;
                    if (bigRooms <= 3)
                        big.AppendFormat(" [big#{0} c=({1:F1},{2:F1}) s=({3:F1},{4:F1})]",
                            r, rr.Center.x, rr.Center.y, rr.W, rr.H);
                }

                sb.AppendLine(string.Format(
                    "R2PROBE seed={0} rooms={1} median={2:F1} tiny={3} cover={4:F3} corridors={5} main={6} service={7} vest={8} longest={9:F1}({17}) mainLongest={10:F1} over32={18}/{19} corrLen={11:F0} open={12} sight_corridor={13:F1} sight_room={14:F1} sight_all={15:F1} samples={16} from=({20:F1},{21:F1}) to=({22:F1},{23:F1}) svcMissing={24}{25} bigRooms={26}{27}",
                    seed, plan.Rooms.Count, median, tiny, roomArea / plan.Bounds.Area,
                    corridors, main, service, vestibule, longest, longestMain,
                    plan.Corridors.Count == 0 ? 0f : SumLength(plan),
                    plan.CorridorOpenings.Count,
                    rep.CorridorMax, rep.RoomMax, rep.GlobalMax, rep.Samples,
                    longestTag, over32, over32Main,
                    rep.From.x, rep.From.y, rep.To.x, rep.To.y,
                    svcMissing, missing.ToString(), bigRooms, big.ToString()));
                if (rep.GlobalMax > 43f)
                    sb.AppendLine("R2PROBE PATH seed=" + seed + " " + Describe(plan, rep.From, rep.To));
            }
            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("R2PROBE.txt", sb.ToString());
        }

        /// <summary>
        /// 复刻 FloorCoverageTests.SlabIsEitherWalkableOrFilled 的判定，把"既不可走、也不在任何墙体/实心盒里"
        /// 的采样点打出来（带最近的可走矩形），用来查这类回归。
        /// </summary>
        public static void Holes()
        {
            int[] seeds = { 1, 2, 3, 7 };
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < seeds.Length; i++)
            {
                int seed = seeds[i];
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seed));
                List<BoxSpec> fill = new List<BoxSpec>();
                FloorGeometry.CollectShellBoxes(plan, fill);
                FloorGeometry.CollectWallBoxes(plan, fill);
                FloorGeometry.CollectVoidBoxes(plan, fill);

                const float step = 0.5f;
                int total = 0, holes = 0, shown = 0;
                StringBuilder detail = new StringBuilder();
                for (float z = step * 0.5f; z < plan.Bounds.H; z += step)
                {
                    for (float x = step * 0.5f; x < plan.Bounds.W; x += step)
                    {
                        Vector2 pt = new Vector2(x, z);
                        if (plan.Envelope != null && plan.Envelope.Outline != null && !FloorEnvelope.Contains(plan.Envelope.Outline, pt)) continue;
                        total++;
                        if (Walkable(plan, pt)) continue;
                        if (InBox(fill, pt)) continue;
                        holes++;
                        if (shown < 6)
                        {
                            shown++;
                            detail.AppendFormat(" [{0:F1},{1:F1}{2}{3} pc={4} ol={5}]", x, z, Nearest(plan, pt), NearbyOpenings(plan, pt),
                                InPieces(plan, pt) ? 1 : 0,
                                plan.Envelope != null && plan.Envelope.Outline != null && FloorEnvelope.Contains(plan.Envelope.Outline, pt) ? 1 : 0);
                        }
                        if (seed == 2 && holes == 1) detail.Append(DumpBoxes(fill, pt));
                    }
                }
                sb.AppendFormat("R2PROBE HOLES seed={0} holes={1}/{2} ({3:F2}%){4}\n", seed, holes, total, total == 0 ? 0f : 100f * holes / total, detail);
            }
            Debug.Log(sb.ToString());
        }

        static bool Walkable(FloorPlan plan, Vector2 pt)
        {
            for (int r = 0; r < plan.Rooms.Count; r++) if (plan.Rooms[r].Rect.Contains(pt)) return true;
            for (int c = 0; c < plan.Corridors.Count; c++) if (plan.Corridors[c].Rect.Contains(pt)) return true;
            for (int s = 0; s < plan.Stairs.Count; s++) if (plan.Stairs[s].Contains(pt)) return true;
            if (plan.Core.Contains(pt)) return true;
            for (int s = 0; s < plan.SpawnZones.Count; s++) if (plan.SpawnZones[s].Contains(pt)) return true;
            return false;
        }

        static bool InBox(List<BoxSpec> boxes, Vector2 pt)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec b = boxes[i];
                float dx = pt.x - b.Center.x;
                float dz = pt.y - b.Center.z;
                if (Mathf.Abs(b.Yaw) > 1e-4f)
                {
                    float c = Mathf.Cos(b.Yaw), s = Mathf.Sin(b.Yaw);
                    float lx = dx * c - dz * s;
                    float lz = dx * s + dz * c;
                    dx = lx; dz = lz;
                }
                if (Mathf.Abs(dx) <= b.Size.x * 0.5f && Mathf.Abs(dz) <= b.Size.z * 0.5f) return true;
            }
            return false;
        }

        static string Nearest(FloorPlan plan, Vector2 pt)
        {
            float best = float.MaxValue;
            string tag = " none";
            for (int c = 0; c < plan.Corridors.Count; c++)
            {
                RectF r = plan.Corridors[c].Rect;
                float dx = Mathf.Max(Mathf.Max(r.X - pt.x, pt.x - r.XMax), 0f);
                float dz = Mathf.Max(Mathf.Max(r.Y - pt.y, pt.y - r.YMax), 0f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best)
                {
                    best = d;
                    tag = string.Format(" C{0} s=({1:F2},{2:F2}) d={3:F2}{4}", c, r.W, r.H, d,
                        plan.Corridors[c].Service ? " svc" : plan.Corridors[c].Vestibule ? " vest" : "");
                }
            }
            for (int r2 = 0; r2 < plan.Rooms.Count; r2++)
            {
                RectF r = plan.Rooms[r2].Rect;
                float dx = Mathf.Max(Mathf.Max(r.X - pt.x, pt.x - r.XMax), 0f);
                float dz = Mathf.Max(Mathf.Max(r.Y - pt.y, pt.y - r.YMax), 0f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best)
                {
                    best = d;
                    tag = string.Format(" R{0} s=({1:F2},{2:F2}) d={3:F2}", r2, r.W, r.H, d);
                }
            }
            return tag;
        }

        static string DumpBoxes(List<BoxSpec> boxes, Vector2 pt)
        {
            StringBuilder sb = new StringBuilder(" BOXES:");
            int n = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec b = boxes[i];
                float dx = Mathf.Abs(pt.x - b.Center.x) - b.Size.x * 0.5f;
                float dz = Mathf.Abs(pt.y - b.Center.z) - b.Size.z * 0.5f;
                if (Mathf.Max(dx, dz) > 1.5f) continue;
                n++;
                sb.AppendFormat(" [c=({0:F2},{1:F2}) s=({2:F2},{3:F2}) yaw={4:F2}]", b.Center.x, b.Center.z, b.Size.x, b.Size.z, b.Yaw);
            }
            sb.Append(" n=").Append(n);
            return sb.ToString();
        }

        static bool InPieces(FloorPlan plan, Vector2 pt)
        {
            if (plan.Envelope == null || plan.Envelope.Pieces == null) return true;
            for (int i = 0; i < plan.Envelope.Pieces.Count; i++)
                if (FloorEnvelope.Contains(plan.Envelope.Pieces[i], pt)) return true;
            return false;
        }

        static string NearbyOpenings(FloorPlan plan, Vector2 pt)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Door d = plan.CorridorOpenings[i];
                if ((d.Center - pt).sqrMagnitude > 4f) continue;
                sb.AppendFormat(" op[{0:F2},{1:F2} {2} w={3:F2} d={4:F2}]",
                    d.Center.x, d.Center.y, d.Side, d.Width, Vector2.Distance(d.Center, pt));
            }
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                Room room = plan.Rooms[r];
                if (room.HasDoor && room.Door.Width > 0.05f && (room.Door.Center - pt).sqrMagnitude <= 4f)
                    sb.AppendFormat(" rdoor[{0:F2},{1:F2} {2} w={3:F2}]", room.Door.Center.x, room.Door.Center.y, room.Door.Side, room.Door.Width);
                for (int k = 0; k < room.ExtraDoors.Count; k++)
                {
                    Door d = room.ExtraDoors[k];
                    if ((d.Center - pt).sqrMagnitude <= 4f)
                        sb.AppendFormat(" xdoor[{0:F2},{1:F2} {2} w={3:F2}]", d.Center.x, d.Center.y, d.Side, d.Width);
                }
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++)
            {
                Door d = plan.LoopDoors[i];
                if ((d.Center - pt).sqrMagnitude <= 4f)
                    sb.AppendFormat(" ldoor[{0:F2},{1:F2} {2} w={3:F2}]", d.Center.x, d.Center.y, d.Side, d.Width);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 专门查两个回归问题：
        /// 1) 同一条墙线上有没有叠两层墙（WallLayoutTests 的判定，容差 0.05m）；
        /// 2) 120 个种子（Podium/Tower/Top 三种带宽）里有没有出生区不够两个"远撤离"。
        /// </summary>
        public static void Problems()
        {
            StringBuilder sb = new StringBuilder();

            int[] seeds = { 1, 3, 7, 11, 19 };
            for (int i = 0; i < seeds.Length; i++)
            {
                int seed = seeds[i];
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seed));
                List<BoxSpec> boxes = new List<BoxSpec>();
                FloorGeometry.CollectWallBoxes(plan, boxes);

                int stacked = 0;
                StringBuilder detail = new StringBuilder();
                for (int a = 0; a < boxes.Count; a++)
                {
                    bool ha = boxes[a].Size.x >= boxes[a].Size.z;
                    float la = ha ? boxes[a].Center.z : boxes[a].Center.x;
                    float a0 = ha ? boxes[a].Center.x - boxes[a].Size.x * 0.5f : boxes[a].Center.z - boxes[a].Size.z * 0.5f;
                    float a1 = ha ? boxes[a].Center.x + boxes[a].Size.x * 0.5f : boxes[a].Center.z + boxes[a].Size.z * 0.5f;
                    for (int b = a + 1; b < boxes.Count; b++)
                    {
                        bool hb = boxes[b].Size.x >= boxes[b].Size.z;
                        if (ha != hb) continue;
                        float lb = hb ? boxes[b].Center.z : boxes[b].Center.x;
                        if (Mathf.Abs(la - lb) > 0.02f) continue;
                        float b0 = hb ? boxes[b].Center.x - boxes[b].Size.x * 0.5f : boxes[b].Center.z - boxes[b].Size.z * 0.5f;
                        float b1 = hb ? boxes[b].Center.x + boxes[b].Size.x * 0.5f : boxes[b].Center.z + boxes[b].Size.z * 0.5f;
                        float overlap = Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
                        if (overlap <= 0.05f) continue;
                        stacked++;
                        if (stacked <= 3)
                            detail.AppendFormat(" [A c=({0:F2},{1:F2},{2:F2}) s=({3:F2},{4:F2},{5:F2}) h={6} line={7:F3} span=[{8:F1},{9:F1}] || B c=({10:F2},{11:F2},{12:F2}) s=({13:F2},{14:F2},{15:F2}) h={16} line={17:F3} span=[{18:F1},{19:F1}] || ov={20:F2}]",
                                boxes[a].Center.x, boxes[a].Center.y, boxes[a].Center.z,
                                boxes[a].Size.x, boxes[a].Size.y, boxes[a].Size.z,
                                ha, la, a0, a1,
                                boxes[b].Center.x, boxes[b].Center.y, boxes[b].Center.z,
                                boxes[b].Size.x, boxes[b].Size.y, boxes[b].Size.z,
                                hb, lb, b0, b1, overlap);
                    }
                }
                sb.AppendLine(string.Format("R2PROBE STACK seed={0} boxes={1} stacked={2}{3}",
                    seed, boxes.Count, stacked, detail.ToString()));
            }

            for (int seed = 1; seed <= 120; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(ExtractionSpec(seed));
                FloorValidator.Grid grid = FloorValidator.BuildGrid(plan);
                List<FloorValidator.SpawnAudit> audits = FloorValidator.AuditSpawns(plan, grid);
                for (int i = 0; i < audits.Count; i++)
                {
                    if (audits[i].FarExtractions >= 2) continue;
                    sb.AppendLine(string.Format(
                        "R2PROBE SPAWN seed={0} spawn={1} far={2} required={3:F0} applied={4:F0} extractions={5} at=({6:F1},{7:F1})",
                        seed, i, audits[i].FarExtractions, audits[i].RequiredDistance, audits[i].AppliedDistance,
                        plan.Extractions.Count, plan.SpawnZones[i].Center.x, plan.SpawnZones[i].Center.y));
                    float[] dist = FloorValidator.Flood(grid, plan.SpawnZones[i].Center);
                    StringBuilder cand = new StringBuilder();
                    for (int c = 0; c < plan.ExtractionCandidates.Count; c++)
                    {
                        float d = FloorValidator.WalkDistanceAt(grid, dist, plan.ExtractionCandidates[c].Center);
                        cand.AppendFormat(" {0}:{1:F0}", plan.ExtractionCandidates[c].Kind, d);
                    }
                    sb.AppendLine("R2PROBE SPAWN-CAND " + cand);
                }
            }
            sb.AppendLine("R2PROBE PROBLEMS done");

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("R2PROBE.txt", sb.ToString());
        }

        static FloorSpec ExtractionSpec(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            int band = seed % 3;
            spec.Band = band == 0 ? FloorBand.Podium : band == 1 ? FloorBand.Tower : FloorBand.Top;
            spec.Width = spec.Band == FloorBand.Podium ? 280f : spec.Band == FloorBand.Tower ? 200f : 160f;
            spec.Height = spec.Band == FloorBand.Podium ? 280f : spec.Band == FloorBand.Tower ? 160f : 120f;
            spec.Prototype = (FloorPrototype)(seed % 6);
            spec.CorridorLevels = 3;
            return spec;
        }

        /// <summary>该段周围 1m 内的开口（看门是开错了墙还是开错了段）。</summary>
        static string NearOpenings(FloorPlan plan, Corridor c)
        {
            StringBuilder sb = new StringBuilder();
            RectF r = c.Rect.Inflate(1.0f);
            int shown = 0;
            for (int i = 0; i < plan.CorridorOpenings.Count && shown < 3; i++)
            {
                Vector2 p = plan.CorridorOpenings[i].Center;
                if (p.x < r.X || p.x > r.XMax || p.y < r.Y || p.y > r.YMax) continue;
                shown++;
                sb.AppendFormat(" op#{0} ({1:F2},{2:F2}) {3} w={4:F1}", i, p.x, p.y,
                    plan.CorridorOpenings[i].Side, plan.CorridorOpenings[i].Width);
            }
            if (shown == 0) sb.Append(" op=none");
            return sb.ToString();
        }

        /// <summary>漏开口的服务廊段旁边的非服务走廊（定位"为什么没配上对"）。</summary>
        static string Neighbours(FloorPlan plan, Corridor c)
        {
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            RectF r = c.Rect;
            for (int i = 0; i < plan.Corridors.Count && shown < 2; i++)
            {
                Corridor o = plan.Corridors[i];
                if (o.Service) continue;
                RectF s = o.Rect;
                float dx = Mathf.Max(0f, Mathf.Max(s.X - r.XMax, r.X - s.XMax));
                float dy = Mathf.Max(0f, Mathf.Max(s.Y - r.YMax, r.Y - s.YMax));
                if (dx > 1.2f || dy > 1.2f) continue;
                float ovX = Mathf.Min(r.XMax, s.XMax) - Mathf.Max(r.X, s.X);
                float ovY = Mathf.Min(r.YMax, s.YMax) - Mathf.Max(r.Y, s.Y);
                if (ovX <= 0.8f && ovY <= 0.8f) continue;
                shown++;
                sb.AppendFormat(" nb#{0} c=({1:F1},{2:F1}) s=({3:F1},{4:F1}) dx={5:F2} dy={6:F2} ovX={7:F1} ovY={8:F1}",
                    i, s.Center.x, s.Center.y, s.W, s.H, dx, dy, ovX, ovY);
            }
            if (shown == 0) sb.Append(" nb=none");
            return sb.ToString();
        }

        /// <summary>服务廊长墙上是否登记了开口（与 SightlineTests 同一判定口径）。</summary>
        static bool HasSideOpening(FloorPlan plan, Corridor c)
        {
            RectF r = c.Rect;
            bool vertical = c.Vertical;
            float start = vertical ? r.Y : r.X;
            float end = vertical ? r.YMax : r.XMax;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Vector2 p = plan.CorridorOpenings[i].Center;
                if (vertical)
                {
                    bool onWall = Mathf.Abs(p.x - r.X) < 0.2f || Mathf.Abs(p.x - r.XMax) < 0.2f;
                    if (!onWall) continue;
                    if (p.y < start - 0.01f || p.y > end + 0.01f) continue;
                    return true;
                }
                bool onHorizontalWall = Mathf.Abs(p.y - r.Y) < 0.2f || Mathf.Abs(p.y - r.YMax) < 0.2f;
                if (!onHorizontalWall) continue;
                if (p.x < start - 0.01f || p.x > end + 0.01f) continue;
                return true;
            }
            return false;
        }

        /// <summary>把一条视线经过的走廊/房间按顺序列出来，定位"哪一段漏了断点"。</summary>
        static string Describe(FloorPlan plan, Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a);
            float len = dir.magnitude;
            if (len < 0.01f) return "";
            dir /= len;
            StringBuilder sb = new StringBuilder();
            string current = null;
            float runStart = 0f;
            for (float t = 0f; t <= len; t += 0.1f)
            {
                Vector2 p = a + dir * t;
                string label = "?";
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    if (!plan.Corridors[c].Rect.Contains(p)) continue;
                    label = string.Format("C{0}{1}{2}", c, plan.Corridors[c].Main ? "M" : "", plan.Corridors[c].Service ? "S" : plan.Corridors[c].Vestibule ? "V" : "");
                    break;
                }
                if (label == "?")
                {
                    for (int r = 0; r < plan.Rooms.Count; r++)
                    {
                        if (!plan.Rooms[r].Rect.Contains(p)) continue;
                        label = "R" + r;
                        break;
                    }
                }
                if (label != current)
                {
                    if (current != null) sb.AppendFormat("{0}[{1:F1}-{2:F1}] ", current, runStart, t);
                    current = label;
                    runStart = t;
                }
            }
            if (current != null) sb.AppendFormat("{0}[{1:F1}-{2:F1}] ", current, runStart, len);
            return sb.ToString();
        }

        static float SumLength(FloorPlan plan)
        {
            float total = 0f;
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                total += Mathf.Max(r.W, r.H);
            }
            return total;
        }
    }
}
