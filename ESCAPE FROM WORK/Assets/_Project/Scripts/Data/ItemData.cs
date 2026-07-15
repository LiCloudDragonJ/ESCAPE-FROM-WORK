using UnityEngine;

namespace EscapeFromWork.Data
{
    /// <summary>
    /// Broad category of an item, governing which systems can interact with it.
    /// </summary>
    public enum ItemType
    {
        /// <summary>回形针 or other in-raid currencies.</summary>
        Currency,
        /// <summary>Ammunition for ranged weapons.</summary>
        Ammo,
        /// <summary>Single-use item consumed during a raid.</summary>
        Consumable,
        /// <summary>Quest or progression item; cannot be sold or discarded.</summary>
        KeyItem,
        /// <summary>Lore or vanity item with no mechanical effect.</summary>
        Collectible
    }

    /// <summary>
    /// Ammunition category linked to a specific weapon class.
    /// </summary>
    public enum AmmoType
    {
        /// <summary>Not ammunition (melee, consumable, etc.).</summary>
        None,
        /// <summary>Standard stapler-staple rounds for Type-A weapons.</summary>
        Staple,
        /// <summary>Keycap projectiles for Type-A precision weapons.</summary>
        Keycap,
        /// <summary>Presentation-clicker rounds for Type-C weapons.</summary>
        PPT,
        /// <summary>Hot-coffee payload for Type-C area weapons.</summary>
        Coffee,
        /// <summary>Ceramic-mug canister for Type-C heavy weapons.</summary>
        Mug
    }

    /// <summary>
    /// Data definition for any item the player can acquire, carry, use, or sell.
    /// Create instances via Assets > Create > Data > Item.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Item", fileName = "NewItem")]
    public class ItemData : ScriptableObject
    {
        // ---- Identity -----------------------------------------------------------

        /// <summary>Display name shown in inventory and tooltips.</summary>
        [SerializeField] private string itemName = "New Item";

        /// <summary>Flavour text describing the item.</summary>
        [SerializeField] [TextArea(2, 4)] private string description;

        /// <summary>Broad item category.</summary>
        [SerializeField] private ItemType itemType;

        /// <summary>Ammunition type this item provides. Ignored unless itemType is Ammo.</summary>
        [SerializeField] private AmmoType ammoType = AmmoType.None;

        // ---- Visual -------------------------------------------------------------

        /// <summary>Inventory / HUD icon.</summary>
        [SerializeField] private Sprite icon;

        // ---- Stacking & Economy -------------------------------------------------

        /// <summary>Maximum number of copies that can occupy one inventory slot.</summary>
        [SerializeField] [Range(1, 999)] private int maxStackSize = 99;

        /// <summary>Sell price in 回形针 (paperclips) at base vendors.</summary>
        [SerializeField] [Range(0, 999999)] private int baseValue;

        // ---- Usage --------------------------------------------------------------

        /// <summary>Whether this item can be used or consumed while inside a raid.</summary>
        [SerializeField] private bool isUsableInRaid;

        // ---- Perishable ---------------------------------------------------------

        /// <summary>
        /// Real-time minutes before this item spoils.
        /// 0 means the item never expires.
        /// </summary>
        [SerializeField] [Range(0f, 10080f)]
        private float freshnessDurationMinutes;

        // ---- Public properties --------------------------------------------------

        public string ItemName => itemName;
        public string Description => description;
        public ItemType ItemType => itemType;
        public AmmoType AmmoType => ammoType;
        public Sprite Icon => icon;
        public int MaxStackSize => maxStackSize;
        public int BaseValue => baseValue;
        public bool IsUsableInRaid => isUsableInRaid;

        /// <summary>
        /// Real-time minutes before this item spoils.
        /// Returns 0 when the item never expires.
        /// </summary>
        public float FreshnessDurationMinutes => freshnessDurationMinutes;

        // ---- Convenience queries ------------------------------------------------

        /// <summary>True when this item supplies ammunition for a weapon.</summary>
        public bool IsAmmo => itemType == ItemType.Ammo && ammoType != AmmoType.None;

        /// <summary>True when this item will spoil if left too long outside raid.</summary>
        public bool IsPerishable => freshnessDurationMinutes > 0f;

        // ---- Validation ---------------------------------------------------------

        private void OnValidate()
        {
            // ammoType is meaningless for non-ammo items — keep it clean in the
            // Inspector so designers don't accidentally leave stale values.
            if (itemType != ItemType.Ammo)
            {
                ammoType = AmmoType.None;
            }
        }
    }
}
