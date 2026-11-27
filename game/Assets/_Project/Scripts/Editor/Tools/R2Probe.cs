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
                            missing.AppendFormat(" [svc#{0} c=({1:F1},{2:F1}) s=({3:F1},{4:F1}) v={5}]",
                                c, cc.Rect.Center.x, cc.Rect.Center.y, cc.Rect.W, cc.Rect.H, cc.Vertical);
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

        /// <summary>
        /// R3 房间尺度体检：按真实轮廓（Outline 鞋带面积）算房间面积，打印分档直方图与中位数。
        /// 目标是中位 90–130㎡、&lt;56㎡ 占比 ≤10%、56–113 / 113–200 / 200–400 / 400+ 约 40/35/20/5。
        /// </summary>
        public static void Scale()
        {
            int[] seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<float> allMedians = new List<float>();
            for (int i = 0; i < seeds.Length; i++)
            {
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seeds[i]));
                List<float> areas = new List<float>();
                for (int r = 0; r < plan.Rooms.Count; r++) areas.Add(AreaOf(plan.Rooms[r]));
                areas.Sort();
                int n = areas.Count;
                float median = n == 0 ? 0f : (n % 2 == 1 ? areas[n / 2] : (areas[n / 2 - 1] + areas[n / 2]) * 0.5f);
                allMedians.Add(median);
                int tiny = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0;
                for (int k = 0; k < n; k++)
                {
                    float a = areas[k];
                    if (a < 56f) tiny++;
                    else if (a < 113f) b1++;
                    else if (a < 200f) b2++;
                    else if (a < 400f) b3++;
                    else b4++;
                }
                System.Text.StringBuilder tinyInfo = new System.Text.StringBuilder();
                int shown = 0;
                for (int r = 0; r < plan.Rooms.Count && shown < 3; r++)
                {
                    float a = AreaOf(plan.Rooms[r]);
                    if (a >= 56f) continue;
                    int doors = (plan.Rooms[r].HasDoor ? 1 : 0) + (plan.Rooms[r].ExtraDoors == null ? 0 : plan.Rooms[r].ExtraDoors.Count);
                    tinyInfo.AppendFormat(" [t#{0} {1:F1}m2 {2:F1}x{3:F1} d={4} ol={5}]", r, a,
                        plan.Rooms[r].Rect.W, plan.Rooms[r].Rect.H, doors, plan.Rooms[r].Outline != null);
                    shown++;
                }
                float total = 0f;
                for (int k = 0; k < n; k++) total += areas[k];
                sb.AppendFormat(
                    "R2PROBE SCALE seed={0} rooms={1} mean={2:F1} median={3:F1} min={4:F1} max={5:F1} " +
                    "tiny<56={6} ({7:F1}%) 56-113={8} ({9:F1}%) 113-200={10} ({11:F1}%) 200-400={12} ({13:F1}%) 400+={14} ({15:F1}%){16}\n",
                    seeds[i], n, n == 0 ? 0f : total / n, median, n == 0 ? 0f : areas[0], n == 0 ? 0f : areas[n - 1],
                    tiny, Pct(tiny, n), b1, Pct(b1, n), b2, Pct(b2, n), b3, Pct(b3, n), b4, Pct(b4, n), tinyInfo);
            }
            Debug.Log(sb.ToString());
        }

        static float Pct(int part, int total)
        {
            return total == 0 ? 0f : 100f * part / total;
        }

        /// <summary>
        /// 追一条长视线为什么没被挡住：打印射线穿过的每面墙盒（沿路径的跨度）和附近的门登记。
        /// 用法：-executeMethod EscapeFromWork.EditorTools.R2Probe.Ray
        /// </summary>
        /// <summary>沿最坏视线打印穿过的房间/走廊/门与挡视屏，用来判断缺口到底在哪一段。</summary>
        /// <summary>道具统计：计划里有多少件、按名字分组、其中多少能查到目录预制体。</summary>
        public static void Props()
        {
            FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(1));
            PropCatalog catalog = PropCatalog.LoadOrCreateDefault();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            int hits = 0;
            for (int i = 0; i < plan.Props.Count; i++)
            {
                string name = plan.Props[i].Spec.Name;
                int n;
                counts.TryGetValue(name, out n);
                counts[name] = n + 1;
                PropCatalog.Entry e;
                if (catalog.TryGet(name, out e)) hits++;
            }

            var sb = new StringBuilder();
            sb.AppendFormat("R2PROBE PROPS total={0} catalogHits={1} entries={2}\n", plan.Props.Count, hits, catalog.Entries.Count);
            foreach (var p in counts)
            {
                PropCatalog.Entry e;
                sb.AppendFormat("  {0} x{1} {2}\n", p.Key, p.Value, catalog.TryGet(p.Key, out e) ? "prefab" : "greybox");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>设施分布：每层多少种设施/主题、各设施的房间数与面积中位数。</summary>
        public static void Facilities()
        {
            int[] seeds = { 1, 2, 3, 7 };
            var sb = new StringBuilder();
            for (int s = 0; s < seeds.Length; s++)
            {
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seeds[s]));
                var byKind = new System.Collections.Generic.Dictionary<FacilityKind, System.Collections.Generic.List<float>>();
                var themes = new System.Collections.Generic.HashSet<FloorTheme>();
                for (int i = 0; i < plan.Zones.Count; i++) themes.Add(plan.Zones[i].Theme);

                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    System.Collections.Generic.List<float> list;
                    if (!byKind.TryGetValue(plan.Rooms[i].Facility, out list))
                    {
                        list = new System.Collections.Generic.List<float>();
                        byKind[plan.Rooms[i].Facility] = list;
                    }
                    list.Add(RoomShape.AreaOf(plan.Rooms[i]));
                }

                sb.AppendFormat("R2PROBE FAC seed={0} rooms={1} themes={2} kinds={3}\n",
                    seeds[s], plan.Rooms.Count, themes.Count, byKind.Count);
                foreach (var pair in byKind)
                {
                    var areas = pair.Value;
                    areas.Sort();
                    float med = areas[areas.Count / 2];
                    sb.AppendFormat("  {0,-14} x{1,-3} median={2:F0} min={3:F0} max={4:F0}\n",
                        pair.Key, areas.Count, med, areas[0], areas[areas.Count - 1]);
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>房间真实面积：有 Outline 用鞋带公式，否则退回包围盒。</summary>
        static float AreaOf(Room room)
        {
            if (room.Outline == null || room.Outline.Length < 3) return room.Rect.Area;
            return Mathf.Abs(FloorEnvelope.SignedArea(room.Outline));
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
