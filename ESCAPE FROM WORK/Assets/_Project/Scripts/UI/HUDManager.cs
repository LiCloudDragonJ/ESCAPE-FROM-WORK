using UnityEngine;
using UnityEngine.UI;
using EscapeFromWork.Level;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.UI
{
    /// <summary>
    /// Central HUD manager that displays player health, ammo, weapon slots,
    /// floor information, extraction timer, interaction prompts, and damage
    /// feedback. Uses polling in Update() for MVP simplicity rather than
    /// event subscriptions.
    ///
    /// <para>Singleton that persists across scene loads via DontDestroyOnLoad.</para>
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        // ---- Singleton ----------------------------------------------------------

        /// <summary>
        /// The single active HUDManager instance. Valid across scene loads;
        /// set in Awake and cleared in OnDestroy.
        /// </summary>
        public static HUDManager Instance { get; private set; }

        // ---- Health -------------------------------------------------------------

        [Header("Health")]
        [Tooltip("Slider bar showing current health as a fraction of max health.")]
        [SerializeField] private Slider healthBar;

        [Tooltip("Text readout showing current and max health, e.g. '75 / 100'.")]
        [SerializeField] private Text healthText;

        // ---- Ammo ---------------------------------------------------------------

        [Header("Ammo")]
        [Tooltip("Text showing current ammo count, e.g. '12 / 30'.")]
        [SerializeField] private Text ammoText;

        [Tooltip("Text showing the ammo type name, e.g. 'Staple'.")]
        [SerializeField] private Text ammoTypeText;

        // ---- Weapons ------------------------------------------------------------

        [Header("Weapons")]
        [Tooltip("Icon for the weapon in Slot A.")]
        [SerializeField] private Image weaponAIcon;

        [Tooltip("Icon for the weapon in Slot C.")]
        [SerializeField] private Image weaponCIcon;

        [Tooltip("Icon for the melee weapon.")]
        [SerializeField] private Image weaponMeleeIcon;

        [Tooltip("Highlight GameObject placed behind the active weapon slot to indicate selection.")]
        [SerializeField] private GameObject activeWeaponHighlight;

        // ---- Floor Info ---------------------------------------------------------

        [Header("Floor Info")]
        [Tooltip("Text showing the current floor number, e.g. '50F'.")]
        [SerializeField] private Text floorNumberText;

        [Tooltip("Text showing floor safety status: 危险 (danger, red) or 安全 (safe, green).")]
        [SerializeField] private Text floorStatusText;

        // ---- Extraction ---------------------------------------------------------

        [Header("Extraction")]
        [Tooltip("Countdown text showing remaining extraction time in seconds.")]
        [SerializeField] private Text extractionTimerText;

        [Tooltip("Flashing red warning GameObject shown during extraction countdown.")]
        [SerializeField] private GameObject extractionWarning;

        // ---- Pickup Prompt ------------------------------------------------------

        [Header("Pickup Prompt")]
        [Tooltip("Text element for interaction prompts, e.g. 'Press E to pick up'.")]
        [SerializeField] private Text interactionPrompt;

        // ---- Damage Vignette ----------------------------------------------------

        [Header("Damage Vignette")]
        [Tooltip("Optional red vignette image that fades in at low health.")]
        [SerializeField] private Image damageVignette;

        // ---- Private state ------------------------------------------------------

        /// <summary>Cached reference to the local player GameObject.</summary>
        private GameObject _player;

        /// <summary>Cached PlayerCombat component on the player.</summary>
        private PlayerCombat _playerCombat;

        /// <summary>Cached PlayerInventory component on the player.</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Cached PlayerInteraction component on the player.</summary>
        private PlayerInteraction _playerInteraction;

        /// <summary>Whether the extraction timer is currently being shown.</summary>
        private bool _extractionActive;

        /// <summary>Remaining extraction seconds, driven by ShowExtractionTimer.</summary>
        private int _extractionSeconds;

        /// <summary>Internal timer for flashing the extraction warning.</summary>
        private float _extractionFlashTimer;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            // Re-find the player if the reference was lost (e.g. scene reload).
            if (_player == null)
            {
                FindPlayer();
                return;
            }

            PollHealth();
            PollAmmo();
            PollWeaponDisplay();
            PollFloorInfo();
            PollInteractionPrompt();
            PollExtractionFlash();
        }

        // ---- Public API ---------------------------------------------------------

        /// <summary>
        /// Update the health bar and health text display.
        /// </summary>
        /// <param name="current">Current health value.</param>
        /// <param name="max">Maximum health value.</param>
        public void UpdateHealth(float current, float max)
        {
            if (healthBar != null)
            {
                healthBar.maxValue = max;
                healthBar.value = current;
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }

            // Cascade to damage vignette based on health percentage.
            float healthPercent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            ShowDamageVignette(healthPercent);
        }

        /// <summary>
        /// Update the ammo display showing current rounds, magazine capacity,
        /// and ammo type name.
        /// </summary>
        /// <param name="current">Rounds currently in the magazine.</param>
        /// <param name="max">Magazine capacity.</param>
        /// <param name="ammoType">Display name of the ammo type.</param>
        public void UpdateAmmo(int current, int max, string ammoType)
        {
            if (ammoText != null)
            {
                ammoText.text = $"{current} / {max}";
            }

            if (ammoTypeText != null)
            {
                ammoTypeText.text = ammoType;
            }
        }

        /// <summary>
        /// Update the weapon slot icons and highlight the active slot.
        /// </summary>
        /// <param name="activeSlot">0 = Slot A, 1 = Slot C, 2 = Melee.</param>
        /// <param name="iconA">Sprite for Slot A icon, or null to clear.</param>
        /// <param name="iconC">Sprite for Slot C icon, or null to clear.</param>
        /// <param name="iconMelee">Sprite for the Melee icon, or null to clear.</param>
        public void UpdateWeaponDisplay(int activeSlot, Sprite iconA, Sprite iconC, Sprite iconMelee)
        {
            if (weaponAIcon != null)
                weaponAIcon.sprite = iconA;

            if (weaponCIcon != null)
                weaponCIcon.sprite = iconC;

            if (weaponMeleeIcon != null)
                weaponMeleeIcon.sprite = iconMelee;

            // Position the highlight behind the active slot.
            if (activeWeaponHighlight != null)
            {
                RectTransform targetRect = GetSlotRect(activeSlot);
                if (targetRect != null)
                {
                    activeWeaponHighlight.transform.position = targetRect.position;
                    activeWeaponHighlight.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Update the floor number and safety status display.
        /// </summary>
        /// <param name="number">Floor number (1-50).</param>
        /// <param name="isSafe">True when all enemies on this floor are cleared.</param>
        public void UpdateFloor(int number, bool isSafe)
        {
            if (floorNumberText != null)
            {
                floorNumberText.text = $"{number}F";
            }

            if (floorStatusText != null)
            {
                floorStatusText.text = isSafe ? "安全" : "危险";
                floorStatusText.color = isSafe ? Color.green : Color.red;
            }
        }

        /// <summary>
        /// Show the extraction countdown timer and flashing red warning.
        /// Call each second with the remaining time; call
        /// <see cref="HideExtractionTimer"/> when extraction completes.
        /// </summary>
        /// <param name="seconds">Remaining extraction time in seconds.</param>
        public void ShowExtractionTimer(int seconds)
        {
            _extractionActive = true;
            _extractionSeconds = seconds;

            if (extractionWarning != null)
            {
                extractionWarning.SetActive(true);
            }

            if (extractionTimerText != null)
            {
                extractionTimerText.gameObject.SetActive(true);
                extractionTimerText.text = $"{seconds}s";
            }
        }

        /// <summary>
        /// Hide the extraction warning and timer text.
        /// </summary>
        public void HideExtractionTimer()
        {
            _extractionActive = false;

            if (extractionWarning != null)
            {
                extractionWarning.SetActive(false);
            }

            if (extractionTimerText != null)
            {
                extractionTimerText.text = string.Empty;
                extractionTimerText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show or hide the interaction prompt text.
        /// Pass an empty string to hide the prompt.
        /// </summary>
        /// <param name="text">Prompt text to display, or empty string to hide.</param>
        public void ShowInteractionPrompt(string text)
        {
            if (interactionPrompt != null)
            {
                bool hasText = !string.IsNullOrEmpty(text);
                interactionPrompt.text = text;
                interactionPrompt.gameObject.SetActive(hasText);
            }
        }

        /// <summary>
        /// Adjust the damage vignette overlay based on remaining health.
        /// The vignette fades in (becomes more visible) as health decreases.
        /// Only visible when health drops below 50%.
        /// </summary>
        /// <param name="healthPercent">Health fraction in [0, 1]. 1 = full HP, 0 = dead.</param>
        public void ShowDamageVignette(float healthPercent)
        {
            if (damageVignette == null)
                return;

            // Vignette is fully opaque at 0% HP, fully transparent at or above 50% HP.
            float alpha = healthPercent < 0.5f
                ? Mathf.Lerp(1f, 0f, healthPercent * 2f)
                : 0f;

            Color c = damageVignette.color;
            c.a = alpha;
            damageVignette.color = c;
        }

        // ---- Polling helpers ----------------------------------------------------

        /// <summary>
        /// Locate the player GameObject by tag and cache component references.
        /// Safe to call repeatedly; no-ops if the player is already cached and valid.
        /// </summary>
        private void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
                return;

            _player = playerObj;
            _playerCombat = _player.GetComponent<PlayerCombat>();
            _playerInventory = _player.GetComponent<PlayerInventory>();
            _playerInteraction = _player.GetComponent<PlayerInteraction>();
        }

        /// <summary>
        /// Poll the player's current health and push to the HUD.
        /// </summary>
        private void PollHealth()
        {
            if (_playerCombat == null)
                return;

            UpdateHealth(_playerCombat.CurrentHealth, _playerCombat.MaxHealth);
        }

        /// <summary>
        /// Poll the current weapon's ammo state and push to the HUD.
        /// Melee weapons show empty ammo since they don't consume ammunition.
        /// </summary>
        private void PollAmmo()
        {
            if (_playerCombat == null)
                return;

            WeaponBase currentWeapon = _playerCombat.CurrentWeapon;
            if (currentWeapon == null || currentWeapon.Data == null)
            {
                UpdateAmmo(0, 0, string.Empty);
                return;
            }

            // Melee weapons have no ammo — show cleared display.
            if (currentWeapon.Data.IsMelee)
            {
                UpdateAmmo(0, 0, string.Empty);
                return;
            }

            string ammoName = currentWeapon.Data.AmmoType.ToString();
            UpdateAmmo(currentWeapon.CurrentAmmo, currentWeapon.Data.MagazineSize, ammoName);
        }

        /// <summary>
        /// Poll weapon slot icons and active slot from PlayerInventory
        /// and PlayerCombat, then push to the HUD.
        /// </summary>
        private void PollWeaponDisplay()
        {
            if (_playerCombat == null || _playerInventory == null)
                return;

            // Determine active slot index from the current weapon.
            int activeSlot = 0;
            WeaponBase currentWeapon = _playerCombat.CurrentWeapon;
            if (currentWeapon != null && currentWeapon.Data != null)
            {
                switch (currentWeapon.Data.Slot)
                {
                    case Data.WeaponSlot.A:
                        activeSlot = 0;
                        break;
                    case Data.WeaponSlot.C:
                        activeSlot = 1;
                        break;
                    case Data.WeaponSlot.Melee:
                        activeSlot = 2;
                        break;
                }
            }

            // Get icons from equipped weapons via PlayerInventory.
            Sprite iconA = GetWeaponIcon(_playerInventory.GetEquippedWeapon(Data.WeaponSlot.A));
            Sprite iconC = GetWeaponIcon(_playerInventory.GetEquippedWeapon(Data.WeaponSlot.C));
            Sprite iconMelee = GetWeaponIcon(_playerInventory.GetEquippedWeapon(Data.WeaponSlot.Melee));

            UpdateWeaponDisplay(activeSlot, iconA, iconC, iconMelee);
        }

        /// <summary>
        /// Poll floor information from the active FloorManager singleton.
        /// </summary>
        private void PollFloorInfo()
        {
            if (FloorManager.Instance == null)
                return;

            int floorNumber = FloorManager.Instance.floorNumber;
            bool isSafe = FloorManager.Instance.IsSafe;

            UpdateFloor(floorNumber, isSafe);
        }

        /// <summary>
        /// Poll the current interaction prompt from PlayerInteraction.
        /// </summary>
        private void PollInteractionPrompt()
        {
            if (_playerInteraction == null)
                return;

            ShowInteractionPrompt(_playerInteraction.CurrentPrompt);
        }

        /// <summary>
        /// Flash the extraction warning on and off at a visible cadence
        /// while the extraction countdown is active.
        /// </summary>
        private void PollExtractionFlash()
        {
            if (!_extractionActive || extractionWarning == null)
                return;

            // Toggle visibility every 0.5 seconds for a pulsing red warning.
            _extractionFlashTimer += Time.deltaTime;
            if (_extractionFlashTimer >= 0.5f)
            {
                _extractionFlashTimer = 0f;
                extractionWarning.SetActive(!extractionWarning.activeSelf);
            }
        }

        // ---- Helpers ------------------------------------------------------------

        /// <summary>
        /// Extract the icon Sprite from a weapon instance's data asset.
        /// </summary>
        /// <param name="weapon">The weapon to get the icon from, or null.</param>
        /// <returns>The weapon's icon Sprite, or null if the weapon or its data is missing.</returns>
        private static Sprite GetWeaponIcon(WeaponBase weapon)
        {
            if (weapon == null || weapon.Data == null)
                return null;

            return weapon.Data.Icon;
        }

        /// <summary>
        /// Get the RectTransform for the weapon slot icon at the given index,
        /// used to position the active-weapon highlight.
        /// </summary>
        /// <param name="slotIndex">0 = Slot A, 1 = Slot C, 2 = Melee.</param>
        /// <returns>The RectTransform of the slot icon, or null.</returns>
        private RectTransform GetSlotRect(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return weaponAIcon != null ? weaponAIcon.rectTransform : null;
                case 1:
                    return weaponCIcon != null ? weaponCIcon.rectTransform : null;
                case 2:
                    return weaponMeleeIcon != null ? weaponMeleeIcon.rectTransform : null;
                default:
                    return null;
            }
        }
    }
}
