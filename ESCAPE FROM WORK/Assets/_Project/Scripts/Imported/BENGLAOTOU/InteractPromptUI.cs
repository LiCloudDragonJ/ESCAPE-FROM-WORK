using UnityEngine;
using TMPro;

/// <summary>
/// Simple world-space prompt that shows/hides a TMP text label.
/// Attach to a child GameObject with a Canvas + TextMeshProUGUI.
/// </summary>
public class InteractPromptUI : MonoBehaviour
{
    [Header("References")]
    public Canvas promptCanvas;
    public TMP_Text promptLabel;

    [Header("Billboarding")]
    [Tooltip("If true, rotates to face the camera each frame.")]
    public bool faceCamera = true;

    private Camera _mainCam;

    private void Awake()
    {
        if (promptCanvas == null)
            promptCanvas = GetComponent<Canvas>();
        if (promptLabel == null)
            promptLabel = GetComponentInChildren<TMP_Text>();

        Hide();
    }

    private void Start()
    {
        _mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCam == null)
            _mainCam = Camera.main;

        if (faceCamera && _mainCam != null)
            transform.rotation = _mainCam.transform.rotation;
    }

    public void Show(string text)
    {
        if (promptLabel != null)
            promptLabel.text = text;
        if (promptCanvas != null)
            promptCanvas.enabled = true;
    }

    public void Hide()
    {
        if (promptCanvas != null)
            promptCanvas.enabled = false;
    }
}
