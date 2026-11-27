using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>Pure structural health metrics from 06 §10. No scene or UnityEditor dependency.</summary>
    public static class LevelAudit
    {
        public const float MaxCorridorSegment = 30f;
        public const float SideExitRequiredFrom = 20f;
        public const float MinDeadBranch = 6f;
        public const float MaxDeadEndRoomRatio = 0.08f;
        public const float LengthEpsilon = 0.0001f;
        public const int DoorSearchCells = 6;

        /// <summary>
        /// 窄于此值的走廊碎片是核心筒/楼梯切出来的墙缝（实测 0.89m），几何上走不过去：
        /// 既不能算“走廊直段”，也不能进骨架，否则会报出假的“无侧出口连通廊”。
        /// 但它必须留在 plan.Corridors 里，验证栅格按走廊矩形铺，丢掉会把整层切成两半。
        /// </summary>
        public const float MinRunWidth = 1.2f;

        public sealed class FloorReport
        {
            public int FloorIndex;
            public float LongestCorridor;
            public int OverlongCorridors;
            public int MissingSideExits;
            public float MaxLateralExitGap;
            public int DeadEndCorridors;
            public int RoomCount;
            public int UnattachedRooms;
            public float RoomAttachmentRate => RoomCount == 0 ? 0f : (RoomCount - UnattachedRooms) / (float)RoomCount;
            public int DeadEndRooms;
            public float DeadEndRoomRatio;
            public int CycleRank;
            public int TargetCycles;
            public int SkeletonCells;
            public int SkeletonEndpoints;
            public int InvalidInputs;
            public SightlineAudit.Report Sightlines;
            public readonly List<string> Violations = new List<string>();
            public int StructuralViolationCount => InvalidInputs + OverlongCorridors + MissingSideExits + DeadEndCorridors + UnattachedRooms;
        }

        public sealed class SliceReport
        {
            public readonly List<FloorReport> Floors = new List<FloorReport>();
            public float PlayableArea;
            public float MinOpenFraction = 1f;
            public float MaxOpenFraction;
            public float WorstSightline;
            public int SightlineSamples;
            public int StructuralViolationCount;
        }

        public static FloorReport AuditFloor(FloorPlan plan, bool includeSightlines = false)
        {
            FloorReport report = new FloorReport();
            if (plan == null)
            {
                report.InvalidInputs = 1;
                report.Violations.Add("floor plan is null");
                return report;
            }

            if (plan.Rooms == null || plan.Rooms.Count == 0)
            {
                report.InvalidInputs = 1;
                report.Violations.Add("floor has no rooms");
            }
            if (plan.Corridors != null)
            {
                List<CorridorRun> runs = BuildRuns(plan);
                AuditStraightRuns(plan, runs, report);
                AuditDeadCorridors(plan, report);
            }
            FloorValidator.Grid walkGrid = FloorValidator.BuildGrid(plan, FloorValidator.CoarseCell);
            AuditRooms(plan, walkGrid, report);
            report.CycleRank = LoopInjector.CycleRank(plan);
            report.TargetCycles = LoopInjector.TargetCycles(plan);
            if (includeSightlines) report.Sightlines = SightlineAudit.Run(plan);
            return report;
        }

        public static SliceReport AuditSlice(RaidPlan raid, bool includeSightlines = false)
        {
            SliceReport report = new SliceReport();
            if (raid == null || raid.Floors == null) return report;
            for (int i = 0; i < raid.Floors.Count; i++)
            {
                SliceFloor floor = raid.Floors[i];
                if (floor == null || floor.Plan == null) continue;
                FloorReport floorReport = AuditFloor(floor.Plan, false);
                floorReport.FloorIndex = floor.FloorIndex;
                if (includeSightlines)
                {
                    // 起点只要在开放片里，命中/采样仍要落在真正有几何的房间、走廊、楼梯口或电梯核上。
                    // 开放片本身只是“本局可进入”的抽象，片里两间房之间的空楼板在最终关卡里是结构体/围挡，
                    // 不是可通视的空地；否则会量出穿越空地、穿过竖向连接条的几十米假通视（实测 F2 48.4m）。
                    FloorPlan plan = floor.Plan;
                    floorReport.Sightlines = SightlineAudit.Run(floor.Plan, floor.IsOpen, p => floor.IsOpen(p) && PlayableAt(plan, p));
                    if (floorReport.Sightlines.GlobalMax > 45f)
                        floorReport.Violations.Add("slice open-area sightline " + floorReport.Sightlines.GlobalMax.ToString("0.00") +
                            "m from (" + floorReport.Sightlines.From.x.ToString("0.0") + "," + floorReport.Sightlines.From.y.ToString("0.0") +
                            ") to (" + floorReport.Sightlines.To.x.ToString("0.0") + "," + floorReport.Sightlines.To.y.ToString("0.0") + ") path=" +
                            PathTrace(plan, floorReport.Sightlines.From, floorReport.Sightlines.To));
                }
                report.Floors.Add(floorReport);
                report.PlayableArea += floor.PlayableArea;
                report.MinOpenFraction = Mathf.Min(report.MinOpenFraction, floor.OpenFraction);
                report.MaxOpenFraction = Mathf.Max(report.MaxOpenFraction, floor.OpenFraction);
                report.StructuralViolationCount += floorReport.StructuralViolationCount;
                if (includeSightlines)
                {
                    report.WorstSightline = Mathf.Max(report.WorstSightline, floorReport.Sightlines.GlobalMax);
                    report.SightlineSamples += floorReport.Sightlines.Samples;
                }
            }
            return report;
        }

        /// <summary>世界点是否落在真正的可玩几何上（房间真实轮廓 / 走廊 / 楼梯 / 电梯核）。</summary>
        static bool PlayableAt(FloorPlan plan, Vector2 p)
        {
            if (plan.Corridors != null)
                for (int i = 0; i < plan.Corridors.Count; i++) if (plan.Corridors[i].Rect.Contains(p)) return true;
            if (plan.Rooms != null)
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    Room room = plan.Rooms[i];
                    if (!room.Rect.Contains(p)) continue;
                    if (RoomShape.Blocked(room.Rect, room.Cuts, p)) continue;
                    return true;
                }
            if (plan.Stairs != null)
                for (int i = 0; i < plan.Stairs.Count; i++) if (plan.Stairs[i].Contains(p)) return true;
            return plan.Core.Contains(p);
        }

        sealed class CorridorRun
        {
            public bool Vertical;
            public float Across;
            public float Band;
            public float Start;
            public float End;
            public readonly List<int> Indices = new List<int>();
            public float Length => End - Start;
        }

        static bool CorridorVertical(Corridor c)
        {
            if (c.Main || c.Service || c.Vestibule) return c.Vertical;
            return c.Rect.H >= c.Rect.W;
        }

        static CorridorRun NewRun(Corridor c, int index)
        {
            bool vertical = CorridorVertical(c);
            CorridorRun run = new CorridorRun { Vertical = vertical, Across = vertical ? c.Rect.Center.x : c.Rect.Center.y };
            run.Band = vertical ? c.Rect.W : c.Rect.H;
            run.Start = vertical ? c.Rect.Y : c.Rect.X;
            run.End = vertical ? run.Start + c.Rect.H : run.Start + c.Rect.W;
            run.Indices.Add(index);
            return run;
        }

        /// <summary>
        /// 两段走廊在世界里是否被一面墙隔开。FloorGeometry 对每个走廊矩形的四条边都默认推一道实墙，
        /// 只有 CorridorOpening / 门才会挖洞，所以“矩形贴在一起”并不等于“走得过去、看得过去”：
        /// 不同来源的走廊首尾相接时中间是有一道实墙的，它们不是同一条直段。
        /// 审计按几何并段会把这些墙忽略掉，量出 15+8+26.5=49.46m 的假直廊（seed 1 F2）。
        /// </summary>
        static bool OpenedAt(FloorPlan plan, bool vertical, float across, float band, float junction)
        {
            if (plan.CorridorOpenings == null) return false;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Door d = plan.CorridorOpenings[i];
                float along = vertical ? d.Center.y : d.Center.x;
                float side = vertical ? d.Center.x : d.Center.y;
                if (Mathf.Abs(along - junction) > 0.45f) continue;
                if (Mathf.Abs(side - across) > band * 0.5f + 0.1f) continue;
                return true;
            }
            return false;
        }

        /// <summary>两段被候选并段之间的接缝：重叠视为连续，否则要求接缝处真有开口。</summary>
        static bool JoinedOpen(FloorPlan plan, CorridorRun a, CorridorRun b)
        {
            float junction;
            if (b.Start < a.End - 0.15f && a.Start < b.End - 0.15f) return true;
            if (b.Start >= a.End - 0.15f) junction = a.End;
            else junction = a.Start;
            return OpenedAt(plan, a.Vertical, a.Across, a.Band, junction);
        }

        static bool CanMerge(FloorPlan plan, CorridorRun a, CorridorRun b)
        {
            if (a.Vertical != b.Vertical || Mathf.Abs(a.Across - b.Across) > 0.08f || Mathf.Abs(a.Band - b.Band) > 0.08f) return false;
            if (b.Start > a.End + 0.08f || b.End < a.Start - 0.08f) return false;
            return JoinedOpen(plan, a, b);
        }

        static void Merge(CorridorRun a, CorridorRun b)
        {
            a.Start = Mathf.Min(a.Start, b.Start);
            a.End = Mathf.Max(a.End, b.End);
            a.Across = (a.Across + b.Across) * 0.5f;
            a.Band = (a.Band + b.Band) * 0.5f;
            a.Indices.AddRange(b.Indices);
        }

        static List<CorridorRun> BuildRuns(FloorPlan plan)
        {
            List<Corridor> corridors = plan.Corridors;
            List<CorridorRun> runs = new List<CorridorRun>();
            for (int i = 0; i < corridors.Count; i++)
            {
                Corridor c = corridors[i];
                if (c.Vestibule || c.Hall || c.Rect.W <= 0f || c.Rect.H <= 0f) continue;
                if (Mathf.Min(c.Rect.W, c.Rect.H) < MinRunWidth) continue;
                runs.Add(NewRun(c, i));
            }
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < runs.Count && !merged; i++)
                for (int j = i + 1; j < runs.Count && !merged; j++)
                {
                    if (!CanMerge(plan, runs[i], runs[j])) continue;
                    Merge(runs[i], runs[j]);
                    runs.RemoveAt(j);
                    merged = true;
                }
            }
            return runs;
        }

        static Vector2 DoorNormal(Door door)
        {
            switch (door.Side)
            {
                case DoorSide.North: return Vector2.up;
                case DoorSide.West: return Vector2.left;
                case DoorSide.East: return Vector2.right;
                default: return Vector2.down;
            }
        }

        static bool OnLongWall(CorridorRun run, Corridor c, Door door, out float along)
        {
            along = 0f;
            if (run.Vertical)
            {
                if (Mathf.Abs(DoorNormal(door).x) < 0.5f || Mathf.Abs(DoorNormal(door).y) > 0.5f) return false;
                if (Mathf.Abs(door.Center.x - c.Rect.X) > 0.75f && Mathf.Abs(door.Center.x - (c.Rect.X + c.Rect.W)) > 0.75f) return false;
                if (door.Center.y < c.Rect.Y - 0.75f || door.Center.y > c.Rect.Y + c.Rect.H + 0.75f) return false;
                along = door.Center.y;
                return true;
            }
            if (Mathf.Abs(DoorNormal(door).y) < 0.5f || Mathf.Abs(DoorNormal(door).x) > 0.5f) return false;
            if (Mathf.Abs(door.Center.y - c.Rect.Y) > 0.75f && Mathf.Abs(door.Center.y - (c.Rect.Y + c.Rect.H)) > 0.75f) return false;
            if (door.Center.x < c.Rect.X - 0.75f || door.Center.x > c.Rect.X + c.Rect.W + 0.75f) return false;
            along = door.Center.x;
            return true;
        }

        static bool OnRunEnd(CorridorRun run, Door door, out float along)
        {
            along = 0f;
            float p = run.Vertical ? door.Center.y : door.Center.x;
            float across = run.Vertical ? door.Center.x : door.Center.y;
            float normalAlong = run.Vertical ? Mathf.Abs(DoorNormal(door).y) : Mathf.Abs(DoorNormal(door).x);
            if (normalAlong < 0.5f || Mathf.Abs(across - run.Across) > run.Band * 0.5f + door.Width + 0.75f) return false;
            if (Mathf.Abs(p - run.Start) <= 0.75f) { along = run.Start; return true; }
            if (Mathf.Abs(p - run.End) <= 0.75f) { along = run.End; return true; }
            return false;
        }

        static float Overlap(float a0, float a1, float b0, float b1) { return Mathf.Min(a1, b1) - Mathf.Max(a0, b0); }

        static bool StairAtEnd(CorridorRun run, FloorPlan plan, float end)
        {
            List<RectF> stairs = plan.Stairs;
            if (stairs == null) return false;
            for (int i = 0; i < stairs.Count; i++)
            {
                RectF r = stairs[i];
                float alongDistance = run.Vertical
                    ? Mathf.Min(Mathf.Abs(end - r.Y), Mathf.Abs(end - (r.Y + r.H)))
                    : Mathf.Min(Mathf.Abs(end - r.X), Mathf.Abs(end - (r.X + r.W)));
                float lateral = run.Vertical
                    ? Overlap(run.Across - run.Band * 0.5f, run.Across + run.Band * 0.5f, r.X, r.X + r.W)
                    : Overlap(run.Across - run.Band * 0.5f, run.Across + run.Band * 0.5f, r.Y, r.Y + r.H);
                if (alongDistance <= 0.75f && lateral > 0.1f) return true;
            }
            return false;
        }

        /// <summary>诊断用：世界点被哪一种几何覆盖（房间/走廊/楼梯/电梯核）。</summary>
        static string OwnerAt(FloorPlan plan, Vector2 p)
        {
            if (plan.Rooms != null)
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    Room room = plan.Rooms[i];
                    if (!room.Rect.Contains(p)) continue;
                    if (RoomShape.Blocked(room.Rect, room.Cuts, p)) continue;
                    return "R" + i + "(" + room.Rect.X.ToString("0.0") + "," + room.Rect.Y.ToString("0.0") + "," +
                           room.Rect.W.ToString("0.0") + "x" + room.Rect.H.ToString("0.0") + ")";
                }
            if (plan.Corridors != null)
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    Corridor c = plan.Corridors[i];
                    if (!c.Rect.Contains(p)) continue;
                    return "C" + i + (c.Main ? "M" : c.Service ? "S" : c.Vestibule ? "B" : c.Hall ? "H" : "c") +
                           "(" + c.Rect.X.ToString("0.0") + "," + c.Rect.Y.ToString("0.0") + "," +
                           c.Rect.W.ToString("0.0") + "x" + c.Rect.H.ToString("0.0") + ")";
                }
            if (plan.Stairs != null)
                for (int i = 0; i < plan.Stairs.Count; i++) if (plan.Stairs[i].Contains(p)) return "S" + i;
            if (plan.Core.Contains(p)) return "K";
            return "void";
        }

        static string PathTrace(FloorPlan plan, Vector2 from, Vector2 to)
        {
            float len = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len));
            string s = "";
            string last = null;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, (float)i / steps);
                string owner = OwnerAt(plan, p);
                if (owner == last) continue;
                if (last != null) s += " @ " + ((float)i / steps * len).ToString("0.0") + "m ";
                s += owner;
                last = owner;
            }
            return s;
        }

        static string RunDetail(FloorPlan plan, CorridorRun run)
        {
            string s = (run.Vertical ? "V " : "H ") + run.Start.ToString("0.00") + "-" + run.End.ToString("0.00") +
                " across=" + run.Across.ToString("0.00") + " band=" + run.Band.ToString("0.00") + " pieces=[";
            for (int i = 0; i < run.Indices.Count; i++)
            {
                Corridor c = plan.Corridors[run.Indices[i]];
                if (i > 0) s += " ";
                s += (c.Main ? "M" : c.Service ? "S" : c.Vestibule ? "B" : "C") + "(" +
                     c.Rect.X.ToString("0.0") + "," + c.Rect.Y.ToString("0.0") + "," +
                     c.Rect.W.ToString("0.0") + "x" + c.Rect.H.ToString("0.0") + ")";
            }
            s += "]";
            if (run.Indices.Count > 1)
            {
                s += " joints=[";
                for (int i = 1; i < run.Indices.Count; i++)
                {
                    Corridor a = plan.Corridors[run.Indices[i - 1]];
                    Corridor b = plan.Corridors[run.Indices[i]];
                    float ja = run.Vertical ? a.Rect.YMax : a.Rect.XMax;
                    float jb = run.Vertical ? b.Rect.Y : b.Rect.X;
                    if (i > 1) s += " ";
                    s += ja.ToString("0.0") + "~" + jb.ToString("0.0") +
                         (OpenedAt(plan, run.Vertical, run.Across, run.Band, (ja + jb) * 0.5f) ? " open" : " wall");
                }
                s += "]";
            }
            return s;
        }

        static void AuditStraightRuns(FloorPlan plan, List<CorridorRun> runs, FloorReport report)
        {
            List<Door> doors = new List<Door>();
            if (plan.Rooms != null)
                for (int i = 0; i < plan.Rooms.Count; i++) AddRoomDoors(plan.Rooms[i], doors);
            if (plan.LoopDoors != null) doors.AddRange(plan.LoopDoors);
            if (plan.CorridorOpenings != null) doors.AddRange(plan.CorridorOpenings);
            for (int i = 0; i < runs.Count; i++)
            {
                CorridorRun run = runs[i];
                report.LongestCorridor = Mathf.Max(report.LongestCorridor, run.Length);
                if (run.Length > MaxCorridorSegment + LengthEpsilon)
                {
                    report.OverlongCorridors++;
                    report.Violations.Add("straight corridor " + run.Length.ToString("0.00") + "m > 30m " + RunDetail(plan, run));
                }
                List<float> exits = new List<float>();
                int sideExitCount = 0;
                for (int d = 0; d < doors.Count; d++)
                {
                    float along;
                    for (int k = 0; k < run.Indices.Count; k++)
                    {

                        if (OnLongWall(run, plan.Corridors[run.Indices[k]], doors[d], out along))
                        {
                            exits.Add(Mathf.Clamp(along, run.Start, run.End));
                            sideExitCount++;
                            break;
                        }
                    }
                    if (OnRunEnd(run, doors[d], out along)) exits.Add(along);
                }
                if (StairAtEnd(run, plan, run.Start)) exits.Add(run.Start);
                if (StairAtEnd(run, plan, run.End)) exits.Add(run.End);
                exits.Sort();
                float maxGap = MaxExitGap(run.Start, run.End, exits);
                report.MaxLateralExitGap = Mathf.Max(report.MaxLateralExitGap, maxGap);
                bool missing = maxGap > MaxCorridorSegment + LengthEpsilon ||
                               (run.Length >= SideExitRequiredFrom - LengthEpsilon && sideExitCount == 0);
                if (!missing) continue;
                report.MissingSideExits++;
                Corridor source = plan.Corridors[run.Indices[0]];
                report.Violations.Add("corridor lateral-exit gap " + maxGap.ToString("0.00") + "m; source=" +
                    (source.Main ? "Main" : source.Service ? "Service" : source.Vestibule ? "Vestibule" : "Connector") +
                    " band=" + source.Rect.W.ToString("0.00") + "x" + source.Rect.H.ToString("0.00") + "; run=" +
                    (run.Vertical ? "V" : "H") + " " + run.Start.ToString("0.00") + "–" + run.End.ToString("0.00") +
                    " across=" + run.Across.ToString("0.00") + " sideExits=" + sideExitCount);
            }
        }

        static float MaxExitGap(float start, float end, List<float> exits)
        {
            float previous = start;
            float maxGap = 0f;
            for (int i = 0; i < exits.Count; i++)
            {
                float p = Mathf.Clamp(exits[i], start, end);
                if (i > 0 && Mathf.Abs(p - exits[i - 1]) < 0.05f) continue;
                maxGap = Mathf.Max(maxGap, p - previous);
                previous = p;
            }
            return Mathf.Max(maxGap, end - previous);
        }

        static void AddRoomDoors(Room room, List<Door> doors)
        {
            if (room.HasDoor) doors.Add(room.Door);
            if (room.ExtraDoors == null) return;
            for (int i = 0; i < room.ExtraDoors.Count; i++) doors.Add(room.ExtraDoors[i]);
        }

        static void AuditDeadCorridors(FloorPlan plan, FloorReport report)
        {
            bool[] skeleton;
            int cols, rows;
            float cell;
            Vector2 origin;
            BuildSkeleton(plan, out skeleton, out cols, out rows, out cell, out origin);
            List<Vector2Int> endpoints = new List<Vector2Int>();
            for (int y = 1; y < rows - 1; y++)
            for (int x = 1; x < cols - 1; x++)
                if (skeleton[y * cols + x] && Degree(skeleton, cols, x, y) == 1) endpoints.Add(new Vector2Int(x, y));
            for (int i = 0; i < skeleton.Length; i++) if (skeleton[i]) report.SkeletonCells++;
            report.SkeletonEndpoints = endpoints.Count;
            for (int i = 0; i < endpoints.Count; i++)
            {
                bool[] visited = new bool[skeleton.Length];
                Vector2Int end = TraceBranch(skeleton, cols, rows, endpoints[i], visited);
                float length = PathLength(endpoints[i], visited, cols) * cell;
                if (length < MinDeadBranch) continue;
                Vector2 world = origin + new Vector2((endpoints[i].x + 0.5f) * cell, (endpoints[i].y + 0.5f) * cell);
                if (TerminalExit(plan, world, cell * DoorSearchCells)) continue;
                report.DeadEndCorridors++;
                report.Violations.Add("dead corridor branch " + length.ToString("0.0") + "m");
            }
        }

        static bool TerminalExit(FloorPlan plan, Vector2 point, float radius)
        {
            List<Door> doors = new List<Door>();
            if (plan.Rooms != null) for (int i = 0; i < plan.Rooms.Count; i++) AddRoomDoors(plan.Rooms[i], doors);
            if (plan.LoopDoors != null) doors.AddRange(plan.LoopDoors);
            if (plan.CorridorOpenings != null) doors.AddRange(plan.CorridorOpenings);
            for (int i = 0; i < doors.Count; i++) if (Vector2.Distance(point, doors[i].Center) <= radius) return true;
            if (plan.Stairs != null)
                for (int i = 0; i < plan.Stairs.Count; i++) if (DistanceToRect(point, plan.Stairs[i]) <= radius) return true;
            return false;
        }

        static float DistanceToRect(Vector2 p, RectF r)
        {
            float dx = Mathf.Max(r.X - p.x, 0f, p.x - (r.X + r.W));
            float dy = Mathf.Max(r.Y - p.y, 0f, p.y - (r.Y + r.H));
            return new Vector2(dx, dy).magnitude;
        }

        static void BuildSkeleton(FloorPlan plan, out bool[] skeleton, out int cols, out int rows, out float cell, out Vector2 origin)
        {
            cell = FloorValidator.CoarseCell;
            origin = new Vector2(plan.Bounds.X, plan.Bounds.Y);
            cols = Mathf.CeilToInt(plan.Bounds.W / cell);
            rows = Mathf.CeilToInt(plan.Bounds.H / cell);
            bool[] mask = new bool[cols * rows];
            if (plan.Corridors != null)
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    Corridor c = plan.Corridors[i];
                    if (Mathf.Min(c.Rect.W, c.Rect.H) < MinRunWidth) continue;
                    Stamp(mask, cols, rows, origin, cell, c.Rect);
                }
            int changed;
            int count = 0;
            do
            {
                changed = Thin(mask, cols, rows, 0) + Thin(mask, cols, rows, 1);
                count++;
            } while (changed > 0 && count < 512);
            skeleton = mask;
        }

        static void Stamp(bool[] mask, int cols, int rows, Vector2 origin, float cell, RectF rect)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((rect.X - origin.x) / cell), 0, cols - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((rect.X + rect.W - origin.x) / cell), 0, cols - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((rect.Y - origin.y) / cell), 0, rows - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((rect.Y + rect.H - origin.y) / cell), 0, rows - 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) mask[y * cols + x] = true;
        }

        static int Thin(bool[] a, int cols, int rows, int step)
        {
            // Guo–Hall：保留 2 像素宽直廊与闭环；旧 Zhang–Suen 条件会把普通直廊整体删空。
            List<int> remove = new List<int>();
            for (int y = 1; y < rows - 1; y++)
            for (int x = 1; x < cols - 1; x++)
            {
                int i = y * cols + x;
                if (!a[i]) continue;
                bool[] p = Neighbors(a, cols, x, y);
                int c = (!p[0] && (p[1] || p[2]) ? 1 : 0) + (!p[2] && (p[3] || p[4]) ? 1 : 0) +
                        (!p[4] && (p[5] || p[6]) ? 1 : 0) + (!p[6] && (p[7] || p[0]) ? 1 : 0);
                int n1 = (p[7] || p[0] ? 1 : 0) + (p[1] || p[2] ? 1 : 0) +
                         (p[3] || p[4] ? 1 : 0) + (p[5] || p[6] ? 1 : 0);
                int n2 = (p[0] || p[1] ? 1 : 0) + (p[2] || p[3] ? 1 : 0) +
                         (p[4] || p[5] ? 1 : 0) + (p[6] || p[7] ? 1 : 0);
                int n = Mathf.Min(n1, n2);
                bool m = step == 0 ? ((p[4] || p[5] || !p[7]) && p[6]) : ((p[0] || p[1] || !p[3]) && p[2]);
                if (c == 1 && n >= 2 && n <= 3 && !m) remove.Add(i);
            }
            for (int i = 0; i < remove.Count; i++) a[remove[i]] = false;
            return remove.Count;
        }

        static bool[] Neighbors(bool[] a, int cols, int x, int y)
        {
            return new[] { a[(y + 1) * cols + x], a[(y + 1) * cols + x + 1], a[y * cols + x + 1], a[(y - 1) * cols + x + 1],
                a[(y - 1) * cols + x], a[(y - 1) * cols + x - 1], a[y * cols + x - 1], a[(y + 1) * cols + x - 1] };
        }

        static int Degree(bool[] a, int cols, int x, int y)
        {
            int n = 0, rows = a.Length / cols;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < cols && ny < rows && a[ny * cols + nx]) n++;
            }
            return n;
        }

        static Vector2Int TraceBranch(bool[] a, int cols, int rows, Vector2Int start, bool[] visited)
        {
            Vector2Int previous = new Vector2Int(-1, -1);
            Vector2Int current = start;
            visited[current.y * cols + current.x] = true;
            for (int guard = 0; guard < a.Length; guard++)
            {
                if (Degree(a, cols, current.x, current.y) > 2) return current;
                Vector2Int next = new Vector2Int(-1, -1);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int p = new Vector2Int(current.x + dx, current.y + dy);
                    if (p.x < 0 || p.y < 0 || p.x >= cols || p.y >= rows || p == previous || !a[p.y * cols + p.x]) continue;
                    next = p;
                }
                if (next.x < 0) return current;
                previous = current;
                current = next;
                visited[current.y * cols + current.x] = true;
            }
            return current;
        }

        static float PathLength(Vector2Int start, bool[] visited, int cols)
        {
            int count = 0;
            for (int i = 0; i < visited.Length; i++) if (visited[i]) count++;
            return Mathf.Max(0, count - 1);
        }

        sealed class Components
        {
            public int[] Labels;
            public int Count;
        }

        static Components LabelComponents(FloorValidator.Grid grid)
        {
            Components result = new Components { Labels = new int[grid.W * grid.H] };
            int[] queue = new int[grid.W * grid.H];
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int start = 0; start < result.Labels.Length; start++)
            {
                if (!grid.Walkable[start] || result.Labels[start] != 0) continue;
                result.Count++;
                int head = 0, tail = 0;
                queue[tail++] = start;
                result.Labels[start] = result.Count;
                while (head < tail)
                {
                    int index = queue[head++];
                    int x = index % grid.W, y = index / grid.W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], ny = y + dy[d];
                        if (nx < 0 || ny < 0 || nx >= grid.W || ny >= grid.H) continue;
                        int next = ny * grid.W + nx;
                        if (!grid.Walkable[next] || result.Labels[next] != 0) continue;
                        result.Labels[next] = result.Count;
                        queue[tail++] = next;
                    }
                }
            }
            return result;
        }

        static int ComponentAt(FloorValidator.Grid grid, Components components, Vector2 point)
        {
            int cx = grid.X(point.x), cy = grid.Y(point.y);
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= grid.W || y >= grid.H) continue;
                int label = components.Labels[y * grid.W + x];
                if (label == 0) continue;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; best = label; }
            }
            return best;
        }

        static int[] CorridorRoots(FloorPlan plan)
        {
            int count = plan.Corridors == null ? 0 : plan.Corridors.Count;
            int[] parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
            {
                RectF a = plan.Corridors[i].Rect, b = plan.Corridors[j].Rect;
                bool touchX = Mathf.Abs(a.X - (b.X + b.W)) <= 0.7f || Mathf.Abs(b.X - (a.X + a.W)) <= 0.7f;
                bool touchY = Mathf.Abs(a.Y - (b.Y + b.H)) <= 0.7f || Mathf.Abs(b.Y - (a.Y + a.H)) <= 0.7f;
                bool overlapX = Overlap(a.X, a.X + a.W, b.X, b.X + b.W) > 0.1f;
                bool overlapY = Overlap(a.Y, a.Y + a.H, b.Y, b.Y + b.H) > 0.1f;
                if ((touchX && overlapY) || (touchY && overlapX) || (overlapX && overlapY)) Union(parent, i, j);
            }
            return parent;
        }

        static int Root(int[] parent, int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        static void Union(int[] parent, int a, int b) { a = Root(parent, a); b = Root(parent, b); if (a != b) parent[b] = a; }

        static Vector2 DoorOutward(Room room, Door door)
        {
            if (DoorNormal(door).sqrMagnitude > 0.1f) return DoorNormal(door).normalized;
            Vector2 center = room.Rect.Center;
            Vector2 delta = door.Center - center;
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? Vector2.right * Mathf.Sign(delta.x) : Vector2.up * Mathf.Sign(delta.y);
        }

        static bool DoorTargetValid(FloorPlan plan, Door door, Vector2 outside)
        {
            bool valid = false;
            if (door.Corridor >= 0 && plan.Corridors != null && door.Corridor < plan.Corridors.Count)
                valid |= DistanceToRect(outside, plan.Corridors[door.Corridor].Rect) <= 3f;
            if (door.OtherRoom >= 0 && plan.Rooms != null && door.OtherRoom < plan.Rooms.Count)
                valid |= DistanceToRect(outside, plan.Rooms[door.OtherRoom].Rect) <= 3f;
            return valid;
        }

        static void AuditRooms(FloorPlan plan, FloorValidator.Grid grid, FloorReport report)
        {
            report.RoomCount = plan.Rooms == null ? 0 : plan.Rooms.Count;
            if (report.RoomCount == 0) return;
            Components components = LabelComponents(grid);
            int[] corridorRoots = CorridorRoots(plan);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                List<Door> doors = new List<Door>();
                AddRoomDoors(room, doors);
                HashSet<int> targets = new HashSet<int>();
                for (int d = 0; d < doors.Count; d++)
                {
                    Door door = doors[d];
                    Vector2 outward = DoorOutward(room, door);
                    Vector2 insidePoint = door.Center - outward * 0.8f;
                    Vector2 outsidePoint = door.Center + outward * 0.8f;
                    if (!DoorTargetValid(plan, door, outsidePoint)) continue;
                    int inside = ComponentAt(grid, components, insidePoint);
                    int outside = ComponentAt(grid, components, outsidePoint);
                    if (inside == 0 || inside != outside) continue;
                    if (door.Corridor >= 0 && door.Corridor < corridorRoots.Length) targets.Add(Root(corridorRoots, door.Corridor));
                    else if (door.OtherRoom >= 0 && door.OtherRoom < plan.Rooms.Count) targets.Add(100000 + door.OtherRoom);
                }
                if (targets.Count == 0)
                {
                    report.UnattachedRooms++;
                    report.Violations.Add("room " + i + " has no valid attached door");
                }
                if (targets.Count <= 1) report.DeadEndRooms++;
            }
            report.DeadEndRoomRatio = report.DeadEndRooms / (float)report.RoomCount;
        }
    }
}
