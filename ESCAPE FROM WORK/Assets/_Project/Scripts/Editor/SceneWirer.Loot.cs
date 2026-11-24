using UnityEngine;
using UnityEditor;
using EscapeFromWork.Data;
using EscapeFromWork.Loot;

/// <summary>
/// Loot-table and item-creation methods for the one-click scene builder.
/// </summary>
public static partial class SceneWirer
{
    /// <summary>Create all ItemData SOs and wire LootTables to containers.</summary>
    static void WireLootTables()
    {
        // ==== 办公消耗品 (Common) ====
        var pp  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Paperclip.asset", "回形针", ItemType.Currency, Rarity.Common, 1, 999, 1);
        var pr  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PrinterPaper.asset", "打印纸", ItemType.OfficeSupply, Rarity.Common, 1, 200, 2);
        var ink = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_InkCartridge.asset", "墨盒", ItemType.OfficeSupply, Rarity.Common, 1, 30, 8);
        var cf  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CoffeeBean.asset", "咖啡豆", ItemType.Consumable, Rarity.Common, 1, 20, 5);
        var ban = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_EnergyBar.asset", "能量棒", ItemType.Consumable, Rarity.Common, 1, 10, 10);
        var tap = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_StickyNotes.asset", "便利贴", ItemType.OfficeSupply, Rarity.Common, 1, 50, 2);

        // ==== 建材 (Common/Uncommon) ====
        var scr = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Screws.asset", "螺丝钉", ItemType.Construction, Rarity.Common, 1, 50, 3);
        var wr  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Wire.asset", "铜芯电线", ItemType.Construction, Rarity.Common, 1, 30, 5);
        var mtl = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_MetalSheet.asset", "金属薄板", ItemType.Construction, Rarity.Uncommon, 2, 15, 15);
        var brd = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CircuitBoard.asset", "电路板", ItemType.Construction, Rarity.Uncommon, 1, 10, 20);

        // ==== 电子产品 (Uncommon/Rare) ====
        var usb = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_USB.asset", "加密U盘", ItemType.Electronics, Rarity.Uncommon, 1, 5, 500);
        var hdd = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_HardDrive.asset", "SSD硬盘", ItemType.Electronics, Rarity.Uncommon, 2, 3, 800);
        var cpu = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CPU.asset", "CPU处理器", ItemType.Electronics, Rarity.Rare, 1, 2, 2000);
        var gpu = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_GPU.asset", "未拆封显卡", ItemType.Electronics, Rarity.Epic, 2, 1, 8000);

        // ==== 奢侈品 (Epic/Legendary) ====
        var pen = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CEOPen.asset", "CEO签名钢笔", ItemType.Luxury, Rarity.Epic, 1, 1, 12000);
        var fig = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Figurine.asset", "绝版手办(未拆)", ItemType.Luxury, Rarity.Legendary, 2, 1, 25000);
        var whi = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Whiskey.asset", "珍藏威士忌", ItemType.Luxury, Rarity.Epic, 1, 1, 15000);

        // ==== 情报 (Uncommon/Rare) ====
        var fil = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PersonnelFile.asset", "人事档案", ItemType.Intel, Rarity.Uncommon, 1, 1, 300);
        var fin = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_FinReport.asset", "财务报告(机密)", ItemType.Intel, Rarity.Rare, 1, 1, 1200);
        var rd  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_RnDNotes.asset", "研发笔记", ItemType.Intel, Rarity.Rare, 1, 1, 1500);

        // ==== 收藏品 - "大货" (Epic/Legendary) ====
        var pto = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PaidLeave.asset", "带薪年假批准函", ItemType.Collectible, Rarity.Legendary, 2, 1, 50000);
        var bns = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_BonusCheck.asset", "年终奖确认函", ItemType.Collectible, Rarity.Legendary, 2, 1, 45000);
        var pro = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Promotion.asset", "升职推荐信", ItemType.Collectible, Rarity.Epic, 1, 1, 20000);
        var trp = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Trophy.asset", "年度优秀员工奖杯", ItemType.Collectible, Rarity.Epic, 2, 1, 18000);
        var key = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_MasterKey.asset", "万能门禁卡", ItemType.KeyItem, Rarity.Epic, 1, 1, 0);
        var stk = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_StockOption.asset", "股权期权证书", ItemType.Collectible, Rarity.Legendary, 1, 1, 60000);

        // ==== 装备：背包 (GearSlot.Backpack) ====
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Small.asset", "帆布背包", ItemType.OfficeSupply, Rarity.Common, 2, 1, GearSlot.Backpack, 4, 3, 50);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Medium.asset", "电脑双肩包", ItemType.OfficeSupply, Rarity.Uncommon, 3, 1, GearSlot.Backpack, 6, 4, 200);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Large.asset", "商务旅行箱", ItemType.OfficeSupply, Rarity.Rare, 3, 2, GearSlot.Backpack, 8, 6, 800);

        // ==== 装备：护甲 (GearSlot.Armor) ====
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Vest.asset", "工装马甲", ItemType.OfficeSupply, Rarity.Common, 2, 1, GearSlot.Armor, 0, 0, 100);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Jacket.asset", "防刺夹克", ItemType.OfficeSupply, Rarity.Uncommon, 3, 1, GearSlot.Armor, 0, 0, 400);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Suit.asset", "CEO防弹西装", ItemType.OfficeSupply, Rarity.Rare, 3, 2, GearSlot.Armor, 0, 0, 1500);

        // Build loot tables per container type
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Desk.asset", new (string, float, int, int)[] {
            (pp, 0.35f, 3, 15), (pr, 0.2f, 2, 8), (tap, 0.15f, 1, 5), (cf, 0.1f, 1, 3), (usb, 0.08f, 1, 2), (scr, 0.05f, 1, 3), (ban, 0.07f, 1, 2)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Cabinet.asset", new (string, float, int, int)[] {
            (pr, 0.3f, 5, 20), (fil, 0.2f, 1, 2), (ink, 0.15f, 2, 5), (wr, 0.1f, 1, 3), (fin, 0.08f, 1, 1), (rd, 0.07f, 1, 1), (usb, 0.1f, 1, 2)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Supply.asset", new (string, float, int, int)[] {
            (scr, 0.25f, 3, 10), (wr, 0.2f, 2, 6), (mtl, 0.15f, 1, 3), (brd, 0.1f, 1, 2), (cf, 0.15f, 2, 5), (ban, 0.15f, 2, 4)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Safe.asset", new (string, float, int, int)[] {
            (pp, 0.25f, 50, 200), (pto, 0.01f, 1, 1), (bns, 0.01f, 1, 1), (stk, 0.005f, 1, 1), (pen, 0.1f, 1, 1), (pro, 0.08f, 1, 1), (fig, 0.04f, 1, 1), (whi, 0.1f, 1, 1), (cpu, 0.15f, 1, 1), (usb, 0.25f, 1, 3)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Server.asset", new (string, float, int, int)[] {
            (usb, 0.3f, 1, 3), (hdd, 0.2f, 1, 2), (cpu, 0.15f, 1, 1), (gpu, 0.1f, 1, 1), (brd, 0.15f, 1, 3), (wr, 0.1f, 1, 3)
        });

        // Assign loot tables to containers by type
        var containerMap = new System.Collections.Generic.Dictionary<ContainerType, string> {
            {ContainerType.Desk, "Assets/_Project/ScriptableObjects/SO_Loot_Desk.asset"},
            {ContainerType.FilingCabinet, "Assets/_Project/ScriptableObjects/SO_Loot_Cabinet.asset"},
            {ContainerType.SupplyCloset, "Assets/_Project/ScriptableObjects/SO_Loot_Supply.asset"},
            {ContainerType.Safe, "Assets/_Project/ScriptableObjects/SO_Loot_Safe.asset"},
            {ContainerType.ServerRack, "Assets/_Project/ScriptableObjects/SO_Loot_Server.asset"},
        };
        foreach (var lc in Object.FindObjectsOfType<LootContainer>())
        {
            var so = new SerializedObject(lc);
            var cType = (ContainerType)so.FindProperty("containerType").enumValueIndex;
            containerMap.TryGetValue(cType, out string path);
            if (path != null)
                so.FindProperty("lootTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            so.ApplyModifiedProperties();
        }
        Debug.Log("Loot tables wired per container type");
    }

    static string MkGear(string path, string name, ItemType type, Rarity rarity, int w, int h, GearSlot slot, int bpW, int bpH, int value)
    {
        var p = MkItem(path, name, type, rarity, w, 1, value);
        var so = AssetDatabase.LoadAssetAtPath<ItemData>(p);
        var sso = new SerializedObject(so);
        sso.FindProperty("height").intValue = h;
        sso.FindProperty("gearSlot").enumValueIndex = (int)slot;
        sso.FindProperty("backpackWidth").intValue = bpW;
        sso.FindProperty("backpackHeight").intValue = bpH;
        sso.ApplyModifiedProperties(); EditorUtility.SetDirty(so);
        return p;
    }

    static string MkItem(string path, string name, ItemType type, Rarity rarity, int w, int maxStack, int value)
    {
        var so = AssetDatabase.LoadAssetAtPath<ItemData>(path) ?? ScriptableObject.CreateInstance<ItemData>();
        if (AssetDatabase.LoadAssetAtPath<ItemData>(path) == null) { so.name = System.IO.Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(so, path); }
        var sso = new SerializedObject(so);
        sso.FindProperty("itemName").stringValue = name;
        sso.FindProperty("itemType").enumValueIndex = (int)type;
        sso.FindProperty("rarity").enumValueIndex = (int)rarity;
        sso.FindProperty("width").intValue = w;
        sso.FindProperty("height").intValue = 1;
        if (w >= 2 && (rarity >= Rarity.Epic || type == ItemType.Collectible)) sso.FindProperty("height").intValue = 2;
        sso.FindProperty("maxStackSize").intValue = maxStack;
        sso.FindProperty("baseValue").intValue = value;
        sso.ApplyModifiedProperties();
        EditorUtility.SetDirty(so);
        return path;
    }

    static void BuildLootTable(string path, (string itemPath, float weight, int min, int max)[] entries)
    {
        var lt = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (lt == null)
        {
            lt = ScriptableObject.CreateInstance<LootTable>();
            lt.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(lt, path);
        }
        var so = new SerializedObject(lt);
        so.FindProperty("minRolls").intValue = 2;
        so.FindProperty("maxRolls").intValue = 5;
        var e = so.FindProperty("entries");
        e.ClearArray();
        foreach (var (ip, w, min, max) in entries)
            AddLootEntry(e, ip, w, min, max);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(lt);
    }

    static void AddLootEntry(SerializedProperty entriesProp, string itemPath, float weight, int minCount, int maxCount)
    {
        int idx = entriesProp.arraySize;
        entriesProp.InsertArrayElementAtIndex(idx);
        var entry = entriesProp.GetArrayElementAtIndex(idx);
        entry.FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("minCount").intValue = minCount;
        entry.FindPropertyRelative("maxCount").intValue = maxCount;
    }
}
