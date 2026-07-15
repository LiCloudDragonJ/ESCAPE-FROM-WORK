using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DDOL singleton that manages scene transitions between world and interiors.
/// Preserves the Player across scene loads and restores position when returning.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scenes")]
    [Tooltip("Exact scene name (must be in Build Settings).")]
    public string worldSceneName = "02_LargeWorld";

    [Header("Fade (optional)")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.4f;

    // ---- runtime state ----
    public bool IsTransitioning { get; private set; }
    private string _targetScene;
    private Vector3 _returnPosition;
    private GameObject _playerInstance;
    private CameraFollow25D _cameraFollow;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // must be root for DontDestroyOnLoad
        DontDestroyOnLoad(gameObject);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ---- public API ----

    /// <summary>Called by BuildingEntrance when player presses interact.</summary>
    public void EnterBuilding(string interiorSceneName, Vector3 returnPosition)
    {
        if (IsTransitioning) return;

        _returnPosition = returnPosition;
        CapturePlayer();
        StartCoroutine(TransitionTo(interiorSceneName));
    }

    /// <summary>Called by InteriorExitTrigger when player walks to exit.</summary>
    public void ExitToWorld()
    {
        if (IsTransitioning) return;

        CapturePlayer();
        StartCoroutine(TransitionTo(worldSceneName));
    }

    // ---- internals ----

    private void CapturePlayer()
    {
        _playerInstance = GameObject.FindWithTag("Player");
        if (_playerInstance != null)
            DontDestroyOnLoad(_playerInstance);

        _cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow25D>()
            : null;
    }

    private IEnumerator TransitionTo(string sceneName)
    {
        IsTransitioning = true;

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(FadeTo(1f));

        var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (asyncOp == null)
        {
            Debug.LogError($"[SceneTransition] Scene '{sceneName}' not in Build Settings.");
            IsTransitioning = false;
            yield break;
        }

        _targetScene = sceneName;
        while (!asyncOp.isDone) yield return null;

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(FadeTo(0f));

        IsTransitioning = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != _targetScene) return;

        // Reposition player if returning to world
        if (scene.name == worldSceneName && _playerInstance != null)
        {
            _playerInstance.transform.position = _returnPosition;
            _playerInstance.transform.rotation = Quaternion.identity;
        }

        // Rebind camera follow
        if (_playerInstance != null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var follow = mainCam.GetComponent<CameraFollow25D>();
                if (follow == null)
                    follow = mainCam.gameObject.AddComponent<CameraFollow25D>();

                follow.target = _playerInstance.transform;

                // Snap camera immediately to avoid visible jump
                follow.SnapToTarget();
            }
        }
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeCanvasGroup == null) yield break;

        float start = fadeCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = targetAlpha;
    }
}
