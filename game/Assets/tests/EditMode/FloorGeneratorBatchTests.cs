using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// 1000 种子批测：任何一颗种子都不允许出现断连、无门、穿模、越界、面积塌陷。
    /// 这是生成器的硬约束网——单个人工挑的种子通过不算数。
    /// </summary>
    public class FloorGeneratorBatchTests
    {
        const int SeedCount = 1000;
        const float Cell = 1f;

        [Test]
        public void Batch1000_AllInvariantsHold()
        {
            int worstDoor = 100;
            float worstCoverage = 1f;
            float worstReach = 1f;
            int worstSeedDoor = 0;
            int worstSeedReach = 0;

            for (int seed = 1; seed <= SeedCount; seed++)
            {
                FloorSpec spec = FloorSpec.Default(seed);
                spec.Prototype = (FloorPrototype)(seed % 6);
                spec.Band = seed % 3 == 0 ? FloorBand.Podium : (seed % 3 == 1 ? FloorBand.Tower : FloorBand.Top);
                if (spec.Band == FloorBand.Podium) { spec.Width = 280f; spec.Height = 280f; }
                else if (spec.Band == FloorBand.Top) { spec.Width = 160f; spec.Height = 120f; }

                FloorPlan plan = FloorGenerator.Generate(spec);

                Assert.Greater(plan.Rooms.Count, 8, "seed " + seed + " too few rooms");
                Assert.GreaterOrEqual(plan.Extractions.Count, 4, "seed " + seed + " extractions");
                Assert.LessOrEqual(plan.Extractions.Count, 6, "seed " + seed + " extractions");
                Assert.AreEqual(4, plan.SpawnZones.Count, "seed " + seed + " spawn zones");
                Assert.Greater(plan.Columns.Count, 20, "seed " + seed + " columns");

                // 覆盖率：0.40 是 R4 之后 1000 种子实测的底线（最差 seed 713 = 0.4162；
                // R2 的服务廊、R3 的长条改判走廊、R4 的分区主题都会从房间面积里拿走一部分），
                // 所以真正的底线看下面的"房间+走廊"联合覆盖率。
                float area = spec.Width * spec.Height;
                float coverage = plan.RoomArea / area;
                if (coverage < worstCoverage) { worstCoverage = coverage; }
                Assert.Greater(coverage, 0.40f, "seed " + seed + " coverage " + coverage.ToString("P0"));
                Assert.Less(coverage, 0.96f, "seed " + seed + " coverage " + coverage.ToString("P0"));
                float corrArea = 0f;
                for (int c = 0; c < plan.Corridors.Count; c++) corrArea += plan.Corridors[c].Rect.Area;
                // R4 之后分区主题会留下更大的房间（数据中心大厅），房间与房间之间的
                // 结构墙垛（HallBreaks）也不算面积，联合覆盖率实测最差降到 0.79 左右，
                // 所以底线定 0.78：再低就说明真的有面积既不是房间也不是走廊。
                Assert.Greater((plan.RoomArea + corrArea) / area, 0.78f,
                    "seed " + seed + " 房间+走廊覆盖率过低，有面积既不是房间也不是走廊");

                // 门
                int withDoor = 0;
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    Room room = plan.Rooms[i];
                    Assert.IsTrue(room.Rect.X >= -0.01f && room.Rect.Y >= -0.01f, "seed " + seed + " room out of bounds");
                    Assert.IsTrue(room.Rect.XMax <= spec.Width + 0.01f && room.Rect.YMax <= spec.Height + 0.01f, "seed " + seed + " room out of bounds");
                    Assert.IsFalse(room.Rect.Overlaps(plan.Core), "seed " + seed + " room inside core");
                    for (int s = 0; s < plan.Stairs.Count; s++)
                        Assert.IsFalse(room.Rect.Overlaps(plan.Stairs[s]), "seed " + seed + " room inside stairs");
                    if (room.HasDoor) withDoor++;
                }

                int doorPct = Mathf.RoundToInt(withDoor * 100f / plan.Rooms.Count);
                if (doorPct < worstDoor) { worstDoor = doorPct; worstSeedDoor = seed; }
                Assert.GreaterOrEqual(doorPct, 90, "seed " + seed + " door rate " + doorPct + "%");

                // 连通性：1m 栅格，从出生区中心泛洪
                float reach = ReachableRatio(plan, spec);
                if (reach < worstReach) { worstReach = reach; worstSeedReach = seed; }
                Assert.Greater(reach, 0.98f, "seed " + seed + " reachable " + reach.ToString("P1"));
            }

            Debug.Log("batch1000 worstDoor=" + worstDoor + "% (seed " + worstSeedDoor + ") worstCoverage=" +
                      worstCoverage.ToString("P1") + " worstReach=" + worstReach.ToString("P2") + " (seed " + worstSeedReach + ")");
        }

        [Test]
        public void Batch_NoRoomOverlapsAnother()
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                FloorSpec spec = FloorSpec.Default(seed);
                FloorPlan plan = FloorGenerator.Generate(spec);
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    for (int j = i + 1; j < plan.Rooms.Count; j++)
                    {
                        Assert.IsFalse(plan.Rooms[i].Rect.Overlaps(plan.Rooms[j].Rect),
                            "seed " + seed + " rooms overlap " + i + "/" + j);
                    }
                }
            }
        }

        static float ReachableRatio(FloorPlan plan, FloorSpec spec)
        {
            int w = Mathf.CeilToInt(spec.Width / Cell);
            int h = Mathf.CeilToInt(spec.Height / Cell);
            bool[] walkable = new bool[w * h];

            for (int z = 0; z < plan.Corridors.Count; z++)
                MarkRect(walkable, w, h, plan.Corridors[z].Rect.Inflate(0.05f));
            for (int r = 0; r < plan.Rooms.Count; r++)
                MarkRect(walkable, w, h, plan.Rooms[r].Rect.Inflate(0.05f));
            for (int s = 0; s < plan.Stairs.Count; s++)
                MarkRect(walkable, w, h, plan.Stairs[s].Inflate(0.05f));
            MarkRect(walkable, w, h, plan.Core.Inflate(0.05f));
            // 门是墙上 1.2m 的开口，人不穿墙但穿门：把门本身也算成可走
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                if (!plan.Rooms[r].HasDoor) continue;
                Vector2 c = plan.Rooms[r].Door.Center;
                MarkRect(walkable, w, h, new RectF(c.x - 0.7f, c.y - 0.7f, 1.4f, 1.4f));
            }
            for (int d = 0; d < plan.LoopDoors.Count; d++)
            {
                Vector2 c = plan.LoopDoors[d].Center;
                MarkRect(walkable, w, h, new RectF(c.x - 0.6f, c.y - 0.6f, 1.2f, 1.2f));
            }
            // 房间的额外门（同一房间开的第二、第三道门）
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                List<Door> extra = plan.Rooms[r].ExtraDoors;
                if (extra == null) continue;
                for (int d = 0; d < extra.Count; d++)
                {
                    Vector2 c = extra[d].Center;
                    MarkRect(walkable, w, h, new RectF(c.x - 0.6f, c.y - 0.6f, 1.2f, 1.2f));
                }
            }

            int total = 0;
            for (int i = 0; i < walkable.Length; i++) if (walkable[i]) total++;
            if (total == 0) return 0f;

            Vector2 startPos = plan.SpawnZones[0].Center;
            int sx = Mathf.Clamp(Mathf.FloorToInt(startPos.x / Cell), 0, w - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(startPos.y / Cell), 0, h - 1);
            int start = sy * w + sx;

            bool[] seen = new bool[walkable.Length];
            Stack<int> stack = new Stack<int>();
            if (walkable[start]) { seen[start] = true; stack.Push(start); }

            int reached = 0;
            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                reached++;
                int x = idx % w;
                int y = idx / w;
                TryPush(walkable, seen, stack, w, h, x + 1, y);
                TryPush(walkable, seen, stack, w, h, x - 1, y);
                TryPush(walkable, seen, stack, w, h, x, y + 1);
                TryPush(walkable, seen, stack, w, h, x, y - 1);
            }

            return reached / (float)total;
        }

        static void TryPush(bool[] walkable, bool[] seen, Stack<int> stack, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int idx = y * w + x;
            if (!walkable[idx] || seen[idx]) return;
            seen[idx] = true;
            stack.Push(idx);
        }

        static void MarkRect(bool[] walkable, int w, int h, RectF r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.X / Cell));
            int x1 = Mathf.Min(w - 1, Mathf.FloorToInt(r.XMax / Cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.Y / Cell));
            int y1 = Mathf.Min(h - 1, Mathf.FloorToInt(r.YMax / Cell));
            for (int y = y0; y <= y1; y++)
            {
                int row = y * w;
                for (int x = x0; x <= x1; x++) walkable[row + x] = true;
            }
        }
    }
}
