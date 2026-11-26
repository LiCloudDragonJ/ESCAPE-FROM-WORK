using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Tests
{
    /// <summary>
    /// Guards the "无意义空腔 / 进去是死胡同" class of bug: every square metre of the slab has to be
    /// either walkable (a room, a corridor, a stair, the core, a spawn pad) or filled by a wall
    /// body / solid mass. Anything in between is a floor slot with no wall that the player can
    /// walk into and get stuck in.
    /// </summary>
    public class FloorCoverageTests
    {
        static bool InAny(List<BoxSpec> boxes, Vector2 pt)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec b = boxes[i];
                float dx = pt.x - b.Center.x;
                float dz = pt.y - b.Center.z;
                if (Mathf.Abs(b.Yaw) > 1e-4f)
                {
                    // Same rotation the mesh builder uses; chamfer walls are rotated boxes.
                    float c = Mathf.Cos(b.Yaw), s = Mathf.Sin(b.Yaw);
                    float lx = dx * c - dz * s;
                    float lz = dx * s + dz * c;
                    dx = lx; dz = lz;
                }
                if (Mathf.Abs(dx) <= b.Size.x * 0.5f && Mathf.Abs(dz) <= b.Size.z * 0.5f) return true;
            }
            return false;
        }

        static bool InsideEnvelope(FloorPlan plan, Vector2 pt)
        {
            if (plan.Envelope == null || plan.Envelope.Outline == null) return true;
            return FloorEnvelope.Contains(plan.Envelope.Outline, pt);
        }

        static bool Walkable(FloorPlan plan, Vector2 pt)
        {
            for (int i = 0; i < plan.Rooms.Count; i++) if (plan.Rooms[i].Rect.Contains(pt)) return true;
            for (int i = 0; i < plan.Corridors.Count; i++) if (plan.Corridors[i].Rect.Contains(pt)) return true;
            for (int i = 0; i < plan.Stairs.Count; i++) if (plan.Stairs[i].Contains(pt)) return true;
            if (plan.Core.Contains(pt)) return true;
            for (int i = 0; i < plan.SpawnZones.Count; i++) if (plan.SpawnZones[i].Contains(pt)) return true;
            return false;
        }

        [Test]
        public void SlabIsEitherWalkableOrFilled([Values(1, 2, 3, 7)] int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            FloorPlan plan = FloorGenerator.Generate(spec);

            List<BoxSpec> fill = new List<BoxSpec>();
            FloorGeometry.CollectShellBoxes(plan, fill);
            FloorGeometry.CollectWallBoxes(plan, fill);
            FloorGeometry.CollectVoidBoxes(plan, fill);

            const float step = 0.5f;
            int total = 0;
            int holes = 0;
            for (float z = step * 0.5f; z < plan.Bounds.H; z += step)
            {
                for (float x = step * 0.5f; x < plan.Bounds.W; x += step)
                {
                    Vector2 pt = new Vector2(x, z);
                    // Outside the envelope is open air (notches, chamfers, sky) — not a hole.
                    if (!InsideEnvelope(plan, pt)) continue;
                    total++;
                    if (Walkable(plan, pt)) continue;
                    if (InAny(fill, pt)) continue;
                    holes++;
                }
            }

            float ratio = total == 0 ? 0f : (float)holes / total;
            Assert.Less(ratio, 0.01f, "seed " + seed + ": " + holes + "/" + total + " samples are bare floor with no wall or mass around them");
        }
    }
}
