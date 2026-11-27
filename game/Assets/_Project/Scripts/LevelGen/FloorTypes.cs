using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum FloorBand { Podium, Tower, Top }

    public enum FloorPrototype { Executive, Meeting, OpenOffice, SmallOffice, Renovation, Utility }

    public enum ExtractionKind { FireStair, Elevator, Freight, WindowBasket, RoofRappel }

    public enum DoorSide { South, North, West, East }

    public struct RectF
    {
        public float X, Y, W, H;

        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public float XMax { get { return X + W; } }
        public float YMax { get { return Y + H; } }
        public Vector2 Center { get { return new Vector2(X + W * 0.5f, Y + H * 0.5f); } }
        public float Area { get { return W * H; } }

        public bool Contains(Vector2 p) { return p.x >= X && p.x <= XMax && p.y >= Y && p.y <= YMax; }
        public bool Overlaps(RectF o) { return X < o.XMax && o.X < XMax && Y < o.YMax && o.Y < YMax; }
        public RectF Inflate(float d) { return new RectF(X - d, Y - d, W + 2f * d, H + 2f * d); }

        public override string ToString()
        {
            return string.Format("({0:0.#},{1:0.#} {2:0.#}x{3:0.#})", X, Y, W, H);
        }
    }

    public struct Corridor
    {
        public RectF Rect;
        /// <summary>主走廊（4.8m）：小地图上按主廊上色。</summary>
        public bool Main;
        /// <summary>主廊旁边并行的服务廊（带宽 2.3m / 净宽 1.9m）：只走人，不摆家具。</summary>
        public bool Service;
        /// <summary>长走廊切段时插入的 3m 门斗：两端横墙各开一扇错位门。</summary>
        public bool Vestibule;
        /// <summary>
        /// 长轴是否沿 Z。门斗是 3m（长轴）× 带宽，带宽 4.8m 时单看长方形会把长轴判反，
        /// 所以由 CorridorBreaks 在切段时写进来，别再让下游用 W/H 比大小去猜。
        /// </summary>
        public bool Vertical;
        /// <summary>
        /// 由房间溶解而来的通行区（RoomDoorPolicy.DissolveRoom）：它是大厅/过厅，不是走廊。
        /// 走廊直段、服务廊开口这类指标不该把大厅算进去，否则两段走廊隔着大厅会被并成一条超长“直廊”。
        /// </summary>
        public bool Hall;
    }

    public struct Zone
    {
        public RectF Rect;
        public string Name;
        public FloorPrototype Prototype;
        /// <summary>本象限的主题（数据中心/冷链/制造/医疗/仓储/办公/行政）：切分力度与设施表都按它走。</summary>
        public FloorTheme Theme;
    }

    public struct Door
    {
        public Vector2 Center;
        public DoorSide Side;
        public float Width;
        /// <summary>门通向的走廊下标；-1 表示不是通向走廊。</summary>
        public int Corridor;
        /// <summary>门通向的房间下标（套间门/环路门）；-1 表示不是房间对房间。</summary>
        public int OtherRoom;
    }

    /// <summary>房间用途。只用来决定摆放/标识/灯光；&lt;56㎡ 的小间才允许是后四种服务间。</summary>
    public enum RoomType
    {
        Normal = 0,
        Storage = 1,
        Toilet = 2,
        Riser = 3,
        Vault = 4,
    }

    public struct Room
    {
        public RectF Rect;
        public int ZoneIndex;
        /// <summary>用途标签：Normal 办公/会议；Storage 储藏；Toilet 卫生间；Riser 管井；Vault 保险库（暂未生成）。</summary>
        public RoomType Type;
        /// <summary>设施细分（机房/冷库/车间/档案室…）：决定道具集与材质槽，不影响 R3 的面积分档。</summary>
        public FacilityKind Facility;
        public Door Door;
        public bool HasDoor;
        /// <summary>门通向的走廊下标；-1 表示门通向相邻房间（套间门）。</summary>
        public int CorridorIndex;
        public bool DoorToCorridor;
        /// <summary>除主门之外的额外门：一个房间可以开多个门，保证绕行与多路联通。</summary>
        public List<Door> ExtraDoors;
        /// <summary>被 45° 切掉的角（0–2 个）。矩形仍是逻辑包围盒，切角只是把角落三角区封成实心。</summary>
        public List<CornerCut> Cuts;
        /// <summary>
        /// 房间真实轮廓（CCW 多边形）。房间是贴着楼层外廓裁出来的，所以可能是梯形、五边形、L 形；
        /// Rect 只是包围盒，墙、地、导航栅格、小地图都必须以 Outline 为准。null 表示就是 Rect。
        /// </summary>
        public Vector2[] Outline;
    }

    /// <summary>房间一个被 45° 斜墙切掉的角。Corner：0=西南 1=东南 2=东北 3=西北。</summary>
    public struct CornerCut
    {
        public int Corner;
        /// <summary>沿两条边从角点量起的长度（米）：越大切得越狠。</summary>
        public float Size;

        public CornerCut(int corner, float size) { Corner = corner; Size = size; }
    }

    /// <summary>
    /// 房间轮廓的几何真值：矩形减去若干切角三角区。所有"这个点是不是房间里能站人的地方"
    /// 的判断（掩体摆放、导航栅格、小地图）都必须走这里，否则斜墙里会长出桌子或者被当成可走区。
    /// </summary>
    public static class RoomShape
    {
        public const float MinCut = 1.4f;
        public const float MaxCut = 3.2f;
        /// <summary>切角三角区的对角线离角点还有多远时仍算"角里"（墙厚的一半）。</summary>
        public const float Skin = 0.25f;

        public static bool IsWest(int corner) { return corner == 0 || corner == 3; }
        public static bool IsSouth(int corner) { return corner == 0 || corner == 1; }

        public static Vector2 CornerPoint(RectF r, int corner)
        {
            float x = IsWest(corner) ? r.X : r.XMax;
            float y = IsSouth(corner) ? r.Y : r.YMax;
            return new Vector2(x, y);
        }

        /// <summary>点到被切掉那个角的"两条直角边距离之和"：小于 Size 就在斜墙外面（实心三角区里）。</summary>
        public static float CornerDepth(RectF r, CornerCut cut, Vector2 p)
        {
            float u = IsWest(cut.Corner) ? p.x - r.X : r.XMax - p.x;
            float v = IsSouth(cut.Corner) ? p.y - r.Y : r.YMax - p.y;
            return u + v;
        }

        public static bool Blocked(RectF r, CornerCut cut, Vector2 p)
        {
            if (cut.Size <= 0.01f) return false;
            if (!r.Contains(p)) return false;
            return CornerDepth(r, cut, p) < cut.Size - Skin;
        }

        public static bool Blocked(RectF r, List<CornerCut> cuts, Vector2 p)
        {
            if (cuts == null) return false;
            for (int i = 0; i < cuts.Count; i++) if (Blocked(r, cuts[i], p)) return true;
            return false;
        }

        /// <summary>世界点是否落在某个房间被切掉的角里（用于过滤掩体、可走性判断）。</summary>
        public static bool BlockedInFloor(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.Cuts == null || room.Cuts.Count == 0) continue;
                if (!room.Rect.Contains(p)) continue;
                if (Blocked(room.Rect, room.Cuts, p)) return true;
            }
            return false;
        }

        /// <summary>房间真实面积：有 Outline（贴外廓裁出来的多边形）就用鞋带公式，否则退回包围盒。</summary>
        public static float AreaOf(Room room)
        {
            if (room.Outline == null || room.Outline.Length < 3) return room.Rect.Area;
            return Mathf.Abs(FloorEnvelope.SignedArea(room.Outline));
        }

        /// <summary>多边形面积（鞋带公式，取绝对值）。</summary>
        public static float OutlineArea(Vector2[] outline)
        {
            if (outline == null || outline.Length < 3) return 0f;
            return Mathf.Abs(FloorEnvelope.SignedArea(outline));
        }

        /// <summary>斜墙盒：沿切角对角线放一段厚墙，把角落三角区封死。</summary>
        public static void CutWall(RectF r, CornerCut cut, out Vector2 center, out float length, out float yawRadians)
        {
            float s = cut.Size;
            float x0 = IsWest(cut.Corner) ? r.X : r.XMax;
            float y0 = IsSouth(cut.Corner) ? r.Y : r.YMax;
            float dx = IsWest(cut.Corner) ? s : -s;
            float dy = IsSouth(cut.Corner) ? s : -s;
            // 对角线的两个端点：A = 角点沿 X 退 s，B = 角点沿 Y 退 s
            Vector2 a = new Vector2(x0 + dx, y0);
            Vector2 b = new Vector2(x0, y0 + dy);
            center = (a + b) * 0.5f;
            length = (b - a).magnitude;
            // 本地 +X 映射到 (cos yaw, -sin yaw)，所以 yaw = atan2(-dz, dx)
            yawRadians = Mathf.Atan2(-(b.y - a.y), b.x - a.x);
        }
    }

    public struct ExtractionPoint
    {
        public RectF Rect;
        public Vector2 Center;
        public ExtractionKind Kind;
        public bool NeedsPower;
        public bool NeedsCard;
        public float ChannelSeconds;
        public string Label;
    }

    public class FloorSpec
    {
        public int Seed = 1;
        /// <summary>Zero-based raid floor slot; display labels add one.</summary>
        public int FloorIndex;
        /// <summary>Real building floor used by signage; zero-based structural slots remain in FloorIndex.</summary>
        public int WorldFloor = 1;
        public float Width = 200f;
        public float Height = 160f;
        public FloorBand Band = FloorBand.Tower;
        public FloorPrototype Prototype = FloorPrototype.OpenOffice;
        public float ColumnGrid = 8f;
        public float MainCorridor = 4.8f;
        public float SecondaryCorridor = 3.6f;
        /// <summary>
        /// 主廊旁并行的服务廊带宽。两条共一条墙线（墙厚 0.4m），所以净宽 = 带宽 - 0.4m：
        /// 2.3m 带宽 → 1.9m 净宽，落在验收要求的 1.8–2.1m 内。
        /// </summary>
        public float ServiceCorridor = 2.3f;
        public Vector2 CoreSize = new Vector2(16f, 12f);
        public float CoreNorthOffset = 4f;
        public float WallInset = 0.2f;
        /// <summary>走廊中间必须留出的通行净宽（两侧摆掩体后仍要能过）。</summary>
        public float CorridorClearLane = 2.4f;
        public bool PlaceCoverProps = true;

        /// <summary>2 = 主走廊 + 次走廊两级；3 = 再加 1.8m 支走廊。</summary>
        public int CorridorLevels = 2;
        public float TertiaryCorridor = 2.7f;
        /// <summary>父区域超过这个面积才铺三级走廊（1.8m），低于它只砌墙。</summary>
        public float TertiaryMinArea = 190f;
        public float LoopDoorChance = 0.42f;

        /// <summary>小于这个面积的叶子不再切、也不收成房间：小隔间是迷宫感的主要来源。</summary>
        public float MinRoomArea = 60f;

        /// <summary>房间最短边；低于它房间连一张办公桌都放不下。</summary>
        public float MinRoomSide = 3.2f;

        /// <summary>R11：由 StairGraphPlan 下发本层全部真实/边界楼梯口；电梯核心仍跨层对齐。</summary>
        public bool UseStairGraph;
        public RectF CoreRect;
        public List<RectF> StairRects = new List<RectF>();

        public static FloorSpec Default(int seed)
        {
            FloorSpec spec = new FloorSpec();
            spec.Seed = seed;
            return spec;
        }
    }

    public class FloorPlan
    {
        public FloorSpec Spec;
        public RectF Bounds;
        /// <summary>楼层外廓（非矩形）。房间与几何都由它裁出来。</summary>
        public EnvelopeShape Envelope;
        public RectF Core;
        public List<RectF> Stairs = new List<RectF>();
        public List<Vector2> Columns = new List<Vector2>();
        public List<Corridor> Corridors = new List<Corridor>();
        public List<Zone> Zones = new List<Zone>();
        public List<Room> Rooms = new List<Room>();
        /// <summary>本层实际启用的撤离点（4–6 个，由 Extractions 从候选里挑出）。</summary>
        public List<ExtractionPoint> Extractions = new List<ExtractionPoint>();
        /// <summary>本层所有物理撤离设施候选（消防梯/电梯/货梯/擦窗机/天台下索），先全放、后挑选。</summary>
        public List<ExtractionPoint> ExtractionCandidates = new List<ExtractionPoint>();
        public List<PropPlacement> Props = new List<PropPlacement>();
        public List<SignPlacement> Signs = new List<SignPlacement>();
        public List<RectF> SpawnZones = new List<RectF>();
        /// <summary>房间与房间之间的额外门（绕过走廊的环路边）。</summary>
        public List<Door> LoopDoors = new List<Door>();
        /// <summary>
        /// 走廊内部开口：横墙门、门斗↔服务廊侧门。走廊矩形的四条边默认全部是实墙，
        /// 所以任何"走廊从这堵墙穿过去"的地方都必须在这里显式登记，否则会被墙封死。
        /// </summary>
        public List<Door> CorridorOpenings = new List<Door>();

        /// <summary>
        /// 大厅内部的挡墙垛：&gt;24m 长的大房间（机房大厅/车间/装卸区）如果没有内部结构，
        /// "房间 30m + 走廊 28m"就能凑出 50m 直线视线。这些 0.4m 厚的墙垛交替从两侧伸出，
        /// 打断视线的同时留出绕行通道；登记在这里供墙体生成与视线审计共用。
        /// </summary>
        public List<RectF> HallBreaks = new List<RectF>();

        /// <summary>房间真实面积（扣掉切角/贴外廓裁掉的三角区），不是包围盒面积。</summary>
        public float RoomArea
        {
            get
            {
                float a = 0f;
                for (int i = 0; i < Rooms.Count; i++) a += RoomShape.AreaOf(Rooms[i]);
                return a;
            }
        }

        public float CorridorLength
        {
            get
            {
                float len = 0f;
                for (int i = 0; i < Corridors.Count; i++)
                {
                    RectF r = Corridors[i].Rect;
                    len += Mathf.Max(r.W, r.H);
                }
                return len;
            }
        }
    }
}
