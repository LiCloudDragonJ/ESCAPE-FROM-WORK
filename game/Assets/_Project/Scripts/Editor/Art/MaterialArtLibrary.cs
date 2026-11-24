using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Builds URP Lit materials from the AI generated texture sets
    /// (Art/Textures/&lt;Set&gt;/T_&lt;Set&gt;_{Albedo,Normal,Mask}.png) into
    /// Materials/Art/&lt;Slot&gt;.mat. FloorMaterials slots are mapped to a texture set
    /// here, so the procedural builder keeps its colours when textures are absent.
    /// </summary>
    public static class MaterialArtLibrary
    {
        public const string TextureRoot = "Assets/_Project/Art/Textures";
        public const string MaterialRoot = "Assets/_Project/Materials/Art";

        // FloorMaterials slot -> texture set name under Art/Textures.
        static readonly string[,] Slots =
        {
            { "Wall", "PaintedWall" },
            { "Floor", "CarpetTile" },
            { "Ceiling", "CeilingTile" },
            { "Column", "ConcreteFloor" },
            { "Core", "DarkMetal" },
            { "Stair", "ConcreteFloor" },
            { "PropLarge", "WoodPanel" },
            { "PropMedium", "DarkMetal" },
            { "PropSmall", "BrushedMetal" },
            { "SightBreak", "PaintedWall" },
        };

        // Texture repeat per metre of the texture set. One uv unit is one metre.
        static float TilingFor(string set)
        {
            switch (set)
            {
                case "CarpetTile": return 2.0f;      // 0.5 m carpet tiles
                case "CeilingTile": return 1.667f;   // 0.6 m mineral fibre panels
                case "BrushedMetal": return 1.667f;  // 0.6 m brush panels
                case "ConcreteFloor": return 0.667f; // 1.5 m pour
                case "PaintedWall": return 0.667f;   // 1.5 m wall patch
                case "DarkMetal": return 1.0f;
                case "WoodPanel": return 1.0f;
                default: return 1.0f;
            }
        }

        public static string SetFor(string slot)
        {
            for (int i = 0; i < Slots.GetLength(0); i++)
                if (Slots[i, 0] == slot) return Slots[i, 1];
            return null;
        }

        /// <summary>Returns the built material or null when no texture set exists.</summary>
        public static Material TryLoad(string slot)
        {
            string set = SetFor(slot);
            if (string.IsNullOrEmpty(set)) return null;

            string matPath = MaterialRoot + "/" + slot + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;
            return Build(slot, set);
        }

        [MenuItem("Escape From Work/Art/Build Material Library From Textures")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/_Project", "Materials");
            EnsureFolder("Assets/_Project/Materials", "Art");

            int built = 0;
            for (int i = 0; i < Slots.GetLength(0); i++)
            {
                Material m = Build(Slots[i, 0], Slots[i, 1]);
                if (m != null) built++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Art] material library: " + built + " / " + Slots.GetLength(0) + " slots built");
        }

        [MenuItem("Escape From Work/Art/Rebuild Art Materials And Scenes")]
        public static void RebuildAll()
        {
            BuildAll();
            M1FloorScene.BuildStreaming();
            M1FloorScene.Build();
            M1FloorScene.Capture();
        }

        static Material Build(string slot, string set)
        {
            string albedoPath = TextureRoot + "/" + set + "/T_" + set + "_Albedo.png";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo == null)
            {
                Debug.LogWarning("[Art] missing albedo for " + set + " (" + albedoPath + ")");
                return null;
            }

            EnsureFolder("Assets/_Project", "Materials");
            EnsureFolder("Assets/_Project/Materials", "Art");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[Art] no lit shader found, cannot build " + slot);
                return null;
            }

            string matPath = MaterialRoot + "/" + slot + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m != null && (m.shader == null || m.shader != shader))
            {
                // A material saved while the lit shader was unresolved ends up on the
                // internal error shader; recreate it instead of patching.
                AssetDatabase.DeleteAsset(matPath);
                m = null;
            }
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, matPath);
            }

            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_MainTex", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureRoot + "/" + set + "/T_" + set + "_Normal.png");
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }

            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureRoot + "/" + set + "/T_" + set + "_Mask.png");
            if (mask != null)
            {
                // R = metallic, G = ao, A = smoothness (URP Lit metallic/gloss layout).
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }

            float t = TilingFor(set);
            m.SetTextureScale("_BaseMap", new Vector2(t, t));
            m.SetTextureScale("_MainTex", new Vector2(t, t));
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(m);
            Debug.Log("[Art] material " + matPath + " <- " + set + " tiling " + t);
            return m;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
