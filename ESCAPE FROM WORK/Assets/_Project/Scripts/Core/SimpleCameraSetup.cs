using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Static utility that configures the Main Camera for top-down orthographic
    /// gameplay on Awake. Sets the camera to orthographic mode, positions it
    /// above the centre of a 150x150-unit floor, rotates it to look straight
    /// down, and attaches a <see cref="SimpleCameraFollow"/> component for
    /// player tracking.
    ///
    /// <para>Attach this to the Main Camera GameObject.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SimpleCameraSetup : MonoBehaviour
    {
        private void Awake()
        {
            Camera cam = GetComponent<Camera>();

            // Top-down orthographic view.
            cam.orthographic = true;

            // Size tuned for a 150x150 floor. Adjust this value to control
            // how much of the floor is visible at once.
            cam.orthographicSize = 12f;

            // Position above the floor centre: (75, 30, -10).
            // X = 75, Z = 75 is the centre of a 150x150 floor.
            // Y = 30 provides elevation; Z = -10 keeps the camera behind the
            // scene for consistent depth ordering.
            cam.transform.position = new Vector3(75f, 30f, -10f);

            // Look straight down (90-degree pitch on the X axis).
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Ensure a follow script is attached so the camera tracks the player.
            if (GetComponent<SimpleCameraFollow>() == null)
            {
                gameObject.AddComponent<SimpleCameraFollow>();
            }
        }
    }
}
