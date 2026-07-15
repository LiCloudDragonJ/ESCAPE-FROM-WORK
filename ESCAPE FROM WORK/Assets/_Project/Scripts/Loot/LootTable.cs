using UnityEngine;
using EscapeFromWork.Data;
using Random = UnityEngine.Random;

namespace EscapeFromWork.Loot
{
    /// <summary>
    /// A single entry in a loot table, defining one possible item drop with its
    /// probability weight and quantity range.
    /// </summary>
    [System.Serializable]
    public class LootEntry
    {
        /// <summary>The item that may drop when this entry is selected.</summary>
        [Tooltip("The item that may drop when this entry is selected.")]
        public ItemData item;

        /// <summary>
        /// Relative probability weight for this entry (0-1).
        /// Higher values make this entry more likely to be chosen.
        /// </summary>
        [Tooltip("Relative probability weight (0-1). Higher values are more likely.")]
        [Range(0f, 1f)]
        public float weight = 0.5f;

        /// <summary>Minimum number of copies to spawn when this entry is selected.</summary>
        [Tooltip("Minimum copies to spawn.")]
        [Min(1)]
        public int minCount = 1;

        /// <summary>Maximum number of copies to spawn when this entry is selected.</summary>
        [Tooltip("Maximum copies to spawn.")]
        [Min(1)]
        public int maxCount = 1;
    }

    /// <summary>
    /// Data-driven loot table that performs weighted random selection to produce
    /// an array of drops each time it is rolled.
    ///
    /// Create instances via Assets > Create > Data > LootTable.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/LootTable", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        /// <summary>
        /// All possible entries in this loot table. Each entry has a weight,
        /// an ItemData reference, and a quantity range.
        /// </summary>
        [Tooltip("All possible drop entries with their weights and quantity ranges.")]
        public LootEntry[] entries;

        /// <summary>Minimum number of times the table is rolled per activation.</summary>
        [Tooltip("Minimum rolls per activation.")]
        [Min(1)]
        public int minRolls = 1;

        /// <summary>Maximum number of times the table is rolled per activation.</summary>
        [Tooltip("Maximum rolls per activation.")]
        [Min(1)]
        public int maxRolls = 3;

        /// <summary>
        /// Performs a weighted random selection from <see cref="entries"/>,
        /// repeated a random number of times between <see cref="minRolls"/> and
        /// <see cref="maxRolls"/>. Each roll randomly picks a quantity between
        /// the selected entry's minCount and maxCount.
        /// </summary>
        /// <returns>
        /// An array of (item, count) pairs representing the rolled loot.
        /// Returns an empty array if there are no entries.
        /// </returns>
        public (ItemData item, int count)[] Roll()
        {
            if (entries == null || entries.Length == 0)
            {
                return new (ItemData, int)[0];
            }

            int rollCount = Random.Range(minRolls, maxRolls + 1);
            var results = new (ItemData item, int count)[rollCount];

            for (int i = 0; i < rollCount; i++)
            {
                LootEntry selected = PickWeightedEntry();
                if (selected != null && selected.item != null)
                {
                    int quantity = Random.Range(selected.minCount, selected.maxCount + 1);
                    results[i] = (selected.item, quantity);
                }
            }

            return results;
        }

        /// <summary>
        /// Selects a single <see cref="LootEntry"/> using cumulative weighted
        /// random selection. Each entry's chance is proportional to its weight
        /// relative to the sum of all weights.
        /// </summary>
        /// <returns>The selected LootEntry, or null if no valid entries exist.</returns>
        private LootEntry PickWeightedEntry()
        {
            // Compute total weight so we can roll against the cumulative sum.
            float totalWeight = 0f;
            foreach (LootEntry entry in entries)
            {
                if (entry != null && entry.item != null && entry.weight > 0f)
                {
                    totalWeight += entry.weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (LootEntry entry in entries)
            {
                if (entry == null || entry.item == null || entry.weight <= 0f)
                {
                    continue;
                }

                cumulative += entry.weight;
                if (roll <= cumulative)
                {
                    return entry;
                }
            }

            // Fallback: return the last valid entry (should not normally be reached
            // due to floating-point rounding, but guards against edge cases).
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                if (entries[i] != null && entries[i].item != null && entries[i].weight > 0f)
                {
                    return entries[i];
                }
            }

            return null;
        }

        private void OnValidate()
        {
            // Keep roll ranges sensible.
            if (maxRolls < minRolls)
            {
                maxRolls = minRolls;
            }
        }
    }
}
