using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum SpawnEnd { Bottom, Top }

    public sealed class ExtractionGate
    {
        public int Id;
        public int FloorIndex;
        public Vector2 Center;
        public int RoomIndex;
        public SpawnEnd ControlsEnd;
        public bool BroadcastOnActivation = true;
    }

    public sealed class PlannedExtraction
    {
        public string Id;
        public int FloorIndex;
        public SpawnEnd OwnerEnd;
        public ExtractionPoint Point;
        public bool InitiallyOpen;
        public int GateId = -1;
        public bool ClosesInFinalThreeMinutes;
    }

    /// <summary>
    /// R11 的整局撤离规则。这里只规划数据，不冒充 T3 的实际跨层移动或经济系统。
    /// 每层两个设施：F2/F3 为门禁点，F1/F4 为另一端拥有的免费点。
    /// </summary>
    public sealed class ExtractionPlan
    {
        public const int PointsPerFloor = 2;
        public const int PointsPerEnd = 4;
        public const float OpposingSpawnClearance = 80f;
        public const float FinalClosureMinutes = 3f;

        public int Seed;
        public List<PlannedExtraction> Points = new List<PlannedExtraction>();
        public List<ExtractionGate> Gates = new List<ExtractionGate>();
        public List<string> FinalClosureIds = new List<string>();
        public int ElevatorCardFloor;
        public List<int> ElevatorCardRooms = new List<int>();
        public bool ElevatorCardsAreOneUse = true;
        public int BossSafeFloor;

        public static ExtractionPlan Generate(RaidPlan raid)
        {
            if (raid == null || raid.Floors.Count != 4) throw new System.ArgumentException("R11 extraction requires exactly four floors", "raid");
            ExtractionPlan result = new ExtractionPlan();
            result.Seed = raid.Seed;
            System.Random rng = new System.Random(raid.Seed * 6553 + 117);
            SliceFloor f1 = raid.Floors[0], f2 = raid.Floors[1], f3 = raid.Floors[2], f4 = raid.Floors[3];
            // 06 §5.3 表固定了点的归属：F1 免费→Top 端、F2 门禁→Top 端、F3 门禁→Bottom 端、F4 免费→Bottom 端。
            // 随机的是两个闸房分别落在 F2 还是 F3，而不是撤离点归属。
            SpawnEnd f2Owner = SpawnEnd.Top;
            SpawnEnd f3Owner = SpawnEnd.Bottom;
            SliceFloor topGateFloor = rng.Next(2) == 0 ? f2 : f3;
            SliceFloor bottomGateFloor = topGateFloor == f2 ? f3 : f2;
            result.Gates.Add(MakeGate(0, topGateFloor, SpawnEnd.Top));
            result.Gates.Add(MakeGate(1, bottomGateFloor, SpawnEnd.Bottom));

            // 每张卡都是一次性交通电梯权限；Boss 安全层取另一中层，不在此决定 §8.3 的战斗形式。
            result.ElevatorCardFloor = rng.Next(2) == 0 ? f2.FloorIndex : f3.FloorIndex;
            SliceFloor cardFloor = result.ElevatorCardFloor == f2.FloorIndex ? f2 : f3;
            result.BossSafeFloor = cardFloor == f2 ? f3.FloorIndex : f2.FloorIndex;
            result.ElevatorCardRooms.AddRange(CentralHighValueRooms(cardFloor, 2));

            List<RectF>[] selected = new List<RectF>[4];
            FloorValidator.Grid[] grids = new FloorValidator.Grid[4];
            List<BoxSpec>[] walls = new List<BoxSpec>[4];
            for (int i = 0; i < 4; i++)
            {
                selected[i] = new List<RectF>();
                grids[i] = FloorValidator.BuildGrid(raid.Floors[i].Plan, FloorValidator.CoarseCell);
                walls[i] = new List<BoxSpec>();
                FloorGeometry.CollectWallBoxes(raid.Floors[i].Plan, walls[i]);
            }
            float[][][] spawnWalk = new float[4][][];
            for (int i = 0; i < 4; i++) spawnWalk[i] = SpawnWalkFields(grids[i], raid.Floors[i].Plan);

            AddFloorPoints(result, raid, f1, 0, f2Owner, true, -1,
                ExtractionKind.FireStair, ExtractionKind.Freight, selected[0], grids[0], walls[0], spawnWalk[0], rng);
            AddFloorPoints(result, raid, f2, 1, f2Owner, false, 0,
                ExtractionKind.FireStair, ExtractionKind.WindowBasket, selected[1], grids[1], walls[1], spawnWalk[1], rng);
            AddFloorPoints(result, raid, f3, 2, f3Owner, false, 1,
                ExtractionKind.FireStair, ExtractionKind.Freight, selected[2], grids[2], walls[2], spawnWalk[2], rng);
            AddFloorPoints(result, raid, f4, 3, f3Owner, true, -1,
                ExtractionKind.RoofRappel, ExtractionKind.WindowBasket, selected[3], grids[3], walls[3], spawnWalk[3], rng);

            ChooseFinalClosure(result, SpawnEnd.Bottom);
            ChooseFinalClosure(result, SpawnEnd.Top);
            Apply(raid, result);
            return result;
        }

        public List<PlannedExtraction> ForEnd(SpawnEnd end)
        {
            return Points.FindAll(p => p.OwnerEnd == end);
        }

        public List<PlannedExtraction> ForFloor(int floorIndex)
        {
            return Points.FindAll(p => p.FloorIndex == floorIndex);
        }

        public static bool IsFreePointSafe(RaidPlan raid, PlannedExtraction extraction)
        {
            if (extraction == null || extraction.InitiallyOpen == false) return false;
            SliceFloor opposing = extraction.OwnerEnd == SpawnEnd.Bottom ? raid.Floors[3] : raid.Floors[0];
            SliceFloor current = raid.At(extraction.FloorIndex);
            if (current == null || opposing == null) return false;
            FloorValidator.Grid grid = FloorValidator.BuildGrid(current.Plan, FloorValidator.CoarseCell);
            if (WalkClearance(grid, SpawnWalkFields(grid, opposing.Plan), extraction.Point.Center) < OpposingSpawnClearance) return false;
            List<BoxSpec> walls = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(current.Plan, walls);
            for (int i = 0; i < opposing.Plan.SpawnZones.Count; i++)
                if (HasLineOfSight(walls, extraction.Point.Center, opposing.Plan.SpawnZones[i].Center)) return false;
            return true;
        }

        /// <summary>每个出生区一份 2m 栅格步行距离场（Flood 的 Distance 是共享暂存，必须拷贝）。</summary>
        static float[][] SpawnWalkFields(FloorValidator.Grid grid, FloorPlan plan)
        {
            float[][] fields = new float[plan.SpawnZones.Count][];
            for (int i = 0; i < fields.Length; i++)
            {
                float[] distance = FloorValidator.Flood(grid, plan.SpawnZones[i].Center);
                fields[i] = (float[])distance.Clone();
            }
            return fields;
        }

        /// <summary>到最近出生区的步行距离；完全不可达视作足够远。</summary>
        static float WalkClearance(FloorValidator.Grid grid, float[][] fields, Vector2 p)
        {
            if (fields == null || fields.Length == 0) return float.MaxValue;
            int x = grid.X(p.x);
            int y = grid.Y(p.y);
            if (!grid.Inside(x, y)) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < fields.Length; i++)
            {
                float d = fields[i][y * grid.W + x];
                if (d < 0f) continue;
                if (d < best) best = d;
            }
            return best;
        }

        static void Apply(RaidPlan raid, ExtractionPlan plan)
        {
            raid.Extraction = plan;
            for (int i = 0; i < raid.Floors.Count; i++)
            {
                SliceFloor floor = raid.Floors[i];
                List<PlannedExtraction> points = plan.ForFloor(floor.FloorIndex);
                floor.Plan.Extractions.Clear();
                floor.Plan.ExtractionCandidates.Clear();
                for (int p = 0; p < points.Count; p++)
                {
                    floor.Plan.Extractions.Add(points[p].Point);
                    floor.Plan.ExtractionCandidates.Add(points[p].Point);
                }
                if (i == 1 || i == 2) floor.Plan.SpawnZones.Clear();
            }
        }

        static void AddFloorPoints(ExtractionPlan result, RaidPlan raid, SliceFloor floor, int floorSlot,
            SpawnEnd owner, bool free, int gateId, ExtractionKind first, ExtractionKind second,
            List<RectF> selected, FloorValidator.Grid grid, List<BoxSpec> walls, float[][] spawnWalk, System.Random rng)
        {
            ExtractionKind[] kinds = { first, second };
            for (int k = 0; k < kinds.Length; k++)
            {
                Vector2 position;
                int[] rejects = new int[12];
                float[] probe = free ? new float[6] : null;
                if (!TryPlace(raid, floor, owner, free, kinds[k], selected, grid, walls, spawnWalk, rejects, probe, rng, out position))
                    throw new System.InvalidOperationException("No legal extraction position on F" + (floorSlot + 1) + " for " + kinds[k]
                        + " [free=" + free + " owner=" + owner + " spawns=" + floor.Plan.SpawnZones.Count
                        + " stairs=" + floor.Plan.Stairs.Count + " openRooms=" + OpenRoomCount(floor)
                        + " rejects=" + string.Join(",", rejects)
                        + (probe == null ? "" : " maxWalk=" + probe[0] + " passWalk=" + probe[1]
                            + " maxEuclid=" + probe[2] + " hiddenWalk=" + probe[3] + " hiddenEuclid=" + probe[4])
                        + " zones=" + string.Join(";", floor.Plan.SpawnZones.ConvertAll(z => z.Center.x.ToString("0") + "," + z.Center.y.ToString("0"))) + "]");
                RectF rect = FacilityRect(kinds[k], position);
                selected.Add(rect);
                ExtractionPoint point = new ExtractionPoint
                {
                    Rect = rect,
                    Center = position,
                    Kind = kinds[k],
                    NeedsPower = !free,
                    NeedsCard = false,
                    ChannelSeconds = Channel(kinds[k]),
                    Label = "F" + (floorSlot + 1) + " · " + KindLabel(kinds[k]) + " · " + (owner == SpawnEnd.Bottom ? "F1端" : "F4端")
                };
                result.Points.Add(new PlannedExtraction
                {
                    Id = "F" + (floorSlot + 1) + "-" + kinds[k],
                    FloorIndex = floor.FloorIndex,
                    OwnerEnd = owner,
                    Point = point,
                    InitiallyOpen = free,
                    GateId = gateId
                });
            }
        }

        static bool TryPlace(RaidPlan raid, SliceFloor floor, SpawnEnd owner, bool free, ExtractionKind kind,
            List<RectF> selected, FloorValidator.Grid grid, List<BoxSpec> walls, float[][] spawnWalk, int[] rejects, float[] probe, System.Random rng, out Vector2 best)
        {
            best = Vector2.zero;
            Vector2 selectedBest = Vector2.zero;
            float bestScore = float.MinValue;
            SliceFloor opposing = free ? (owner == SpawnEnd.Bottom ? raid.Floors[3] : raid.Floors[0]) : null;
            RectF bounds = floor.Plan.Bounds;
            List<Vector2> usablePos = free ? new List<Vector2>() : null;
            List<float> usableZone = free ? new List<float>() : null;
            System.Action<Vector2> consider = p =>
            {
                RectF rect = FacilityRect(kind, p);
                int reason = Usable(floor, rect, p, selected, grid);
                if (reason != 0) { rejects[reason]++; return; }
                float clearance = float.MaxValue;
                if (free)
                {
                    float nearestZone = float.MaxValue;
                    for (int s = 0; s < opposing.Plan.SpawnZones.Count; s++)
                        nearestZone = Mathf.Min(nearestZone, Vector2.Distance(p, opposing.Plan.SpawnZones[s].Center));
                    if (nearestZone > probe[2]) probe[2] = nearestZone;
                    usablePos.Add(p);
                    usableZone.Add(nearestZone);
                    // “80m 内”按可步行距离计：绕墙 80m 以上、且完全不可通视才算安全。
                    float walk = WalkClearance(grid, spawnWalk, p);
                    if (walk > probe[0]) probe[0] = walk;
                    if (walk < OpposingSpawnClearance) { rejects[10]++; return; }
                    probe[1] += 1f;
                    for (int s = 0; s < opposing.Plan.SpawnZones.Count; s++)
                    {
                        Vector2 spawn = opposing.Plan.SpawnZones[s].Center;
                        clearance = Mathf.Min(clearance, Vector2.Distance(p, spawn));
                        // 两个条件都是硬约束：既不能靠近，也不能与对端出生区通视。
                        if (HasLineOfSight(walls, p, spawn)) { rejects[11]++; return; }
                    }
                }
                float centerPenalty = Vector2.Distance(p, bounds.Center) * (free ? 0f : 0.02f);
                float score = (free ? clearance : 100f) - centerPenalty + (float)rng.NextDouble() * 0.001f;
                if (score > bestScore) { bestScore = score; selectedBest = p; }
            };
            // 房间中心是最可靠的“墙已挡住对端视线”候选；4m 网格可能完全漏掉小房间。
            for (int i = 0; i < floor.Plan.Rooms.Count; i++)
                if (floor.IsOpen(floor.Plan.Rooms[i].Rect.Center)) consider(floor.Plan.Rooms[i].Rect.Center);
            for (float y = bounds.Y + 2f; y <= bounds.YMax - 2f; y += 2f)
            for (float x = bounds.X + 2f; x <= bounds.XMax - 2f; x += 2f)
                consider(new Vector2(x, y));
            if (free && usablePos.Count > 0)
            {
                int[] order = new int[usablePos.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                System.Array.Sort(order, (a, b) => usableZone[b].CompareTo(usableZone[a]));
                int examined = 0;
                for (int i = 0; i < order.Length && examined < 12; i++)
                {
                    examined++;
                    Vector2 probePoint = usablePos[order[i]];
                    bool hidden = true;
                    for (int s = 0; s < opposing.Plan.SpawnZones.Count; s++)
                        if (HasLineOfSight(walls, probePoint, opposing.Plan.SpawnZones[s].Center)) { hidden = false; break; }
                    if (!hidden) continue;
                    float hiddenWalk = WalkClearance(grid, spawnWalk, probePoint);
                    if (hiddenWalk > probe[3]) { probe[3] = hiddenWalk; probe[4] = usableZone[order[i]]; }
                }
            }
            best = selectedBest;
            return bestScore > float.MinValue;
        }

        static int OpenRoomCount(SliceFloor floor)
        {
            int count = 0;
            for (int i = 0; i < floor.Plan.Rooms.Count; i++)
                if (floor.IsOpen(floor.Plan.Rooms[i].Rect.Center)) count++;
            return count;
        }

        // 返回 0 表示可用，否则返回拒绝原因码（用于定位“无合法点位”）。
        static int Usable(SliceFloor floor, RectF rect, Vector2 p, List<RectF> selected, FloorValidator.Grid grid)
        {
            FloorPlan plan = floor.Plan;
            RectF b = plan.Bounds;
            if (rect.X < b.X || rect.Y < b.Y || rect.XMax > b.XMax || rect.YMax > b.YMax) return 1;
            if (!floor.IsOpen(p)) return 2;
            if (!grid.IsWalkable(grid.X(p.x), grid.Y(p.y))) return 3;
            if (plan.Envelope != null && !FloorEnvelope.Contains(plan.Envelope.Outline, p)) return 4;
            for (int i = 0; i < plan.Stairs.Count; i++) if (rect.Overlaps(plan.Stairs[i].Inflate(1f))) return 5;
            if (rect.Overlaps(plan.Core.Inflate(1f))) return 6;
            for (int i = 0; i < plan.SpawnZones.Count; i++) if (rect.Overlaps(plan.SpawnZones[i].Inflate(1f))) return 7;
            for (int i = 0; i < selected.Count; i++)
            {
                if (rect.Overlaps(selected[i].Inflate(2f))) return 8;
                if (Vector2.Distance(rect.Center, selected[i].Center) < 20f) return 8;
            }
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (Vector2.Distance(p, plan.Rooms[i].Door.Center) < 3f) return 9;
                if (plan.Rooms[i].ExtraDoors == null) continue;
                for (int d = 0; d < plan.Rooms[i].ExtraDoors.Count; d++)
                    if (Vector2.Distance(p, plan.Rooms[i].ExtraDoors[d].Center) < 3f) return 9;
            }
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
                if (Vector2.Distance(p, plan.CorridorOpenings[i].Center) < 3f) return 9;
            return 0;
        }

        static ExtractionGate MakeGate(int id, SliceFloor floor, SpawnEnd owner)
        {
            List<int> rooms = CentralHighValueRooms(floor, 1);
            int room = rooms.Count > 0 ? rooms[0] : -1;
            return new ExtractionGate
            {
                Id = id,
                FloorIndex = floor.FloorIndex,
                RoomIndex = room,
                Center = room >= 0 ? floor.Plan.Rooms[room].Rect.Center : floor.Plan.Bounds.Center,
                ControlsEnd = owner
            };
        }

        static List<int> CentralHighValueRooms(SliceFloor floor, int count)
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < floor.Plan.Rooms.Count; i++)
            {
                Room room = floor.Plan.Rooms[i];
                if (RoomShape.AreaOf(room) >= 60f && floor.IsOpen(room.Rect.Center)) candidates.Add(i);
            }
            candidates.Sort((a, b) =>
            {
                float da = Vector2.Distance(floor.Plan.Rooms[a].Rect.Center, floor.Plan.Bounds.Center);
                float db = Vector2.Distance(floor.Plan.Rooms[b].Rect.Center, floor.Plan.Bounds.Center);
                int byDistance = da.CompareTo(db);
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
            if (candidates.Count < count)
                for (int i = 0; i < floor.Plan.Rooms.Count && candidates.Count < count; i++)
                    if (!candidates.Contains(i)) candidates.Add(i);
            if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);
            return candidates;
        }

        static void ChooseFinalClosure(ExtractionPlan plan, SpawnEnd end)
        {
            List<PlannedExtraction> free = plan.ForEnd(end).FindAll(p => p.InitiallyOpen);
            if (free.Count != 2) throw new System.InvalidOperationException("Each end must own exactly two free extractions");
            free.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            free[free.Count - 1].ClosesInFinalThreeMinutes = true;
            plan.FinalClosureIds.Add(free[free.Count - 1].Id);
        }

        static RectF FacilityRect(ExtractionKind kind, Vector2 p)
        {
            Vector2 size = kind == ExtractionKind.FireStair ? new Vector2(4f, 5f) :
                kind == ExtractionKind.WindowBasket ? new Vector2(2.4f, 6f) :
                kind == ExtractionKind.RoofRappel ? new Vector2(6f, 6f) : new Vector2(4.4f, 4f);
            return new RectF(p.x - size.x * 0.5f, p.y - size.y * 0.5f, size.x, size.y);
        }

        static float Channel(ExtractionKind kind)
        {
            if (kind == ExtractionKind.FireStair) return 20f;
            if (kind == ExtractionKind.Freight || kind == ExtractionKind.WindowBasket) return 25f;
            if (kind == ExtractionKind.RoofRappel) return 30f;
            throw new System.ArgumentException("Traffic elevator is never an extraction", "kind");
        }

        static string KindLabel(ExtractionKind kind)
        {
            if (kind == ExtractionKind.FireStair) return "消防梯";
            if (kind == ExtractionKind.Freight) return "货梯";
            if (kind == ExtractionKind.WindowBasket) return "擦窗机";
            return "天台下索";
        }

        static bool HasLineOfSight(List<BoxSpec> walls, Vector2 a, Vector2 b)
        {
            for (int i = 0; i < walls.Count; i++) if (SegmentHits(walls[i].Footprint, a, b)) return false;
            return true;
        }

        static bool SegmentHits(RectF rect, Vector2 a, Vector2 b)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Clip(a.x, d.x, rect.X, rect.XMax, ref t0, ref t1)) return false;
            if (!Clip(a.y, d.y, rect.Y, rect.YMax, ref t0, ref t1)) return false;
            return t1 > 0.001f && t0 < 0.999f;
        }

        static bool Clip(float origin, float direction, float min, float max, ref float t0, ref float t1)
        {
            if (Mathf.Abs(direction) < 0.00001f) return origin >= min && origin <= max;
            float a = (min - origin) / direction, b = (max - origin) / direction;
            if (a > b) { float tmp = a; a = b; b = tmp; }
            t0 = Mathf.Max(t0, a);
            t1 = Mathf.Min(t1, b);
            return t0 <= t1;
        }
    }
}
