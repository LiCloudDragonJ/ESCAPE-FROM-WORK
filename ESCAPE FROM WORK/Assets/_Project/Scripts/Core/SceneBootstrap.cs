using UnityEngine;
using EscapeFromWork.Level;
using EscapeFromWork.Player;
using EscapeFromWork.UI;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Auto-configures the test scene on Play. Attach this to a GameObject
    /// (typically the GameManager holder) to ensure all essential systems are
    /// present without manual scene setup.
    ///
    /// <para>On <see cref="Awake"/>: ensures the GameManager singleton exists,
    /// spawns a player prefab if none is in the scene, then spawns the HUD and
    /// DeathScreen prefabs.</para>
    ///
    /// <para>On <see cref="Start"/>: initialises floor 50 with a random seed
    /// and calls <see cref="GameManager.StartRaid"/> to begin the test raid.</para>
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("The player character prefab to spawn if no PlayerController exists in the scene.")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("The HUD prefab to spawn if no HUDManager exists in the scene.")]
        [SerializeField] private GameObject hudPrefab;

        [Tooltip("The death screen prefab to spawn if no DeathScreen exists in the scene.")]
        [SerializeField] private GameObject deathScreenPrefab;

        [Header("Spawn")]
        [Tooltip("Transform marking where the player should spawn. Falls back to this GameObject's transform if left null.")]
        [SerializeField] private Transform playerSpawnPoint;

        private void Awake()
        {
            // Ensure GameManager singleton exists.
            if (GameManager.Instance == null)
            {
                gameObject.AddComponent<GameManager>();
            }

            // Spawn player if not already in the scene.
            PlayerController existingPlayer = FindObjectOfType<PlayerController>();
            if (existingPlayer == null && playerPrefab != null)
            {
                Transform spawn = playerSpawnPoint != null ? playerSpawnPoint : transform;
                Instantiate(playerPrefab, spawn.position, Quaternion.identity);
            }

            // Spawn HUD if not already in the scene.
            HUDManager existingHud = FindObjectOfType<HUDManager>();
            if (existingHud == null && hudPrefab != null)
            {
                Instantiate(hudPrefab);
            }

            // Spawn DeathScreen if not already in the scene.
            DeathScreen existingDeathScreen = FindObjectOfType<DeathScreen>();
            if (existingDeathScreen == null && deathScreenPrefab != null)
            {
                Instantiate(deathScreenPrefab);
            }
        }

        private void Start()
        {
            // Auto-start the test raid at floor 50.
            FloorManager fm = FindObjectOfType<FloorManager>();
            if (fm != null)
            {
                fm.InitializeFloor(50, Random.Range(0, 99999));
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartRaid(50);
            }
            else
            {
                Debug.LogError("[SceneBootstrap] GameManager.Instance is null — cannot start raid.");
            }
        }
    }
}
