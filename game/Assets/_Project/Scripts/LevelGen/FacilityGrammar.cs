using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// 房间设施细分。RoomType 只分"办公/储藏/卫生间/管井"四档，粒度太粗——一层楼看过去
    /// 全是 ninety-something 平米的方盒子。这里再叠一层设施类型：决定这个房间是机房、
    /// 冷库、车间还是档案室，进而决定面积目标、道具集、材质槽和门规则。
    /// </summary>
    public enum FacilityKind
    {
        Office = 0,
        Meeting = 1,
        ServerHall = 2,
        ServerRoom = 3,
        Riser = 4,
        ColdRoom = 5,
        PipeGallery = 6,
        Workshop = 7,
        ControlRoom = 8,
        Substation = 9,
        TankRoom = 10,
        IsolationRoom = 11,
        LoadingDock = 12,
        Archive = 13,
        CleanRoom = 14,
        LivingPod = 15,
        Storage = 16,
        Toilet = 17,
    }

    /// <summary>分区主题。每层 4 个象限各自挂一个主题，切分力度与设施表都按主题走。</summary>
    public enum FloorTheme
    {
        Office = 0,
        Admin = 1,
        DataCenter = 2,
        ColdChain = 3,
        Manufacturing = 4,
        Medical = 5,
        Storage = 6,
    }

    /// <summary>
    /// 一个设施类型的规则：面积区间、材质槽、道具集。
    /// 面积区间只作为"这个房间最像什么"的匹配依据，不反过来改房间尺寸——
    /// 房间尺寸由分区驱动的切分决定，这里只负责贴标签。
    /// </summary>
    public struct FacilityProfile
    {
        public FacilityKind Kind;
        public string Name;
        public float MinArea;
        public float MaxArea;
        /// <summary>地面材质槽（对应 19 套 PBR 的套名，R5 接材质时用）。</summary>
        public string FloorMaterial;
        /// <summary>墙面材质槽。</summary>
        public string WallMaterial;
    }

    public static class FacilityGrammar
    {
        // ---- 主题 ------------------------------------------------------------

        /// <summary>
        /// 象限主题：办公类原型以办公/行政为主，但**每层至少掺两个工业主题**，否则一层楼
        /// 无论种子怎么变都还是办公室。渗入比例由种子决定，保证同种子可复现。
        /// </summary>
        public static FloorTheme ThemeOf(FloorPrototype prototype, FloorSpec spec, int zoneIndex)
        {
            var rng = new System.Random(unchecked(spec.Seed * 5147 + zoneIndex * 977 + 13));
            double r = rng.NextDouble();

            switch (prototype)
            {
                case FloorPrototype.Renovation:
                    return r < 0.40 ? FloorTheme.Manufacturing
                        : r < 0.65 ? FloorTheme.Storage
                        : r < 0.85 ? FloorTheme.DataCenter
                        : FloorTheme.Office;

                case FloorPrototype.Utility:
                    return r < 0.35 ? FloorTheme.DataCenter
                        : r < 0.55 ? FloorTheme.ColdChain
                        : r < 0.80 ? FloorTheme.Storage
                        : FloorTheme.Manufacturing;

                case FloorPrototype.Executive:
                    return r < 0.70 ? FloorTheme.Admin : FloorTheme.Office;

                default:
                    // OpenOffice / Meeting / SmallOffice：办公室为主体，工业主题做"这一翼在改造"的错位。
                    return r < 0.45 ? FloorTheme.Office
                        : r < 0.60 ? FloorTheme.DataCenter
                        : r < 0.72 ? FloorTheme.Manufacturing
                        : r < 0.82 ? FloorTheme.Medical
                        : r < 0.90 ? FloorTheme.ColdChain
                        : r < 0.96 ? FloorTheme.Storage
                        : FloorTheme.Admin;
            }
        }

        public static string ThemeName(FloorTheme theme)
        {
            switch (theme)
            {
                case FloorTheme.Office: return "办公";
                case FloorTheme.Admin: return "行政";
                case FloorTheme.DataCenter: return "数据中心";
                case FloorTheme.ColdChain: return "冷链";
                case FloorTheme.Manufacturing: return "制造";
                case FloorTheme.Medical: return "医疗隔离";
                case FloorTheme.Storage: return "仓储";
                default: return "办公";
            }
        }

        /// <summary>
        /// 主题对切分停止概率的乘数：&lt;1 更容易停（留大空间），&gt;1 更愿意继续切（小隔间）。
        /// 数据中心要留 300–900㎡ 的机房大厅，医疗隔离要切出 40–140㎡ 的小间。
        /// </summary>
        public static float ZoneStopPBias(FloorTheme theme)
        {
            switch (theme)
            {
                case FloorTheme.DataCenter: return 0.42f;
                case FloorTheme.Manufacturing: return 0.55f;
                case FloorTheme.Storage: return 0.70f;
                case FloorTheme.ColdChain: return 0.62f;
                case FloorTheme.Medical: return 1.35f;
                case FloorTheme.Admin: return 1.05f;
                default: return 1.0f;
            }
        }

        /// <summary>主题对最小房间面积的乘数：医疗/生活舱可以小到 30㎡，机房大厅不该被切成小格子。</summary>
        public static float ZoneMinAreaScale(FloorTheme theme)
        {
            switch (theme)
            {
                case FloorTheme.DataCenter: return 1.6f;
                case FloorTheme.Manufacturing: return 1.3f;
                case FloorTheme.Storage: return 1.1f;
                case FloorTheme.ColdChain: return 1.2f;
                case FloorTheme.Medical: return 1.0f;
                case FloorTheme.Admin: return 1.0f;
                default: return 1.0f;
            }
        }

        public static FloorTheme ThemeAt(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                if (plan.Zones[i].Rect.Contains(p)) return plan.Zones[i].Theme;
            }
            return plan.Zones.Count > 0 ? plan.Zones[0].Theme : FloorTheme.Office;
        }

        // ---- 设施表 -----------------------------------------------------------

        static readonly FacilityProfile[] Profiles =
        {
            Profile(FacilityKind.Office,        "办公区",    60f, 300f, "CarpetTile",   "PaintedWall"),
            Profile(FacilityKind.Meeting,       "会议室",    60f, 180f, "VenueCarpet",  "WoodPanel"),
            Profile(FacilityKind.ServerHall,    "机房大厅", 220f, 900f, "CleanPanel",   "ServerPanel"),
            Profile(FacilityKind.ServerRoom,    "机房",      60f, 220f, "CleanPanel",   "ServerPanel"),
            Profile(FacilityKind.Riser,         "管井",      14f,  56f, "ConcreteFloor","ConcreteWall"),
            Profile(FacilityKind.ColdRoom,      "冷库",      80f, 500f, "EpoxyFloor",   "CleanPanel"),
            Profile(FacilityKind.PipeGallery,   "管廊",      18f,  70f, "ConcreteFloor","IndustrialPaint"),
            Profile(FacilityKind.Workshop,      "车间",     140f, 700f, "DiamondPlate", "IndustrialPaint"),
            Profile(FacilityKind.ControlRoom,   "中控室",    60f, 220f, "TileFloor",    "CleanPanel"),
            Profile(FacilityKind.Substation,    "变电室",    40f, 160f, "RubberStair",  "IndustrialPaint"),
            Profile(FacilityKind.TankRoom,      "储罐间",    60f, 320f, "EpoxyFloor",   "ColdMetal"),
            Profile(FacilityKind.IsolationRoom, "隔离室",    36f, 140f, "CleanPanel",   "CleanPanel"),
            Profile(FacilityKind.LoadingDock,   "装卸区",   150f, 600f, "ConcreteFloor","IndustrialPaint"),
            Profile(FacilityKind.Archive,       "档案室",    60f, 300f, "WoodPanel",    "PaintedWall"),
            Profile(FacilityKind.CleanRoom,     "洁净间",    60f, 240f, "CleanPanel",   "CleanPanel"),
            Profile(FacilityKind.LivingPod,     "生活舱",    20f,  60f, "VenueCarpet",  "PaintedWall"),
            Profile(FacilityKind.Storage,       "储藏间",    14f, 120f, "ConcreteFloor","PaintedWall"),
            Profile(FacilityKind.Toilet,        "卫生间",    12f,  56f, "TileFloor",    "TileFloor"),
        };

        static FacilityProfile Profile(FacilityKind kind, string name, float min, float max, string floor, string wall)
        {
            FacilityProfile p;
            p.Kind = kind;
            p.Name = name;
            p.MinArea = min;
            p.MaxArea = max;
            p.FloorMaterial = floor;
            p.WallMaterial = wall;
            return p;
        }

        public static FacilityProfile Get(FacilityKind kind)
        {
            for (int i = 0; i < Profiles.Length; i++) if (Profiles[i].Kind == kind) return Profiles[i];
            return Profiles[0];
        }

        static readonly FacilityKind[] OfficeSet = { FacilityKind.Office, FacilityKind.Meeting, FacilityKind.Office, FacilityKind.LivingPod };
        static readonly FacilityKind[] AdminSet = { FacilityKind.Office, FacilityKind.Meeting, FacilityKind.ControlRoom, FacilityKind.Archive };
        static readonly FacilityKind[] DataCenterSet = { FacilityKind.ServerHall, FacilityKind.ServerRoom, FacilityKind.Riser, FacilityKind.ControlRoom, FacilityKind.Substation };
        static readonly FacilityKind[] ColdChainSet = { FacilityKind.ColdRoom, FacilityKind.LoadingDock, FacilityKind.PipeGallery, FacilityKind.Substation };
        static readonly FacilityKind[] ManufacturingSet = { FacilityKind.Workshop, FacilityKind.PipeGallery, FacilityKind.ControlRoom, FacilityKind.Substation, FacilityKind.TankRoom };
        static readonly FacilityKind[] MedicalSet = { FacilityKind.IsolationRoom, FacilityKind.CleanRoom, FacilityKind.Toilet, FacilityKind.LivingPod };
        static readonly FacilityKind[] StorageSet = { FacilityKind.Archive, FacilityKind.LoadingDock, FacilityKind.Storage, FacilityKind.TankRoom };

        static FacilityKind[] SetOf(FloorTheme theme)
        {
            switch (theme)
            {
                case FloorTheme.Admin: return AdminSet;
                case FloorTheme.DataCenter: return DataCenterSet;
                case FloorTheme.ColdChain: return ColdChainSet;
                case FloorTheme.Manufacturing: return ManufacturingSet;
                case FloorTheme.Medical: return MedicalSet;
                case FloorTheme.Storage: return StorageSet;
                default: return OfficeSet;
            }
        }

        /// <summary>
        /// 给每个房间贴设施标签：先按主题拿候选集，再挑面积区间最贴合的；面积区间都不贴合时
        /// 按"超大的给大厅、超小的给服务间"兜底，保证 ≥300㎡ 不会是储藏间、&lt;56㎡ 不会是机房大厅。
        /// </summary>
        public static void Assign(FloorPlan plan, FloorSpec spec)
        {
            var rng = new System.Random(unchecked(spec.Seed * 8117 + 191));
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                float area = RoomShape.AreaOf(room);
                FloorTheme theme = room.ZoneIndex >= 0 && room.ZoneIndex < plan.Zones.Count
                    ? plan.Zones[room.ZoneIndex].Theme
                    : FloorTheme.Office;

                // R3 规则不动：≥56㎡ 一定是 Normal 类用途，房间类型标签只决定摆放/标识/灯光。
                FacilityKind best = FacilityKind.Office;
                float bestCost = float.MaxValue;
                FacilityKind[] set = SetOf(theme);
                for (int k = 0; k < set.Length; k++)
                {
                    FacilityProfile p = Get(set[k]);
                    float cost;
                    if (area < p.MinArea) cost = p.MinArea - area;
                    else if (area > p.MaxArea) cost = (area - p.MaxArea) * 0.35f;
                    else cost = 0f;
                    // 同分时用房间下标扰动，避免一层楼里所有"刚好合身"的房间都变成同一个设施。
                    cost += (float)rng.NextDouble() * 0.01f;
                    if (cost < bestCost) { bestCost = cost; best = set[k]; }
                }

                room.Facility = best;
                if (area < 56f)
                {
                    // 服务间：设施跟着粗分类走，卫生间就是卫生间，别出现 30㎡ 的"机房大厅"。
                    switch (room.Type)
                    {
                        case RoomType.Toilet: room.Facility = FacilityKind.Toilet; break;
                        case RoomType.Riser: room.Facility = FacilityKind.Riser; break;
                        case RoomType.Storage:
                            room.Facility = theme == FloorTheme.Storage || theme == FloorTheme.ColdChain
                                ? FacilityKind.Storage
                                : (rng.NextDouble() < 0.5 ? FacilityKind.Storage : FacilityKind.Riser);
                            break;
                    }
                }
                plan.Rooms[i] = room;
            }
        }

        // ---- 房间内家具 --------------------------------------------------------

        static PropSpec P(string name, float w, float d, float h, PropSize size, bool blocks)
        {
            PropSpec s;
            s.Name = name;
            s.Width = w;
            s.Depth = d;
            s.Size = size;
            s.BlocksSight = blocks;
            s.Vaultable = size != PropSize.Large;
            s.Height = h;
            return s;
        }

        static PropSpec[] PropsOf(FacilityKind kind)
        {
            switch (kind)
            {
                case FacilityKind.ServerHall:
                case FacilityKind.ServerRoom:
                    return new[]
                    {
                        P("服务器机柜", 0.6f, 0.9f, 2.05f, PropSize.Large, true),
                        P("列头柜", 0.8f, 0.6f, 2.0f, PropSize.Large, true),
                        P("配线架", 0.6f, 0.45f, 1.9f, PropSize.Medium, false),
                    };
                case FacilityKind.Riser:
                    return new[] { P("管井架", 0.6f, 0.6f, 2.0f, PropSize.Medium, false) };
                case FacilityKind.ColdRoom:
                    return new[]
                    {
                        P("冷库货架", 1.2f, 0.8f, 2.0f, PropSize.Large, true),
                        P("冷风机", 1.0f, 0.5f, 0.9f, PropSize.Medium, false),
                    };
                case FacilityKind.PipeGallery:
                    return new[]
                    {
                        P("管道支架", 0.8f, 0.4f, 1.2f, PropSize.Medium, false),
                        P("阀门组", 0.6f, 0.6f, 1.0f, PropSize.Small, false),
                    };
                case FacilityKind.Workshop:
                    return new[]
                    {
                        P("工作台", 1.8f, 0.8f, 0.9f, PropSize.Large, false),
                        P("工具柜", 1.0f, 0.5f, 1.6f, PropSize.Medium, true),
                    };
                case FacilityKind.ControlRoom:
                    return new[]
                    {
                        P("控制台", 2.0f, 0.8f, 0.75f, PropSize.Large, false),
                        P("屏幕墙", 1.6f, 0.3f, 2.2f, PropSize.Large, true),
                    };
                case FacilityKind.Substation:
                    return new[] { P("配电柜", 1.2f, 0.6f, 2.2f, PropSize.Large, true) };
                case FacilityKind.TankRoom:
                    return new[]
                    {
                        P("储罐", 1.6f, 1.6f, 2.6f, PropSize.Large, true),
                        P("泵组", 1.0f, 0.6f, 0.8f, PropSize.Medium, false),
                    };
                case FacilityKind.IsolationRoom:
                    return new[]
                    {
                        P("隔离床", 1.2f, 2.0f, 0.8f, PropSize.Large, false),
                        P("医疗柜", 0.8f, 0.45f, 1.8f, PropSize.Medium, false),
                    };
                case FacilityKind.LoadingDock:
                    return new[]
                    {
                        P("装卸托盘", 1.2f, 1.0f, 0.6f, PropSize.Large, false),
                        P("货堆", 1.4f, 1.0f, 1.6f, PropSize.Large, true),
                    };
                case FacilityKind.Archive:
                    return new[] { P("档案架", 1.4f, 0.6f, 2.1f, PropSize.Large, true) };
                case FacilityKind.CleanRoom:
                    return new[]
                    {
                        P("洁净工作台", 1.6f, 0.7f, 0.9f, PropSize.Large, false),
                        P("传递柜", 0.9f, 0.5f, 1.8f, PropSize.Medium, false),
                    };
                case FacilityKind.LivingPod:
                    return new[]
                    {
                        P("床铺", 1.0f, 2.0f, 0.55f, PropSize.Large, false),
                        P("储物柜", 0.8f, 0.5f, 1.8f, PropSize.Medium, true),
                    };
                case FacilityKind.Toilet:
                    return new[] { P("隔间", 1.0f, 1.4f, 2.1f, PropSize.Medium, true) };
                case FacilityKind.Storage:
                    return new[] { P("货架", 1.4f, 0.6f, 2.0f, PropSize.Large, true) };
                case FacilityKind.Meeting:
                    return new[]
                    {
                        P("会议桌", 2.2f, 1.0f, 0.75f, PropSize.Large, false),
                        P("折叠椅", 0.5f, 0.45f, 0.5f, PropSize.Small, false),
                    };
                default:
                    return new[]
                    {
                        P("办公桌组", 1.2f, 0.8f, 0.75f, PropSize.Medium, false),
                        P("文件柜", 0.9f, 0.5f, 1.7f, PropSize.Medium, true),
                    };
            }
        }

        /// <summary>
        /// 房间内家具：沿四面墙贴边摆，按面积给件数（每 24㎡ 一件，2–14 件），
        /// 避开所有门 1.5m、避开走廊/核心筒/楼梯、避开已有道具。走廊家具由 CoverPlacer 负责，
        /// 这里只填房间（CorridorIndex = -1），所以不会影响走廊净宽检查。
        /// </summary>
        public static void AddRoomProps(FloorPlan plan, FloorSpec spec)
        {
            var rng = new System.Random(unchecked(spec.Seed * 6653 + 89));
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                float area = RoomShape.AreaOf(room);
                if (area < 20f) continue;

                PropSpec[] set = PropsOf(room.Facility);
                int count = Mathf.Clamp(Mathf.RoundToInt(area / 24f), 2, 14);
                RectF inner = room.Rect.Inflate(-1.0f);
                if (inner.W < 1.4f || inner.H < 1.4f) continue;

                // 四面墙轮流试：先短边（更像"沿着墙摆一排"），再长边。
                for (int n = 0; n < count; n++)
                {
                    PropSpec ps = set[rng.Next(set.Length)];
                    bool placed = false;
                    for (int attempt = 0; attempt < 8 && !placed; attempt++)
                    {
                        int wall = rng.Next(4);
                        float w = Mathf.Min(ps.Width, 2.4f);
                        float d = Mathf.Min(ps.Depth, 2.2f);
                        float x, y;
                        switch (wall)
                        {
                            case 0: // 南墙
                                x = Mathf.Lerp(inner.X + w * 0.5f, inner.XMax - w * 0.5f, (float)rng.NextDouble());
                                y = inner.Y + d * 0.5f;
                                break;
                            case 1: // 北墙
                                x = Mathf.Lerp(inner.X + w * 0.5f, inner.XMax - w * 0.5f, (float)rng.NextDouble());
                                y = inner.YMax - d * 0.5f;
                                break;
                            case 2: // 西墙
                                x = inner.X + w * 0.5f;
                                y = Mathf.Lerp(inner.Y + d * 0.5f, inner.YMax - d * 0.5f, (float)rng.NextDouble());
                                break;
                            default: // 东墙
                                x = inner.XMax - w * 0.5f;
                                y = Mathf.Lerp(inner.Y + d * 0.5f, inner.YMax - d * 0.5f, (float)rng.NextDouble());
                                break;
                        }

                        RectF rect = new RectF(x - w * 0.5f, y - d * 0.5f, w, d);
                        if (!room.Rect.Inflate(-0.05f).Contains(rect.Center)) continue;
                        if (RoomShape.BlockedInFloor(plan, rect.Center)) continue;
                        if (rect.Overlaps(plan.Core)) continue;
                        bool blocked = false;
                        for (int s = 0; s < plan.Stairs.Count && !blocked; s++) blocked = rect.Overlaps(plan.Stairs[s]);
                        if (blocked) continue;
                        if (NearAnyDoor(plan, room, rect.Center, 1.5f)) continue;
                        if (OverlapsAnyProp(plan, rect)) continue;
                        bool hitsBreak = false;
                        for (int b = 0; b < plan.HallBreaks.Count && !hitsBreak; b++)
                            hitsBreak = plan.HallBreaks[b].Inflate(0.3f).Overlaps(rect);
                        if (hitsBreak) continue;

                        PropPlacement pp;
                        pp.Rect = rect;
                        pp.CorridorIndex = -1;
                        pp.FarSide = false;
                        pp.Spec = ps;
                        plan.Props.Add(pp);
                        placed = true;
                    }
                }
            }
        }

        // ---- 大厅挡墙垛 --------------------------------------------------------

        /// <summary>
        /// 大房间（长边 &gt;24m）内部的挡墙垛：交替从两侧伸出、每 8m 一道，厚度 0.4m，
        /// 长度取短边的 55%（上限 6.5m）。目的是把"房间 30m + 走廊 28m = 58m 直线视线"
        /// 打断成 ≤45m，同时留出 45% 短边的绕行走道，不妨碍通行也不封死房间。
        /// </summary>
        public static void AddHallBreaks(FloorPlan plan, FloorSpec spec)
        {
            plan.HallBreaks.Clear();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                RectF r = room.Rect;
                float longSide = Mathf.Max(r.W, r.H);
                float shortSide = Mathf.Min(r.W, r.H);
                if (longSide <= 24f || shortSide < 8f) continue;

                bool alongX = r.W >= r.H;
                float usable = longSide - 2.4f;
                int count = Mathf.FloorToInt((usable - 2f) / 8f);
                if (count < 1) continue;
                float stub = Mathf.Min(shortSide * 0.55f, 6.5f);
                float offset = (usable - (count - 1) * 8f) * 0.5f;

                for (int k = 0; k < count; k++)
                {
                    float t = 1.2f + offset + k * 8f;
                    bool fromLow = (k % 2) == 0;
                    RectF b;
                    if (alongX)
                    {
                        float x = r.X + t - 0.2f;
                        float y = fromLow ? r.Y + 0.2f : r.YMax - 0.2f - stub;
                        b = new RectF(x, y, 0.4f, stub);
                    }
                    else
                    {
                        float y = r.Y + t - 0.2f;
                        float x = fromLow ? r.X + 0.2f : r.XMax - 0.2f - stub;
                        b = new RectF(x, y, stub, 0.4f);
                    }

                    if (NearAnyDoor(plan, room, b.Center, 2.0f)) continue;
                    bool blocked = false;
                    for (int d = 0; d < plan.HallBreaks.Count && !blocked; d++)
                        blocked = plan.HallBreaks[d].Inflate(0.5f).Overlaps(b);
                    if (!blocked) plan.HallBreaks.Add(b);
                }
            }
        }

        /// <summary>
        /// 房间内的"挡视屏"：同一间房有两扇相对的门、且在墙上的位置几乎对齐时，站在一扇门口
        /// 就能一眼看到另一扇门外 20–30m。R1 的 StaggerOpposite 只负责把门的**编号**错开，
        /// 实际生成里仍会出现对齐的一对（实测 seed 2 从 (124.6,107.0) 穿过三间房看到 52.4m）。
        /// 这里在门内侧 1.8m 立一道 0.4m 厚、2.6m 长的墙屏，屏比门偏 1.3m，人从旁边绕过去，
        /// 直线视线被截断。登记进 plan.HallBreaks，墙体生成与视线审计共用同一份数据。
        /// </summary>
        public static void AddDoorScreens(FloorPlan plan, FloorSpec spec)
        {
            var allDoors = new List<Door>();
            CollectAllDoors(plan, allDoors);
            if (allDoors.Count == 0) return;

            var onWall = new List<Door>();
            var sides = new List<DoorSide>();

            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                RectF box = plan.Rooms[r].Rect;
                if (box.W < 4.0f || box.H < 4.0f) continue;

                onWall.Clear();
                sides.Clear();
                // 门记录是"谁建的谁存"，所以要看全层所有门，再判断它落在哪面墙上——
                // 房间之间共享的门只有一侧有记录（另一侧记在邻居的 ExtraDoors 里），只看自己会漏。
                for (int i = 0; i < allDoors.Count; i++)
                {
                    DoorSide side;
                    if (!OnBoundary(box, allDoors[i].Center, out side)) continue;
                    onWall.Add(allDoors[i]);
                    sides.Add(side);
                }

                for (int a = 0; a < onWall.Count; a++)
                {
                    for (int b = a + 1; b < onWall.Count; b++)
                    {
                        if (!RoomDoorPolicy.AreOpposite(sides[a], sides[b])) continue;
                        float alongA = sides[a] == DoorSide.North || sides[a] == DoorSide.South ? onWall[a].Center.x : onWall[a].Center.y;
                        float alongB = sides[b] == DoorSide.North || sides[b] == DoorSide.South ? onWall[b].Center.x : onWall[b].Center.y;
                        if (Mathf.Abs(alongA - alongB) > 0.9f) continue;

                        bool horizontalWall = sides[a] == DoorSide.North || sides[a] == DoorSide.South;
                        float wallLength = horizontalWall ? box.W : box.H;
                        float span = Mathf.Clamp(wallLength - 2.4f, 1.2f, 2.6f);
                        if (span < 1.2f) continue;

                        float lateral = (alongA + alongB) * 0.5f + span * 0.5f;
                        RectF screen;
                        if (horizontalWall)
                        {
                            if (box.H < 4.6f) continue;
                            float y = box.Y + box.H * 0.5f;
                            float x0 = Mathf.Clamp(lateral - span * 0.5f, box.X + 0.5f, box.XMax - span - 0.5f);
                            screen = new RectF(x0, y, span, 0.4f);
                        }
                        else
                        {
                            if (box.W < 4.6f) continue;
                            float x = box.X + box.W * 0.5f;
                            float y0 = Mathf.Clamp(lateral - span * 0.5f, box.Y + 0.5f, box.YMax - span - 0.5f);
                            screen = new RectF(x, y0, 0.4f, span);
                        }

                        bool overlaps = false;
                        for (int i = 0; i < plan.HallBreaks.Count && !overlaps; i++)
                            overlaps = plan.HallBreaks[i].Inflate(0.4f).Overlaps(screen);
                        if (overlaps) continue;
                        if (NearAnyPoint(plan, screen.Center, 1.2f)) continue;
                        plan.HallBreaks.Add(screen);
                        break; // 一间房一道屏就够，多了会把房间切碎
                    }
                }
            }
        }

        /// <summary>把全层所有门（房间主门 / 附加门 / 环路门）收进一个列表。</summary>
        static void CollectAllDoors(FloorPlan plan, List<Door> doors)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) doors.Add(plan.Rooms[i].Door);
                if (plan.Rooms[i].ExtraDoors == null) continue;
                for (int k = 0; k < plan.Rooms[i].ExtraDoors.Count; k++) doors.Add(plan.Rooms[i].ExtraDoors[k]);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) doors.Add(plan.LoopDoors[i]);
        }

        /// <summary>门心是否落在这个矩形的一条边上（容差 0.5m），并给出是哪条边。</summary>
        static bool OnBoundary(RectF box, Vector2 c, out DoorSide side)
        {
            const float tol = 0.5f;
            if (c.x >= box.X - tol && c.x <= box.XMax + tol && c.y >= box.Y - tol && c.y <= box.YMax + tol)
            {
                if (Mathf.Abs(c.y - box.Y) <= tol) { side = DoorSide.South; return true; }
                if (Mathf.Abs(c.y - box.YMax) <= tol) { side = DoorSide.North; return true; }
                if (Mathf.Abs(c.x - box.X) <= tol) { side = DoorSide.West; return true; }
                if (Mathf.Abs(c.x - box.XMax) <= tol) { side = DoorSide.East; return true; }
            }
            side = DoorSide.South;
            return false;
        }

        /// <summary>点附近是否有任何门或挡视屏（避免屏把门堵住）。</summary>
        static bool NearAnyPoint(FloorPlan plan, Vector2 p, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor && (plan.Rooms[i].Door.Center - p).sqrMagnitude < r2) return true;
                if (plan.Rooms[i].ExtraDoors == null) continue;
                for (int k = 0; k < plan.Rooms[i].ExtraDoors.Count; k++)
                    if ((plan.Rooms[i].ExtraDoors[k].Center - p).sqrMagnitude < r2) return true;
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++)
                if ((plan.LoopDoors[i].Center - p).sqrMagnitude < r2) return true;
            for (int i = 0; i < plan.HallBreaks.Count; i++)
                if (plan.HallBreaks[i].Inflate(radius).Contains(p)) return true;
            return false;
        }

        static bool NearAnyDoor(FloorPlan plan, Room room, Vector2 p, float radius)
        {
            if (room.HasDoor && (room.Door.Center - p).sqrMagnitude < radius * radius) return true;
            if (room.ExtraDoors != null)
            {
                for (int i = 0; i < room.ExtraDoors.Count; i++)
                    if ((room.ExtraDoors[i].Center - p).sqrMagnitude < radius * radius) return true;
            }
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
                if ((plan.CorridorOpenings[i].Center - p).sqrMagnitude < radius * radius) return true;
            return false;
        }

        static bool OverlapsAnyProp(FloorPlan plan, RectF rect)
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                if (plan.Props[i].Rect.Inflate(0.06f).Overlaps(rect)) return true;
            }
            return false;
        }
    }
}
