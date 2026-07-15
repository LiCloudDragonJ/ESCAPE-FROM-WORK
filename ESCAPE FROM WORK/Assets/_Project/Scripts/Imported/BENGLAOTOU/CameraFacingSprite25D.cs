using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class CameraFacingSprite25D : MonoBehaviour
{
    [Header("Target")]
    public Camera targetCamera;

    [Header("Rotation Offset")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    private Transform _camTransform;

    private void OnEnable()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (_camTransform == null && !ResolveCamera())
            return;

        transform.rotation = _camTransform.rotation * Quaternion.Euler(rotationOffsetEuler);
    }

    private bool ResolveCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        _camTransform = cam != null ? cam.transform : null;
        return _camTransform != null;
    }
}
