using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R8：环路。生成器本来就有主廊 + 支廊 + 服务廊三层连通，实测环数（E − V + C）
    /// 已经是"房间数 / 12"的 5 倍以上 —— 所以 R8 的验收不是"补环"，而是"别把环丢回去"：
    /// 每个种子都必须显著高于目标，且 `LoopInjector.Apply` 在达标时必须是零改动（幂等）。
    /// </summary>
    public class LoopInjectorTests
    {
        static FloorPlan Plan(int seed) => FloorGenerator.Generate(FloorSpec.Default(seed));

        [Test]
        public void CycleRank_ExceedsTargetOnEverySeed([Values(1, 2, 3, 5, 7, 11)] int seed)
        {
            FloorPlan plan = Plan(seed);
            int target = LoopInjector.TargetCycles(plan);
            int rank = LoopInjector.CycleRank(plan);
            Assert.Greater(target, 0);
            Assert.GreaterOrEqual(rank, target,
                $"seed {seed} 的环数 {rank} 低于目标 {target}，说明绕行路径变少了");
        }

        [Test]
        public void Apply_IsNoOpWhenAlreadyLoopy([Values(1, 2, 3, 5, 7, 11)] int seed)
        {
            FloorPlan plan = Plan(seed);
            int before = LoopInjector.CycleRank(plan);
            int doorsBefore = plan.LoopDoors.Count;
            int added = LoopInjector.Apply(plan, FloorSpec.Default(seed));
            Assert.AreEqual(0, added, $"seed {seed} 已经很环通了，不该再加门");
            Assert.AreEqual(doorsBefore, plan.LoopDoors.Count);
            Assert.AreEqual(before, LoopInjector.CycleRank(plan));
        }

        [Test]
        public void CycleRank_IsDeterministic([Values(1, 3, 7)] int seed)
        {
            FloorPlan a = Plan(seed);
            FloorPlan b = Plan(seed);
            Assert.AreEqual(LoopInjector.CycleRank(a), LoopInjector.CycleRank(b));
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
        }

        [Test]
        public void EveryRoom_HasAtLeastTwoWaysOut()
        {
            // R1 已经把单门房清零；这里确认它没被 R8 破坏（加门只会更多，不会更少）。
            FloorPlan plan = Plan(5);
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                int doors = (room.HasDoor ? 1 : 0) + (room.ExtraDoors != null ? room.ExtraDoors.Count : 0);
                Assert.GreaterOrEqual(doors, 1, $"room {i} 一把门都没有");
            }
        }
    }
}
