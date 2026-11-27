using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// R2 建筑级走廊断点。
    ///
    /// 长走廊按 28m 站距切段，站上插一段 3m 门斗（vestibule）：门斗两端的横墙各开一扇门，
    /// 两扇门沿走廊横向一左一右错开，所以没有任何一条直线视线能连着穿过两扇（想穿第二扇
    /// 必须转身或大幅斜穿，斜穿在 4.8m 带宽里几米就被侧墙挡住）。
    ///
    /// 主廊旁边并行的服务廊（2.3m 带宽 / 1.9m 净宽）用同一组站距切开，门斗在长墙上给它开
    /// 一道 1.2m 侧门，服务廊因此可以整层绕行；每个服务廊段都必须有至少一个开口，否则就是
    /// 一块谁也进不去的死空间。
    ///
    /// 这一步只写 plan.Corridors 与 plan.CorridorOpenings：墙是 FloorGeometry 从每个矩形的四条
    /// 边推出来的（默认全是实墙），洞是 CorridorOpenings 挖出来的。所以"走廊从墙里穿过去"的
    /// 每一处都必须在这里登记，漏一处就是一道封死的墙。
    ///
    /// 确定性：纯几何 + 固定站距，不吃随机数，同 seed 同结果。
    /// </summary>
    public static class CorridorBreaks
    {
        /// <summary>断点站距。验收要求每 26–32m 一个建筑断点，取中值 28m。</summary>
        public const float StationSpacing = 28f;
        /// <summary>门斗长度（沿走廊长轴）。</summary>
        public const float VestibuleLength = 3f;
        /// <summary>主廊横墙上的门宽。</summary>
        public const float MainDoorWidth = 1.4f;
        /// <summary>服务廊横墙上的门宽：1.9m 净宽里要并排两扇错位门，只能取 0.8m。</summary>
        public const float ServiceDoorWidth = 0.8f;
        /// <summary>门斗 → 服务廊的侧门宽。</summary>
        public const float SideOpeningWidth = 1.2f;
        /// <summary>06 §10：28m 站距 + 2m 最小尾段，保证任何直段都不超过 30m。</summary>
        public const float MinTail = 2f;
        /// <summary>短于这个长度的碎片不单独成段（会并进相邻段）。</summary>
        public const float MinPiece = 2f;

        const float Eps = 0.06f;

        struct Piece
        {
            public RectF Rect;
            /// <summary>长轴沿 Z（矩形高更长）。</summary>
            public bool Vertical;
            public bool Service;
            public bool Vestibule;
            /// <summary>带宽（长轴的垂直方向尺寸）。</summary>
            public float Band;
            /// <summary>带宽方向的起点（竖走廊是 X，横走廊是 Y）。</summary>
            public float Across;
            /// <summary>长轴上的起止。</summary>
            public float Start;
            public float End;
        }

        public static void Apply(FloorPlan plan, FloorSpec spec)
        {
            if (plan.Corridors.Count == 0) return;

            List<Corridor> src = new List<Corridor>(plan.Corridors);
            plan.Corridors.Clear();
            plan.CorridorOpenings.Clear();

            List<Piece> pieces = new List<Piece>();
            for (int i = 0; i < src.Count; i++) Split(src[i], spec, pieces, plan.CorridorOpenings);

            DropUnopenableService(pieces);
            DropOrphanService(pieces);
            AddLateralOpenings(pieces, plan.CorridorOpenings);

            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                Corridor c = default;
                c.Rect = p.Rect;
                // 门斗也是主廊的一段（3m × 4.8m，短边不够宽，光看尺寸会被判成支廊）
                c.Main = !p.Service && (p.Vestibule || Mathf.Min(p.Rect.W, p.Rect.H) >= spec.MainCorridor - 0.01f);
                c.Service = p.Service;
                c.Vestibule = p.Vestibule;
                c.Vertical = p.Vertical;
                plan.Corridors.Add(c);
            }

            EnsureServiceOpenings(plan);
        }

        /// <summary>Shorten an unserved 20–30m run with a real staggered-door vestibule.</summary>
        public static void EnsureLateralExitCompliance(FloorPlan plan, FloorSpec spec)
        {
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor old = plan.Corridors[i];
                if (old.Service || old.Vestibule) continue;
                bool vertical = old.Main ? old.Vertical : old.Rect.H > old.Rect.W;
                float length = vertical ? old.Rect.H : old.Rect.W;
                if (length < LevelAudit.SideExitRequiredFrom || length > LevelAudit.MaxCorridorSegment + 0.05f) continue;
                if (HasLateralExit(plan, old, vertical)) continue;
                float start = vertical ? old.Rect.Y : old.Rect.X;
                float end = vertical ? old.Rect.YMax : old.Rect.XMax;
                float mid = (start + end) * 0.5f;
                float a = mid - VestibuleLength * 0.5f, b = mid + VestibuleLength * 0.5f;
                if (a - start < MinPiece || end - b < MinPiece) continue;
                float band = vertical ? old.Rect.W : old.Rect.H;
                float across = vertical ? old.Rect.X : old.Rect.Y;
                Corridor left = old, vestibule = old, right = old;
                left.Rect = vertical ? new RectF(across, start, band, a - start) : new RectF(start, across, a - start, band);
                left.Vertical = vertical;
                vestibule.Rect = vertical ? new RectF(across, a, band, VestibuleLength) : new RectF(a, across, VestibuleLength, band);
                vestibule.Vertical = vertical;
                vestibule.Vestibule = true;
                right.Rect = vertical ? new RectF(across, b, band, end - b) : new RectF(b, across, end - b, band);
                right.Vertical = vertical;
                plan.Corridors[i] = left;
                int vestibuleIndex = plan.Corridors.Count;
                plan.Corridors.Add(vestibule);
                int rightIndex = plan.Corridors.Count;
                plan.Corridors.Add(right);
                RemapSplitDoors(plan, i, vestibuleIndex, rightIndex, a, b, vertical);
                AddCrossDoor(plan.CorridorOpenings, vertical, band, across, a, MainDoorWidth);
                AddCrossDoor(plan.CorridorOpenings, vertical, band, across, b, MainDoorWidth);
            }
        }

        static bool HasLateralExit(FloorPlan plan, Corridor c, bool vertical)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor && IsSideDoor(c.Rect, vertical, room.Door)) return true;
                if (room.ExtraDoors != null)
                    for (int d = 0; d < room.ExtraDoors.Count; d++)
                        if (IsSideDoor(c.Rect, vertical, room.ExtraDoors[d])) return true;
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++)
                if (IsSideDoor(c.Rect, vertical, plan.LoopDoors[i])) return true;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
                if (IsSideDoor(c.Rect, vertical, plan.CorridorOpenings[i])) return true;
            return false;
        }

        /// <summary>
        /// 侧出口必须是开在长墙上的门/开口：竖廊要 East/West 法线，横廊要 North/South 法线。
        /// 只看位置不看朝向会把横墙上的门当成侧出口，体检器于是报 20–30m 段无侧出口
        /// （实测 seed 1 F2 3.25×26.5 的 Connector 就是这么漏掉的）。
        /// </summary>
        static bool IsSideDoor(RectF rect, bool vertical, Door door)
        {
            Vector2 n;
            switch (door.Side)
            {
                case DoorSide.North: n = Vector2.up; break;
                case DoorSide.South: n = Vector2.down; break;
                case DoorSide.East: n = Vector2.right; break;
                default: n = Vector2.left; break;
            }
            if (vertical)
            {
                if (Mathf.Abs(n.x) < 0.5f || Mathf.Abs(n.y) > 0.5f) return false;
            }
            else
            {
                if (Mathf.Abs(n.y) < 0.5f || Mathf.Abs(n.x) > 0.5f) return false;
            }
            return OnLongWall(rect, vertical, door.Center);
        }

        static bool OnLongWall(RectF r, bool vertical, Vector2 p)
        {
            if (vertical)
                return p.y >= r.Y - 0.05f && p.y <= r.YMax + 0.05f &&
                    (Mathf.Abs(p.x - r.X) <= 0.75f || Mathf.Abs(p.x - r.XMax) <= 0.75f);
            return p.x >= r.X - 0.05f && p.x <= r.XMax + 0.05f &&
                (Mathf.Abs(p.y - r.Y) <= 0.75f || Mathf.Abs(p.y - r.YMax) <= 0.75f);
        }

        static Door RemapSplitDoor(Door d, int oldIndex, int vestibule, int right, float a, float b, bool vertical)
        {
            if (d.Corridor != oldIndex) return d;
            float along = vertical ? d.Center.y : d.Center.x;
            if (along >= b) d.Corridor = right;
            else if (along >= a) d.Corridor = vestibule;
            return d;
        }

        static void RemapSplitDoors(FloorPlan plan, int oldIndex, int vestibule, int right, float a, float b, bool vertical)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor) room.Door = RemapSplitDoor(room.Door, oldIndex, vestibule, right, a, b, vertical);
                if (room.ExtraDoors != null)
                    for (int d = 0; d < room.ExtraDoors.Count; d++)
                        room.ExtraDoors[d] = RemapSplitDoor(room.ExtraDoors[d], oldIndex, vestibule, right, a, b, vertical);
                room.CorridorIndex = room.HasDoor ? room.Door.Corridor : -1;
                room.DoorToCorridor = room.CorridorIndex >= 0;
                plan.Rooms[i] = room;
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++)
                plan.LoopDoors[i] = RemapSplitDoor(plan.LoopDoors[i], oldIndex, vestibule, right, a, b, vertical);
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
                plan.CorridorOpenings[i] = RemapSplitDoor(plan.CorridorOpenings[i], oldIndex, vestibule, right, a, b, vertical);
        }

        /// <summary>
        /// 收尾保证：每条服务廊段的长墙上必须真的登记了一个开口。前面的配对是"就近找邻居"，
        /// 会出现"门开在邻居墙上、这一段其实还没通"的情况（实测 seed 3/6 每层还有 3–4 段），
        /// 所以这里按 SightlineTests 同一口径复核一遍，缺就补。
        /// </summary>
        static void EnsureServiceOpenings(FloorPlan plan)
        {
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                if (!c.Service || HasOpening(plan, c)) continue;
                for (int j = 0; j < plan.Corridors.Count; j++)
                {
                    if (j == i || plan.Corridors[j].Service) continue;
                    RectF other = plan.Corridors[j].Rect;
                    if (!Adjacent(other, c.Rect, out DoorSide side)) continue;
                    float from = c.Vertical ? Mathf.Max(c.Rect.Y, other.Y) : Mathf.Max(c.Rect.X, other.X);
                    float to = c.Vertical ? Mathf.Min(c.Rect.YMax, other.YMax) : Mathf.Min(c.Rect.XMax, other.XMax);
                    if (to - from < SideOpeningWidth + 1.2f) continue;
                    Piece target;
                    target.Rect = c.Rect;
                    target.Vertical = c.Vertical;
                    target.Service = true;
                    target.Vestibule = false;
                    target.Band = c.Vertical ? c.Rect.W : c.Rect.H;
                    target.Across = c.Vertical ? c.Rect.X : c.Rect.Y;
                    target.Start = from;
                    target.End = to;
                    AddOpening(plan.CorridorOpenings, target, Flip(side), (from + to) * 0.5f);
                    break;
                }
            }
        }

        /// <summary>与 SightlineTests.HasSideOpening 同一口径：长墙上登记过开口就算通。</summary>
        static bool HasOpening(FloorPlan plan, Corridor c)
        {
            RectF r = c.Rect;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Vector2 p = plan.CorridorOpenings[i].Center;
                if (c.Vertical)
                {
                    bool onWall = Mathf.Abs(p.x - r.X) < 0.2f || Mathf.Abs(p.x - r.XMax) < 0.2f;
                    if (onWall && p.y > r.Y - 0.01f && p.y < r.YMax + 0.01f) return true;
                }
                else
                {
                    bool onWall = Mathf.Abs(p.y - r.Y) < 0.2f || Mathf.Abs(p.y - r.YMax) < 0.2f;
                    if (onWall && p.x > r.X - 0.01f && p.x < r.XMax + 0.01f) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ 切段

        static void Split(Corridor c, FloorSpec spec, List<Piece> pieces, List<Door> openings)
        {
            RectF r = c.Rect;
            bool vertical = r.H >= r.W;
            float band = vertical ? r.W : r.H;
            float across = vertical ? r.X : r.Y;
            float start = vertical ? r.Y : r.X;
            float end = vertical ? r.YMax : r.XMax;

            // Emit 把走廊矩形 Clip 掉 Core/Stairs 之后会留下带宽被削掉一条的碎片
            // （2.3 的服务廊被削成 2.0，净宽只剩 1.6，验收要求 1.8–2.1）。这种服务廊碎片
            // 既不合规又是挨着核心筒的死条带，直接丢掉。
            //
            // 主廊碎片不能丢：楼梯/电梯口常常就贴着那条被削窄的段，丢掉会让消防梯变成
            // 一格都走不到的孤岛（实测 seed 31/34 的 FireStair 距离变成 -1）。
            if (c.Service && Mathf.Abs(band - spec.ServiceCorridor) > 0.02f) return;

            float doorWidth = c.Service ? ServiceDoorWidth : MainDoorWidth;

            List<float> stations = new List<float>();
            for (float s = start + StationSpacing; end - (s + VestibuleLength * 0.5f) > MinTail; s += StationSpacing)
                stations.Add(s);
            // 一条 32–33.5m 的走廊上面不加站也超长、加站又只剩 2–3m 尾巴，这时把站挪到中间，
            // 让两侧各留十几米而不是留一整段 33m 的通视。
            if (stations.Count == 0 && end - start > StationSpacing + MinTail)
                stations.Add(start + (end - start) * 0.5f);

            float prev = start;
            for (int i = 0; i < stations.Count; i++)
            {
                float st = stations[i];
                float a = st - VestibuleLength * 0.5f;
                float b = st + VestibuleLength * 0.5f;
                if (a - prev >= MinPiece) AddPiece(pieces, c, vertical, prev, a, band, across, false);
                AddPiece(pieces, c, vertical, a, b, band, across, true);
                AddCrossDoor(openings, vertical, band, across, a, doorWidth);
                AddCrossDoor(openings, vertical, band, across, b, doorWidth);
                prev = b;
            }
            if (end - prev >= MinPiece) AddPiece(pieces, c, vertical, prev, end, band, across, false);
        }

        static void AddPiece(List<Piece> pieces, Corridor c, bool vertical, float from, float to,
            float band, float across, bool vestibule)
        {
            Piece p;
            p.Vertical = vertical;
            p.Service = c.Service;
            p.Vestibule = vestibule;
            p.Band = band;
            p.Across = across;
            p.Start = from;
            p.End = to;
            p.Rect = vertical
                ? new RectF(across, from, band, to - from)
                : new RectF(from, across, to - from, band);
            pieces.Add(p);
        }

        /// <summary>
        /// 横墙门。一左一右错位是这一步的全部意义：两扇门沿带宽方向不重叠，直线看过去
        /// 左边那扇后面永远是墙。
        ///
        /// 取哪一侧必须由门的**绝对坐标**决定，不能按"这条走廊上的第几个站"编号：走廊被
        /// Core/楼梯切开后，同一条直线上的几段走廊各自从 0 开始编号，交接处的两扇门就会
        /// 落在同一侧（实测 seed 7 从 (91.1,0.9) 一眼看到 60m 外，正是 26.5/29.5 两扇门
        /// 都在低侧）。用 line / VestibuleLength 的奇偶做取侧，同一条线上的门天然交替。
        /// </summary>
        static void AddCrossDoor(List<Door> openings, bool vertical, float band, float across,
            float line, float width)
        {
            // 窄廊里 1.4m 的门几乎占满带宽，两扇错位门会重叠成一条直线，视线照样穿过去
            // （实测 seed 1 F2 一条 1.6m 宽的绕行廊，站距两侧各 25m，视线 0.6→49.2 穿过两扇门）。
            // 带宽不够时按“两侧各留 0.2m 墙垛、两扇门之间至少留 0.3m 墙垛”把门收窄；
            // 宽廊（>=3.4m）不受影响，仍然是 1.4m 主门。
            float maxDoor = (band - 0.4f) * 0.5f - 0.15f;
            if (width > maxDoor) width = Mathf.Max(0.5f, maxDoor);
            float half = width * 0.5f;
            float margin = Mathf.Max(0.2f, (band - width * 2f) * 0.25f);
            // 取侧：line 的奇偶保证同一条走廊上的门交替，across 的奇偶再拆开"两条几乎重叠的走廊"
            // （RoomDoorPolicy 的喉道连接器会跟主廊并在一起，两者的 across 差 0.05–2.5m）。
            // 只用 line 的话，两条重叠走廊各自贡献一扇门、恰好落在同一侧，直线就能穿过去
            // （实测 seed 7 在 (91.1,0.9) 一眼看到 60m 外）。
            // 用 floor 而不是 round：RoundToInt 在 .5 上取偶数，25.5/3=8.5→8、28.5/3=9.5→10，
            // 同一个门斗的两扇门会算成同奇偶、落在同一侧（实测 seed 1 视线 51.5m）。
            bool nearSide = ((Mathf.FloorToInt(line / VestibuleLength) + Mathf.FloorToInt(across)) & 1) == 0;
            float centre = nearSide ? across + margin + half : across + band - margin - half;

            Door d;
            d.Width = width;
            d.Corridor = -1;
            d.OtherRoom = -1;
            if (vertical)
            {
                // 竖走廊的横墙法线沿 Z：DoorSide.North 走水平墙字典，坐标取 Center.y = line
                d.Side = DoorSide.North;
                d.Center = new Vector2(centre, line);
            }
            else
            {
                // 横走廊的横墙法线沿 X：DoorSide.East 走垂直墙字典，坐标取 Center.x = line
                d.Side = DoorSide.East;
                d.Center = new Vector2(line, centre);
            }
            openings.Add(d);
        }

        // ------------------------------------------------------------------ 侧向开口

        /// <summary>
        /// 服务廊被核心筒/楼梯削掉一条边时，会和主廊之间留出 0.3–0.4m 的夹缝：两圈墙谁也进不去，
        /// 这段服务廊天生就是死空间。直接丢掉——留着只会让"每段都要有开口"变成假要求。
        /// </summary>
        /// <summary>
        /// 短到开不出一道侧门的服务廊碎片（被核心筒/楼梯裁掉一截、或被门斗站切出 2–2.4m 的尾巴）
        /// 永远满足不了“每段服务廊都要有开口”，留着只是走不进去的死空间，直接丢。
        /// 侧门要求沿长轴重叠 >= SideOpeningWidth + 1.2m，比这还短的段不可能开门。
        /// </summary>
        static void DropUnopenableService(List<Piece> pieces)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                if (!pieces[i].Service) continue;
                if (pieces[i].End - pieces[i].Start < SideOpeningWidth + 1.2f) pieces.RemoveAt(i);
            }
        }

        static void DropOrphanService(List<Piece> pieces)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                if (!pieces[i].Service) continue;
                bool any = false;
                for (int j = 0; j < pieces.Count && !any; j++)
                {
                    if (j == i || pieces[j].Service) continue;
                    any = Adjacent(pieces[j].Rect, pieces[i].Rect, out DoorSide ignored);
                }
                if (!any) pieces.RemoveAt(i);
            }
        }

        static void AddLateralOpenings(List<Piece> pieces, List<Door> openings)
        {
            // 每个主廊门斗都对服务廊开一道侧门。门必须开在服务廊自己那面墙上，位置取两段在
            // 长轴上的重叠中点：早先版本按门斗自己的中线开门，门落到了相邻的另一段服务廊上，
            // 结果两段里有一段"开了"、另一段仍然是封死的（实测 seed 3/6 每层 4–8 段）。
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                if (p.Service || !p.Vestibule) continue;
                int j = FindAdjacent(pieces, i, true, out DoorSide side);
                if (j < 0) continue;
                if (!OverlapAlong(p, pieces[j], out float along)) continue;
                AddOpening(openings, pieces[j], Flip(side), along);
            }

            // 兜底：每条服务廊段都要有一个开在自己长墙上的口，否则就是封死的死空间
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i].Service || HasOpeningOn(pieces[i], openings)) continue;
                int j = FindAdjacent(pieces, i, false, out DoorSide side);
                if (j < 0) continue;
                if (!OverlapAlong(pieces[i], pieces[j], out float along)) continue;
                AddOpening(openings, pieces[i], side, along);
            }

        }

        static int FindAnyAdjacent(List<Piece> pieces, int i, out DoorSide side)
        {
            side = DoorSide.East;
            int best = -1;
            float bestOverlap = 0.8f;
            for (int j = 0; j < pieces.Count; j++)
            {
                if (j == i || !Adjacent(pieces[i].Rect, pieces[j].Rect, out DoorSide candidate)) continue;
                float overlap = Mathf.Min(pieces[i].End, pieces[j].End) - Mathf.Max(pieces[i].Start, pieces[j].Start);
                if (overlap <= bestOverlap) continue;
                bestOverlap = overlap;
                best = j;
                side = candidate;
            }
            return best;
        }

        /// <summary>两段在长轴上的重叠够不够开一扇侧门；够就把门心位置交出来。</summary>
        static bool OverlapAlong(Piece a, Piece b, out float along)
        {
            along = 0f;
            float from = Mathf.Max(a.Start, b.Start);
            float to = Mathf.Min(a.End, b.End);
            if (to - from < SideOpeningWidth + 1.2f) return false;
            along = (from + to) * 0.5f;
            return true;
        }

        static DoorSide Flip(DoorSide side)
        {
            switch (side)
            {
                case DoorSide.East: return DoorSide.West;
                case DoorSide.West: return DoorSide.East;
                case DoorSide.North: return DoorSide.South;
                default: return DoorSide.North;
            }
        }

        static void AddOpening(List<Door> openings, Piece target, DoorSide side, float along)
        {
            Door d;
            d.Width = SideOpeningWidth;
            d.Corridor = -1;
            d.OtherRoom = -1;
            d.Side = side;
            switch (side)
            {
                case DoorSide.East: d.Center = new Vector2(target.Rect.XMax, along); break;
                case DoorSide.West: d.Center = new Vector2(target.Rect.X, along); break;
                case DoorSide.North: d.Center = new Vector2(along, target.Rect.YMax); break;
                default: d.Center = new Vector2(along, target.Rect.Y); break;
            }
            openings.Add(d);
        }

        /// <summary>这一段的长墙上是否已经有开口（与 SightlineTests 同一口径）。</summary>
        static bool HasOpeningOn(Piece p, List<Door> openings)
        {
            RectF r = p.Rect;
            for (int i = 0; i < openings.Count; i++)
            {
                Vector2 c = openings[i].Center;
                if (p.Vertical)
                {
                    bool onWall = Mathf.Abs(c.x - r.X) < 0.2f || Mathf.Abs(c.x - r.XMax) < 0.2f;
                    if (onWall && c.y > p.Start - 0.01f && c.y < p.End + 0.01f) return true;
                }
                else
                {
                    bool onWall = Mathf.Abs(c.y - r.Y) < 0.2f || Mathf.Abs(c.y - r.YMax) < 0.2f;
                    if (onWall && c.x > p.Start - 0.01f && c.x < p.End + 0.01f) return true;
                }
            }
            return false;
        }

        /// <summary>找与第 i 段共长边、沿长轴重叠最多的另一侧走廊段。</summary>
        static int FindAdjacent(List<Piece> pieces, int i, bool wantService, out DoorSide side)
        {
            side = DoorSide.East;
            int best = -1;
            float bestOverlap = 0.8f;
            for (int j = 0; j < pieces.Count; j++)
            {
                if (j == i) continue;
                if (pieces[j].Service != wantService) continue;
                if (!Adjacent(pieces[i].Rect, pieces[j].Rect, out DoorSide candidate)) continue;
                float overlap = Mathf.Min(pieces[i].End, pieces[j].End) - Mathf.Max(pieces[i].Start, pieces[j].Start);
                if (overlap <= bestOverlap) continue;
                bestOverlap = overlap;
                best = j;
                side = candidate;
            }
            return best;
        }

        static bool Adjacent(RectF main, RectF service, out DoorSide side)
        {
            side = DoorSide.East;
            if (Near(main.XMax, service.X) && OverlapLength(main.Y, main.YMax, service.Y, service.YMax) > 0.8f)
            {
                side = DoorSide.East;
                return true;
            }
            if (Near(service.XMax, main.X) && OverlapLength(main.Y, main.YMax, service.Y, service.YMax) > 0.8f)
            {
                side = DoorSide.West;
                return true;
            }
            if (Near(main.YMax, service.Y) && OverlapLength(main.X, main.XMax, service.X, service.XMax) > 0.8f)
            {
                side = DoorSide.North;
                return true;
            }
            if (Near(service.YMax, main.Y) && OverlapLength(main.X, main.XMax, service.X, service.XMax) > 0.8f)
            {
                side = DoorSide.South;
                return true;
            }
            return false;
        }

        static bool Near(float a, float b) { return Mathf.Abs(a - b) <= Eps; }

        static float OverlapLength(float a0, float a1, float b0, float b1)
        {
            return Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
        }

        // ------------------------------------------------------------------ 统计（测试/实验室用）

        /// <summary>一条走廊被切成了多少段：用来断言"没有一段超过 32m"。</summary>
        public static float LongestSegment(FloorPlan plan)
        {
            float best = 0f;
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                best = Mathf.Max(best, Mathf.Max(r.W, r.H));
            }
            return best;
        }

        public static int CountVestibules(FloorPlan plan)
        {
            int n = 0;
            for (int i = 0; i < plan.Corridors.Count; i++) if (plan.Corridors[i].Vestibule) n++;
            return n;
        }
    }
}
