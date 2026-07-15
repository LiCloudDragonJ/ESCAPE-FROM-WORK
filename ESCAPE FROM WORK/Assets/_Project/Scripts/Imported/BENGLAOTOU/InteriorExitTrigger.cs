using UnityEngine;

/// <summary>
/// Placed at the interior scene's door. When the player walks into this
/// trigger zone, it fires the transition back to the world scene.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class InteriorExitTrigger : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMovement25D>() == null) return;
        if (SceneTransitionManager.Instance == null) return;

        SceneTransitionManager.Instance.ExitToWorld();
    }
}
