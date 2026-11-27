using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
/// <summary>
/// 道具朝向：正面必须背对它所贴的那面墙（0=朝 +Z / 90=朝 +X / 180=朝 −Z / 270=朝 −X）。
/// 这条不变量以前没有，PropSpawner 用 rect.H &gt; rect.W 猜 90°，于是东西墙的家具全都背对墙。
/// </summary>
public class PropFacingTests
{
    static List<FloorPlan> Plans()
    {
        List<FloorPlan> plans = new List<FloorPlan>();
        for (int seed = 1; seed <= 6; seed++)
            plans.Add(FloorGenerator.Generate(new FloorSpec { Seed = seed }));
        return plans;
    }

    [Test]
    public void EveryProp_HasCardinalYaw()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                float yaw = Mathf.Repeat(p.Yaw, 360f);
                bool ok = Mathf.Approximately(yaw, 0f) || Mathf.Approximately(yaw, 90f) ||
                          Mathf.Approximately(yaw, 180f) || Mathf.Approximately(yaw, 270f);
                Assert.That(ok, Is.True, "道具朝向必须是 0/90/180/270，实际 " + p.Yaw + "（" + p.Spec.Name + "）");
            }
        }
    }

    [Test]
    public void CorridorProps_FaceIntoTheCorridor()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                if (p.CorridorIndex < 0 || p.CorridorIndex >= plan.Corridors.Count) continue;
                RectF c = plan.Corridors[p.CorridorIndex].Rect;
                bool horiz = c.W >= c.H;
                float expected;
                if (horiz) expected = p.Rect.Center.y < c.Center.y ? 0f : 180f;
                else expected = p.Rect.Center.x < c.Center.x ? 90f : 270f;
                Assert.That(Mathf.Repeat(p.Yaw, 360f), Is.EqualTo(expected).Within(0.01f),
                    "走廊掩体 " + p.Spec.Name + " 背对墙的方向不对");
            }
        }
    }

    [Test]
    public void RoomProps_FaceIntoTheRoom()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                if (p.CorridorIndex >= 0) continue;
                Room room;
                if (!TryFindRoom(plan, p.Rect.Center, out room)) continue;
                if (p.Spec.Name == "隔断墙") continue; // 大厅挡墙垛是自由站立的，不贴墙

                // 真不变量：背面贴墙、正面朝开阔地。背面到房间边界必须留 0–0.35m（贴墙但不穿墙）。
                float gap = BackGap(p, room.Rect);
                Assert.That(gap, Is.GreaterThanOrEqualTo(0f),
                    "房间家具 " + p.Spec.Name + " 的背面穿出了房间墙（" + RoomOf(room) + "）");
                Assert.That(gap, Is.LessThanOrEqualTo(0.35f),
                    "房间家具 " + p.Spec.Name + " 没有贴墙，离墙 " + gap.ToString("0.00") + "m（" + RoomOf(room) + "）");
            }
        }
    }

    [Test]
    public void RoomProps_KeepAuthorWidthAlongTheWall()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                if (p.CorridorIndex >= 0) continue;
                Room room;
                if (!TryFindRoom(plan, p.Rect.Center, out room)) continue;

                bool alongX = Mathf.Approximately(Mathf.Repeat(p.Yaw, 180f), 0f);
                float wantW = alongX ? p.Spec.Width : p.Spec.Depth;
                float wantD = alongX ? p.Spec.Depth : p.Spec.Width;
                // 生成器会把超出房间的尺寸夹到 2.4 / 2.2，但只会变小不会变大。
                Assert.That(p.Rect.W, Is.LessThanOrEqualTo(wantW + 0.01f), "沿墙尺寸超了：" + p.Spec.Name);
                Assert.That(p.Rect.H, Is.LessThanOrEqualTo(wantD + 0.01f), "进深超了：" + p.Spec.Name);
            }
        }
    }

    /// <summary>背面到外墙边界的距离：0 表示正好贴墙，负数表示穿墙。</summary>
    static float BackGap(PropPlacement p, RectF bounds)
    {
        float yaw = Mathf.Repeat(p.Yaw, 360f);
        if (Mathf.Approximately(yaw, 0f)) return p.Rect.Y - bounds.Y;
        if (Mathf.Approximately(yaw, 180f)) return bounds.YMax - p.Rect.YMax;
        if (Mathf.Approximately(yaw, 90f)) return p.Rect.X - bounds.X;
        return bounds.XMax - p.Rect.XMax;
    }

    static bool TryFindRoom(FloorPlan plan, Vector2 p, out Room room)
    {
        for (int i = 0; i < plan.Rooms.Count; i++)
            if (plan.Rooms[i].Rect.Contains(p)) { room = plan.Rooms[i]; return true; }
        room = default(Room);
        return false;
    }

    static string RoomOf(Room room)
    {
        return room.Facility + "@" + room.Rect.X.ToString("0") + "," + room.Rect.Y.ToString("0");
    }
}
}
