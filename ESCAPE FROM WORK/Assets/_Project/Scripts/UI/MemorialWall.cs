using System.Collections.Generic;
using UnityEngine;
using EscapeFromWork.Core;

namespace EscapeFromWork.UI
{
    /// <summary>
    /// Tea room memorial wall that displays all fallen characters throughout
    /// the campaign. Each entry shows the character's name, the floor where
    /// they died, and their cause of death.
    ///
    /// <para>Automatically adds entries by subscribing to the
    /// <see cref="GameManager"/> onPlayerDied event. Entries persist in
    /// memory for the session and should be restored from save data on
    /// scene load via <see cref="LoadMemorials"/>.</para>
    /// </summary>
    public class MemorialWall : MonoBehaviour
    {
        // ---- Inspector fields --------------------------------------------------

        [Header("Prefabs")]
        [Tooltip("Prefab for a single memorial entry. Must have child Text " +
                 "components named 'NameText', 'FloorText', and 'CauseText'.")]
        [SerializeField] private GameObject memorialEntryPrefab;

        [Header("Layout")]
        [Tooltip("Parent transform under which memorial entries are instantiated.")]
        [SerializeField] private Transform entriesParent;

        [Header("Events")]
        [Tooltip("DeathContext event raised by GameManager when a player dies. " +
                 "Assign the same ScriptableObject used by GameManager.")]
        [SerializeField] private DeathContextEvent onPlayerDiedEvent;

        // ---- Private state -----------------------------------------------------

        /// <summary>
        /// In-memory list of all memorial entries added this session.
        /// </summary>
        private readonly List<CharacterMemorial> _memorials = new List<CharacterMemorial>();

        // ---- Public properties -------------------------------------------------

        /// <summary>
        /// Read-only view of the memorial list. Useful for UI iteration
        /// or save-data serialization.
        /// </summary>
        public IReadOnlyList<CharacterMemorial> Memorials => _memorials;

        // ---- Unity lifecycle ---------------------------------------------------

        private void OnEnable()
        {
            if (onPlayerDiedEvent != null)
            {
                onPlayerDiedEvent.AddListener(OnPlayerDied);
            }
        }

        private void OnDisable()
        {
            if (onPlayerDiedEvent != null)
            {
                onPlayerDiedEvent.RemoveListener(OnPlayerDied);
            }
        }

        // ---- Public API --------------------------------------------------------

        /// <summary>
        /// Add a single memorial entry to the wall. Instantiates a prefab
        /// instance under <see cref="entriesParent"/> and populates its
        /// child text fields.
        /// </summary>
        /// <param name="memorial">The memorial data to display.</param>
        public void AddMemorial(CharacterMemorial memorial)
        {
            if (memorial == null)
            {
                Debug.LogError("[MemorialWall] AddMemorial called with null memorial.");
                return;
            }

            _memorials.Add(memorial);

            if (memorialEntryPrefab == null)
            {
                Debug.LogWarning("[MemorialWall] memorialEntryPrefab is not assigned — entry not instantiated.");
                return;
            }

            Transform parent = entriesParent != null ? entriesParent : transform;

            GameObject entry = Instantiate(memorialEntryPrefab, parent);

            // Populate child text fields by name lookup.
            SetChildText(entry, "NameText", memorial.name);
            SetChildText(entry, "FloorText", $"Floor {memorial.deathFloor}");
            SetChildText(entry, "CauseText", memorial.causeOfDeath);
        }

        /// <summary>
        /// Restore memorial entries from persistent save data. Clears the
        /// current wall and rebuilds it from the save file. Called on
        /// scene load (e.g., when entering the tea room).
        ///
        /// <para>If no save data exists, the wall starts empty.</para>
        /// </summary>
        public void LoadMemorials()
        {
            // Clear existing entries in the UI.
            ClearEntries();

            // TODO: Replace with the project's save system (e.g., SaveData.Memorials).
            // For now, memorials are session-only and reset on application quit.
            //
            // Example integration:
            //   var saved = SaveManager.Load<List<CharacterMemorial>>("memorials");
            //   if (saved != null)
            //   {
            //       foreach (var memorial in saved)
            //       {
            //           AddMemorial(memorial);
            //       }
            //   }

            Debug.Log("[MemorialWall] LoadMemorials — save system integration pending. " +
                      "Memorials are session-only for now.");
        }

        // ---- Event handlers ----------------------------------------------------

        /// <summary>
        /// Callback invoked by the <see cref="onPlayerDiedEvent"/> when a
        /// player character dies. Automatically creates a memorial entry
        /// from the death context.
        /// </summary>
        /// <param name="ctx">The death context describing the fallen character.</param>
        private void OnPlayerDied(DeathContext ctx)
        {
            if (ctx == null)
            {
                Debug.LogError("[MemorialWall] OnPlayerDied received null DeathContext.");
                return;
            }

            CharacterMemorial memorial = new CharacterMemorial(ctx);
            AddMemorial(memorial);
        }

        // ---- Helpers -----------------------------------------------------------

        /// <summary>
        /// Finds a child Transform by name and sets its Text component text.
        /// Logs a warning if the child is not found.
        /// </summary>
        /// <param name="parent">The parent GameObject to search under.</param>
        /// <param name="childName">The name of the child Transform.</param>
        /// <param name="text">The text to set.</param>
        private static void SetChildText(GameObject parent, string childName, string text)
        {
            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                Debug.LogWarning($"[MemorialWall] Child '{childName}' not found on prefab '{parent.name}'.");
                return;
            }

            UnityEngine.UI.Text textComponent = child.GetComponent<UnityEngine.UI.Text>();
            if (textComponent != null)
            {
                textComponent.text = text;
            }
            else
            {
                Debug.LogWarning($"[MemorialWall] Child '{childName}' has no Text component.");
            }
        }

        /// <summary>
        /// Destroy all instantiated memorial entry GameObjects under
        /// <see cref="entriesParent"/> and clear the in-memory list.
        /// </summary>
        private void ClearEntries()
        {
            Transform parent = entriesParent != null ? entriesParent : transform;

            foreach (Transform child in parent)
            {
                // Only destroy children that are memorial entries (not other UI).
                if (child.name.Contains("Memorial") || child.name.Contains("Entry"))
                {
                    Destroy(child.gameObject);
                }
            }

            _memorials.Clear();
        }
    }
}
