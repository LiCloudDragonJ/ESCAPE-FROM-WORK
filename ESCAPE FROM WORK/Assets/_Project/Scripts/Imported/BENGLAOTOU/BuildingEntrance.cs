using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Placed at a building door. Shows an interact prompt when the player
/// walks into the trigger zone. Pressing the interact key transitions
/// to the corresponding interior scene.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BuildingEntrance : MonoBehaviour
{
    [Header("Scene Target")]
    [Tooltip("Exact scene name (must be in Build Settings).")]
    public string interiorSceneName;

    [Tooltip("World-space position where the player reappears when exiting.")]
    public Vector3 exitReturnPosition;

    [Header("Prompt")]
    [Tooltip("Text shown above the entrance.")]
    public string promptText = "按 E 进入";

    [Tooltip("Optional explicit reference. Auto-finds child if null.")]
    public InteractPromptUI promptUI;

    private bool _playerInRange;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;

        // Place entrance on VisualClearanceProbe layer so the HD2D probe ignores it
        int probeLayer = LayerMask.NameToLayer("VisualClearanceProbe");
        if (probeLayer >= 0)
            gameObject.layer = probeLayer;

        if (promptUI == null)
            promptUI = GetComponentInChildren<InteractPromptUI>(includeInactive: true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = true;
            if (promptUI != null) promptUI.Show(promptText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = false;
            if (promptUI != null) promptUI.Hide();
        }
    }

    private void Update()
    {
        if (!_playerInRange) return;
        if (SceneTransitionManager.Instance == null) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SceneTransitionManager.Instance.EnterBuilding(
                interiorSceneName,
                exitReturnPosition
            );
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Check by component presence (tag may not be set)
        return other.GetComponent<PlayerMovement25D>() != null;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(interiorSceneName))
            interiorSceneName = gameObject.name;
    }
}
