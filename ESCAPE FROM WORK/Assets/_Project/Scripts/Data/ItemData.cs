using UnityEngine;

namespace EscapeFromWork.Data
{
    /// <summary>Item rarity tier — 白绿蓝紫金红.</summary>
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

    /// <summary>Broad item category — maps to container loot tables and hideout upgrades.</summary>
    public enum ItemType
    {
        Currency,       // 回形针
        Ammo,           // 订书钉, 键帽, PPT页面, 咖啡豆, 马克杯
        Consumable,     // 速溶咖啡, 能量棒, 创可贴
        Construction,   // 建材: 木板, 金属板, 螺丝, 电线
        Electronics,    // U盘, 硬盘, CPU, 显卡
        OfficeSupply,   // 打印纸, 墨盒, 文件夹
        Luxury,         // 奢侈品: 比特币, 钢笔, 手办
        Intel,          // 情报: 人事档案, 财务报告
        KeyItem,        // 任务/进度道具: 工牌, 门禁卡
        Collectible     // 纯收藏: 名画, 奖杯
    }

    /// <summary>Container type — determines grid size and loot table category.</summary>
    public enum ContainerType
    {
        Desk, FilingCabinet, Safe, SupplyCloset, ServerRack, CEODesk
    }

    /// <summary>Gear slot type for equippable items.</summary>
    public enum GearSlot
    {
        None,       // regular item
        WeaponA,    // 主武器
        WeaponC,    // 特殊武器
        Melee,      // 近战武器
        Armor,      // 护甲
        Backpack    // 背包
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
        /// <summary>Presentation-clicker rounds (PPT pages).</summary>
        PPT,
        /// <summary>Hot-coffee payload. Also used for CoffeeInjector self-buff.</summary>
        Coffee,
        /// <summary>Ceramic-mug canister for Type-A AOE weapon.</summary>
        Mug,
        /// <summary>Projector bulb — continuous beam weapon ammo (投影仪射线枪).</summary>
        BulbLife,
        /// <summary>Meeting invite link — root/stun ammo (会议邀请法杖).</summary>
        MeetingLink,
        /// <summary>Junk e-mail payload — delayed explosive ammo (邮件炸弹).</summary>
        JunkMail
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

        // ---- Equipment -----------------------------------------------------------

        /// <summary>If this item is gear, which slot it equips to.</summary>
        [SerializeField] private GearSlot gearSlot = GearSlot.None;

        /// <summary>For backpack items: internal grid width (0 if not a backpack).</summary>
        [SerializeField] [Range(0, 10)] private int backpackWidth;

        /// <summary>For backpack items: internal grid height (0 if not a backpack).</summary>
        [SerializeField] [Range(0, 8)] private int backpackHeight;

        // ---- Grid Size -----------------------------------------------------------

        /// <summary>Grid cells this item occupies horizontally (1-8).</summary>
        [SerializeField] [Range(1, 8)] private int width = 1;

        /// <summary>Grid cells this item occupies vertically (1-8).</summary>
        [SerializeField] [Range(1, 8)] private int height = 1;

        // ---- Visual -------------------------------------------------------------

        /// <summary>Inventory / HUD icon.</summary>
        [SerializeField] private Sprite icon;

        // ---- Rarity & Economy ----------------------------------------------------

        /// <summary>Rarity tier — affects drop rate and sale value.</summary>
        [SerializeField] private Rarity rarity = Rarity.Common;

        /// <summary>Maximum number of copies that can occupy one inventory slot (1×1 items only).</summary>
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
        public int Width => width;
        public int Height => height;
        public Rarity Rarity => rarity;
        public GearSlot GearSlot => gearSlot;
        public int BackpackWidth => backpackWidth;
        public int BackpackHeight => backpackHeight;
        public bool IsBackpack => gearSlot == GearSlot.Backpack && backpackWidth > 0 && backpackHeight > 0;
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
