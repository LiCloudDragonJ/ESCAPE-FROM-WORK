using System.Collections.Generic;
using EscapeFromWork.Gameplay;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// T3：一局固定四层，四层按 4.2 m 净层高叠放；核心筒电梯在每一层必须是同一个平面位置，
    /// 相邻层之间各有一条电梯 + 两条楼梯。运行时只允许当前层（可选相邻层）存在。
    /// </summary>
    public class TowerPlanTests
    {
        const float FloorToFloor = 4.2f;

        [Test]
        public void Raid_HasFourFloorsWithConsecutiveWorldFloors()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            Assert.AreEqual(4, raid.FloorCount, "a raid is exactly four slices");
            for (int i = 1; i < raid.FloorCount; i++)
            {
                Assert.AreEqual(raid.Floors[i - 1].Spec.WorldFloor + 1, raid.Floors[i].Spec.WorldFloor,
                    "slice floors must be consecutive real floors");
                Assert.AreEqual(i, raid.Floors[i].FloorIndex);
            }
            Assert.AreEqual(raid.FloorCount * FloorToFloor, 16.8f, 0.001f,
                "four floors at 4.2 m give a 16.8 m tower body");
        }

        [Test]
        public void EveryFloor_UsesTheSameElevatorRect()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            RectF first = raid.Floors[0].Plan.Core;
            for (int i = 1; i < raid.FloorCount; i++)
            {
                RectF core = raid.Floors[i].Plan.Core;
                Assert.AreEqual(first.X, core.X, 0.001f, "elevator shaft must line up on x");
                Assert.AreEqual(first.Y, core.Y, 0.001f, "elevator shaft must line up on y");
                Assert.AreEqual(first.W, core.W, 0.001f);
                Assert.AreEqual(first.H, core.H, 0.001f);
            }
        }

        [Test]
        public void EachAdjacentPair_HasOneElevatorAndTwoStairs()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            for (int pair = 0; pair < raid.FloorCount - 1; pair++)
            {
                int stairs = 0;
                int elevators = 0;
                for (int i = 0; i < raid.Links.Count; i++)
                {
                    VerticalLink link = raid.Links[i];
                    if (link.LowerFloor != pair || link.UpperFloor != pair + 1) continue;
                    if (link.Kind == VerticalLinkKind.Elevator) elevators++;
                    else if (link.Kind == VerticalLinkKind.Stair) stairs++;
                }
                Assert.AreEqual(1, elevators, "pair " + pair + " needs exactly one elevator");
                Assert.AreEqual(2, stairs, "pair " + pair + " needs exactly two single-storey stairs");
            }
        }

        [Test]
        public void Floors_AreGeneratedFromIndependentSeeds()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            HashSet<int> seeds = new HashSet<int>();
            for (int i = 0; i < raid.FloorCount; i++) seeds.Add(raid.Floors[i].Spec.Seed);
            Assert.AreEqual(raid.FloorCount, seeds.Count, "each slice should be its own layout");
        }

        [Test]
        public void TowerRuntime_StacksFloorsFourPointTwoMetresApart()
        {
            GameObject host = new GameObject("TowerRuntimeTest");
            try
            {
                TowerRuntime runtime = host.AddComponent<TowerRuntime>();
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(i * FloorToFloor, runtime.LevelY(i), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
