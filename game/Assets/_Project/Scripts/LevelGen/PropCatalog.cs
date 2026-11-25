using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Maps a generated prop name ("办公桌", "双柜组" …) to a real prefab. The generator only
    /// ever emits name + footprint, so the greybox and the art pass share one interface: fill
    /// this asset and every placement of that name becomes the prefab, no generator change.
    /// A name with no entry keeps the merged greybox box.
    /// </summary>
    [CreateAssetMenu(menuName = "Escape From Work/Prop Catalog", fileName = "PropCatalog")]
    public class PropCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("必须与 PropSpec.Name 完全一致，例如 办公桌 / 双柜组 / 沙发")]
            public string Name = string.Empty;

            [Tooltip("Prefab：pivot 在底面中心，正面朝 +Z，1 unit = 1 m")]
            public GameObject Prefab;

            [Tooltip("期望占地的 宽/高/深（米）。与预制体渲染包围盒不符时打警告，但不缩放")]
            public Vector3 Size = new Vector3(1f, 1f, 1f);

            [Tooltip("沿 +Z 的前后偏移与高度微调（米）")]
            public Vector3 Offset = Vector3.zero;

            [Tooltip("预制体自带碰撞体时勾掉，避免叠一层 BoxCollider")]
            public bool AddBoxCollider = true;
        }

        public List<Entry> Entries = new List<Entry>();

        public bool TryGet(string name, out Entry entry)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && Entries[i].Prefab != null && Entries[i].Name == name)
                {
                    entry = Entries[i];
                    return true;
                }
            }
            entry = null;
            return false;
        }

        /// <summary>Create the default catalog asset next to the other data assets (editor only).</summary>
        public static PropCatalog LoadOrCreateDefault()
        {
#if UNITY_EDITOR
            const string path = "Assets/_Project/Data/PropCatalog.asset";
            PropCatalog asset = UnityEditor.AssetDatabase.LoadAssetAtPath<PropCatalog>(path);
            if (asset != null) return asset;

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Project", "Data");

            asset = CreateInstance<PropCatalog>();
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return asset;
#else
            return null;
#endif
        }
    }

    /// <summary>Instantiates catalog prefabs for placed props; the fallback box path stays in geometry.</summary>
    public static class PropSpawner
    {
        /// <summary>Returns true when the prop was spawned as a prefab (caller then skips the greybox box).</summary>
        public static bool TrySpawn(PropCatalog catalog, PropPlacement p, Transform parent)
        {
            if (catalog == null) return false;
            PropCatalog.Entry entry;
            if (!catalog.TryGet(p.Spec.Name, out entry)) return false;

            bool alongZ = p.Rect.H > p.Rect.W;
            Vector3 position = new Vector3(
                p.Rect.Center.x + (alongZ ? 0f : entry.Offset.x),
                entry.Offset.y,
                p.Rect.Center.y + (alongZ ? entry.Offset.z : entry.Offset.x));
            Quaternion rotation = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);

            GameObject go = UnityEngine.Object.Instantiate(entry.Prefab, position, rotation, parent);
            go.name = "Prop_" + entry.Name;

            if (entry.AddBoxCollider && go.GetComponentInChildren<Collider>() == null)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, entry.Size.y * 0.5f, 0f);
                box.size = entry.Size;
            }
            return true;
        }
    }
}
