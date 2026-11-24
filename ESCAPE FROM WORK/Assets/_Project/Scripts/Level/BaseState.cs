using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Persistent base (tea-room) state — stash inventory, weapon rack,
    /// upgrades, and raid stats. Survives player death.
    ///
    /// Stored as a JSON file in Application.persistentDataPath; loaded at
    /// game start and saved after every extraction.
    /// </summary>
    [System.Serializable]
    public class BaseState
    {
        // ---- Stash ----

        /// <summary>Item asset path (GUID-based) → stacked count.</summary>
        public Dictionary<string, int> storedItems = new Dictionary<string, int>();

        public int stashWidth  = 8;
        public int stashHeight = 10;
        public int StashSlots => stashWidth * stashHeight;

        /// <summary>Stack multiplier — base stack size × this = stash stack size.</summary>
        public int stashMaxStackMultiplier = 10;

        // ---- Weapon rack ----

        /// <summary>Asset paths of weapons the player has acquired.</summary>
        public List<string> ownedWeaponPaths = new List<string>();

        // ---- Stats ----

        public int raidsCompleted;
        public int deepestFloorReached = 50; // start at top floor
        public int totalValueStashed;

        // ---- Upgrades ----

        public int stashUpgradeLevel;
        public int weaponRackUpgradeLevel;

        // ---- Singleton ----

        private static BaseState _instance;
        public static BaseState Instance => _instance ??= new BaseState();

        public static void Reset() { _instance = new BaseState(); }

        // ---- Stash operations ----

        /// <summary>Try to store an item. Returns false if stash is full.</summary>
        public bool StoreItem(string assetPath, int count)
        {
            if (count <= 0) return false;

            if (storedItems.TryGetValue(assetPath, out int existing))
            {
                storedItems[assetPath] = existing + count;
            }
            else
            {
                // Check capacity — one unique item = one slot.
                if (storedItems.Count >= StashSlots)
                {
                    Debug.LogWarning("[BaseState] Stash full — cannot store new item type.");
                    return false;
                }
                storedItems[assetPath] = count;
            }

            totalValueStashed += count;
            return true;
        }

        /// <summary>Try to retrieve items. Returns true if enough were available.</summary>
        public bool RetrieveItem(string assetPath, int count)
        {
            if (!storedItems.TryGetValue(assetPath, out int existing)) return false;
            if (existing < count) return false;

            storedItems[assetPath] = existing - count;
            if (storedItems[assetPath] <= 0)
                storedItems.Remove(assetPath);

            return true;
        }

        /// <summary>Get how many of an item are in the stash.</summary>
        public int GetItemCount(string assetPath)
        {
            storedItems.TryGetValue(assetPath, out int count);
            return count;
        }

        /// <summary>Add a weapon to the rack.</summary>
        public void AddWeapon(string assetPath)
        {
            if (!ownedWeaponPaths.Contains(assetPath))
                ownedWeaponPaths.Add(assetPath);
        }

        // ---- Upgrades ----

        public bool UpgradeStash()
        {
            stashUpgradeLevel++;
            stashWidth  += 2;
            stashHeight += 2;
            return true;
        }
    }
}
