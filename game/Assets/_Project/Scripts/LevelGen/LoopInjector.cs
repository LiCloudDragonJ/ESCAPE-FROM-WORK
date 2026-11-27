using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// R8：环路。迷宫感的来源不是"房间多"，而是图里几乎没有环 —— 追击、绕后、听声辨位都要求
    /// 同一对位置之间存在多条步行路径。
    ///
    /// 实测（`R2Probe.Loops`，12 个种子）：环数（E − V + C）49–69，而"房间数 / 12"只有 9–11 的
    /// 最低预算 —— 主廊 + 支廊 + 并行服务廊本来就把环数撑到了 5–6 倍。所以这里的目标是**下限**，
    /// `Apply` 只在低于下限 70% 时补门（正常分布下是零改动），真正要做的是别把环丢回去。
    ///
    /// 只加门、不拆墙：找"已经连通、但只靠一条长绕路相连"的相邻房间，在共享墙上再开一道门，环数 +1。
    /// 已经达标时不删门（删门会破坏 R1 的单门房验收和连通性保证）。
    /// </summary>
    public static class LoopInjector
    {
        public static int TargetCycles(FloorPlan plan)
        {
            return Mathf.Max(1, Mathf.RoundToInt(plan.Rooms.Count / 12f));
        }

        /// <summary>
        /// 环数 = E − V + C。只统计真正参与图的节点（没门的孤立走廊是设计上的死空间，不计入 V）。
        /// 边只来自房间自己的门记录（room.Door + room.ExtraDoors）；`plan.LoopDoors` 是这些门的重复登记，
        /// 再算一遍会把环数翻倍。
        /// </summary>
        public static int CycleRank(FloorPlan plan)
        {
            int roomCount = plan.Rooms.Count;
            int corridorCount = plan.Corridors.Count;
            int n = roomCount + corridorCount;
            if (n == 0) return 0;

            int[] parent = new int[n];
            bool[] used = new bool[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            HashSet<long> edges = new HashSet<long>();
            for (int i = 0; i < roomCount; i++)
            {
                Room room = plan.Rooms[i];
                LinkDoor(parent, used, edges, i, room.Door, roomCount, corridorCount);
                if (room.ExtraDoors == null) continue;
                for (int k = 0; k < room.ExtraDoors.Count; k++)
                    LinkDoor(parent, used, edges, i, room.ExtraDoors[k], roomCount, corridorCount);
            }

            int v = 0;
            for (int i = 0; i < n; i++) if (used[i]) v++;
            int components = 0;
            for (int i = 0; i < n; i++) if (used[i] && Find(parent, i) == i) components++;
            return edges.Count - v + components;
        }

        static void LinkDoor(int[] parent, bool[] used, HashSet<long> edges, int roomIndex, Door d,
            int roomCount, int corridorCount)
        {
            int target;
            if (d.OtherRoom >= 0 && d.OtherRoom < roomCount) target = d.OtherRoom;
            else if (d.Corridor >= 0 && d.Corridor < corridorCount) target = roomCount + d.Corridor;
            else return;
            if (target == roomIndex) return;

            used[roomIndex] = true;
            used[target] = true;
            long key = roomIndex < target
                ? ((long)roomIndex << 32) | (uint)target
                : ((long)target << 32) | (uint)roomIndex;
            if (!edges.Add(key)) return;
            Union(parent, roomIndex, target);
        }

        /// <summary>补环：只在"当前图上已连通"的房间对之间加门；返回新增门数。</summary>
        public static int Apply(FloorPlan plan, FloorSpec spec)
        {
            int target = TargetCycles(plan);
            int want = Mathf.RoundToInt(target * 0.7f) - CycleRank(plan);
            if (want <= 0) return 0;

            int[] parent = BuildUnion(plan);
            System.Random rng = new System.Random(unchecked(spec.Seed * 8647 + 211));
            int added = 0;

            for (int pass = 0; pass < 3 && added < want; pass++)
            {
                for (int i = 0; i < plan.Rooms.Count && added < want; i++)
                {
                    if (rng.NextDouble() > 0.6) continue;
                    Room a = plan.Rooms[i];
                    for (int j = i + 1; j < plan.Rooms.Count && added < want; j++)
                    {
                        Room b = plan.Rooms[j];
                        Vector2 center;
                        DoorSide side;
                        float overlap;
                        if (!FloorGenerator.FindDoorBetween(a.Rect, b.Rect, out center, out side, out overlap)) continue;
                        if (overlap < 2.4f) continue;
                        if (FloorGenerator.DoorNear(a, center, 1.6f)) continue;
                        if (FloorGenerator.DoorNear(b, center, 1.6f)) continue;
                        // 必须已经连通：否则加的是桥，环数不涨。
                        if (Find(parent, i) != Find(parent, j)) continue;

                        Door d;
                        d.Center = center;
                        d.Side = side;
                        d.Width = Mathf.Clamp(overlap * 0.4f, 1.0f, 1.6f);
                        d.Corridor = -1;
                        d.OtherRoom = j;
                        AddExtraDoor(plan, i, d);
                        d.OtherRoom = i;
                        AddExtraDoor(plan, j, d);
                        plan.LoopDoors.Add(d);
                        added++;
                    }
                }
            }
            return added;
        }

        static int[] BuildUnion(FloorPlan plan)
        {
            int roomCount = plan.Rooms.Count;
            int n = roomCount + plan.Corridors.Count;
            int[] parent = new int[Mathf.Max(1, n)];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            bool[] used = new bool[parent.Length];
            HashSet<long> edges = new HashSet<long>();
            for (int i = 0; i < roomCount; i++)
            {
                Room room = plan.Rooms[i];
                LinkDoor(parent, used, edges, i, room.Door, roomCount, plan.Corridors.Count);
                if (room.ExtraDoors == null) continue;
                for (int k = 0; k < room.ExtraDoors.Count; k++)
                    LinkDoor(parent, used, edges, i, room.ExtraDoors[k], roomCount, plan.Corridors.Count);
            }
            return parent;
        }

        static void AddExtraDoor(FloorPlan plan, int roomIndex, Door d)
        {
            Room room = plan.Rooms[roomIndex];
            if (room.ExtraDoors == null) room.ExtraDoors = new List<Door>();
            room.ExtraDoors.Add(d);
            plan.Rooms[roomIndex] = room;
        }

        static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }
    }
}
