using System.Collections.Generic;
using UnityEngine;
using EscapeFromWork.Data;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Inspector-friendly entry for configuring initial ammo reserve on the player prefab.
    /// </summary>
    [System.Serializable]
    public class AmmoReserveEntry
    {
        public AmmoType ammoType;
        public int count;
    }

    /// <summary>
    /// A single slot in the player's backpack, holding a stack of a single item type.
    /// </summary>
    [System.Serializable]
    public class InventorySlot
    {
        /// <summary>The item data for this slot. Null when the slot is empty.</summary>
        public ItemData item;

        /// <summary>How many copies of the item are currently in this slot.</summary>
        public int count;

        /// <summary>
        /// True when this slot contains no items (null item or zero count).
        /// </summary>
        public bool IsEmpty => item == null || count <= 0;
    }

    /// <summary>
    /// Player inventory system managing backpack slots and three weapon slots
    /// (A, C, Melee). Handles item stacking, weapon swapping, and total loot
    /// valuation for the death memorial system.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        // ---- Serialized fields ---------------------------------------------------

        [Header("Backpack")]
        [Tooltip("Number of backpack slots available for carrying items.")]
        [SerializeField] private int backpackSlots = 16;

        [Header("Ammo Reserve")]
        [Tooltip("Ammo carried outside the grid (not occupying backpack slots).")]
        [SerializeField] private AmmoReserveEntry[] initialAmmoReserve;

        /// <summary>
        /// Ammo reserve indexed by AmmoType. Ammo lives outside the grid for
        /// performance — no grid search on every shot. Reload pulls from here.
        /// </summary>
        private Dictionary<AmmoType, int> _ammoReserve = new Dictionary<AmmoType, int>();

        [Header("References")]
        [Tooltip("Reference to the PlayerCombat component for weapon equipping.")]
        [SerializeField] private PlayerCombat playerCombat;

        // ---- Weapon slots --------------------------------------------------------

        /// <summary>
        /// Equipped weapons indexed by slot: [0] = Slot A, [1] = Slot C, [2] = Melee.
        /// </summary>
        [SerializeField] private WeaponBase[] equippedWeapons = new WeaponBase[3];

        // ---- Backpack ------------------------------------------------------------

        /// <summary>
        /// Internal list of backpack slots. Slots may be empty (null item or zero count).
        /// </summary>
        [SerializeField] private List<InventorySlot> _backpack = new List<InventorySlot>();

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// The maximum number of backpack slots. Defined at initialization from
        /// <see cref="backpackSlots"/>.
        /// </summary>
        public int BackpackCapacity => backpackSlots;

        /// <summary>
        /// Reference to the PlayerCombat component on this GameObject.
        /// </summary>
        public PlayerCombat Combat => playerCombat;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            // Initialize backpack to the configured capacity.
            for (int i = 0; i < backpackSlots; i++)
            {
                _backpack.Add(new InventorySlot());
            }

            // Initialize ammo reserve from inspector-configured starting ammo.
            if (initialAmmoReserve != null)
            {
                foreach (var entry in initialAmmoReserve)
                {
                    if (entry.ammoType != AmmoType.None && entry.count > 0)
                        _ammoReserve[entry.ammoType] = entry.count;
                }
            }
        }

        private void Start()
        {
            if (playerCombat == null)
                playerCombat = GetComponent<PlayerCombat>();

            for (int i = 0; i < 3; i++)
            {
                if (equippedWeapons[i] != null)
                {
                    equippedWeapons[i].Initialize(equippedWeapons[i].Data);
                    playerCombat.EquipWeapon(equippedWeapons[i]);
                }
            }
            if (equippedWeapons[0] != null)
                SwapToSlot(WeaponSlot.A);
            else if (equippedWeapons[2] != null)
                SwapToSlot(WeaponSlot.Melee);
        }

        // ---- Item management -----------------------------------------------------

        /// <summary>
        /// Attempts to add an item to the backpack. Stacks with existing partial
        /// stacks of the same item first, then fills a new empty slot if needed.
        /// </summary>
        /// <param name="item">The item data to add.</param>
        /// <param name="count">How many copies to add.</param>
        /// <returns>True if all items were successfully added; false if there was
        /// insufficient space and only a partial amount was added.</returns>
        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0)
                return false;

            int remaining = count;

            // First pass: try to stack onto existing partial stacks of the same item.
            foreach (InventorySlot slot in _backpack)
            {
                if (slot.IsEmpty)
                    continue;

                if (slot.item == item && slot.count < item.MaxStackSize)
                {
                    int space = item.MaxStackSize - slot.count;
                    int toAdd = Mathf.Min(space, remaining);
                    slot.count += toAdd;
                    remaining -= toAdd;

                    if (remaining <= 0)
                        return true;
                }
            }

            // Second pass: fill empty slots with new stacks.
            foreach (InventorySlot slot in _backpack)
            {
                if (!slot.IsEmpty)
                    continue;

                int toAdd = Mathf.Min(item.MaxStackSize, remaining);
                slot.item = item;
                slot.count = toAdd;
                remaining -= toAdd;

                if (remaining <= 0)
                    return true;
            }

            // If we get here, not all items fit.
            return remaining <= 0;
        }

        /// <summary>
        /// Returns the total count of a specific item across all backpack slots.
        /// </summary>
        /// <param name="item">The item to count.</param>
        /// <returns>Total number of copies of the item in the backpack.</returns>
        public int GetItemCount(ItemData item)
        {
            if (item == null)
                return 0;

            int total = 0;
            foreach (InventorySlot slot in _backpack)
            {
                if (slot.IsEmpty)
                    continue;

                if (slot.item == item)
                {
                    total += slot.count;
                }
            }

            return total;
        }

        /// <summary>
        /// Removes a specified number of copies of an item from the backpack.
        /// Removes from the last-filled slots first (LIFO-style) for a natural
        /// inventory feel. The operation is atomic: if there are not enough
        /// copies, no items are removed.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="count">How many copies to remove.</param>
        /// <returns>True if the full amount was removed; false if there were not
        /// enough copies (no items are removed in that case).</returns>
        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0)
                return false;

            // Verify we have enough before removing anything.
            int available = GetItemCount(item);
            if (available < count)
                return false;

            int remaining = count;

            // Remove from the end (LIFO) for a natural feel.
            for (int i = _backpack.Count - 1; i >= 0; i--)
            {
                InventorySlot slot = _backpack[i];
                if (slot.IsEmpty)
                    continue;

                if (slot.item == item)
                {
                    int toRemove = Mathf.Min(slot.count, remaining);
                    slot.count -= toRemove;
                    remaining -= toRemove;

                    if (slot.count <= 0)
                    {
                        slot.item = null;
                        slot.count = 0;
                    }

                    if (remaining <= 0)
                        return true;
                }
            }

            return remaining <= 0;
        }

        // ---- Weapon management ---------------------------------------------------

        /// <summary>
        /// Equips a weapon into its designated slot and notifies
        /// <see cref="PlayerCombat"/>.
        /// </summary>
        /// <param name="weapon">The weapon instance to equip.</param>
        public void EquipWeapon(WeaponBase weapon)
        {
            if (weapon == null || weapon.Data == null)
                return;

            int index = WeaponSlotToIndex(weapon.Data.Slot);
            equippedWeapons[index] = weapon;

            if (playerCombat != null)
            {
                playerCombat.EquipWeapon(weapon);
            }
        }

        /// <summary>
        /// Swaps to the weapon in the specified slot, making it the active weapon
        /// via <see cref="PlayerCombat.EquipWeapon"/>.
        /// </summary>
        /// <param name="slot">The weapon slot to swap to (A, C, or Melee).</param>
        public void SwapToSlot(WeaponSlot slot)
        {
            int index = WeaponSlotToIndex(slot);
            WeaponBase weapon = equippedWeapons[index];

            if (weapon != null && playerCombat != null)
            {
                playerCombat.EquipWeapon(weapon);
            }
        }

        /// <summary>
        /// Cycles the active weapon by the given direction, wrapping around the
        /// three weapon slots. Skips empty slots so the player never selects an
        /// unequipped position.
        /// </summary>
        /// <param name="direction">+1 to cycle forward (A -> C -> Melee -> A),
        /// -1 to cycle backward.</param>
        public void CycleWeapon(int direction)
        {
            if (direction == 0)
                return;

            // Determine the current active slot index from PlayerCombat.
            int currentIndex = 0;
            if (playerCombat != null && playerCombat.CurrentWeapon != null)
            {
                WeaponData currentData = playerCombat.CurrentWeapon.Data;
                if (currentData != null)
                {
                    currentIndex = WeaponSlotToIndex(currentData.Slot);
                }
            }

            int step = direction > 0 ? 1 : -1;
            int attempts = 0;

            do
            {
                currentIndex = (currentIndex + step + 3) % 3;
                attempts++;
            }
            while (equippedWeapons[currentIndex] == null && attempts < 3);

            WeaponBase target = equippedWeapons[currentIndex];
            if (target != null && playerCombat != null)
            {
                playerCombat.EquipWeapon(target);
            }
        }

        /// <summary>
        /// Gets the weapon equipped in the specified slot.
        /// </summary>
        /// <param name="slot">The weapon slot to query (A, C, or Melee).</param>
        /// <returns>The WeaponBase in that slot, or null if the slot is empty.</returns>
        public WeaponBase GetEquippedWeapon(WeaponSlot slot)
        {
            int index = WeaponSlotToIndex(slot);
            return equippedWeapons[index];
        }

        // ---- Backpack access -----------------------------------------------------

        /// <summary>
        /// Returns a copy of the current backpack slot list for UI display.
        /// The returned list is a defensive copy; modifications to it do not
        /// affect the actual inventory.
        /// </summary>
        /// <returns>A list of all backpack slots (may include empty slots).</returns>
        public List<InventorySlot> GetBackpack()
        {
            return new List<InventorySlot>(_backpack);
        }

        // ---- Valuation -----------------------------------------------------------

        /// <summary>
        /// Calculates the total value of all items in the backpack, summing
        /// (count * BaseValue) for each occupied slot. Used for the death
        /// memorial screen to show the paperclip value of lost loot.
        /// </summary>
        /// <returns>Total paperclip value of all backpack contents.</returns>
        public int CalculateTotalValue()
        {
            int total = 0;

            foreach (InventorySlot slot in _backpack)
            {
                if (slot.IsEmpty || slot.item == null)
                    continue;

                total += slot.count * slot.item.BaseValue;
            }

            return total;
        }

        // ---- Ammo Reserve ---------------------------------------------------------

        /// <summary>
        /// Add ammo to the reserve. Called when picking up ammo items.
        /// </summary>
        public void AddAmmo(AmmoType type, int count)
        {
            if (type == AmmoType.None || count <= 0) return;

            if (_ammoReserve.ContainsKey(type))
                _ammoReserve[type] += count;
            else
                _ammoReserve[type] = count;
        }

        /// <summary>
        /// Consume ammo from the reserve. Called during reload to transfer
        /// reserve → magazine. Returns the amount actually consumed.
        /// </summary>
        public int ConsumeAmmo(AmmoType type, int requested)
        {
            if (type == AmmoType.None || requested <= 0) return 0;

            if (!_ammoReserve.TryGetValue(type, out int available)) return 0;

            int taken = Mathf.Min(available, requested);
            _ammoReserve[type] = available - taken;
            if (_ammoReserve[type] <= 0)
                _ammoReserve.Remove(type);
            return taken;
        }

        /// <summary>
        /// Returns the current ammo reserve count for a specific type.
        /// </summary>
        public int GetAmmoCount(AmmoType type)
        {
            if (type == AmmoType.None) return 0;
            return _ammoReserve.TryGetValue(type, out int count) ? count : 0;
        }

        /// <summary>
        /// Total ammo across all types. Used for the ammo safety net check (GDD: <10 → emergency cabinet).
        /// </summary>
        public int GetTotalAmmoCount()
        {
            int total = 0;
            foreach (var kvp in _ammoReserve) total += kvp.Value;
            return total;
        }

        // ---- Helpers -------------------------------------------------------------

        /// <summary>
        /// Converts a <see cref="WeaponSlot"/> enum value to its corresponding
        /// array index in <see cref="equippedWeapons"/>.
        /// </summary>
        /// <param name="slot">The weapon slot enum value.</param>
        /// <returns>0 for A, 1 for C, 2 for Melee. Defaults to 2 for unknown values.</returns>
        private static int WeaponSlotToIndex(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.A: return 0;
                case WeaponSlot.C: return 1;
                case WeaponSlot.Melee: return 2;
                default: return 2;
            }
        }
    }
}
