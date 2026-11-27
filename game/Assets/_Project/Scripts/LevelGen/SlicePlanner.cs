using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum SealReason { Power, Construction, Collapse, Cordon, Flood }

    public enum VerticalLinkKind { Stair, Elevator, Freight, Service }

    /// <summary>相邻两层之间的一条单层竖向通路；楼梯按层对错位，电梯保持对位。</summary>
    public struct VerticalLink
    {
        public int LowerFloor;
        public int UpperFloor;
        public Vector2 Center;
        public VerticalLinkKind Kind;
        /// <summary>楼梯所在象限：0 左下、1 左上、2 右上、3 右下；电梯固定为 -1。</summary>
        public int Column;
    }

    /// <summary>被封死的楼板区域：世界内原因（断电/围挡/坍塌/封锁带/水淹），同时是免费边界压力。</summary>
    public struct SealedArea
    {
        public RectF Rect;
        public SealReason Reason;
    }

    /// <summary>一局里的某一层：完整单层生成结果 + 本局开放的楼板区域。</summary>
    public class SliceFloor
    {
        public int FloorIndex;
        public FloorSpec Spec;
        public FloorPlan Plan;
        /// <summary>主开放带（横贯全宽的一条），面积占比由 SlicePlanner 调到目标值。</summary>
        public RectF Band;
        /// <summary>本层开放的楼板片：主带 + 竖向核（两座楼梯 + 核心筒）+ 连接短廊。</summary>
        public List<RectF> Open = new List<RectF>();
        public List<SealedArea> Sealed = new List<SealedArea>();
        public float PlayableArea;
        /// <summary>开放区占整层可玩面积的比例（F1/F4：21–22%；F2/F3：26–28%）。</summary>
        public float OpenFraction;
        public bool IsLandmark;

        public bool IsOpen(Vector2 p)
        {
            for (int i = 0; i < Open.Count; i++)
                if (Open[i].Contains(p)) return true;
            return false;
        }
    }

    /// <summary>
    /// R11：固定 F1–F4（索引 0–3），F1/F4 开放 21–22%，F2/F3 开放 26–28%，
    /// 合计 30,000–33,000㎡。每对相邻层恰有两部错位单层楼梯和一部交通电梯。
    /// </summary>
    public class RaidPlan
    {
        public int Seed;
        public List<SliceFloor> Floors = new List<SliceFloor>();
        public List<VerticalLink> Links = new List<VerticalLink>();
        public StairGraphPlan StairGraph;
        public ExtractionPlan Extraction;
        /// <summary>本局可玩面积合计（㎡），验收 30,000–33,000。</summary>
        public float PlayableArea;

        public int FloorCount { get { return Floors.Count; } }

        public SliceFloor At(int worldFloor)
        {
            for (int i = 0; i < Floors.Count; i++)
                if (Floors[i].FloorIndex == worldFloor) return Floors[i];
            return null;
        }

        public bool IsOpen(int worldFloor, Vector2 p)
        {
            SliceFloor f = At(worldFloor);
            return f != null && f.IsOpen(p);
        }
    }

    /// <summary>
    /// R5：把单层生成器组装成"一局"。只做规划（选层 + 选区 + 封死 + 连通/竖向校验），
    /// 不改单层几何——切片是"这层只开这一块"，不是"重新生成一个小楼层"。
    /// </summary>
    public static class SlicePlanner
    {
        public const int MinFloors = 4;
        public const int MaxFloors = 4;
        public const float MinPlayableArea = 30000f;
        public const float MaxPlayableArea = 33000f;
        public const float TargetPlayableArea = 31500f;
        public const float MinOpenFraction = 0.21f;
        public const float MaxOpenFraction = 0.28f;

        public static RaidPlan Generate(int seed)
        {
            return Generate(seed, FloorCount);
        }

        const int FloorCount = 4;

        public static RaidPlan Generate(int seed, int floorCount)
        {
            if (floorCount != FloorCount)
                throw new System.ArgumentException("R11 raid is fixed at exactly four floors", "floorCount");
            System.Random rng = new System.Random(seed * 7331 + floorCount);
            // 真实楼层号：标识牌资产只覆盖 40F–50F，四层切片落在其中连续四层，
            // 与 02 §5.4 "楼层号取 SlicePlanner 的 worldFloor，不是 0..n" 一致。
            int startFloor = rng.Next(40, 48);
            RaidPlan raid = new RaidPlan();
            raid.Seed = seed;
            FloorSpec template = FloorSpec.Default(seed);
            raid.StairGraph = StairGraphPlan.Generate(seed, template.Width, template.Height);

            for (int i = 0; i < FloorCount; i++)
            {
                FloorSpec spec = FloorSpec.Default(seed * 7919 + i * 131);
                spec.FloorIndex = i;
                spec.WorldFloor = startFloor + i;
                spec.Band = FloorBand.Tower;
                spec.UseStairGraph = true;
                spec.CoreRect = raid.StairGraph.ElevatorForFloor(i);
                spec.StairRects.AddRange(raid.StairGraph.StairsOnFloor(i));
                FloorPlan plan = FloorGenerator.Generate(spec);
                raid.Floors.Add(new SliceFloor { FloorIndex = i, Spec = spec, Plan = plan });
            }

            float[] targets = { 0.215f, 0.275f, 0.275f, 0.215f };
            for (int i = 0; i < FloorCount; i++) Layout(raid.Floors[i], targets[i], rng);
            for (int pass = 0; pass < 6; pass++)
            {
                float total = 0f;
                for (int i = 0; i < FloorCount; i++) total += raid.Floors[i].PlayableArea;
                if (total >= MinPlayableArea && total <= MaxPlayableArea) break;
                float correction = TargetPlayableArea / Mathf.Max(1f, total);
                for (int i = 0; i < FloorCount; i++)
                {
                    float min, max;
                    FractionRange(i, out min, out max);
                    targets[i] = Mathf.Clamp(targets[i] * correction, min, max);
                    Layout(raid.Floors[i], targets[i], rng);
                }
            }

            for (int i = 0; i < FloorCount; i++)
                if (i == 0 || i == FloorCount - 1) ReseatSpawnZones(raid.Floors[i]);

            raid.PlayableArea = 0f;
            for (int i = 0; i < FloorCount; i++) raid.PlayableArea += raid.Floors[i].PlayableArea;
            BuildLinks(raid);
            ExtractionPlan.Generate(raid);
            return raid;
        }

        /// <summary>
        /// 出生层（F1/F4）的出生区必须落在开放带内、且贴着带的两端。
        /// FloorGenerator 在整块楼板上按四角选房，切片后这些房间可能落在封死区（06 §4.1：出生点只在边缘服务节点），
        /// 既让玩家出生在不可达区域，也让对端免费撤离点的 80m 预算被幽灵出生区吃掉。
        /// 每端 2 个出生区，保留 4 区的容量与同层队伍间距，同时给带走半层的撤离点留出步行余量。
        /// </summary>
        static void ReseatSpawnZones(SliceFloor floor)
        {
            FloorPlan plan = floor.Plan;
            RectF band = floor.Band;
            List<RectF> pool = new List<RectF>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                if (r.W < 4f || r.H < 4f) continue;
                if (!floor.IsOpen(r.Center)) continue;
                if (plan.Envelope != null && !FloorEnvelope.Contains(plan.Envelope.Outline, r.Center)) continue;
                pool.Add(r);
            }
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                if (r.W < 4f || r.H < 4f) continue;
                if (!floor.IsOpen(r.Center)) continue;
                pool.Add(r);
            }
            if (pool.Count < 4) return;

            float yLow = band.Center.y - band.H * 0.25f;
            float yHigh = band.Center.y + band.H * 0.25f;
            Vector2[] anchors =
            {
                new Vector2(band.X + 12f, yLow),
                new Vector2(band.X + 12f, yHigh),
                new Vector2(band.XMax - 12f, yLow),
                new Vector2(band.XMax - 12f, yHigh),
            };

            List<RectF> chosen = new List<RectF>();
            List<int> used = new List<int>();
            for (int a = 0; a < anchors.Length; a++)
            {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    float d = Vector2.Distance(pool[i].Center, anchors[a]);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (best < 0) continue;
                used.Add(best);
                chosen.Add(MakeSpawnRect(pool[best]));
            }
            if (chosen.Count != 4) return;

            plan.SpawnZones.Clear();
            plan.SpawnZones.AddRange(chosen);
        }

        static RectF MakeSpawnRect(RectF pick)
        {
            float size = Mathf.Min(10f, Mathf.Min(pick.W - 0.4f, pick.H - 0.4f));
            if (size < 2f) size = 2f;
            Vector2 center = pick.Center;
            return new RectF(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        }

        public static void FractionRange(int floorIndex, out float min, out float max)
        {
            bool middle = floorIndex == 1 || floorIndex == 2;
            min = middle ? 0.26f : 0.21f;
            max = middle ? 0.28f : 0.22f;
        }

        /// <summary>
        /// 开放比例的基数（06 §10：F1/F4 21–22%、F2/F3 26–28%，合计 30,000–33,000㎡）。
        /// 必须用整层楼板面积：房间+走廊的可玩面积只有约 29,500㎡，而四层开放比例合计约 98%，
        /// 按它算即使开到上限也只有约 28,900㎡，永远到不了 30,000 下限（实测 seed 1 只有 28,891㎡）。
        /// </summary>
        public static float FullPlayableArea(FloorPlan plan)
        {
            return plan.Bounds.Area;
        }

        /// <summary>开放带位置由楼层种子固定，带宽双向收敛到本层目标比例。</summary>
        static void Layout(SliceFloor floor, float fraction, System.Random rng)
        {
            FloorPlan plan = floor.Plan;
            float full = Mathf.Max(1f, FullPlayableArea(plan));
            RectF bounds = plan.Bounds;
            float jitter = (float)new System.Random(floor.Spec.Seed * 97 + 13).NextDouble();
            float centerY = bounds.Center.y + Mathf.Lerp(-8f, 8f, jitter);
            var cores = new List<RectF>();
            cores.Add(plan.Core);
            for (int i = 0; i < plan.Stairs.Count; i++) cores.Add(plan.Stairs[i]);

            float low = 12f, high = bounds.H;
            float bestError = float.MaxValue;
            RectF bestBand = bounds;
            List<RectF> bestParts = null;
            float bestArea = 0f;
            float bestH = 12f;

            // PlayableInOpen 对带宽是“台阶函数”（房间按中心是否落在开放片里整块计入），
            // 纯二分会在台阶边缘来回跳、停在离目标 0.6pp 之外（实测 F4 只到 0.2214 > 22%）。
            // 先 80 点粗扫，再在最优带宽邻域内二分细化。
            float Evaluate(float h)
            {
                float y0 = Mathf.Clamp(centerY - h * 0.5f, bounds.Y, bounds.YMax - h);
                RectF band = new RectF(bounds.X, y0, bounds.W, h);
                var parts = new List<RectF>();
                parts.Add(band);
                for (int i = 0; i < cores.Count; i++)
                {
                    RectF c = cores[i].Inflate(0.2f);
                    if (band.Contains(c.Center) && band.Contains(new Vector2(c.X, c.Y)) &&
                        band.Contains(new Vector2(c.XMax, c.YMax))) continue;
                    parts.Add(c);
                    if (!Intersects(band, c)) parts.Add(Connector(band, c));
                }
                float area = PlayableInOpen(plan, parts, band);
                float measured = area / full;
                float error = Mathf.Abs(measured - fraction);
                if (error < bestError)
                {
                    bestError = error;
                    bestBand = band;
                    bestParts = new List<RectF>(parts);
                    bestArea = area;
                    bestH = h;
                }
                return measured;
            }

            const int Coarse = 80;
            float span = bounds.H - 12f;
            for (int i = 0; i <= Coarse; i++) Evaluate(12f + span * i / Coarse);
            float step = span / Coarse;
            low = Mathf.Max(12f, bestH - step);
            high = Mathf.Min(bounds.H, bestH + step);
            for (int pass = 0; pass < 30; pass++)
            {
                float h = (low + high) * 0.5f;
                if (Evaluate(h) < fraction) low = h;
                else high = h;
            }

            floor.Band = bestBand;
            floor.Open.Clear();
            floor.Open.AddRange(bestParts);
            floor.PlayableArea = bestArea;
            floor.OpenFraction = bestArea / full;
            BuildSealed(floor, rng);
        }

        /// <summary>从主带通到竖向核的短廊：取两者重叠的轴向中点，铺一条 2.6m 宽的通道。</summary>
        static RectF Connector(RectF band, RectF core)
        {
            const float width = 2.6f;
            bool bandAbove = core.YMax <= band.Y;
            bool bandBelow = core.Y >= band.YMax;

            if (bandAbove || bandBelow)
            {
                float x = Mathf.Clamp(core.Center.x, band.X + width, band.XMax - width);
                float yA = bandAbove ? core.YMax : band.YMax;
                float yB = bandAbove ? band.Y : core.Y;
                return new RectF(x - width * 0.5f, Mathf.Min(yA, yB), width, Mathf.Abs(yB - yA));
            }

            float yc = Mathf.Clamp(core.Center.y, band.Y + width, band.YMax - width);
            float xa = core.XMax <= band.X ? core.XMax : band.XMax;
            float xb = core.XMax <= band.X ? band.X : core.X;
            return new RectF(Mathf.Min(xa, xb), yc - width * 0.5f, Mathf.Abs(xb - xa), width);
        }

        static bool Intersects(RectF a, RectF b)
        {
            return a.X < b.XMax && a.XMax > b.X && a.Y < b.YMax && a.YMax > b.Y;
        }

        /// <summary>开放区内的可玩面积：房间按真实轮廓（中心落在开放片里）、走廊按与主带相交部分。</summary>
        static float PlayableInOpen(FloorPlan plan, List<RectF> open, RectF band)
        {
            float area = 0f;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                // 房间按“真实轮廓面积 × 落进开放带的包围盒比例”连续计入。
                // 原来按房间中心是否在开放片里整块计入，是台阶函数（一个 200㎡ 房间就是 0.6pp），
                // 二分永远凑不到 21.5%，实测 F4 最好只能到 22.14%（超 22% 上限）。
                Room room = plan.Rooms[i];
                float rectArea = room.Rect.Area;
                if (rectArea <= 0.01f) continue;
                float inter = IntersectionArea(room.Rect, band);
                if (inter <= 0f) continue;
                area += RoomShape.AreaOf(room) * Mathf.Clamp01(inter / rectArea);
            }
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                area += IntersectionArea(r, band);
            }
            return area;
        }

        static float IntersectionArea(RectF a, RectF b)
        {
            float w = Mathf.Min(a.XMax, b.XMax) - Mathf.Max(a.X, b.X);
            float h = Mathf.Min(a.YMax, b.YMax) - Mathf.Max(a.Y, b.Y);
            return w <= 0f || h <= 0f ? 0f : w * h;
        }

        static bool ContainsAny(List<RectF> parts, Vector2 p)
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Contains(p)) return true;
            return false;
        }

        /// <summary>把主带以外的楼板切成封死区，附世界内原因。</summary>
        static void BuildSealed(SliceFloor floor, System.Random rng)
        {
            floor.Sealed.Clear();
            RectF b = floor.Plan.Bounds;
            RectF band = floor.Band;
            var reasons = (SealReason[])System.Enum.GetValues(typeof(SealReason));

            if (band.Y > 0.5f)
                floor.Sealed.Add(new SealedArea
                {
                    Rect = new RectF(b.X, b.Y, b.W, band.Y - b.Y),
                    Reason = reasons[rng.Next(reasons.Length)],
                });
            if (band.YMax < b.YMax - 0.5f)
                floor.Sealed.Add(new SealedArea
                {
                    Rect = new RectF(b.X, band.YMax, b.W, b.YMax - band.YMax),
                    Reason = reasons[rng.Next(reasons.Length)],
                });
        }

        /// <summary>每对相邻层严格三条：StairGraph 的两部单层楼梯 + 一部交通电梯。</summary>
        static void BuildLinks(RaidPlan raid)
        {
            raid.Links.Clear();
            for (int i = 0; i + 1 < raid.Floors.Count; i++)
            {
                SliceFloor lower = raid.Floors[i];
                SliceFloor upper = raid.Floors[i + 1];
                List<StairLink> stairs = raid.StairGraph.LinksBetween(lower.FloorIndex, upper.FloorIndex);
                for (int s = 0; s < stairs.Count; s++)
                {
                    Vector2 center = stairs[s].LowerRect.Center;
                    AddLink(raid, lower, upper, center, VerticalLinkKind.Stair,
                        Quadrant(center, raid.StairGraph.FloorCenter));
                }
                AddLink(raid, lower, upper, raid.StairGraph.Elevator.Center, VerticalLinkKind.Elevator, -1);
            }
        }

        static int Quadrant(Vector2 p, Vector2 center)
        {
            if (p.x < center.x) return p.y < center.y ? 0 : 1;
            return p.y < center.y ? 3 : 2;
        }

        static void AddLink(RaidPlan raid, SliceFloor lower, SliceFloor upper, Vector2 center,
            VerticalLinkKind kind, int column)
        {
            VerticalLink link = new VerticalLink();
            link.LowerFloor = lower.FloorIndex;
            link.UpperFloor = upper.FloorIndex;
            link.Center = center;
            link.Kind = kind;
            link.Column = column;
            raid.Links.Add(link);
        }
    }
}
