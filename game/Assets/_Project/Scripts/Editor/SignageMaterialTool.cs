using System.IO;
using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Unity 打包时会把只被 Shader.Find 引用的 shader 剥离掉，SignageRenderer 在 player 里
    /// 于是找不到 Unlit/Transparent（InvalidOperationException: Signage requires ...）。
    /// 这里生成一张共享材质放在 Resources 下，运行时代码用 Resources.Load 拿到它，
    /// 打包器看到 Resources 里的直接引用就不会剥 shader。
    /// </summary>
    public static class SignageMaterialTool
    {
        public const string ResourceDir = "Assets/_Project/Resources";
        public const string AssetPath = ResourceDir + "/SignageUnlit.mat";

        [MenuItem("Escape From Work/Art/Rebuild Signage Material")]
        public static void CreateSignageMaterial()
        {
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogError("[Signage] Unlit/Transparent shader not found; cannot build the resource material.");
                return;
            }

            if (!Directory.Exists(ResourceDir)) Directory.CreateDirectory(ResourceDir);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            if (existing == null)
            {
                Material mat = new Material(shader);
                mat.name = "SignageUnlit";
                AssetDatabase.CreateAsset(mat, AssetPath);
                existing = mat;
                Debug.Log("[Signage] resource material created -> " + AssetPath);
            }
            else
            {
                existing.shader = shader;
                EditorUtility.SetDirty(existing);
                Debug.Log("[Signage] resource material refreshed -> " + AssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
