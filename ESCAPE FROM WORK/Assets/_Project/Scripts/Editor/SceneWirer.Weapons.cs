using UnityEngine;
using UnityEditor;
using EscapeFromWork.Core;
using EscapeFromWork.Data;
using EscapeFromWork.Enemies;
using EscapeFromWork.Loot;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;

/// <summary>
/// Weapon wiring, extraction-point placement, and loose-loot spawning
/// for the one-click scene builder.
/// </summary>
public static partial class SceneWirer
{
    /// <summary>
    /// Create weapon prefabs, wire them to PlayerInventory, and wire
    /// enemy data + loot tables.
    /// </summary>
    static void WireWeapons()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogWarning("Player not found — skipping weapon wiring"); return; }

        var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Projectile.prefab");
        if (projPrefab == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tmp.name = "Projectile"; tmp.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            tmp.AddComponent<Projectile>();
            tmp.GetComponent<Collider>().isTrigger = true;
            var rb = tmp.AddComponent<Rigidbody>(); rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            projPrefab = PrefabUtility.SaveAsPrefabAsset(tmp, "Assets/_Project/Prefabs/Weapons/Projectile.prefab");
            UnityEngine.Object.DestroyImmediate(tmp);
        }

        var pistol = player.transform.Find("Weapon_StaplerPistol")?.GetComponent<RangedWeapon>();
        if (pistol == null)
        {
            var go = new GameObject("Weapon_StaplerPistol"); go.transform.SetParent(player.transform, false);
            var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = new Vector3(0, 0, 0.5f);
            pistol = go.AddComponent<RangedWeapon>();
            var so = new SerializedObject(pistol);
            so.FindProperty("data").objectReferenceValue = GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_StaplerPistol.asset");
            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
            so.ApplyModifiedProperties();
        }

        var melee = player.transform.Find("Weapon_KeyboardMelee")?.GetComponent<MeleeWeapon>();
        if (melee == null)
        {
            var go = new GameObject("Weapon_KeyboardMelee"); go.transform.SetParent(player.transform, false);
            melee = go.AddComponent<MeleeWeapon>();
            var so = new SerializedObject(melee);
            so.FindProperty("data").objectReferenceValue = GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_KeyboardMelee.asset");
            so.ApplyModifiedProperties();
        }

        var inv = player.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            var so = new SerializedObject(inv);
            var eq = so.FindProperty("equippedWeapons");
            eq.ClearArray();
            eq.InsertArrayElementAtIndex(0); eq.GetArrayElementAtIndex(0).objectReferenceValue = pistol;
            eq.InsertArrayElementAtIndex(1); eq.GetArrayElementAtIndex(1).objectReferenceValue = null;
            eq.InsertArrayElementAtIndex(2); eq.GetArrayElementAtIndex(2).objectReferenceValue = melee;
            so.ApplyModifiedProperties();
        }

        var enemyData = GetOrCreateSO<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/SO_Enemy_KPIZombie.asset");
        var enemyHolder = GameObject.Find("--- Enemies ---");
        if (enemyHolder != null)
            foreach (var e in enemyHolder.GetComponentsInChildren<KPIZombie>())
                new SerializedObject(e).FindProperty("data").objectReferenceValue = enemyData;

        // Wire loot types and tables
        WireLootTables();

        // Give player initial small backpack
        var initBp = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Small.asset");
        var playerCombat = Object.FindObjectOfType<PlayerCombat>();
        if (playerCombat != null && initBp != null && playerCombat.SlotBackpack == null)
            playerCombat.SetSlotItem(GearSlot.Backpack, initBp);

        Debug.Log("Weapons + LootTable wired");
    }

    /// <summary>
    /// Create a cylindrical extraction-trigger zone at the given position.
    /// </summary>
    static void AddExtractionPoint(string name, Vector3 pos, bool fireEscape, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(3f, 0.05f, 3f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.tag = "Respawn";
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        var et = go.AddComponent<ExtractionTrigger>();
        var etSO = new SerializedObject(et);
        etSO.FindProperty("useFireEscape").boolValue = fireEscape;
        etSO.ApplyModifiedProperties();
    }

    /// <summary>
    /// Spawn loose-loot valuables near pre-defined furniture positions.
    /// Only large / valuable items appear loose in the world.
    /// </summary>
    static void BuildLooseLoot(Material mat)
    {
        var looseHolder = new GameObject("--- Loose Loot ---");
        string[] looseItems = {
            "Assets/_Project/ScriptableObjects/Items/SO_Item_GPU.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Figurine.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Whiskey.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_PaidLeave.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Trophy.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_StockOption.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_BonusCheck.asset",
        };
        Vector3[] furnitureSpots = {
            new(15,0.4f,15), new(35,0.4f,15), new(55,0.4f,15), new(75,0.4f,15),
            new(15,0.4f,45), new(35,0.4f,45), new(85,0.4f,35), new(60,0.4f,65),
            new(25,0.4f,75), new(45,0.4f,85), new(80,0.4f,80), new(20,0.4f,85),
        };
        for (int i = 0; i < 8; i++)
        {
            var itemSO = AssetDatabase.LoadAssetAtPath<ItemData>(looseItems[Random.Range(0, looseItems.Length)]);
            if (itemSO == null) continue;
            var pos = furnitureSpots[i] + new Vector3(Random.Range(-3f,3f), 0, Random.Range(-3f,3f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Loose_{itemSO.ItemName}";
            go.tag = "Loot";
            go.transform.parent = looseHolder.transform;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(itemSO.Width * 0.5f, 0.3f, (itemSO.Height > 1 ? itemSO.Height : 1) * 0.5f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var trigger = new GameObject("Trigger").AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 2f;
            trigger.transform.SetParent(go.transform, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(mat) { color = itemSO.Rarity switch {
                Rarity.Legendary => new Color(1f, 0.6f, 0f), Rarity.Epic => new Color(0.7f, 0.3f, 1f),
                Rarity.Rare => new Color(0.2f, 0.6f, 1f), _ => new Color(0.3f, 0.8f, 0.3f)
            }};
            var pickup = go.AddComponent<PickupItem>();
            pickup.Initialize(itemSO, 1);
            go.AddComponent<LooseLootBob>();
        }
        Debug.Log("Loose loot: 8 big valuables near furniture");
    }
}
