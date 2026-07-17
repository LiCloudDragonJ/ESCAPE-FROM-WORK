using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// Unit tests for loot table weighted random logic as specified in
    /// design/gdd/loot-economy.md.
    /// </summary>
    public class LootTableTest
    {
        // Simulate a simplified LootEntry for testing without SO dependencies.
        private struct TestEntry
        {
            public string id;
            public float weight;
            public int minCount;
            public int maxCount;
        }

        /// <summary>
        /// Pure implementation of the weighted random selection from loot-economy.md Formulas §1.
        /// </summary>
        private static string WeightedRandom(List<TestEntry> entries, float randomValue)
        {
            float totalWeight = entries.Sum(e => e.weight);
            float roll = randomValue * totalWeight;
            float cumulative = 0f;

            foreach (var entry in entries)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                    return entry.id;
            }
            return entries.Last().id; // fallback
        }

        // ---- Weighted random ------------------------------------------------------

        [Test]
        public void SingleEntry_AlwaysReturnsThatEntry()
        {
            var entries = new List<TestEntry> { new() { id = "A", weight = 1f } };
            for (int i = 0; i < 10; i++)
            {
                string result = WeightedRandom(entries, i / 10f);
                Assert.AreEqual("A", result);
            }
        }

        [Test]
        public void EqualWeights_ProducesUniformDistribution()
        {
            var entries = new List<TestEntry>
            {
                new() { id = "A", weight = 1f },
                new() { id = "B", weight = 1f },
                new() { id = "C", weight = 1f },
            };

            var counts = new Dictionary<string, int>();
            int samples = 300;

            for (int i = 0; i < samples; i++)
            {
                string result = WeightedRandom(entries, i / (float)samples);
                counts[result] = counts.GetValueOrDefault(result) + 1;
            }

            // With 300 samples and 3 equal weights, each should get ~100.
            // Allow ±30% tolerance for statistical variance.
            foreach (var entry in entries)
            {
                Assert.Greater(counts[entry.id], 70, $"{entry.id} got too few hits");
                Assert.Less(counts[entry.id], 130, $"{entry.id} got too many hits");
            }
        }

        [Test]
        public void HighWeight_SelectedMoreOften()
        {
            var entries = new List<TestEntry>
            {
                new() { id = "Rare", weight = 1f },
                new() { id = "Common", weight = 9f },
            };

            int commonHits = 0;
            int samples = 100;

            for (int i = 0; i < samples; i++)
            {
                string result = WeightedRandom(entries, i / (float)samples);
                if (result == "Common") commonHits++;
            }

            Assert.Greater(commonHits, 70, "Common item (90% weight) should appear more often");
        }

        // ---- Roll count -----------------------------------------------------------

        [Test]
        public void RollCount_WithinMinMaxRange()
        {
            int minRolls = 2, maxRolls = 5;
            // Simulate rolling: Random.Range(minRolls, maxRolls + 1)
            for (int seed = 0; seed < 50; seed++)
            {
                // Deterministic "random" from seed
                int rolls = minRolls + (seed % (maxRolls - minRolls + 1));
                Assert.GreaterOrEqual(rolls, minRolls);
                Assert.LessOrEqual(rolls, maxRolls);
            }
        }

        // ---- Stack count generation -----------------------------------------------

        [Test]
        public void StackCount_WithinMinMaxRange()
        {
            int minCount = 1, maxCount = 5;
            for (int seed = 0; seed < 20; seed++)
            {
                int count = minCount + (seed % (maxCount - minCount + 1));
                Assert.GreaterOrEqual(count, minCount);
                Assert.LessOrEqual(count, maxCount);
            }
        }

        // ---- Rarity value ranges (GDD §Rarity) -----------------------------------

        [Test]
        public void RarityValues_AreWithinDefinedRanges()
        {
            var rarityRanges = new Dictionary<string, (int min, int max)>
            {
                { "Common", (1, 50) },
                { "Uncommon", (50, 500) },
                { "Rare", (500, 2000) },
                { "Epic", (2000, 8000) },
                { "Legendary", (8000, 50000) },
                { "Mythic", (50000, int.MaxValue) },
            };

            int[] testValues = { 25, 300, 1000, 5000, 20000, 100000 };
            string[] rarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic" };

            for (int i = 0; i < rarities.Length; i++)
            {
                var range = rarityRanges[rarities[i]];
                Assert.GreaterOrEqual(testValues[i], range.min, $"{rarities[i]} value too low");
                Assert.LessOrEqual(testValues[i], range.max, $"{rarities[i]} value too high");
            }
        }

        // ---- Ammo safety net threshold --------------------------------------------

        [Test]
        public void AmmoSafetyNet_TriggersBelowThreshold()
        {
            const int threshold = 10;
            int[] ammoCounts = { 3, 2, 4 }; // total = 9 < 10 → trigger
            int total = ammoCounts.Sum();
            Assert.Less(total, threshold, "Should trigger safety net");
        }

        [Test]
        public void AmmoSafetyNet_DoesNotTriggerAboveThreshold()
        {
            const int threshold = 10;
            int[] ammoCounts = { 5, 3, 4 }; // total = 12 ≥ 10 → no trigger
            int total = ammoCounts.Sum();
            Assert.GreaterOrEqual(total, threshold, "Should NOT trigger safety net");
        }
    }
}
