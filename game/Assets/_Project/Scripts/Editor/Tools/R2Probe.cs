using System.Collections.Generic;
using System.Text;
using EscapeFromWork.LevelGen;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// R2/R3 鎵嬪伐浣撴宸ュ叿锛堜笉杩涙父鎴忥級锛氫竴娆℃墦鍗拌嫢骞茬瀛愮殑璧板粖/鎴块棿/瑙嗙嚎缁熻銆?    /// 鐢?`Tuanjie.exe -batchmode -quit -projectPath game -executeMethod EscapeFromWork.EditorTools.R2Probe.Dump -logFile probe.log`
    /// 璺戝畬鍦ㄦ棩蹇楅噷 grep "R2PROBE"銆?    /// </summary>
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

        /// <summary>鏈嶅姟寤婇暱澧欎笂鏄惁鐧昏浜嗗紑鍙ｏ紙涓?SightlineTests 鍚屼竴鍒ゅ畾鍙ｅ緞锛夈€?/summary>
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

        /// <summary>鎶婁竴鏉¤绾跨粡杩囩殑璧板粖/鎴块棿鎸夐『搴忓垪鍑烘潵锛屽畾浣?鍝竴娈垫紡浜嗘柇鐐?銆?/summary>
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
        /// R3 鎴块棿灏哄害浣撴锛氭寜鐪熷疄杞粨锛圤utline 闉嬪甫闈㈢Н锛夌畻鎴块棿闈㈢Н锛屾墦鍗板垎妗ｇ洿鏂瑰浘涓庝腑浣嶆暟銆?        /// 鐩爣鏄腑浣?90鈥?30銕°€?lt;56銕?鍗犳瘮 鈮?0%銆?6鈥?13 / 113鈥?00 / 200鈥?00 / 400+ 绾?40/35/20/5銆?        /// </summary>
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
        /// 杩戒竴鏉￠暱瑙嗙嚎涓轰粈涔堟病琚尅浣忥細鎵撳嵃灏勭嚎绌胯繃鐨勬瘡闈㈠鐩掞紙娌胯矾寰勭殑璺ㄥ害锛夊拰闄勮繎鐨勯棬鐧昏銆?        /// 鐢ㄦ硶锛?executeMethod EscapeFromWork.EditorTools.R2Probe.Ray
        /// </summary>
        /// <summary>娌挎渶鍧忚绾挎墦鍗扮┛杩囩殑鎴块棿/璧板粖/闂ㄤ笌鎸¤灞忥紝鐢ㄦ潵鍒ゆ柇缂哄彛鍒板簳鍦ㄥ摢涓€娈点€?/summary>
        /// <summary>閬撳叿缁熻锛氳鍒掗噷鏈夊灏戜欢銆佹寜鍚嶅瓧鍒嗙粍銆佸叾涓灏戣兘鏌ュ埌鐩綍棰勫埗浣撱€?/summary>
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

        /// <summary>璁炬柦鍒嗗竷锛氭瘡灞傚灏戠璁炬柦/涓婚銆佸悇璁炬柦鐨勬埧闂存暟涓庨潰绉腑浣嶆暟銆?/summary>
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

        /// <summary>鎴块棿鐪熷疄闈㈢Н锛氭湁 Outline 鐢ㄩ瀷甯﹀叕寮忥紝鍚﹀垯閫€鍥炲寘鍥寸洅銆?/summary>
        static float AreaOf(Room room)
        {
            if (room.Outline == null || room.Outline.Length < 3) return room.Rect.Area;
            return Mathf.Abs(FloorEnvelope.SignedArea(room.Outline));
        }

        /// <summary>
        /// 鐢熸垚鑰楁椂鍩虹嚎锛歊7 鐨勫嚭鐢熷尯鏁ｅ竷淇浼氶澶栬窇绮楁爡鏍?flood锛?000 绉嶅瓙鎵规祴瀵规垚鏈緢鏁忔劅锛?        /// 鐢ㄨ繖涓帰閽堢‘璁ゃ€屾瘡灞傚鑺卞灏戞绉掋€嶈€屼笉鏄瓑鎵规祴璺戝崄鍒嗛挓銆?        /// </summary>
        public static void SpawnBench()
        {
            const int n = 120;
            System.Diagnostics.Stopwatch gen = System.Diagnostics.Stopwatch.StartNew();
            List<FloorPlan> plans = new List<FloorPlan>();
            for (int seed = 1; seed <= n; seed++) plans.Add(FloorGenerator.Generate(FloorSpec.Default(seed)));
            gen.Stop();

            System.Diagnostics.Stopwatch audit = System.Diagnostics.Stopwatch.StartNew();
            int failed = 0;
            float worstRatio = float.MaxValue;
            float minDiameter = float.MaxValue;
            for (int i = 0; i < plans.Count; i++)
            {
                FloorPlan plan = plans[i];
                FloorValidator.Grid g = FloorValidator.BuildGrid(plan, FloorValidator.CoarseCell);
                float diameter = FloorValidator.CoarseDiameter(plan, g);
                if (diameter < minDiameter) minDiameter = diameter;
                FloorValidator.SpawnSpacingAudit a = FloorValidator.AuditSpawnSpacing(plan, g, diameter);
                if (!a.Ok)
                {
                    failed++;
                    Debug.Log(string.Format(
                        "R2PROBE BENCH FAIL seed={0} spawns={1} min={2:F1} req={3:F1} diameter={4:F1} ratio={5:F2}",
                        i + 1, plan.SpawnZones.Count, a.MinDistance, a.Required, diameter,
                        a.Required > 0f ? a.MinDistance / a.Required : 0f));
                }
                if (a.Required > 0f)
                {
                    float ratio = a.MinDistance / a.Required;
                    if (ratio < worstRatio) worstRatio = ratio;
                }
            }
            audit.Stop();
            Debug.Log(string.Format(
                "R2PROBE BENCH seeds={0} gen_ms={1} gen_per_seed={2:F1} audit_ms={3} audit_per_seed={4:F1} spacing_fail={5} worst_ratio={6:F2} min_diameter={7:F1}",
                n, gen.ElapsedMilliseconds, gen.ElapsedMilliseconds / (double)n,
                audit.ElapsedMilliseconds, audit.ElapsedMilliseconds / (double)n, failed, worstRatio, minDiameter));
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
