using UnityEngine;
using EscapeFromWork.Player;

namespace EscapeFromWork.Loot
{
    /// <summary>
    /// A world-space item pickup the player can interact with to collect a single
    /// item stack. Spawned by <see cref="LootContainer"/> when the player's
    /// inventory is full, or placed directly by level designers.
    /// </summary>
    public class PickupItem : MonoBehaviour, IInteractable
    {
        // ---- Private state -------------------------------------------------------

        /// <summary>The item data this pickup represents.</summary>
        [SerializeField] private EscapeFromWork.Data.ItemData _item;

        /// <summary>How many copies of the item this pickup contains.</summary>
        [SerializeField] private int _count;

        // ---- Initialization ------------------------------------------------------

        /// <summary>
        /// Initializes the pickup with an item type and stack count.
        /// Must be called before the pickup is interactable.
        /// </summary>
        /// <param name="item">The item data for this pickup.</param>
        /// <param name="count">How many copies of the item this pickup holds.</param>
        public void Initialize(EscapeFromWork.Data.ItemData item, int count)
        {
            _item = item;
            _count = Mathf.Max(1, count);
        }

        // ---- IInteractable implementation ---------------------------------------

        /// <inheritdoc />
        public void Interact(GameObject interactor)
        {
            // Guard clauses.
            if (_item == null || _count <= 0)
            {
                Debug.LogWarning(
                    $"[PickupItem] '{name}' has no valid item data or zero count.",
                    this
                );
                return;
            }

            if (interactor == null)
            {
                return;
            }

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning(
                    $"[PickupItem] Interactor '{interactor.name}' has no PlayerInventory.",
                    this
                );
                return;
            }

            // Attempt to add the full stack to the player's inventory.
            bool added = inventory.AddItem(_item, _count);

            if (added)
            {
                // Successfully collected — remove the pickup from the world.
                Destroy(gameObject);
            }
            // If not fully added, the pickup stays in the world.
            // The player can make space and try again.
        }

        /// <inheritdoc />
        public string GetPromptText()
        {
            if (_item == null || _count <= 0)
            {
                return string.Empty;
            }

            return $"[E] {_item.ItemName} x{_count}";
        }
    }
}
