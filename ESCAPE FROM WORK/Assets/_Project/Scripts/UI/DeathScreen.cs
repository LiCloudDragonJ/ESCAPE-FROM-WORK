using UnityEngine;
using UnityEngine.UI;
using EscapeFromWork.Core;

namespace EscapeFromWork.UI
{
    /// <summary>
    /// Death screen displayed when the player character dies during a raid.
    /// Shows the character's name, floor reached, cause of death, loot value,
    /// and a memorial flavor message. Provides a button to select a new
    /// character and continue playing.
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        // ---- Inspector fields --------------------------------------------------

        [Header("Panel")]
        [Tooltip("The root GameObject of the death screen. Shown/hidden via Show/Hide.")]
        [SerializeField] private GameObject panel;

        [Header("Text Fields")]
        [Tooltip("Displays the name of the fallen character.")]
        [SerializeField] private Text nameText;

        [Tooltip("Displays the floor number where the character died.")]
        [SerializeField] private Text floorText;

        [Tooltip("Displays the cause of death (e.g., 'Combat', 'Hazard').")]
        [SerializeField] private Text causeText;

        [Tooltip("Displays the total paperclip value of loot held at time of death.")]
        [SerializeField] private Text lootText;

        [Tooltip("Memorial flavor text shown below the death details.")]
        [SerializeField] private Text memorialText;

        [Header("Buttons")]
        [Tooltip("Button to select a new character and return to base.")]
        [SerializeField] private Button newCharacterButton;

        // ---- Unity lifecycle ---------------------------------------------------

        private void Awake()
        {
            // Ensure the panel starts hidden.
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Start()
        {
            // Wire up the new-character button.
            if (newCharacterButton != null)
            {
                newCharacterButton.onClick.AddListener(OnNewCharacterClicked);
            }
        }

        private void OnDestroy()
        {
            // Clean up listener to prevent memory leaks.
            if (newCharacterButton != null)
            {
                newCharacterButton.onClick.RemoveListener(OnNewCharacterClicked);
            }
        }

        // ---- Public API --------------------------------------------------------

        /// <summary>
        /// Display the death screen with the given death context data.
        /// Populates all text fields and reveals the panel.
        /// </summary>
        /// <param name="ctx">The death context containing character name,
        /// floor number, cause of death, and loot value.</param>
        public void Show(DeathContext ctx)
        {
            if (ctx == null)
            {
                Debug.LogError("[DeathScreen] Show called with null DeathContext.");
                return;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (nameText != null)
            {
                nameText.text = ctx.characterName;
            }

            if (floorText != null)
            {
                floorText.text = $"Floor {ctx.floorNumber}";
            }

            if (causeText != null)
            {
                causeText.text = ctx.causeOfDeath;
            }

            if (lootText != null)
            {
                lootText.text = $"{ctx.lootValueReturned} paperclips";
            }

            if (memorialText != null)
            {
                memorialText.text = "茶水间纪念墙上多了一枚工牌。幸存者们将选出下一位代表。";
            }
        }

        /// <summary>
        /// Hide the death screen panel without triggering any state change.
        /// </summary>
        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        // ---- Button handlers ---------------------------------------------------

        /// <summary>
        /// Called when the player clicks the "New Character" button.
        /// Hides the death screen and delegates to
        /// <see cref="GameManager.Instance"/> to select a new character.
        /// </summary>
        private void OnNewCharacterClicked()
        {
            Hide();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SelectNewCharacter();
            }
            else
            {
                Debug.LogError("[DeathScreen] GameManager.Instance is null — cannot select new character.");
            }
        }
    }
}
