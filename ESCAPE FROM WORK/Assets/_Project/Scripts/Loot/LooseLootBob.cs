using UnityEngine;

namespace EscapeFromWork.Loot
{
    /// <summary>
    /// Gentle floating animation for loose loot in the world.
    /// Makes items visible and attractive to pick up.
    /// </summary>
    public class LooseLootBob : MonoBehaviour
    {
        public float bobHeight = 0.3f;
        public float bobSpeed = 2f;
        private Vector3 _startPos;

        void Start() => _startPos = transform.position;

        void Update()
        {
            float y = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, y, _startPos.z);
            transform.Rotate(Vector3.up, 40f * Time.deltaTime);
        }
    }
}
