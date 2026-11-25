using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeFromWork.Gameplay
{
    /// Minimal full-auto hitscan weapon so the playtest has shooting feel: kick, spread, impact marks.
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [Header("Ballistics")]
        public float roundsPerMinute = 600f;
        public float range = 120f;
        public float spreadDegrees = 1.4f;

        [Header("Kick")]
        public float recoilPitch = 1.05f;
        public float recoilYaw = 0.45f;

        FirstPersonController _controller;
        Camera _view;
        float _nextShotTime;

        public int ShotsFired { get; private set; }

        void Awake()
        {
            _controller = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            if (_controller == null || !_controller.CursorLocked) return;
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) return;
            if (_view == null) _view = ResolveCamera();
            if (_view == null) return;
            if (Time.time < _nextShotTime) return;

            float interval = 60f / Mathf.Max(60f, roundsPerMinute);
            _nextShotTime = Time.time + interval;
            Fire(_view);
        }

        Camera ResolveCamera()
        {
            if (_controller.cameraRig != null)
            {
                Camera rigged = _controller.cameraRig.GetComponent<Camera>();
                if (rigged != null) return rigged;
            }
            return Camera.main;
        }

        void Fire(Camera view)
        {
            Transform t = view.transform;
            Vector3 direction = ApplySpread(t.forward, t.right, t.up, spreadDegrees);
            Vector3 origin = t.position;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
                ImpactFx.Spawn(hit.point, hit.normal);

            ImpactFx.Muzzle(origin + direction * 0.5f + t.right * 0.12f - t.up * 0.12f);
            _controller.AddRecoil(recoilPitch * Random.Range(0.8f, 1.2f), recoilYaw * Random.Range(-1f, 1f));
            ShotsFired++;
        }

        static Vector3 ApplySpread(Vector3 forward, Vector3 right, Vector3 up, float coneDegrees)
        {
            float radius = Mathf.Tan(coneDegrees * 0.5f * Mathf.Deg2Rad);
            Vector2 offset = Random.insideUnitCircle * radius;
            return (forward + right * offset.x + up * offset.y).normalized;
        }
    }
}
