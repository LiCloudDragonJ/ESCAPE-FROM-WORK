using UnityEngine;

[DisallowMultipleComponent]
public class HD2DVisualClearanceProbe : MonoBehaviour
{
    [Header("References")]
    public CharacterController characterController;
    public Transform playerVisual;
    public Camera targetCamera;

    [Header("Blocking Mask")]
    public LayerMask blockingMask;

    [Header("Sprite Probe Shape")]
    public Vector2 spriteWorldSize = new Vector2(2.67f, 2.67f);
    public float probeThickness    = 0.12f;
    public float clearancePadding  = 0.05f;
    public float probeWidthScale   = 0.75f;
    public float probeHeightScale  = 0.85f;
    public bool  useBoxCast        = true;

    [Header("Debug")]
    public bool  drawDebugGizmos   = true;

    private int _layerPlayer     = -1;
    private int _layerVisual     = -1;
    private int _layerGround     = -1;
    private int _layerProbe      = -1;

    private void Reset()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
    }

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        CacheLayers();
    }

    private void OnEnable()
    {
        CacheLayers();
    }

    private void CacheLayers()
    {
        _layerPlayer = LayerMask.NameToLayer("Player");
        _layerVisual = LayerMask.NameToLayer("PlayerVisual");
        _layerGround = LayerMask.NameToLayer("Ground");
        _layerProbe  = LayerMask.NameToLayer("VisualClearanceProbe");
    }

    private Camera ResolveCamera()
    {
        return targetCamera != null ? targetCamera : Camera.main;
    }

    private Quaternion GetProbeRotation()
    {
        if (playerVisual != null) return playerVisual.rotation;
        var cam = ResolveCamera();
        return cam != null ? cam.transform.rotation : Quaternion.identity;
    }

    private void ComputeProbeAt(Vector3 feetPosition, out Vector3 center, out Vector3 halfExtents, out Quaternion rot)
    {
        rot = GetProbeRotation();
        Vector3 visualUp = rot * Vector3.up;
        float halfWidth     = spriteWorldSize.x * probeWidthScale  * 0.5f + clearancePadding;
        float halfHeight    = spriteWorldSize.y * probeHeightScale * 0.5f + clearancePadding;
        float halfThickness = probeThickness * 0.5f;
        halfExtents = new Vector3(halfWidth, halfHeight, halfThickness);
        center = feetPosition + visualUp * halfHeight;
    }

    public bool IsVisualClearAt(Vector3 candidatePlayerPosition)
    {
        ComputeProbeAt(candidatePlayerPosition, out Vector3 center, out Vector3 halfExtents, out Quaternion rot);
        var hits = Physics.OverlapBox(center, halfExtents, rot, blockingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
            if (IsColliderValid(hits[i])) return false;
        return true;
    }

    public bool WouldVisualSweepHit(Vector3 currentPlayerPosition, Vector3 candidatePlayerPosition)
    {
        if (!useBoxCast) return false;

        ComputeProbeAt(currentPlayerPosition,   out Vector3 startCenter, out Vector3 halfExtents, out Quaternion rot);
        ComputeProbeAt(candidatePlayerPosition, out Vector3 endCenter,   out _,                    out _);

        Vector3 dir = endCenter - startCenter;
        float dist = dir.magnitude;
        if (dist < 1e-4f) return false;
        dir /= dist;

        var hits = Physics.BoxCastAll(startCenter, halfExtents, dir, rot, dist, blockingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
            if (IsColliderValid(hits[i].collider)) return true;
        return false;
    }

    public Vector3 FilterHorizontalDelta(Vector3 currentPosition, Vector3 desiredHorizontalDelta)
    {
        if (desiredHorizontalDelta.sqrMagnitude < 1e-8f)
            return Vector3.zero;

        if (TryClear(currentPosition, desiredHorizontalDelta))
            return desiredHorizontalDelta;

        // X-only slide
        if (Mathf.Abs(desiredHorizontalDelta.x) > 1e-5f)
        {
            var xDelta = new Vector3(desiredHorizontalDelta.x, 0f, 0f);
            if (TryClear(currentPosition, xDelta))
                return xDelta;
        }

        // Z-only slide
        if (Mathf.Abs(desiredHorizontalDelta.z) > 1e-5f)
        {
            var zDelta = new Vector3(0f, 0f, desiredHorizontalDelta.z);
            if (TryClear(currentPosition, zDelta))
                return zDelta;
        }

        return Vector3.zero;
    }

    private bool TryClear(Vector3 currentPos, Vector3 delta)
    {
        Vector3 candidate = currentPos + delta;
        if (WouldVisualSweepHit(currentPos, candidate)) return false;
        if (!IsVisualClearAt(candidate)) return false;
        return true;
    }

    private bool IsColliderValid(Collider c)
    {
        if (c == null) return false;

        if (characterController != null && c.transform.IsChildOf(characterController.transform))
            return false;

        int layer = c.gameObject.layer;
        if (layer == _layerPlayer) return false;
        if (layer == _layerVisual) return false;
        if (layer == _layerGround) return false;
        if (layer == _layerProbe)  return false;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        if (_layerPlayer < 0) CacheLayers();

        ComputeProbeAt(transform.position, out Vector3 center, out Vector3 halfExtents, out Quaternion rot);

        bool clear = true;
        // Only run Physics query in Play mode to avoid spamming edit-time overlaps
        if (Application.isPlaying) clear = IsVisualClearAt(transform.position);

        Color colClear = new Color(0.20f, 1.00f, 0.35f, 0.85f);
        Color colBlock = new Color(1.00f, 0.30f, 0.30f, 0.90f);
        Gizmos.color = clear ? colClear : colBlock;

        Matrix4x4 saved = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = saved;
    }
}
