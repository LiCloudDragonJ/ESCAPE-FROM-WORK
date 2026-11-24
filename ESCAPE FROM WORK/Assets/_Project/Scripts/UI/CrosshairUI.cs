using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromWork.UI
{
    /// <summary>
    /// Screen-center crosshair that changes color based on aim context.
    /// Green = default, Red = enemy in crosshairs, Yellow = lootable target.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Line References")]
        [SerializeField] private Image horizontalLine;
        [SerializeField] private Image verticalLine;

        [Header("Colors")]
        [SerializeField] private Color defaultColor   = new Color(0f, 1f, 0.4f, 0.85f);
        [SerializeField] private Color enemyColor     = new Color(1f, 0.2f, 0.1f, 0.9f);
        [SerializeField] private Color lootableColor  = new Color(1f, 0.9f, 0.2f, 0.85f);
        [SerializeField] private Color friendlyColor  = new Color(0.3f, 0.7f, 1f, 0.85f);

        private Color _currentColor;
        private Color _targetColor;
        private float _transitionSpeed = 6f;

        private void Start()
        {
            _currentColor = defaultColor;
            _targetColor  = defaultColor;
        }

        private void Update()
        {
            // Smooth color transition.
            _currentColor = Color.Lerp(_currentColor, _targetColor,
                1f - Mathf.Exp(-_transitionSpeed * Time.deltaTime));

            if (horizontalLine != null) horizontalLine.color = _currentColor;
            if (verticalLine   != null) verticalLine.color   = _currentColor;

            // Detect what's under the crosshair.
            UpdateTargetColor();
        }

        private void UpdateTargetColor()
        {
            var cam = Camera.main;
            if (cam == null) { _targetColor = defaultColor; return; }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var go = hit.collider.gameObject;

                if (go.CompareTag("Enemy"))
                    _targetColor = enemyColor;
                else if (go.CompareTag("Loot"))
                    _targetColor = lootableColor;
                else if (go.CompareTag("Player"))
                    _targetColor = friendlyColor;
                else
                    _targetColor = defaultColor;
            }
            else
            {
                _targetColor = defaultColor;
            }
        }

        /// <summary>Force the crosshair to a specific color (e.g., during damage feedback).</summary>
        public void FlashColor(Color color, float duration = 0.15f)
        {
            _currentColor = color;
            _targetColor  = defaultColor;
        }
    }
}
