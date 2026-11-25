using UnityEngine;

namespace EscapeFromWork.Gameplay
{
    /// Asset-free impact and muzzle effects for the greybox playtest.
    public static class ImpactFx
    {
        static Material _markMaterial;
        static Material _flashMaterial;

        static Material MarkMaterial()
        {
            if (_markMaterial != null) return _markMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _markMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _markMaterial.SetColor("_BaseColor", new Color(0.09f, 0.07f, 0.05f, 1f));
            _markMaterial.color = new Color(0.09f, 0.07f, 0.05f, 1f);
            return _markMaterial;
        }

        public static void Spawn(Vector3 point, Vector3 normal)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "Impact";
            Object.Destroy(mark.GetComponent<Collider>());
            mark.transform.position = point + normal * 0.012f;
            mark.transform.rotation = Quaternion.LookRotation(normal);
            mark.transform.localScale = new Vector3(0.07f, 0.07f, 0.02f);
            mark.GetComponent<Renderer>().sharedMaterial = MarkMaterial();
            Object.Destroy(mark, 6f);
        }

        public static void Muzzle(Vector3 position)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "MuzzleFlash";
            Object.Destroy(flash.GetComponent<Collider>());
            flash.transform.position = position;
            flash.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            flash.GetComponent<Renderer>().sharedMaterial = FlashMaterial();
            Object.Destroy(flash, 0.05f);
        }

        static Material FlashMaterial()
        {
            if (_flashMaterial != null) return _flashMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _flashMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _flashMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f, 1f));
            _flashMaterial.color = new Color(1f, 0.85f, 0.45f, 1f);
            return _flashMaterial;
        }
    }
}
