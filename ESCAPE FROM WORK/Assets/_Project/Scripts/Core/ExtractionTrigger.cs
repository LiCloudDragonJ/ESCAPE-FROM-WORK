using UnityEngine;
using EscapeFromWork.Level;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Trigger zone that extracts the player from the current floor.
    /// Place in stairwell rooms or at extraction points.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExtractionTrigger : MonoBehaviour
    {
        [Tooltip("True for fire-escape extraction (diagonal), false for normal stairs.")]
        [SerializeField] private bool useFireEscape;

        private bool _triggered;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player"))
                return;

            _triggered = true;
            Debug.Log($"[ExtractionTrigger] Player entered — extracting via {(useFireEscape ? "fire escape" : "stairs")}");
            FloorManager fm = FloorManager.Instance;
            if (fm != null)
                fm.Extract(useFireEscape);
        }
    }
}
