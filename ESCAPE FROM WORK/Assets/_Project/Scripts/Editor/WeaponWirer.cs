using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;
using EscapeFromWork.Data;
using EscapeFromWork.Enemies;

public static class WeaponWirer
{
    [MenuItem("ESCAPE FROM WORK/Wire Weapons")]
    public static void Wire()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("❌ Player not found — run Build Scene first"); return; }

        // Create projectile prefab
        var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Projectile.prefab");
        if (projPrefab == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tmp.name = "Projectile";
            tmp.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            tmp.AddComponent<Projectile>();
            tmp.GetComponent<Collider>().isTrigger = true;
            var rb = tmp.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            projPrefab = PrefabUtility.SaveAsPrefabAsset(tmp, "Assets/_Project/Prefabs/Weapons/Projectile.prefab");
            Object.DestroyImmediate(tmp);
        }

        // Create weapons and attach to player
        var pistolGo = new GameObject("Weapon_StaplerPistol");
        pistolGo.transform.SetParent(player.transform, false);
        var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(pistolGo.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f); // front of weapon
        var pistol = pistolGo.AddComponent<RangedWeapon>();
        var pistolSO = new SerializedObject(pistol);
        pistolSO.FindProperty("data").objectReferenceValue =
            GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_StaplerPistol.asset");
        pistolSO.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
        pistolSO.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
        pistolSO.ApplyModifiedProperties();

        var meleeGo = new GameObject("Weapon_KeyboardMelee");
        meleeGo.transform.SetParent(player.transform, false);
        var melee = meleeGo.AddComponent<MeleeWeapon>();
        var meleeSO = new SerializedObject(melee);
        meleeSO.FindProperty("data").objectReferenceValue =
            GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_KeyboardMelee.asset");
        meleeSO.ApplyModifiedProperties();

        // Wire inventory
        var inv = player.GetComponent<PlayerInventory>();
        var invSO = new SerializedObject(inv);
        var eq = invSO.FindProperty("equippedWeapons");
        eq.ClearArray();
        eq.InsertArrayElementAtIndex(0); eq.GetArrayElementAtIndex(0).objectReferenceValue = pistol;
        eq.InsertArrayElementAtIndex(1); eq.GetArrayElementAtIndex(1).objectReferenceValue = null; // C weapon not yet
        eq.InsertArrayElementAtIndex(2); eq.GetArrayElementAtIndex(2).objectReferenceValue = melee;
        invSO.ApplyModifiedProperties();

        // Wire enemy data
        var enemyData = GetOrCreateSO<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/SO_Enemy_KPIZombie.asset");
        var enemyHolder = GameObject.Find("--- Enemies ---");
        if (enemyHolder != null && enemyData != null)
        {
            foreach (var e in enemyHolder.GetComponentsInChildren<KPIZombie>())
            {
                var eso = new SerializedObject(e);
                eso.FindProperty("data").objectReferenceValue = enemyData;
                eso.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("✅ Weapons wired — Pistol, Melee, Projectile, EnemyData. Press Play!");
    }

    static T GetOrCreateSO<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var so = ScriptableObject.CreateInstance<T>();
        so.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(so, path);
        return so;
    }
}
