using UnityEngine;

namespace EscapeFromWork.Data
{
    /// <summary>
    /// Weapon classification by mechanical role.
    /// Type-A: standard ranged weapons (staplers, keyboard-based).
    /// Type-C: creative / special-effect ranged weapons.
    /// Melee: close-quarters weapons.
    /// </summary>
    public enum WeaponClass
    {
        /// <summary>Standard ranged firearm (stapler guns, keyboard launchers).</summary>
        TypeA,
        /// <summary>Creative ranged weapon with a unique special effect.</summary>
        TypeC,
        /// <summary>Close-quarters melee weapon.</summary>
        Melee
    }

    /// <summary>
    /// Equipment slot a weapon occupies on the character.
    /// </summary>
    public enum WeaponSlot
    {
        /// <summary>Slot A — primary ranged weapon.</summary>
        A,
        /// <summary>Slot C — creative / special weapon.</summary>
        C,
        /// <summary>Melee slot — always available.</summary>
        Melee
    }

    /// <summary>
    /// Data definition for a wieldable weapon.
    /// Create instances via Assets > Create > Data > Weapon.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Weapon", fileName = "NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        // ---- Identity -----------------------------------------------------------

        /// <summary>Display name shown in inventory and HUD.</summary>
        [SerializeField] private string weaponName = "New Weapon";

        /// <summary>Mechanical weapon class.</summary>
        [SerializeField] private WeaponClass weaponClass;

        /// <summary>Equipment slot this weapon occupies.</summary>
        [SerializeField] private WeaponSlot slot;

        // ---- Ammunition ---------------------------------------------------------

        /// <summary>Type of ammunition this weapon consumes. None for melee weapons.</summary>
        [SerializeField] private AmmoType ammoType = AmmoType.None;

        // ---- Combat stats -------------------------------------------------------

        /// <summary>Damage dealt per hit before modifiers.</summary>
        [SerializeField] [Range(0f, 1000f)] private float baseDamage = 10f;

        /// <summary>Shots per second. Ignored for melee weapons.</summary>
        [SerializeField] [Range(0.1f, 60f)] private float fireRate = 5f;

        /// <summary>Effective range in world units.</summary>
        [SerializeField] [Range(0.1f, 100f)] private float range = 10f;

        /// <summary>Shot spread. 0 = pinpoint, 1 = very wide cone.</summary>
        [SerializeField] [Range(0f, 1f)] private float spread = 0.05f;

        /// <summary>Rounds per magazine before a reload is required.</summary>
        [SerializeField] [Range(1, 200)] private int magazineSize = 30;

        /// <summary>Time in seconds to complete a full reload.</summary>
        [SerializeField] [Range(0.1f, 10f)] private float reloadTime = 1.5f;

        // ---- Bonuses ------------------------------------------------------------

        /// <summary>Whether headshots deal bonus damage with this weapon.</summary>
        [SerializeField] private bool hasHeadshotBonus;

        /// <summary>
        /// Description of the unique effect for Type-C weapons.
        /// Examples: "fires three-way spread", "bounces once", "slows on hit".
        /// </summary>
        [SerializeField] [TextArea(1, 3)] private string specialEffect;

        // ---- Melee-specific -----------------------------------------------------

        /// <summary>Attack reach in world units. Only relevant for Melee weapons.</summary>
        [SerializeField] [Range(0.1f, 5f)] private float meleeRange = 1.5f;

        /// <summary>Swing arc in degrees. Only relevant for Melee weapons.</summary>
        [SerializeField] [Range(10f, 360f)] private float meleeArc = 120f;

        /// <summary>
        /// Time in seconds to fully charge a heavy attack.
        /// 0 means the weapon has no charge attack.
        /// </summary>
        [SerializeField] [Range(0f, 5f)] private float chargeUpTime;

        /// <summary>Stamina consumed by a light melee attack with this weapon.</summary>
        [SerializeField] [Range(5f, 30f)] private float meleeLightStaminaCost = 15f;

        /// <summary>Stamina consumed by a heavy melee attack with this weapon.</summary>
        [SerializeField] [Range(10f, 50f)] private float meleeHeavyStaminaCost = 30f;

        // ---- Assets -------------------------------------------------------------

        /// <summary>Inventory / HUD icon.</summary>
        [SerializeField] private Sprite icon;

        /// <summary>World-space prefab instantiated when the weapon is equipped or dropped.</summary>
        [SerializeField] private GameObject prefab;

        // ---- Public properties --------------------------------------------------

        public string WeaponName => weaponName;
        public WeaponClass WeaponClass => weaponClass;
        public WeaponSlot Slot => slot;
        public AmmoType AmmoType => ammoType;
        public float BaseDamage => baseDamage;
        public float FireRate => fireRate;
        public float Range => range;
        public float Spread => spread;
        public int MagazineSize => magazineSize;
        public float ReloadTime => reloadTime;
        public bool HasHeadshotBonus => hasHeadshotBonus;
        public string SpecialEffect => specialEffect;
        public float MeleeRange => meleeRange;
        public float MeleeArc => meleeArc;
        public float ChargeUpTime => chargeUpTime;
        public float MeleeLightStaminaCost => meleeLightStaminaCost;
        public float MeleeHeavyStaminaCost => meleeHeavyStaminaCost;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;

        // ---- Convenience queries ------------------------------------------------

        /// <summary>True when this is a melee weapon.</summary>
        public bool IsMelee => weaponClass == WeaponClass.Melee;

        /// <summary>True when this is a Type-C weapon with a special effect defined.</summary>
        public bool HasSpecialEffect => weaponClass == WeaponClass.TypeC
                                        && !string.IsNullOrWhiteSpace(specialEffect);

        /// <summary>True when this weapon has a charge-up heavy attack.</summary>
        public bool HasChargeAttack => chargeUpTime > 0f;

        /// <summary>Calculated shots per second, or 0 for melee weapons without a fire rate.</summary>
        public float EffectiveFireRate => IsMelee ? 0f : fireRate;

        // ---- Validation ---------------------------------------------------------

        private void OnValidate()
        {
            // Melee weapons don't use ammo — keep the field clean.
            if (weaponClass == WeaponClass.Melee)
            {
                ammoType = AmmoType.None;
                fireRate = 0f;
            }

            // Type-C weapons must have a special effect defined.
            // Type-A weapons should not.
            if (weaponClass == WeaponClass.TypeA)
            {
                specialEffect = string.Empty;
            }
        }
    }
}
