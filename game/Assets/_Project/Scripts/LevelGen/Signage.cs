using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum SignKind { Stair, Elevator, Floor, Room, Evacuation }

    /// <summary>Wall-mounted sign in plan coordinates. Normal points away from its supporting wall.</summary>
    public struct SignPlacement
    {
        public SignKind Kind;
        public Vector2 Center;
        public Vector2 Normal;
        public Vector2 Size;
        public RectF Footprint;
        public float Elevation;
        public string Text;
        public string TexturePath;
        public int RoomIndex;
        public Vector2 MapAnchor;
    }

    /// <summary>R9 pure placement pass: deterministic, door-safe and independent of scene objects.</summary>
    public static class SignageGenerator
    {
        public static void Populate(FloorPlan plan)
        {
            plan.Signs.Clear();
            int displayFloor = plan.Spec.WorldFloor;
            for (int i = 0; i < plan.Stairs.Count; i++)
                PlaceOnRect(plan, plan.Stairs[i], SignKind.Stair, "STAIRS", "Wayfinding/S_Way_Stairs",
                    new Vector2(0.55f, 0.55f), -1, null);

            PlaceOnRect(plan, plan.Core, SignKind.Elevator, "ELEVATOR", "Elevator/S_Elevator_Hall",
                new Vector2(0.8f, 0.4f), -1, null);
            PlaceOnRect(plan, plan.Core, SignKind.Floor, "F" + displayFloor, "Floor/S_Floor_Plate",
                new Vector2(0.65f, 0.65f), -1, null);

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                string number = (displayFloor * 100 + i + 1).ToString("0000");
                Door? preferred = room.HasDoor ? room.Door : (Door?)null;
                PlaceOnRect(plan, room.Rect, SignKind.Room, number, "Room/S_Room_Plate_Master",
                    new Vector2(0.5f, 0.25f), i, preferred);
            }

            PlaceEvacuation(plan, displayFloor);
        }

        static void PlaceEvacuation(FloorPlan plan, int displayFloor)
        {
            var rooms = new List<int>();
            for (int i = 0; i < plan.Rooms.Count; i++) rooms.Add(i);
            rooms.Sort((a, b) => RoomShape.AreaOf(plan.Rooms[b]).CompareTo(RoomShape.AreaOf(plan.Rooms[a])));
            for (int i = 0; i < rooms.Count; i++)
            {
                int index = rooms[i];
                Room room = plan.Rooms[index];
                Door? preferred = room.HasDoor ? room.Door : (Door?)null;
                if (PlaceOnRect(plan, room.Rect, SignKind.Evacuation, "YOU ARE HERE · F" + displayFloor,
                    null, new Vector2(0.9f, 0.675f), index, preferred)) return;
            }
            // A stair/core wall is a valid fallback on an unusually fragmented floor.
            for (int i = 0; i < plan.Stairs.Count; i++)
                if (PlaceOnRect(plan, plan.Stairs[i], SignKind.Evacuation, "YOU ARE HERE · F" + displayFloor,
                    null, new Vector2(0.9f, 0.675f), -1, null)) return;
            PlaceOnRect(plan, plan.Core, SignKind.Evacuation, "YOU ARE HERE · F" + displayFloor,
                null, new Vector2(0.9f, 0.675f), -1, null);
        }

        static bool PlaceOnRect(FloorPlan plan, RectF rect, SignKind kind, string text, string texture,
            Vector2 size, int roomIndex, Door? preferred)
        {
            if (preferred.HasValue)
            {
                Door door = preferred.Value;
                Vector2 tangent = Tangent(door.Side);
                float offset = (door.Width + size.x) * 0.5f + 0.3f;
                if (TrySide(plan, rect, door.Side, kind, text, texture, size, roomIndex,
                    door.Center + tangent * offset, door.Center - tangent * offset)) return true;
            }

            DoorSide first = kind == SignKind.Evacuation && preferred.HasValue ? preferred.Value.Side : DoorSide.South;
            for (int n = 0; n < 4; n++)
            {
                DoorSide side = (DoorSide)(((int)first + n) % 4);
                float length = side == DoorSide.North || side == DoorSide.South ? rect.W : rect.H;
                if (length < size.x + 0.3f) continue;
                if (TrySide(plan, rect, side, kind, text, texture, size, roomIndex,
                    Along(rect, side, 0.2f), Along(rect, side, 0.35f), Along(rect, side, 0.5f),
                    Along(rect, side, 0.65f), Along(rect, side, 0.8f))) return true;
            }
            return false;
        }

        static Vector2 Along(RectF rect, DoorSide side, float t)
        {
            if (side == DoorSide.South) return new Vector2(Mathf.Lerp(rect.X, rect.XMax, t), rect.Y);
            if (side == DoorSide.North) return new Vector2(Mathf.Lerp(rect.XMax, rect.X, t), rect.YMax);
            if (side == DoorSide.West) return new Vector2(rect.X, Mathf.Lerp(rect.Y, rect.YMax, t));
            return new Vector2(rect.XMax, Mathf.Lerp(rect.YMax, rect.Y, t));
        }

        static bool TrySide(FloorPlan plan, RectF rect, DoorSide side, SignKind kind, string text, string texture,
            Vector2 size, int roomIndex, params Vector2[] candidates)
        {
            Vector2 normal = Normal(side);
            Vector2 tangent = Tangent(side);
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 wall = candidates[i];
                float min = side == DoorSide.North || side == DoorSide.South ? rect.X : rect.Y;
                float max = side == DoorSide.North || side == DoorSide.South ? rect.XMax : rect.YMax;
                float value = side == DoorSide.North || side == DoorSide.South ? wall.x : wall.y;
                if (value < min + size.x * 0.5f + 0.12f || value > max - size.x * 0.5f - 0.12f) continue;
                Vector2 center = wall + normal * 0.025f;
                Vector2 footprintSize = Mathf.Abs(tangent.x) > 0.5f ? new Vector2(size.x, 0.05f) : new Vector2(0.05f, size.x);
                RectF footprint = new RectF(center.x - footprintSize.x * 0.5f, center.y - footprintSize.y * 0.5f,
                    footprintSize.x, footprintSize.y);
                if (!ClearOfDoors(plan, footprint)) continue;
                bool overlapsSign = false;
                for (int s = 0; s < plan.Signs.Count; s++)
                    if (footprint.Overlaps(plan.Signs[s].Footprint.Inflate(0.08f))) { overlapsSign = true; break; }
                if (overlapsSign) continue;
                plan.Signs.Add(new SignPlacement
                {
                    Kind = kind,
                    Center = center,
                    Normal = normal,
                    Size = size,
                    Footprint = footprint,
                    Elevation = kind == SignKind.Elevator ? 2.25f : kind == SignKind.Room ? 1.55f : 1.65f,
                    Text = text,
                    TexturePath = texture,
                    RoomIndex = roomIndex,
                    MapAnchor = kind == SignKind.Evacuation ? center : Vector2.zero
                });
                return true;
            }
            return false;
        }

        static bool ClearOfDoors(FloorPlan plan, RectF footprint)
        {
            RectF safe = footprint.Inflate(0.12f);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor && HitsDoor(safe, room.Door)) return false;
                if (room.ExtraDoors != null)
                    for (int d = 0; d < room.ExtraDoors.Count; d++) if (HitsDoor(safe, room.ExtraDoors[d])) return false;
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) if (HitsDoor(safe, plan.LoopDoors[i])) return false;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++) if (HitsDoor(safe, plan.CorridorOpenings[i])) return false;
            return true;
        }

        static bool HitsDoor(RectF sign, Door door)
        {
            bool horizontal = door.Side == DoorSide.North || door.Side == DoorSide.South;
            Vector2 size = horizontal ? new Vector2(door.Width, 0.35f) : new Vector2(0.35f, door.Width);
            RectF opening = new RectF(door.Center.x - size.x * 0.5f, door.Center.y - size.y * 0.5f, size.x, size.y);
            return sign.Overlaps(opening);
        }

        public static Vector2 Normal(DoorSide side)
        {
            if (side == DoorSide.North) return Vector2.up;
            if (side == DoorSide.South) return Vector2.down;
            if (side == DoorSide.West) return Vector2.left;
            return Vector2.right;
        }

        static Vector2 Tangent(DoorSide side)
        {
            Vector2 n = Normal(side);
            return new Vector2(n.y, -n.x);
        }
    }
}
