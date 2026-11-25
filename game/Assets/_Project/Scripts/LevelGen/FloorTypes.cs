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
        public bool Main;
    }

    public struct Zone
    {
        public RectF Rect;
        public string Name;
        public FloorPrototype Prototype;
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

    public struct Room
    {
        public RectF Rect;
        public int ZoneIndex;
        public Door Door;
        public bool HasDoor;
        /// <summary>门通向的走廊下标；-1 表示门通向相邻房间（套间门）。</summary>
        public int CorridorIndex;
        public bool DoorToCorridor;
        /// <summary>除主门之外的额外门：一个房间可以开多个门，保证绕行与多路联通。</summary>
        public List<Door> ExtraDoors;
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
        public float Width = 200f;
        public float Height = 160f;
        public FloorBand Band = FloorBand.Tower;
        public FloorPrototype Prototype = FloorPrototype.OpenOffice;
        public float ColumnGrid = 8f;
        public float MainCorridor = 4.8f;
        public float SecondaryCorridor = 3.6f;
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
        public List<RectF> SpawnZones = new List<RectF>();
        /// <summary>房间与房间之间的额外门（绕过走廊的环路边）。</summary>
        public List<Door> LoopDoors = new List<Door>();

        public float RoomArea
        {
            get
            {
                float a = 0f;
                for (int i = 0; i < Rooms.Count; i++) a += Rooms[i].Rect.Area;
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
