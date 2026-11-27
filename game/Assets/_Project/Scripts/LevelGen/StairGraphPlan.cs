using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    public enum StairPortalDirection { Up, Down }

    /// <summary>A door on one end of a single-storey stair link.</summary>
    public struct StairDoor
    {
        public DoorSide Side;
        public Vector2 Center;
        public float Width;

        public DoorSide Direction { get { return Side; } }

        public StairDoor(DoorSide side, Vector2 center, float width)
        {
            Side = side;
            Center = center;
            Width = width;
        }
    }

    /// <summary>Two independent floor records share XY, but retain separate doors and portals.</summary>
    public class StairLink
    {
        public string Id;
        public int LowerFloor;
        public int UpperFloor;
        public RectF LowerRect;
        public RectF UpperRect;
        public List<StairDoor> LowerDoors = new List<StairDoor>();
        public List<StairDoor> UpperDoors = new List<StairDoor>();

        public Vector2 Center { get { return LowerRect.Center; } }
    }

    /// <summary>A real stair endpoint, or an explicit sealed stub beyond the fixed four-floor building.</summary>
    public class StairPortal
    {
        public string Id;
        public int FloorIndex;
        public string LinkId;
        public RectF Rect;
        public StairPortalDirection Direction;
        public bool IsBoundary;
        public bool IsSealed;

        public Vector2 Center { get { return Rect.Center; } }
    }

    /// <summary>
    /// Deterministic pure-data vertical circulation for floors 0..3. Stair positions rotate
    /// between adjacent floor pairs; the single elevator is traffic only, never an extraction.
    /// </summary>
    public class StairGraphPlan
    {
        public const int FloorCount = 4;
        public const float MinStairCenterDistance = 30f;
        public const float MaxStairCenterDistance = 45f;

        public int Seed;
        public float Width;
        public float Height;
        public List<StairLink> Links = new List<StairLink>();
        public List<StairPortal> Portals = new List<StairPortal>();
        public RectF Elevator;

        public RectF Bounds { get { return new RectF(0f, 0f, Width, Height); } }
        public Vector2 FloorCenter { get { return new Vector2(Width * 0.5f, Height * 0.5f); } }

        public static StairGraphPlan Generate(int seed, float width, float height)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width < 70f)
                throw new ArgumentOutOfRangeException("width", "Stair graph requires a finite width of at least 70m.");
            if (float.IsNaN(height) || float.IsInfinity(height) || height < 60f)
                throw new ArgumentOutOfRangeException("height", "Stair graph requires a finite height of at least 60m.");

            StairGraphPlan plan = new StairGraphPlan();
            plan.Seed = seed;
            plan.Width = width;
            plan.Height = height;

            float stairW = Mathf.Min(8f, width * 0.08f);
            float stairH = Mathf.Min(14f, height * 0.12f);
            float elevatorW = Mathf.Min(12f, width * 0.10f);
            float elevatorH = Mathf.Min(10f, height * 0.10f);
            Vector2 floorCenter = plan.FloorCenter;
            plan.Elevator = RectAt(floorCenter, elevatorW, elevatorH);

            // Opposite quadrants give each pair two genuinely independent stairs. Rotating
            // the quadrant pattern on every pair prevents the old aligned west/east shafts.
            float baseX = Mathf.Min(32f, width * 0.28f);
            float baseY = Mathf.Min(22f, height * 0.25f);
            System.Random rng = new System.Random(seed);
            for (int lower = 0; lower < FloorCount - 1; lower++)
            {
                int upper = lower + 1;
                for (int stair = 0; stair < 2; stair++)
                {
                    int quadrant = (lower + stair * 2) % 4;
                    float signX = quadrant == 0 || quadrant == 3 ? -1f : 1f;
                    float signY = quadrant == 0 || quadrant == 1 ? -1f : 1f;
                    float jitterX = NextSigned(rng, 4f);
                    float jitterY = NextSigned(rng, 4f);
                    Vector2 offset = new Vector2(signX * (baseX + jitterX), signY * (baseY + jitterY));
                    if (offset.magnitude > 44f) offset = offset.normalized * 44f;

                    Vector2 center = floorCenter + offset;
                    center.x = Mathf.Clamp(center.x, 2f + stairW * 0.5f, width - 2f - stairW * 0.5f);
                    center.y = Mathf.Clamp(center.y, 2f + stairH * 0.5f, height - 2f - stairH * 0.5f);
                    RectF rect = RectAt(center, stairW, stairH);

                    StairLink link = new StairLink();
                    link.Id = "stair-" + lower + "-" + upper + "-" + stair;
                    link.LowerFloor = lower;
                    link.UpperFloor = upper;
                    link.LowerRect = rect;
                    link.UpperRect = rect;
                    link.LowerDoors = CreateDoors(rect, floorCenter);
                    link.UpperDoors = CreateDoors(rect, floorCenter);
                    plan.Links.Add(link);

                    plan.Portals.Add(new StairPortal
                    {
                        Id = "portal-" + link.Id + "-lower",
                        FloorIndex = lower,
                        LinkId = link.Id,
                        Rect = link.LowerRect,
                        Direction = StairPortalDirection.Up,
                        IsBoundary = false,
                        IsSealed = false,
                    });
                    plan.Portals.Add(new StairPortal
                    {
                        Id = "portal-" + link.Id + "-upper",
                        FloorIndex = upper,
                        LinkId = link.Id,
                        Rect = link.UpperRect,
                        Direction = StairPortalDirection.Down,
                        IsBoundary = false,
                        IsSealed = false,
                    });
                }
            }

            AddBoundaryStubs(plan, 0, StairPortalDirection.Down, stairW, stairH);
            AddBoundaryStubs(plan, FloorCount - 1, StairPortalDirection.Up, stairW, stairH);
            return plan;
        }

        public List<StairLink> LinksBetween(int lowerFloor, int upperFloor)
        {
            List<StairLink> result = new List<StairLink>();
            for (int i = 0; i < Links.Count; i++)
            {
                StairLink link = Links[i];
                if (link.LowerFloor == lowerFloor && link.UpperFloor == upperFloor) result.Add(link);
            }
            return result;
        }

        public StairLink FindLink(string id)
        {
            for (int i = 0; i < Links.Count; i++) if (Links[i].Id == id) return Links[i];
            return null;
        }

        public StairPortal FindPortal(string id)
        {
            for (int i = 0; i < Portals.Count; i++) if (Portals[i].Id == id) return Portals[i];
            return null;
        }

        public List<StairPortal> PortalsOnFloor(int floorIndex)
        {
            List<StairPortal> result = new List<StairPortal>();
            for (int i = 0; i < Portals.Count; i++)
                if (Portals[i].FloorIndex == floorIndex) result.Add(Portals[i]);
            return result;
        }

        public List<StairPortal> RealPortalsOnFloor(int floorIndex)
        {
            List<StairPortal> result = new List<StairPortal>();
            for (int i = 0; i < Portals.Count; i++)
            {
                StairPortal portal = Portals[i];
                if (portal.FloorIndex == floorIndex && !portal.IsBoundary && !portal.IsSealed) result.Add(portal);
            }
            return result;
        }

        public List<StairPortal> PortalsForLink(string linkId)
        {
            List<StairPortal> result = new List<StairPortal>();
            for (int i = 0; i < Portals.Count; i++) if (Portals[i].LinkId == linkId) result.Add(Portals[i]);
            return result;
        }

        public RectF ElevatorForFloor(int floorIndex)
        {
            ValidateFloor(floorIndex);
            return Elevator;
        }

        /// <summary>Real stair rectangles on a floor: two on boundary floors, four on middle floors.</summary>
        public List<RectF> StairsOnFloor(int floorIndex)
        {
            List<RectF> result = new List<RectF>();
            List<StairPortal> portals = RealPortalsOnFloor(floorIndex);
            for (int i = 0; i < portals.Count; i++) result.Add(portals[i].Rect);
            return result;
        }

        /// <summary>
        /// Compatibility projection for the existing single-floor model. It changes only
        /// FloorPlan.Core/Stairs; in particular it never creates an elevator extraction.
        /// </summary>
        public void ApplyTo(FloorPlan plan, int floorIndex)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            plan.Core = ElevatorForFloor(floorIndex);
            if (plan.Stairs == null) plan.Stairs = new List<RectF>();
            else plan.Stairs.Clear();
            List<RectF> stairs = StairsOnFloor(floorIndex);
            for (int i = 0; i < stairs.Count; i++) plan.Stairs.Add(stairs[i]);
        }

        public string Fingerprint()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Seed).Append(';');
            AppendNumber(sb, Width);
            AppendNumber(sb, Height);
            AppendRect(sb, Elevator);
            for (int i = 0; i < Links.Count; i++)
            {
                StairLink link = Links[i];
                sb.Append(link.Id).Append(':').Append(link.LowerFloor).Append(':').Append(link.UpperFloor).Append(':');
                AppendRect(sb, link.LowerRect);
                AppendRect(sb, link.UpperRect);
                AppendDoors(sb, link.LowerDoors);
                AppendDoors(sb, link.UpperDoors);
            }
            for (int i = 0; i < Portals.Count; i++)
            {
                StairPortal portal = Portals[i];
                sb.Append(portal.Id).Append(':').Append(portal.FloorIndex).Append(':')
                  .Append(portal.LinkId).Append(':').Append(portal.Direction).Append(':')
                  .Append(portal.IsBoundary).Append(':').Append(portal.IsSealed).Append(':');
                AppendRect(sb, portal.Rect);
            }
            return sb.ToString();
        }

        static float NextSigned(System.Random rng, float amplitude)
        {
            return ((float)rng.NextDouble() * 2f - 1f) * amplitude;
        }

        static RectF RectAt(Vector2 center, float width, float height)
        {
            return new RectF(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        static List<StairDoor> CreateDoors(RectF rect, Vector2 floorCenter)
        {
            float width = Mathf.Min(2f, rect.W * 0.5f);
            DoorSide vertical = rect.Center.y < floorCenter.y ? DoorSide.North : DoorSide.South;
            DoorSide horizontal = rect.Center.x < floorCenter.x ? DoorSide.East : DoorSide.West;
            Vector2 verticalCenter = new Vector2(rect.Center.x, vertical == DoorSide.North ? rect.YMax : rect.Y);
            Vector2 horizontalCenter = new Vector2(horizontal == DoorSide.East ? rect.XMax : rect.X, rect.Center.y);
            List<StairDoor> doors = new List<StairDoor>();
            doors.Add(new StairDoor(vertical, verticalCenter, width));
            doors.Add(new StairDoor(horizontal, horizontalCenter, width));
            return doors;
        }

        static void AddBoundaryStubs(StairGraphPlan plan, int floorIndex, StairPortalDirection direction,
            float stairW, float stairH)
        {
            const float margin = 2f;
            float y = direction == StairPortalDirection.Down ? margin : plan.Height - margin - stairH;
            string directionName = direction == StairPortalDirection.Down ? "down" : "up";
            for (int i = 0; i < 2; i++)
            {
                float x = plan.Width * (i == 0 ? 0.25f : 0.75f) - stairW * 0.5f;
                x = Mathf.Clamp(x, margin, plan.Width - margin - stairW);
                plan.Portals.Add(new StairPortal
                {
                    Id = "boundary-" + floorIndex + "-" + directionName + "-" + i,
                    FloorIndex = floorIndex,
                    LinkId = string.Empty,
                    Rect = new RectF(x, y, stairW, stairH),
                    Direction = direction,
                    IsBoundary = true,
                    IsSealed = true,
                });
            }
        }

        static void ValidateFloor(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= FloorCount)
                throw new ArgumentOutOfRangeException("floorIndex", "Floor index must be in 0..3.");
        }

        static void AppendDoors(StringBuilder sb, List<StairDoor> doors)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                StairDoor door = doors[i];
                sb.Append(door.Side).Append(':');
                AppendNumber(sb, door.Center.x);
                AppendNumber(sb, door.Center.y);
                AppendNumber(sb, door.Width);
            }
        }

        static void AppendRect(StringBuilder sb, RectF rect)
        {
            AppendNumber(sb, rect.X);
            AppendNumber(sb, rect.Y);
            AppendNumber(sb, rect.W);
            AppendNumber(sb, rect.H);
        }

        static void AppendNumber(StringBuilder sb, float value)
        {
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture)).Append(';');
        }
    }
}
