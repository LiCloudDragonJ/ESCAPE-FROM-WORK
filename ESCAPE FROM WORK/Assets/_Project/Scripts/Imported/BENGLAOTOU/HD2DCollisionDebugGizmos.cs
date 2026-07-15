using UnityEngine;

[DisallowMultipleComponent]
public class HD2DCollisionDebugGizmos : MonoBehaviour
{
    [Header("Draw")]
    public bool drawCapsule = true;
    public bool drawGroundClearance = true;
    public Color capsuleColor = new Color(0.20f, 0.85f, 1.00f, 0.75f);
    public Color clearanceColor = new Color(1.00f, 0.85f, 0.20f, 0.60f);
    [Range(8, 128)] public int clearanceSegments = 48;

    private void OnDrawGizmosSelected()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 pos = transform.position;

        if (drawCapsule)
        {
            Gizmos.color = capsuleColor;
            Vector3 center = pos + cc.center;
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            Vector3 bottom = center - Vector3.up * half;
            Vector3 top    = center + Vector3.up * half;

            Gizmos.DrawWireSphere(bottom, cc.radius);
            Gizmos.DrawWireSphere(top,    cc.radius);
            Gizmos.DrawLine(bottom + Vector3.right   * cc.radius, top + Vector3.right   * cc.radius);
            Gizmos.DrawLine(bottom - Vector3.right   * cc.radius, top - Vector3.right   * cc.radius);
            Gizmos.DrawLine(bottom + Vector3.forward * cc.radius, top + Vector3.forward * cc.radius);
            Gizmos.DrawLine(bottom - Vector3.forward * cc.radius, top - Vector3.forward * cc.radius);
        }

        if (drawGroundClearance)
        {
            Gizmos.color = clearanceColor;
            Vector3 ringCenter = new Vector3(pos.x, pos.y, pos.z);
            float r = cc.radius;
            Vector3 prev = ringCenter + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= clearanceSegments; i++)
            {
                float a = (i / (float)clearanceSegments) * Mathf.PI * 2f;
                Vector3 next = ringCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
