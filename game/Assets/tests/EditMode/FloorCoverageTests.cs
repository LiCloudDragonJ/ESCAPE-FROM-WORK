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

        // 采样的判定容差：几何管线的"可走/实心"是在 0.2 m 栅格上算的（CollectVoidBoxes），
        // 本测试却按 0.5 m 网格撒点，两者在矩形边界上必然错开最多一个栅格。于是紧贴墙根
        // 2–10cm 的采样点会既落在房间矩形之外、又落在墙体盒之外（墙体从矩形边界起算），
        // 被误算成"裸地"。实测这类边界条纹每层 ~0.8%，R2 加了服务廊后 seed 2 顶到 1.06%。
        // 真正的空腔（能走进去卡住的那种）至少 30cm 宽，用 0.2 m 容差既能消掉条纹又不放过它。
        const float SampleTolerance = 0.2f;

        static bool Walkable(FloorPlan plan, Vector2 pt)
        {
            for (int i = 0; i < plan.Rooms.Count; i++) if (plan.Rooms[i].Rect.Inflate(SampleTolerance).Contains(pt)) return true;
            for (int i = 0; i < plan.Corridors.Count; i++) if (plan.Corridors[i].Rect.Inflate(SampleTolerance).Contains(pt)) return true;
            for (int i = 0; i < plan.Stairs.Count; i++) if (plan.Stairs[i].Inflate(SampleTolerance).Contains(pt)) return true;
            if (plan.Core.Inflate(SampleTolerance).Contains(pt)) return true;
            for (int i = 0; i < plan.SpawnZones.Count; i++) if (plan.SpawnZones[i].Inflate(SampleTolerance).Contains(pt)) return true;
            return false;
        }

        // 门洞是"设计上就该通"的：墙体在这里被 CutDoor 挖掉，门洞处既不在房间矩形里，也不在实心体里。
        // R2 给走廊加了门斗/服务廊的侧向开口（每层 ~200 个），这些门槛面积会把裸地比例顶过 1%，
        // 所以判定里必须把"门的开启范围（宽 × 墙厚）"算作可走，否则测的是门而不是空腔。
        static bool InOpening(Door d, Vector2 pt, float body)
        {
            if (d.Width <= 0.05f) return false;
            bool horizontal = d.Side == DoorSide.North || d.Side == DoorSide.South;
            float along = Mathf.Abs(horizontal ? pt.x - d.Center.x : pt.y - d.Center.y);
            float across = Mathf.Abs(horizontal ? pt.y - d.Center.y : pt.x - d.Center.x);
            return along <= d.Width * 0.5f + 0.02f && across <= body * 0.5f + 0.05f;
        }

        static bool NearOpening(FloorPlan plan, Vector2 pt, float body)
        {
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
                if (InOpening(plan.CorridorOpenings[i], pt, body)) return true;
            for (int i = 0; i < plan.LoopDoors.Count; i++)
                if (InOpening(plan.LoopDoors[i], pt, body)) return true;
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                Room room = plan.Rooms[r];
                if (room.HasDoor && InOpening(room.Door, pt, body)) return true;
                for (int k = 0; k < room.ExtraDoors.Count; k++)
                    if (InOpening(room.ExtraDoors[k], pt, body)) return true;
            }
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
            float body = Mathf.Max(0.2f, spec.WallInset * 2f);
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
                    if (NearOpening(plan, pt, body)) continue;
                    if (InAny(fill, pt)) continue;
                    holes++;
                }
            }

            float ratio = total == 0 ? 0f : (float)holes / total;
            Assert.Less(ratio, 0.01f, "seed " + seed + ": " + holes + "/" + total + " samples are bare floor with no wall or mass around them");
        }
    }
}
