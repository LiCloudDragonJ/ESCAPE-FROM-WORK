using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Import settings for the AI generated material texture sets under
    /// Assets/_Project/Art/Textures. Albedo is sRGB and tiling; normal and mask stay
    /// linear so the derived PBR data is not gamma mangled.
    /// </summary>
    public sealed class MaterialArtTextureImporter : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Art/Textures/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            bool normal = assetPath.EndsWith("_Normal.png");
            bool mask = assetPath.EndsWith("_Mask.png");
            if (normal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else if (mask)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }
        }
    }
}
