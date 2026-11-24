using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// Opens the whitebox scene and renders one 1920x1080 frame to docs for review.
    public static class M0Capture
    {
        [MenuItem("Escape From Work/M0/Capture Whitebox")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Whitebox.unity", OpenSceneMode.Single);
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[M0] main camera missing");
                return;
            }

            cam.transform.position = new Vector3(0f, 1.7f, -18f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

            const int width = 1920;
            const int height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/3d-rebuild/screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "m0-whitebox.png"), png);

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[M0] captured " + Path.Combine(dir, "m0-whitebox.png"));
        }
    }
}
