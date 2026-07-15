using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow25D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    [Tooltip("How fast the camera catches up to the target.")]
    public float followSpeed = 10f;

    [Tooltip("Offset from the target. For HD-2D ~45 degree downward angle, use (0, 10, -10).")]
    public Vector3 offset = new Vector3(0f, 10f, -10f);

    [Header("Tilt")]
    [Tooltip("Extra downward tilt in degrees. 0 = natural tilt from offset alone.")]
    [Range(0f, 30f)]
    public float extraTilt = 0f;

    [Header("Height")]
    [Tooltip("Lock camera to a fixed world Y height. For HD-2D, this prevents the camera from dropping when the player moves vertically.")]
    public bool lockHeight = true;

    [Tooltip("Fixed world-space Y position for the camera (only used when lockHeight is true).")]
    public float fixedHeight = 10f;

    private void Start()
    {
        if (lockHeight && fixedHeight == 0f)
            fixedHeight = offset.y; // default from offset
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Position: XZ follows target, Y is either relative or locked
        Vector3 desiredPosition;
        if (lockHeight)
        {
            desiredPosition = new Vector3(
                target.position.x + offset.x,
                fixedHeight,
                target.position.z + offset.z
            );
        }
        else
        {
            desiredPosition = target.position + offset;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // Rotation: look at the target (not the fixed-height point, so tilt angle stays natural)
        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion baseLook = Quaternion.LookRotation(toTarget, Vector3.up);
            Quaternion extraTiltRot = Quaternion.Euler(extraTilt, 0f, 0f);
            transform.rotation = baseLook * extraTiltRot;
        }
    }

    /// <summary>Immediately snap to target — call after scene transitions.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        if (lockHeight)
            transform.position = new Vector3(target.position.x + offset.x, fixedHeight, target.position.z + offset.z);
        else
            transform.position = target.position + offset;
    }
}
