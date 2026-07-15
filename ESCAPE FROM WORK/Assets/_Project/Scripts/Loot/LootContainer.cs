using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EscapeFromWork.Data;
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

        [Header("Container")]
        [Tooltip("Container type — determines grid size and loot category.")]
        [SerializeField] private ContainerType containerType = ContainerType.Desk;

        [Header("Loot")]
        [Tooltip("The loot table this container draws from.")]
        [SerializeField] private LootTable lootTable;

        [Header("Visuals")]
        [Tooltip("Visual GameObject shown when the container has been looted (open/empty).")]
        [SerializeField] private GameObject openVisual;

        [Tooltip("Visual GameObject shown when the container has not yet been looted (closed/full).")]
        [SerializeField] private GameObject closedVisual;

        // ---- Private state -------------------------------------------------------

        /// <summary>Items not yet revealed (still loading).</summary>
        private List<EscapeFromWork.Data.ItemData> _pendingItems = new List<EscapeFromWork.Data.ItemData>();
        /// <summary>Items already revealed in the container grid.</summary>
        private List<EscapeFromWork.Data.ItemData> _loadedItems = new List<EscapeFromWork.Data.ItemData>();
        /// <summary>True after first roll — prevents re-rolling on every open.</summary>
        private bool _hasGenerated;
        /// <summary>Coroutine currently loading items.</summary>
        private Coroutine _loadingRoutine;
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
                FloorManager.Instance.State != null &&
                FloorManager.Instance.State.IsContainerLooted(_containerId))
            {
                _hasGenerated = true;
            }

            UpdateVisuals();
        }

        // ---- IInteractable implementation ---------------------------------------

        /// <inheritdoc />
        public void Interact(GameObject interactor)
        {
            PlayerInventory inventory = interactor?.GetComponent<PlayerInventory>();
            if (inventory == null) return;
            if (lootTable == null) { Debug.LogWarning($"[LootContainer] No LootTable on '{name}'", this); return; }

            // First open: roll loot
            if (!_hasGenerated)
            {
                var drops = lootTable.Roll();
                foreach (var d in drops)
                    if (d.item != null && d.count > 0)
                        for (int i = 0; i < System.Math.Min(d.count, 5); i++)
                            _pendingItems.Add(d.item);
                _hasGenerated = true;
                Debug.Log($"[LootContainer] Generated {_pendingItems.Count} items");
            }

            var containerUI = FindObjectOfType<EscapeFromWork.UI.LootContainerUI>();
            if (containerUI != null)
            {
                containerUI.OpenWithState(_loadedItems, new List<EscapeFromWork.Data.ItemData>(_pendingItems), inventory, containerType, this);
                if (_loadingRoutine == null)
                    _loadingRoutine = StartCoroutine(LoadRoutine(containerUI));
            }
            else
            {
                // Fallback: instant pickup all loaded + pending
                foreach (var item in _loadedItems) inventory.AddItem(item, 1);
                foreach (var item in _pendingItems) inventory.AddItem(item, 1);
                _loadedItems.Clear(); _pendingItems.Clear(); _hasGenerated = false;
            }
        }

        /// <summary>Called by UI when an item is transferred from container to player.</summary>
        public void OnItemTransferred(EscapeFromWork.Data.ItemData item)
        {
            _loadedItems.Remove(item);
        }

        private System.Collections.IEnumerator LoadRoutine(EscapeFromWork.UI.LootContainerUI ui)
        {
            while (_pendingItems.Count > 0)
            {
                var item = _pendingItems[0];
                _pendingItems.RemoveAt(0);
                float delay = item.Rarity switch
                {
                    Rarity.Mythic => 3f, Rarity.Legendary => 2f, Rarity.Epic => 1f,
                    Rarity.Rare => 0.5f, Rarity.Uncommon => 0.2f, _ => 0.1f
                };
                yield return new WaitForSeconds(delay);
                _loadedItems.Add(item);
                ui.RefreshFromContainer();
            }
            _loadingRoutine = null;
            UpdateVisuals(); // mark as fully looted
        }

        /// <inheritdoc />
        public string GetPromptText()
        {
            return _hasGenerated ? "[空]" : "[E] 搜刮";
        }

        // ---- Visuals -------------------------------------------------------------

        /// <summary>
        /// Swaps between the open (looted) and closed (unlooted) visual GameObjects.
        /// </summary>
        private void UpdateVisuals()
        {
            if (openVisual != null)
            {
                openVisual.SetActive(_hasGenerated);
            }

            if (closedVisual != null)
            {
                closedVisual.SetActive(!_hasGenerated);
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
