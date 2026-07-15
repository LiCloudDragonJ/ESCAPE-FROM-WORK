using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Top-level game state labels. Each state gates which systems are active and
    /// which UI is shown.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        InRaid,
        BaseBuilding,
        Dead,
        Victory
    }

    /// <summary>
    /// Top-level game orchestrator managing game state transitions, floor tracking,
    /// and cross-system event dispatch. Persists across scene loads via
    /// DontDestroyOnLoad.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ---- Singleton ----------------------------------------------------------

        public static GameManager Instance { get; private set; }

        // ---- Inspector-assigned event channels ----------------------------------

        [SerializeField] private IntEvent onFloorEnter;
        [SerializeField] private GameEvent onFloorExtract;
        [SerializeField] private DeathContextEvent onPlayerDied;
        [SerializeField] private IntEvent onFloorCleared;

        [SerializeField] private GameEvent onNewCharacterSelected;

        // ---- Public state -------------------------------------------------------

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>
        /// The floor the player is currently on (1 = top, 50 = ground/lobby).
        /// Adventures start at floor 50 and descend toward floor 1.
        /// </summary>
        public int CurrentFloorNumber { get; private set; } = 50;

        /// <summary>
        /// Memorial record of the most recent character death, or null if no death
        /// has occurred this session.
        /// </summary>
        public CharacterMemorial LastDeath { get; private set; }

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
            if (Instance == this) Instance = null;
        }

        // ---- Public API (state transitions) -------------------------------------

        /// <summary>
        /// Begin a raid starting at the given floor number. The floor number is
        /// clamped to the valid range of 1-50.
        /// </summary>
        public void StartRaid(int floorNumber)
        {
            floorNumber = Mathf.Clamp(floorNumber, 1, 50);
            CurrentState = GameState.InRaid;
            CurrentFloorNumber = floorNumber;
            onFloorEnter?.Raise(floorNumber);
        }

        /// <summary>
        /// Extract from the current floor back to base. Preserves base-building
        /// progress and loot.
        /// </summary>
        public void ExtractFromFloor()
        {
            CurrentState = GameState.BaseBuilding;
            onFloorExtract?.Raise();
        }

        /// <summary>
        /// Handle player character death. Stores a memorial record and transitions
        /// to the Dead state so the player can select a new character.
        /// </summary>
        public void PlayerDied(DeathContext ctx)
        {
            if (ctx == null)
            {
                Debug.LogError("[GameManager] PlayerDied called with null DeathContext.");
                return;
            }

            CurrentState = GameState.Dead;
            LastDeath = new CharacterMemorial(ctx);
            onPlayerDied?.Raise(ctx);
        }

        /// <summary>
        /// Select a new character after death and return to the base-building phase.
        /// Base progress and upgrades are preserved. Only valid in the Dead state.
        /// </summary>
        public void SelectNewCharacter()
        {
            if (CurrentState != GameState.Dead)
            {
                Debug.LogWarning($"[GameManager] SelectNewCharacter called in state {CurrentState}. Expected Dead.");
                return;
            }

            CurrentState = GameState.BaseBuilding;
            onNewCharacterSelected?.Raise();
        }

        /// <summary>
        /// Called when all enemies on a floor have been eliminated.
        /// Only valid during an active raid.
        /// </summary>
        public void OnFloorCleared(int floorNumber)
        {
            if (CurrentState != GameState.InRaid)
            {
                Debug.LogWarning($"[GameManager] OnFloorCleared called in state {CurrentState}. Expected InRaid.");
                return;
            }

            onFloorCleared?.Raise(floorNumber);
        }
    }
}
