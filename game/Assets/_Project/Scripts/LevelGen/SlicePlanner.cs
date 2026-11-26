using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum SealReason { Power, Construction, Collapse, Cordon, Flood }

    public enum VerticalLinkKind { Stair, Elevator, Freight, Service }

    /// <summary>相邻两层之间的一条竖向通路。竖向核固定对位，随机的是房间——所以链接位置每局都一样。</summary>
    public struct VerticalLink
    {
        public int LowerFloor;
        public int UpperFloor;
        public Vector2 Center;
        public VerticalLinkKind Kind;
        /// <summary>0 = 西侧核，1 = 中部核，2 = 东侧核。用于验收"不全在同一象限"。</summary>
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
        /// <summary>开放区占整层可玩面积的比例（验收 20–32%）。</summary>
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
    /// 一局的地图：随机 3–4 层 × 每层 20–32% 楼板，合计 24,000–30,000㎡，
    /// 相邻层固定 3 条竖向通路（西梯 / 核心筒电梯 / 东梯），其余楼板用世界内原因封死。
    /// </summary>
    public class RaidPlan
    {
        public int Seed;
        public List<SliceFloor> Floors = new List<SliceFloor>();
        public List<VerticalLink> Links = new List<VerticalLink>();
        /// <summary>R6：本局所有楼层共用的竖向核布局（跨层对齐的结构保证）。</summary>
        public VerticalLinkPlan CorePlan;
        /// <summary>本局可玩面积合计（㎡），验收 24,000–30,000。</summary>
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
        public const int MinFloors = 3;
        public const int MaxFloors = 4;
        public const float MinPlayableArea = 24000f;
        public const float MaxPlayableArea = 30000f;
        public const float TargetPlayableArea = 27000f;
        public const float MinOpenFraction = 0.20f;
        public const float MaxOpenFraction = 0.32f;

        public static RaidPlan Generate(int seed)
        {
            System.Random rng = new System.Random(seed);
            int floorCount = rng.NextDouble() < 0.55 ? 3 : 4;
            return Generate(seed, floorCount);
        }

        public static RaidPlan Generate(int seed, int floorCount)
        {
            System.Random rng = new System.Random(seed * 7331 + floorCount);
            RaidPlan raid = new RaidPlan();
            raid.Seed = seed;
            floorCount = Mathf.Clamp(floorCount, MinFloors, MaxFloors);

            int start = rng.Next(3, 52 - floorCount);
            float perFloorTarget = TargetPlayableArea / floorCount;

            // R6：核坐标一局只算一次，逐层下发；层与层之间不可能对不上。
            raid.CorePlan = VerticalLinkPlan.Default(FloorSpec.Default(seed));

            for (int i = 0; i < floorCount; i++)
            {
                int worldFloor = start + i;
                FloorSpec spec = FloorSpec.Default(seed * 7919 + worldFloor * 131);
                spec.Band = FloorBand.Tower;
                raid.CorePlan.ApplyTo(spec, worldFloor);
                FloorPlan plan = FloorGenerator.Generate(spec);

                SliceFloor floor = new SliceFloor();
                floor.FloorIndex = worldFloor;
                floor.Spec = spec;
                floor.Plan = plan;
                raid.Floors.Add(floor);
            }

            // 先按面积密度估一个起始比例，再迭代逼近目标总量（每层独立微调，保证都在 20–32% 内）。
            for (int i = 0; i < raid.Floors.Count; i++)
            {
                SliceFloor f = raid.Floors[i];
                float full = FullPlayableArea(f.Plan);
                float fraction = Mathf.Clamp(perFloorTarget / Mathf.Max(1f, full), MinOpenFraction, MaxOpenFraction);
                Layout(f, fraction, rng);
            }

            for (int pass = 0; pass < 4; pass++)
            {
                float total = 0f;
                for (int i = 0; i < raid.Floors.Count; i++) total += raid.Floors[i].PlayableArea;
                float error = TargetPlayableArea / Mathf.Max(1f, total);
                if (Mathf.Abs(total - TargetPlayableArea) < 400f) break;

                for (int i = 0; i < raid.Floors.Count; i++)
                {
                    SliceFloor f = raid.Floors[i];
                    float full = FullPlayableArea(f.Plan);
                    float fraction = Mathf.Clamp(f.OpenFraction * error, MinOpenFraction, MaxOpenFraction);
                    Layout(f, fraction, rng);
                }
            }

            raid.PlayableArea = 0f;
            for (int i = 0; i < raid.Floors.Count; i++) raid.PlayableArea += raid.Floors[i].PlayableArea;

            BuildLinks(raid);
            return raid;        }

        /// <summary>整层可玩面积（房间真实轮廓 + 走廊），用来算开放比例。</summary>
        public static float FullPlayableArea(FloorPlan plan)
        {
            float rooms = plan.RoomArea;
            float corridors = 0f;
            for (int i = 0; i < plan.Corridors.Count; i++) corridors += plan.Corridors[i].Rect.Area;
            return rooms + corridors;
        }

        /// <summary>按比例给某层铺开放带：横贯全宽的一条 + 三处竖向核 + 连接短廊。</summary>
        static void Layout(SliceFloor floor, float fraction, System.Random rng)
        {
            FloorPlan plan = floor.Plan;
            float full = Mathf.Max(1f, FullPlayableArea(plan));
            RectF bounds = plan.Bounds;

            float h = Mathf.Clamp(bounds.H * fraction, 24f, bounds.H * MaxOpenFraction);
            float y0 = Mathf.Lerp(0f, bounds.H - h, (float)rng.NextDouble());
            // 带子尽量别把核心筒劈成两半：核心筒在东侧偏北，带子贴到它下沿或完全包含它。
            foreach (RectF core in new[] { plan.Core })
            {
                bool overlapsCore = y0 < core.YMax && y0 + h > core.Y;
                bool splits = overlapsCore && y0 > core.Y && y0 + h < core.YMax;
                if (splits) y0 = core.Y - h + 2f;
            }
            // 调整后必须夹回楼层内，否则北侧会露出没被登记成"封死区"的楼板（铺不满整层）。
            y0 = Mathf.Clamp(y0, 0f, bounds.H - h);

            // 竖直核：两座楼梯 + 核心筒，永远开放——跨层通路靠它们，随机的是房间不是楼梯。
            var cores = new List<RectF>();
            cores.Add(plan.Core);
            for (int i = 0; i < plan.Stairs.Count; i++) cores.Add(plan.Stairs[i]);

            // 迭代收敛：房间中心在带内、但房间探出带外时，按包围盒统计会把整间都算进可玩面积
            // （实测虚高到 35.3%），所以每轮量完按超出比例回收带宽，再重算竖向核与连接廊。
            var parts = new List<RectF>();
            RectF band = new RectF(bounds.X, y0, bounds.W, h);
            for (int pass = 0; pass < 5; pass++)
            {
                band = new RectF(bounds.X, y0, bounds.W, h);
                parts.Clear();
                parts.Add(band);
                for (int i = 0; i < cores.Count; i++)
                {
                    RectF c = cores[i].Inflate(0.2f);
                    // 只有"带子完整包住竖向核"才不用另加；部分重叠时核的中心点可能在带外，
                    // 玩家就到不了楼梯/电梯（seed 1 第 13 层就是这样漏的）。
                    if (band.Contains(c.Center) && band.Contains(new Vector2(c.X, c.Y)) &&
                        band.Contains(new Vector2(c.XMax, c.YMax))) continue;
                    parts.Add(c);
                    if (!Intersects(band, c)) parts.Add(Connector(band, c));
                }

                float measured = PlayableInOpen(plan, parts, band) / full;
                if (measured <= MaxOpenFraction + 0.004f) break;
                float shrink = Mathf.Clamp((MaxOpenFraction - 0.004f) / Mathf.Max(0.01f, measured), 0.70f, 1f);
                h = Mathf.Max(24f, h * shrink);
                y0 = Mathf.Clamp(y0, 0f, bounds.H - h);
            }

            floor.Band = band;
            floor.Open.Clear();
            floor.Open.AddRange(parts);
            floor.PlayableArea = PlayableInOpen(plan, floor.Open, band);
            floor.OpenFraction = floor.PlayableArea / full;
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
                Vector2 c = plan.Rooms[i].Rect.Center;
                if (ContainsAny(open, c)) area += RoomShape.AreaOf(plan.Rooms[i]);
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

        /// <summary>相邻层之间的 3 条竖向通路，坐标直接取自本局共用的核布局（R6）。</summary>
        static void BuildLinks(RaidPlan raid)
        {
            raid.Links.Clear();
            if (raid.CorePlan == null) return;
            for (int i = 0; i + 1 < raid.Floors.Count; i++)
            {
                SliceFloor lower = raid.Floors[i];
                SliceFloor upper = raid.Floors[i + 1];
                for (int c = 0; c < raid.CorePlan.Cores.Count; c++)
                {
                    VerticalCore core = raid.CorePlan.Cores[c];
                    AddLink(raid, lower, upper, core.Center, core.Kind, core.Column);
                }
            }
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
