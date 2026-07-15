using UnityEngine;
using EscapeFromWork.Player;
using EscapeFromWork.Level;

namespace EscapeFromWork.Loot
{
    /// <summary>
    /// World-space container that the player can interact with to roll a
    /// <see cref="LootTable"/> and receive items. Each container can only be
    /// looted once per raid; looted state is tracked by <see cref="FloorManager"/>.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        // ---- Serialized fields ---------------------------------------------------

        [Header("Loot")]
        [Tooltip("The loot table this container draws from.")]
        [SerializeField] private LootTable lootTable;

        [Header("Visuals")]
        [Tooltip("Visual GameObject shown when the container has been looted (open/empty).")]
        [SerializeField] private GameObject openVisual;

        [Tooltip("Visual GameObject shown when the container has not yet been looted (closed/full).")]
        [SerializeField] private GameObject closedVisual;

        // ---- Private state -------------------------------------------------------

        /// <summary>Whether this container has been looted this raid.</summary>
        private bool _isLooted;

        /// <summary>
        /// Cached instance ID used as the key in FloorManager.State's looted
        /// container tracking.
        /// </summary>
        private int _containerId;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            _containerId = GetInstanceID();
        }

        private void Start()
        {
            // If the floor state already marks us as looted (e.g., after a scene
            // reload or save restore), sync visuals immediately.
            if (FloorManager.Instance != null &&
                FloorManager.Instance.State.IsContainerLooted(_containerId))
            {
                _isLooted = true;
            }

            UpdateVisuals();
        }

        // ---- IInteractable implementation ---------------------------------------

        /// <inheritdoc />
        public void Interact(GameObject interactor)
        {
            // Guard: already looted?
            if (_isLooted)
            {
                return;
            }

            // Guard: check floor-level looted state (defence in depth).
            if (FloorManager.Instance != null &&
                FloorManager.Instance.State.IsContainerLooted(_containerId))
            {
                _isLooted = true;
                UpdateVisuals();
                return;
            }

            // Resolve the interactor's inventory.
            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null)
            {
                Debug.LogWarning(
                    $"[LootContainer] Interactor '{interactor?.name}' has no PlayerInventory.",
                    this
                );
                return;
            }

            // Roll the loot table.
            if (lootTable == null)
            {
                Debug.LogWarning(
                    $"[LootContainer] No LootTable assigned to '{name}'.",
                    this
                );
                return;
            }

            (EscapeFromWork.Data.ItemData item, int count)[] drops = lootTable.Roll();

            foreach ((EscapeFromWork.Data.ItemData item, int count) in drops)
            {
                if (item == null || count <= 0)
                {
                    continue;
                }

                // Track how many of this item were already in the backpack
                // before attempting to add, so we can compute the true overflow.
                int carriedBefore = inventory.GetItemCount(item);
                inventory.AddItem(item, count);
                int carriedAfter = inventory.GetItemCount(item);

                int actuallyAdded = carriedAfter - carriedBefore;
                int overflow = count - actuallyAdded;

                if (overflow > 0)
                {
                    SpawnPickupItem(item, overflow, interactor.transform.position);
                }
            }

            // Mark as looted both locally and in the floor state.
            _isLooted = true;

            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.State.MarkContainerLooted(_containerId);
            }

            UpdateVisuals();
        }

        /// <inheritdoc />
        public string GetPromptText()
        {
            return _isLooted ? "[空]" : "[F] 搜刮";
        }

        // ---- Visuals -------------------------------------------------------------

        /// <summary>
        /// Swaps between the open (looted) and closed (unlooted) visual GameObjects.
        /// </summary>
        private void UpdateVisuals()
        {
            if (openVisual != null)
            {
                openVisual.SetActive(_isLooted);
            }

            if (closedVisual != null)
            {
                closedVisual.SetActive(!_isLooted);
            }
        }

        // ---- Helpers -------------------------------------------------------------

        /// <summary>
        /// Instantiates a world-space <see cref="PickupItem"/> at the given
        /// position so the player (or another player) can still collect the
        /// overflow loot.
        /// </summary>
        /// <param name="item">The item data for the pickup.</param>
        /// <param name="count">How many copies to include in the pickup.</param>
        /// <param name="nearPosition">World position to spawn near.</param>
        private static void SpawnPickupItem(
            EscapeFromWork.Data.ItemData item,
            int count,
            Vector3 nearPosition)
        {
            // Create a simple world-space GameObject with a PickupItem component.
            GameObject pickupObject = new GameObject($"Pickup_{item.ItemName}_{count}");
            pickupObject.transform.position = nearPosition + RandomOffset();

            // Add a collider so the interaction system can detect it.
            SphereCollider collider = pickupObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            // The PickupItem component handles the rest.
            PickupItem pickup = pickupObject.AddComponent<PickupItem>();
            pickup.Initialize(item, count);
        }

        /// <summary>
        /// Small random offset so spawned pickups don't all stack at the exact
        /// same location.
        /// </summary>
        /// <returns>A random offset vector in the XZ plane.</returns>
        private static Vector3 RandomOffset()
        {
            return new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f,
                Random.Range(-0.5f, 0.5f)
            );
        }

        // ---- Editor helpers ------------------------------------------------------

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Cache instance ID in editor so _containerId is valid for prefabs too.
            _containerId = GetInstanceID();
        }
#endif
    }
}
