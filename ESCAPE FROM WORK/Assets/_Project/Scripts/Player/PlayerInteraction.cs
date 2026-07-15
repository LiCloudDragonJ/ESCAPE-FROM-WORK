using UnityEngine;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Interface for any object the player can interact with by pressing the
    /// interact key (E) while within range.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Called when the player presses the interact key while targeting this object.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction
        /// (typically the player).</param>
        void Interact(GameObject interactor);

        /// <summary>
        /// Returns the prompt text to display on the HUD when the player is in range.
        /// Example: "Press E to pick up", "Press E to talk".
        /// </summary>
        /// <returns>A localized prompt string shown to the player.</returns>
        string GetPromptText();
    }

    /// <summary>
    /// Handles player interaction with nearby <see cref="IInteractable"/> objects
    /// using 3D overlap detection. Highlights the nearest interactable and invokes
    /// its Interact method when the player presses E.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        // ---- Serialized fields ---------------------------------------------------

        [Header("Interaction")]
        [Tooltip("Maximum 3D distance at which the player can interact with objects.")]
        [SerializeField] private float interactRange = 3f;

        [Tooltip("Layers to check for interactable objects.")]
        [SerializeField] private LayerMask interactableMask = ~0;

        // ---- Private state -------------------------------------------------------

        /// <summary>
        /// The nearest interactable currently in range, or null.
        /// </summary>
        private IInteractable _currentTarget;

        /// <summary>
        /// The GameObject of the current target, cached for gizmo and UI purposes.
        /// </summary>
        private GameObject _currentTargetObject;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// The nearest <see cref="IInteractable"/> the player can currently
        /// interact with, or null if nothing is in range.
        /// </summary>
        public IInteractable CurrentTarget => _currentTarget;

        /// <summary>
        /// True when there is a valid interactable target in range.
        /// </summary>
        public bool HasTarget => _currentTarget != null;

        /// <summary>
        /// The prompt text for the current target, or an empty string if no target
        /// is available. Safe to call every frame for UI display.
        /// </summary>
        public string CurrentPrompt => _currentTarget != null
            ? _currentTarget.GetPromptText()
            : string.Empty;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Update()
        {
            FindNearestInteractable();
            PollInteractInput();
        }

        // ---- Detection -----------------------------------------------------------

        /// <summary>
        /// Uses <see cref="Physics.OverlapSphere"/> (3D) to find the nearest
        /// <see cref="IInteractable"/> within <see cref="interactRange"/>.
        /// Updates <see cref="_currentTarget"/> accordingly. If no target is
        /// found, clears the current target.
        /// </summary>
        private void FindNearestInteractable()
        {
            _currentTarget = null;
            _currentTargetObject = null;

            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                interactRange,
                interactableMask
            );

            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                // Check if this object or any of its parent chain implements IInteractable.
                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null)
                    continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    _currentTarget = interactable;
                    _currentTargetObject = col.gameObject;
                }
            }
        }

        // ---- Input ---------------------------------------------------------------

        /// <summary>
        /// Polls the interact key (E) and invokes the current target's Interact
        /// method if a target is available.
        /// </summary>
        private void PollInteractInput()
        {
            if (!Input.GetKeyDown(KeyCode.E))
                return;

            if (_currentTarget == null)
                return;

            Interact();
        }

        /// <summary>
        /// Calls the current target's <see cref="IInteractable.Interact"/> method,
        /// passing this player GameObject as the interactor.
        /// </summary>
        public void Interact()
        {
            if (_currentTarget == null)
                return;

            _currentTarget.Interact(gameObject);
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Interaction range sphere.
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, interactRange);

            // Highlight current target.
            if (_currentTargetObject != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _currentTargetObject.transform.position);
                Gizmos.DrawWireCube(_currentTargetObject.transform.position, Vector3.one * 0.5f);
            }
        }
    }
}
