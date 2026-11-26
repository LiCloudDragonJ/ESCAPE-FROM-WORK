using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Final door-layout pass. Connectivity repair runs before this pass, so every
    /// later opening is normalized here instead of being fixed in three different places.
    /// Corner distances are measured from the opening edge, never from its centre.
    /// </summary>
    public static class RoomDoorPolicy
    {
        public const float MinOppositeStagger = 1.10f;
        const float MaxGap = 0.8f;
        const float MaxConnectorGap = 3.0f;
        const float MinOverlap = 1.0f;
        const float PreferredWidth = 1.10f;

        sealed class Candidate
        {
            public Door Door;
            public int Neighbor;
            public bool IsCorridor;
            public bool HasGeometry;
            public float OverlapMin;
            public float OverlapMax;
            public float Normal;
            public RectF Other;
            public float TargetNormal;
            public bool NeedsConnector;
            public bool NeedsWrap;
            public DoorSide TargetSide;
            public FloorPlan Plan;
        }

        public static void Apply(FloorPlan plan)
        {
            if (plan == null || plan.Rooms == null) return;

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                List<Candidate> all = BuildCandidates(plan, i);
                List<Candidate> selected = NormalizeExisting(room, all);
                AddWrapCandidates(plan, room.Rect, all, selected);

                // A generated room should retain a corridor connection whenever its
                // geometry offers one; room-to-room loops are additional egress.
                if (!HasCorridorDoor(selected))
                    AddBest(room.Rect, all, selected, true, false, 0, false, 0f, false);

                if (room.Rect.Area > 400f)
                    EnsureHallEnds(room.Rect, all, selected);

                int target = room.Rect.Area >= 100f ? 3 : 2;
                int guard = 0;
                while ((selected.Count < target || SideCount(selected) < 2) && guard++ < 8)
                {
                    bool needSide = SideCount(selected) < 2;
                    if (AddBest(room.Rect, all, selected, false, needSide, 0, false, 0f, false))
                        continue;
                    // A wide corridor can legally provide two openings on different
                    // sides, or two separated openings as a last connectivity resort.
                    if (!AddBest(room.Rect, all, selected, false, needSide, 0, false, 0f, true))
                        break;
                }

                StaggerOpposite(room.Rect, selected);
                MaterializeConnectors(plan, room.Rect, selected);
                Assign(ref room, selected);
                plan.Rooms[i] = room;
            }

            DissolveNoncompliantRooms(plan);
            RebuildLoopDoors(plan);
        }

        static void DissolveNoncompliantRooms(FloorPlan plan)
        {
            // A few clipped BSP leaves have no legal jamb on a second wall. Keeping one as a
            // dead-end room would fake compliance; retain its floor and openings as a real
            // walk-through vestibule instead. R2 can then treat it as circulation geometry.
            int guard = plan.Rooms.Count + 1;
            while (guard-- > 0)
            {
                int victim = -1;
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    if (MeetsPolicy(plan.Rooms[i])) continue;
                    victim = i;
                    break;
                }
                if (victim < 0) return;
                DissolveRoom(plan, victim);
            }
        }

        static bool MeetsPolicy(Room room)
        {
            List<Door> doors = new List<Door>();
            if (room.HasDoor) doors.Add(room.Door);
            if (room.ExtraDoors != null) doors.AddRange(room.ExtraDoors);
            int target = room.Rect.Area >= 100f ? 3 : 2;
            if (doors.Count < target) return false;
            int sides = 0;
            bool nearMin = false, nearMax = false;
            bool horizontal = room.Rect.W >= room.Rect.H;
            DoorSide endA = horizontal ? DoorSide.South : DoorSide.West;
            DoorSide endB = horizontal ? DoorSide.North : DoorSide.East;
            float endMin = horizontal ? room.Rect.X : room.Rect.Y;
            float endMax = horizontal ? room.Rect.XMax : room.Rect.YMax;
            for (int i = 0; i < doors.Count; i++)
            {
                Door a = doors[i];
                sides |= 1 << (int)a.Side;
                float min = SideMin(room.Rect, a.Side), max = SideMax(room.Rect, a.Side);
                float nearest = Mathf.Min(Tangent(a) - a.Width * 0.5f - min, max - Tangent(a) - a.Width * 0.5f);
                if (!((nearest >= 0.12f && nearest <= 0.53f) || (nearest >= 0.87f && nearest <= 1.23f))) return false;
                if (room.Rect.Area > 400f && (a.Side == endA || a.Side == endB))
                {
                    nearMin |= Mathf.Abs(Tangent(a) - endMin) <= 1.2f + a.Width * 0.5f + 0.04f;
                    nearMax |= Mathf.Abs(Tangent(a) - endMax) <= 1.2f + a.Width * 0.5f + 0.04f;
                }
                for (int j = i + 1; j < doors.Count; j++)
                    if (AreOpposite(a.Side, doors[j].Side) && Mathf.Abs(Tangent(a) - Tangent(doors[j])) < MinOppositeStagger - 0.02f)
                        return false;
            }
            int sideCount = 0;
            for (int side = 0; side < 4; side++) if ((sides & (1 << side)) != 0) sideCount++;
            return sideCount >= 2 && (room.Rect.Area <= 400f || (nearMin && nearMax));
        }

        static void DissolveRoom(FloorPlan plan, int index)
        {
            Room old = plan.Rooms[index];
            int corridor = plan.Corridors.Count;
            plan.Corridors.Add(new Corridor { Rect = old.Rect, Main = false });
            plan.Rooms.RemoveAt(index);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor) room.Door = RemapRoomDoor(room.Door, index, corridor);
                if (room.ExtraDoors != null)
                    for (int d = 0; d < room.ExtraDoors.Count; d++)
                        room.ExtraDoors[d] = RemapRoomDoor(room.ExtraDoors[d], index, corridor);
                if (room.HasDoor)
                {
                    room.CorridorIndex = room.Door.Corridor;
                    room.DoorToCorridor = room.Door.Corridor >= 0;
                }
                plan.Rooms[i] = room;
            }
            List<Door> oldDoors = new List<Door>();
            if (old.HasDoor) oldDoors.Add(old.Door);
            if (old.ExtraDoors != null) oldDoors.AddRange(old.ExtraDoors);
            for (int i = 0; i < oldDoors.Count; i++)
            {
                Door door = RemapRoomDoor(oldDoors[i], index, corridor);
                if (door.Corridor < 0) { door.Corridor = corridor; door.OtherRoom = -1; }
                plan.LoopDoors.Add(door);
            }
        }

        static Door RemapRoomDoor(Door door, int removed, int corridor)
        {
            if (door.OtherRoom == removed) { door.OtherRoom = -1; door.Corridor = corridor; }
            else if (door.OtherRoom > removed) door.OtherRoom--;
            return door;
        }

        static List<Candidate> NormalizeExisting(Room room, List<Candidate> all)
        {
            List<Door> original = new List<Door>();
            if (room.HasDoor && room.Door.Width > 0.01f) original.Add(room.Door);
            if (room.ExtraDoors != null)
            {
                for (int i = 0; i < room.ExtraDoors.Count; i++)
                    if (room.ExtraDoors[i].Width > 0.01f) original.Add(room.ExtraDoors[i]);
            }

            List<Candidate> selected = new List<Candidate>();
            for (int i = 0; i < original.Count; i++)
            {
                Door old = original[i];
                Candidate match = FindMatch(all, old);
                if (match == null) continue;
                Candidate placed = new Candidate
                {
                    Door = old,
                    Neighbor = match.Neighbor,
                    IsCorridor = match.IsCorridor,
                    HasGeometry = true,
                    OverlapMin = match.OverlapMin,
                    OverlapMax = match.OverlapMax,
                    Normal = match.Normal,
                    Other = match.Other,
                    TargetNormal = match.TargetNormal,
                    NeedsConnector = match.NeedsConnector,
                    NeedsWrap = match.NeedsWrap,
                    TargetSide = match.TargetSide,
                    Plan = match.Plan
                };
                placed.Door.Side = match.Door.Side;
                placed.Door.Width = match.Door.Width;
                placed.Door.Corridor = match.Door.Corridor;
                placed.Door.OtherRoom = match.Door.OtherRoom;
                Door positioned;
                if (Place(room.Rect, placed, selected, Tangent(old), out positioned))
                {
                    placed.Door = positioned;
                    selected.Add(placed);
                }
            }

            // If repair produced a door whose neighbour is no longer representable,
            // retain it only when there is no geometric candidate at all. This avoids
            // silently deleting the sole egress from an otherwise valid room.
            if (selected.Count == 0 && original.Count > 0 && all.Count == 0)
            {
                selected.Add(new Candidate
                {
                    Door = original[0],
                    Neighbor = original[0].Corridor >= 0 ? original[0].Corridor : original[0].OtherRoom,
                    IsCorridor = original[0].Corridor >= 0,
                    HasGeometry = false
                });
            }
            return selected;
        }

        static Candidate FindMatch(List<Candidate> all, Door old)
        {
            bool corridor = old.Corridor >= 0;
            int neighbor = corridor ? old.Corridor : old.OtherRoom;
            Candidate fallback = null;
            float best = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                Candidate c = all[i];
                if (c.IsCorridor != corridor || c.Neighbor != neighbor) continue;
                if (c.Door.Side == old.Side) return c;
                float distance = Vector2.Distance(c.Door.Center, old.Center);
                if (distance < best) { best = distance; fallback = c; }
            }
            return fallback;
        }

        static List<Candidate> BuildCandidates(FloorPlan plan, int roomIndex)
        {
            List<Candidate> result = new List<Candidate>();
            RectF room = plan.Rooms[roomIndex].Rect;
            for (int i = 0; i < plan.Corridors.Count; i++)
                AddCandidates(result, room, plan.Corridors[i].Rect, i, true, plan.Spec.WallInset);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (i == roomIndex) continue;
                AddCandidates(result, room, plan.Rooms[i].Rect, i, false, plan.Spec.WallInset);
            }
            for (int i = 0; i < result.Count; i++) result[i].Plan = plan;
            return result;
        }

        static void AddCandidates(List<Candidate> result, RectF room, RectF other, int neighbor, bool corridor, float inset)
        {
            AddCandidate(result, room, other, neighbor, corridor, DoorSide.East, inset);
            AddCandidate(result, room, other, neighbor, corridor, DoorSide.West, inset);
            AddCandidate(result, room, other, neighbor, corridor, DoorSide.North, inset);
            AddCandidate(result, room, other, neighbor, corridor, DoorSide.South, inset);
        }

        static void AddCandidate(List<Candidate> result, RectF room, RectF other,
            int neighbor, bool corridor, DoorSide side, float inset)
        {
            float min, max, normal, gap;
            if (side == DoorSide.East || side == DoorSide.West)
            {
                min = Mathf.Max(room.Y, other.Y);
                max = Mathf.Min(room.YMax, other.YMax);
                if (side == DoorSide.East)
                {
                    gap = other.X - room.XMax;
                    normal = (room.XMax + other.X) * 0.5f;
                }
                else
                {
                    gap = room.X - other.XMax;
                    normal = (room.X + other.XMax) * 0.5f;
                }
            }
            else
            {
                min = Mathf.Max(room.X, other.X);
                max = Mathf.Min(room.XMax, other.XMax);
                if (side == DoorSide.North)
                {
                    gap = other.Y - room.YMax;
                    normal = (room.YMax + other.Y) * 0.5f;
                }
                else
                {
                    gap = room.Y - other.YMax;
                    normal = (room.Y + other.YMax) * 0.5f;
                }
            }

            float overlap = max - min;
            bool connector = gap > MaxGap;
            if (gap < -0.05f || gap > MaxConnectorGap || overlap < MinOverlap) return;
            float width = Mathf.Min(PreferredWidth, overlap - 0.04f);
            if (width < 0.85f) return;

            float targetNormal = side == DoorSide.East ? other.X
                : side == DoorSide.West ? other.XMax
                : side == DoorSide.North ? other.Y : other.YMax;
            if (connector)
            {
                normal = side == DoorSide.East ? room.XMax + inset
                    : side == DoorSide.West ? room.X - inset
                    : side == DoorSide.North ? room.YMax + inset : room.Y - inset;
                // A room's far jamb belongs to its inflated wall, not the playable
                // rectangle. This keeps the throat outside both room volumes.
                if (!corridor)
                    targetNormal = side == DoorSide.East ? other.X - inset
                        : side == DoorSide.West ? other.XMax + inset
                        : side == DoorSide.North ? other.Y - inset : other.YMax + inset;
            }

            Candidate c = new Candidate
            {
                Neighbor = neighbor,
                IsCorridor = corridor,
                HasGeometry = true,
                OverlapMin = min,
                OverlapMax = max,
                Normal = normal,
                Other = other,
                TargetNormal = targetNormal,
                NeedsConnector = connector,
                TargetSide = OppositeSide(side),
                Door = new Door
                {
                    Side = side,
                    Width = width,
                    Corridor = corridor ? neighbor : -1,
                    OtherRoom = corridor ? -1 : neighbor
                }
            };
            result.Add(c);
        }

        static bool AddBest(RectF room, List<Candidate> all, List<Candidate> selected,
            bool corridorOnly, bool requireNewSide, int sideMask,
            bool hasDesired, float desired, bool allowSameConnection)
        {
            Candidate bestCandidate = null;
            Door bestDoor = default(Door);
            float bestScore = float.MinValue;

            for (int i = 0; i < all.Count; i++)
            {
                Candidate c = all[i];
                if (corridorOnly && !c.IsCorridor) continue;
                if (sideMask != 0 && (sideMask & (1 << (int)c.Door.Side)) == 0) continue;
                bool newSide = !HasSide(selected, c.Door.Side);
                if (requireNewSide && !newSide) continue;
                if (!allowSameConnection && HasConnection(selected, c)) continue;

                Door positioned;
                float wanted = hasDesired ? desired : PreferredTangent(room, c, selected);
                if (!Place(room, c, selected, wanted, out positioned)) continue;

                float opposite = OppositeDistance(positioned, selected);
                float score = (newSide ? 500f : 0f) + (c.IsCorridor ? 30f : 0f)
                    + (c.OverlapMax - c.OverlapMin) * 0.2f
                    + (opposite >= MinOppositeStagger - 0.01f ? 200f : -600f)
                    - Mathf.Abs(Tangent(positioned) - wanted) * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = c;
                    bestDoor = positioned;
                }
            }

            if (bestCandidate == null) return false;
            Candidate copy = new Candidate
            {
                Door = bestDoor,
                Neighbor = bestCandidate.Neighbor,
                IsCorridor = bestCandidate.IsCorridor,
                HasGeometry = true,
                OverlapMin = bestCandidate.OverlapMin,
                OverlapMax = bestCandidate.OverlapMax,
                Normal = bestCandidate.Normal,
                Other = bestCandidate.Other,
                TargetNormal = bestCandidate.TargetNormal,
                NeedsConnector = bestCandidate.NeedsConnector,
                NeedsWrap = bestCandidate.NeedsWrap,
                TargetSide = bestCandidate.TargetSide,
                Plan = bestCandidate.Plan
            };
            selected.Add(copy);
            return true;
        }

        static float PreferredTangent(RectF room, Candidate c, List<Candidate> selected)
        {
            float min = SideMin(room, c.Door.Side);
            float max = SideMax(room, c.Door.Side);
            float mid = (min + max) * 0.5f;
            float best = mid;
            float bestDistance = -1f;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].Door.Side != c.Door.Side) continue;
                float t = Tangent(selected[i].Door);
                if (Mathf.Abs(t - mid) > bestDistance) { bestDistance = Mathf.Abs(t - mid); best = t; }
            }
            if (bestDistance >= 0f) return Mathf.Abs(best - min) < Mathf.Abs(best - max) ? max : min;
            return mid;
        }

        static void EnsureHallEnds(RectF room, List<Candidate> all, List<Candidate> selected)
        {
            bool horizontal = room.W >= room.H;
            int mask = horizontal
                ? (1 << (int)DoorSide.South) | (1 << (int)DoorSide.North)
                : (1 << (int)DoorSide.West) | (1 << (int)DoorSide.East);
            float min = horizontal ? room.X : room.Y;
            float max = horizontal ? room.XMax : room.YMax;
            if (!HasEndpoint(selected, mask, min))
                AddBest(room, all, selected, false, false, mask, true, min, true);
            if (!HasEndpoint(selected, mask, max))
                AddBest(room, all, selected, false, false, mask, true, max, true);
        }

        static bool HasEndpoint(List<Candidate> selected, int mask, float end)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                Door d = selected[i].Door;
                if ((mask & (1 << (int)d.Side)) == 0) continue;
                if (Mathf.Abs(Tangent(d) - end) <= 1.2f + d.Width * 0.5f + 0.03f) return true;
            }
            return false;
        }

        static bool Place(RectF room, Candidate c, List<Candidate> selected, float desired, out Door result)
        {
            result = c.Door;
            if (!c.HasGeometry) return false;
            float bestScore = float.MinValue;
            float bestTangent = 0f;
            bool found = false;
            float min = SideMin(room, c.Door.Side);
            float max = SideMax(room, c.Door.Side);
            float half = c.Door.Width * 0.5f;

            ConsiderCenterInterval(room, c, selected, desired, min + 0.15f + half, min + 0.50f + half,
                ref found, ref bestScore, ref bestTangent);
            ConsiderCenterInterval(room, c, selected, desired, min + 0.90f + half, min + 1.20f + half,
                ref found, ref bestScore, ref bestTangent);
            ConsiderCenterInterval(room, c, selected, desired, max - 1.20f - half, max - 0.90f - half,
                ref found, ref bestScore, ref bestTangent);
            ConsiderCenterInterval(room, c, selected, desired, max - 0.50f - half, max - 0.15f - half,
                ref found, ref bestScore, ref bestTangent);
            if (!found) return false;

            result.Center = c.Door.Side == DoorSide.East || c.Door.Side == DoorSide.West
                ? new Vector2(c.Normal, bestTangent)
                : new Vector2(bestTangent, c.Normal);
            return true;
        }

        static void ConsiderCenterInterval(RectF room, Candidate c, List<Candidate> selected, float desired,
            float centerMin, float centerMax, ref bool found, ref float bestScore, ref float bestTangent)
        {
            float half = c.Door.Width * 0.5f;
            float lo = Mathf.Max(centerMin, c.OverlapMin + half);
            float hi = Mathf.Min(centerMax, c.OverlapMax - half);
            if (hi < lo - 0.001f) return;
            EvaluateTangent(room, c, selected, desired, lo, ref found, ref bestScore, ref bestTangent);
            EvaluateTangent(room, c, selected, desired, hi, ref found, ref bestScore, ref bestTangent);
            EvaluateTangent(room, c, selected, desired, (lo + hi) * 0.5f, ref found, ref bestScore, ref bestTangent);
            EvaluateTangent(room, c, selected, desired, Mathf.Clamp(desired, lo, hi), ref found, ref bestScore, ref bestTangent);
        }

        static void EvaluateTangent(RectF room, Candidate c, List<Candidate> selected, float desired, float tangent,
            ref bool found, ref float bestScore, ref float bestTangent)
        {
            float sideMin = SideMin(room, c.Door.Side);
            float sideMax = SideMax(room, c.Door.Side);
            float half = c.Door.Width * 0.5f;
            float nearest = Mathf.Min(tangent - half - sideMin, sideMax - tangent - half);
            if (!((nearest >= 0.12f && nearest <= 0.53f) || (nearest >= 0.87f && nearest <= 1.23f))) return;
            Door probe = c.Door;
            probe.Center = c.Door.Side == DoorSide.East || c.Door.Side == DoorSide.West
                ? new Vector2(c.Normal, tangent)
                : new Vector2(tangent, c.Normal);
            float opposite = OppositeDistance(probe, selected);
            float same = SameSideDistance(probe, selected);
            if (opposite < MinOppositeStagger - 0.005f) return;
            if (same < c.Door.Width + 0.20f) return;
            if (c.NeedsConnector && !ConnectorClear(room, c, probe)) return;
            float score = Mathf.Min(opposite, MinOppositeStagger) * 100f
                + Mathf.Min(same, c.Door.Width + 0.35f) * 8f
                - Mathf.Abs(tangent - desired) * 0.02f;
            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTangent = tangent;
            }
        }

        static void AddWrapCandidates(FloorPlan plan, RectF room, List<Candidate> all, List<Candidate> selected)
        {
            for (int i = 0; i < all.Count; i++) all[i].Plan = plan;
            int original = selected.Count;
            for (int i = 0; i < original; i++)
            {
                Candidate existing = selected[i];
                if (!existing.IsCorridor || existing.NeedsConnector || existing.Neighbor < 0 || existing.Neighbor >= plan.Corridors.Count) continue;
                RectF corridor = plan.Corridors[existing.Neighbor].Rect;
                if (existing.Door.Side == DoorSide.North || existing.Door.Side == DoorSide.South)
                {
                    float target = existing.Door.Side == DoorSide.North ? corridor.Y : corridor.YMax;
                    DoorSide targetSide = OppositeSide(existing.Door.Side);
                    AddWrapCandidate(all, room, corridor, existing.Neighbor, DoorSide.West, room.X - plan.Spec.WallInset, target, targetSide);
                    AddWrapCandidate(all, room, corridor, existing.Neighbor, DoorSide.East, room.XMax + plan.Spec.WallInset, target, targetSide);
                }
                else
                {
                    AddWrapCandidate(all, room, corridor, existing.Neighbor, DoorSide.South, room.Y - plan.Spec.WallInset, corridor.Y, DoorSide.South);
                    AddWrapCandidate(all, room, corridor, existing.Neighbor, DoorSide.North, room.YMax + plan.Spec.WallInset, corridor.YMax, DoorSide.North);
                }
            }
            for (int i = 0; i < all.Count; i++) all[i].Plan = plan;
        }

        static void AddWrapCandidate(List<Candidate> all, RectF room, RectF corridor, int neighbor,
            DoorSide side, float normal, float targetNormal, DoorSide targetSide)
        {
            all.Add(new Candidate
            {
                Neighbor = neighbor,
                IsCorridor = true,
                HasGeometry = true,
                OverlapMin = SideMin(room, side),
                OverlapMax = SideMax(room, side),
                Normal = normal,
                Other = corridor,
                TargetNormal = targetNormal,
                NeedsConnector = true,
                NeedsWrap = true,
                TargetSide = targetSide,
                Door = new Door { Side = side, Width = PreferredWidth, Corridor = neighbor, OtherRoom = -1 }
            });
        }

        static float WrapTargetTangent(Candidate c, Door door)
        {
            float half = (door.Width + 0.30f) * 0.5f;
            if (c.Door.Side == DoorSide.West) return c.Normal - half;
            if (c.Door.Side == DoorSide.East) return c.Normal + half;
            return Mathf.Clamp(Tangent(door), c.Other.X + door.Width * 0.5f, c.Other.XMax - door.Width * 0.5f);
        }

        static RectF ConnectorRect(RectF room, Candidate c, Door door)
        {
            float half = (door.Width + 0.30f) * 0.5f;
            float t = Tangent(door);
            if (c.NeedsWrap)
            {
                if (c.Door.Side == DoorSide.West || c.Door.Side == DoorSide.East)
                {
                    float x0 = c.Door.Side == DoorSide.West ? c.Normal - half * 2f : c.Normal;
                    float wrapY0 = Mathf.Min(c.TargetNormal, t - half);
                    float wrapY1 = Mathf.Max(c.TargetNormal, t + half);
                    return new RectF(x0, wrapY0, half * 2f, wrapY1 - wrapY0);
                }
                float target = WrapTargetTangent(c, door);
                float wy0 = c.Door.Side == DoorSide.South ? c.Normal - half * 2f : c.Normal;
                float wx0 = Mathf.Min(t, target) - half;
                float wx1 = Mathf.Max(t, target) + half;
                return new RectF(wx0, wy0, wx1 - wx0, half * 2f);
            }
            if (c.Door.Side == DoorSide.East || c.Door.Side == DoorSide.West)
            {
                float x0 = c.Door.Side == DoorSide.East ? c.Normal : c.TargetNormal;
                float x1 = c.Door.Side == DoorSide.East ? c.TargetNormal : c.Normal;
                return new RectF(x0, t - half, x1 - x0, half * 2f);
            }
            float y0 = c.Door.Side == DoorSide.North ? c.Normal : c.TargetNormal;
            float y1 = c.Door.Side == DoorSide.North ? c.TargetNormal : c.Normal;
            return new RectF(t - half, y0, half * 2f, y1 - y0);
        }

        static bool ConnectorClear(RectF room, Candidate c, Door door)
        {
            RectF throat = ConnectorRect(room, c, door);
            if (throat.W < 0.35f || throat.H < 0.35f) return false;
            FloorPlan plan = c.Plan;
            if (plan == null) return false;
            if (throat.X < 0.05f || throat.Y < 0.05f || throat.XMax > plan.Bounds.W - 0.05f || throat.YMax > plan.Bounds.H - 0.05f) return false;
            if (c.NeedsWrap)
            {
                float target = WrapTargetTangent(c, door);
                float half = door.Width * 0.5f;
                if (target - half < c.Other.X || target + half > c.Other.XMax) return false;
            }
            if (!c.IsCorridor && !FarRoomDoorClear(plan, c, door)) return false;
            for (int i = 0; i < plan.Rooms.Count; i++)
                if (plan.Rooms[i].Rect.Overlaps(throat)) return false;
            if (plan.Core.Overlaps(throat))
            {
                bool northLobby = c.NeedsWrap
                    && throat.X >= plan.Core.X && throat.XMax <= plan.Core.XMax
                    && throat.Y >= plan.Core.YMax - 1.6f && throat.YMax <= plan.Core.YMax + 0.01f;
                if (!northLobby) return false;
            }
            for (int i = 0; i < plan.Stairs.Count; i++)
                if (plan.Stairs[i].Overlaps(throat)) return false;
            return true;
        }


        static bool FarRoomDoorClear(FloorPlan plan, Candidate c, Door door)
        {
            if (c.Neighbor < 0 || c.Neighbor >= plan.Rooms.Count) return false;
            Room other = plan.Rooms[c.Neighbor];
            float tangent = Tangent(door);
            float half = door.Width * 0.5f;
            float min = SideMin(other.Rect, c.TargetSide);
            float max = SideMax(other.Rect, c.TargetSide);
            float nearest = Mathf.Min(tangent - half - min, max - tangent - half);
            if (!((nearest >= 0.12f && nearest <= 0.53f) || (nearest >= 0.87f && nearest <= 1.23f))) return false;

            List<Door> existing = new List<Door>();
            if (other.HasDoor) existing.Add(other.Door);
            if (other.ExtraDoors != null) existing.AddRange(other.ExtraDoors);
            for (int i = 0; i < existing.Count; i++)
            {
                Door d = existing[i];
                float distance = Mathf.Abs(tangent - Tangent(d));
                if (AreOpposite(c.TargetSide, d.Side) && distance < MinOppositeStagger - 0.005f) return false;
                if (c.TargetSide == d.Side && distance < door.Width + 0.20f) return false;
            }
            return true;
        }

        static void AddConnectorDoor(FloorPlan plan, int roomIndex, Door door, int connector)
        {
            Room other = plan.Rooms[roomIndex];
            door.Corridor = connector;
            door.OtherRoom = -1;
            if (!other.HasDoor)
            {
                other.Door = door;
                other.HasDoor = true;
                other.CorridorIndex = connector;
                other.DoorToCorridor = true;
                other.ExtraDoors = new List<Door>();
            }
            else
            {
                if (other.ExtraDoors == null) other.ExtraDoors = new List<Door>();
                other.ExtraDoors.Add(door);
            }
            plan.Rooms[roomIndex] = other;
        }

        static void MaterializeConnectors(FloorPlan plan, RectF room, List<Candidate> selected)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                Candidate c = selected[i];
                if (!c.NeedsConnector) continue;
                RectF throat = ConnectorRect(room, c, c.Door);
                int connector = plan.Corridors.Count;
                plan.Corridors.Add(new Corridor { Rect = throat, Main = false });

                Door far = c.Door;
                far.Side = c.TargetSide;
                float farTangent = c.NeedsWrap ? WrapTargetTangent(c, c.Door) : Tangent(c.Door);
                far.Center = far.Side == DoorSide.East || far.Side == DoorSide.West
                    ? new Vector2(c.TargetNormal, farTangent)
                    : new Vector2(farTangent, c.TargetNormal);
                far.OtherRoom = -1;
                if (!c.IsCorridor && !c.NeedsWrap)
                {
                    AddConnectorDoor(plan, c.Neighbor, far, connector);
                }
                else
                {
                    far.Corridor = c.Neighbor;
                    plan.LoopDoors.Add(far);
                }

                c.Door.Corridor = connector;
                c.Door.OtherRoom = -1;
                c.IsCorridor = true;
                c.Neighbor = connector;
                c.NeedsConnector = false;
            }
        }

        static void StaggerOpposite(RectF room, List<Candidate> selected)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    Candidate c = selected[i];
                    if (!c.HasGeometry) continue;
                    List<Candidate> others = new List<Candidate>(selected);
                    others.RemoveAt(i);
                    Door positioned;
                    if (Place(room, c, others, Tangent(c.Door), out positioned)) c.Door = positioned;
                }
            }
        }

        static float OppositeDistance(Door door, List<Candidate> selected)
        {
            float best = MinOppositeStagger;
            for (int i = 0; i < selected.Count; i++)
            {
                Door other = selected[i].Door;
                if (!AreOpposite(door.Side, other.Side)) continue;
                float distance = Mathf.Abs(Tangent(door) - Tangent(other));
                if (distance < best) best = distance;
            }
            return best;
        }

        static float SameSideDistance(Door door, List<Candidate> selected)
        {
            float best = door.Width + 0.35f;
            for (int i = 0; i < selected.Count; i++)
            {
                Door other = selected[i].Door;
                if (door.Side != other.Side) continue;
                float distance = Mathf.Abs(Tangent(door) - Tangent(other));
                if (distance < best) best = distance;
            }
            return best;
        }

        static bool HasConnection(List<Candidate> selected, Candidate c)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].IsCorridor == c.IsCorridor && selected[i].Neighbor == c.Neighbor
                    && selected[i].Door.Side == c.Door.Side) return true;
            }
            return false;
        }

        static bool HasCorridorDoor(List<Candidate> selected)
        {
            for (int i = 0; i < selected.Count; i++) if (selected[i].IsCorridor) return true;
            return false;
        }

        static bool HasSide(List<Candidate> selected, DoorSide side)
        {
            for (int i = 0; i < selected.Count; i++) if (selected[i].Door.Side == side) return true;
            return false;
        }

        static int SideCount(List<Candidate> selected)
        {
            int mask = 0;
            for (int i = 0; i < selected.Count; i++) mask |= 1 << (int)selected[i].Door.Side;
            int count = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        static void Assign(ref Room room, List<Candidate> selected)
        {
            room.ExtraDoors = new List<Door>();
            if (selected.Count == 0)
            {
                room.HasDoor = false;
                room.CorridorIndex = -1;
                room.DoorToCorridor = false;
                room.Door = default(Door);
                return;
            }

            int primary = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].IsCorridor) { primary = i; break; }
            }
            room.Door = selected[primary].Door;
            room.HasDoor = true;
            room.CorridorIndex = room.Door.Corridor;
            room.DoorToCorridor = room.Door.Corridor >= 0;
            for (int i = 0; i < selected.Count; i++)
                if (i != primary) room.ExtraDoors.Add(selected[i].Door);
        }

        static void RebuildLoopDoors(FloorPlan plan)
        {
            List<Door> connectorEnds = plan.LoopDoors.FindAll(d => d.Corridor >= 0 && d.OtherRoom < 0);
            plan.LoopDoors.Clear();
            plan.LoopDoors.AddRange(connectorEnds);
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                Room room = plan.Rooms[r];
                List<Door> doors = new List<Door>();
                if (room.HasDoor) doors.Add(room.Door);
                if (room.ExtraDoors != null) doors.AddRange(room.ExtraDoors);
                for (int i = 0; i < doors.Count; i++)
                {
                    Door d = doors[i];
                    if (d.Corridor >= 0 || d.OtherRoom < 0) continue;
                    bool duplicate = false;
                    for (int j = 0; j < plan.LoopDoors.Count; j++)
                    {
                        if (Vector2.Distance(plan.LoopDoors[j].Center, d.Center) < 0.25f)
                        { duplicate = true; break; }
                    }
                    if (!duplicate) plan.LoopDoors.Add(d);
                }
            }
        }

        public static float Tangent(Door door)
        {
            return door.Side == DoorSide.East || door.Side == DoorSide.West ? door.Center.y : door.Center.x;
        }

        static DoorSide OppositeSide(DoorSide side)
        {
            return side == DoorSide.North ? DoorSide.South
                : side == DoorSide.South ? DoorSide.North
                : side == DoorSide.East ? DoorSide.West : DoorSide.East;
        }

        public static bool AreOpposite(DoorSide a, DoorSide b)
        {
            return (a == DoorSide.North && b == DoorSide.South)
                || (a == DoorSide.South && b == DoorSide.North)
                || (a == DoorSide.East && b == DoorSide.West)
                || (a == DoorSide.West && b == DoorSide.East);
        }

        static float SideMin(RectF room, DoorSide side)
        {
            return side == DoorSide.East || side == DoorSide.West ? room.Y : room.X;
        }

        static float SideMax(RectF room, DoorSide side)
        {
            return side == DoorSide.East || side == DoorSide.West ? room.YMax : room.XMax;
        }
    }
}
