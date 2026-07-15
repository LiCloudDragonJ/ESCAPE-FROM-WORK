using UnityEngine;
using System.Reflection;
using EscapeFromWork.Level;
using EscapeFromWork.Enemies;
using EscapeFromWork.Player;

namespace EscapeFromWork.Core
{
    public class QuickStart : MonoBehaviour
    {
        public GameObject roomOffice;
        public GameObject roomHallway;
        public GameObject roomTeaRoom;
        public GameObject roomStairwell;
        public GameObject roomConference;
        public GameObject enemyKPIZombie;

        GameObject _player;
        Camera _cam;

        void Awake()
        {
            // --- CAMERA: kill all, create one clean ---
            foreach (var c in FindObjectsOfType<Camera>()) Destroy(c.gameObject);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            _cam.orthographic = true;
            _cam.orthographicSize = 25f;
            _cam.transform.position = new Vector3(50f, 50f, 50f);
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 300f;

            // --- PLAYER ---
            var existing = GameObject.Find("Player");
            if (existing != null) Destroy(existing);
            _player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _player.name = "Player";
            _player.tag = "Player";
            _player.transform.position = new Vector3(50f, 1f, 20f);
            _player.transform.localScale = new Vector3(1f, 1.5f, 1f);
            var rb = _player.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // Player scripts
            _player.AddComponent<PlayerController>();
            _player.AddComponent<PlayerAim>();
            _player.AddComponent<PlayerCombat>();
            _player.AddComponent<PlayerInventory>();
            _player.AddComponent<PlayerInteraction>();
            _player.AddComponent<PlayerHealth>();
            // Color
            var mr = _player.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mr.material.color = Color.yellow;

            // --- CAMERA FOLLOW ---
            var cf = camGo.AddComponent<SimpleCameraFollow>();
            SetPrivate(cf, "target", _player.transform);
            SetPrivate(cf, "followSpeed", 10f);
        }

        void Start()
        {
            // --- FLOOR ---
            var fg = gameObject.AddComponent<FloorGenerator>();
            SetPrivate(fg, "roomPrefabs", new[] { roomOffice, roomHallway, roomTeaRoom, roomStairwell, roomConference });
            SetPrivate(fg, "gridWidth", 5);
            SetPrivate(fg, "gridHeight", 5);
            SetPrivate(fg, "tileSize", new Vector2(20f, 20f));
            fg.GetType().GetMethod("GenerateFloor", BindingFlags.Public | BindingFlags.Instance)
              ?.Invoke(fg, new object[] { Random.Range(0, 99999) });
            Debug.Log("[QuickStart] Floor generated: 5x5 grid, 20x20 tiles");

            // --- ENEMIES ---
            var es = gameObject.AddComponent<EnemySpawner>();
            SetPrivate(es, "enemyPrefabs", new[] { enemyKPIZombie });
            SetPrivate(es, "minEnemies", 3);
            SetPrivate(es, "maxEnemies", 8);
            // Create spawn zones throughout the floor
            es.transform.position = Vector3.zero;
            // Set spawn zones via reflection
            var zone1 = new GameObject("Zone1"); zone1.transform.position = new Vector3(30f, 0f, 30f); zone1.transform.localScale = new Vector3(15f, 1f, 15f);
            var zone2 = new GameObject("Zone2"); zone2.transform.position = new Vector3(60f, 0f, 60f); zone2.transform.localScale = new Vector3(15f, 1f, 15f);
            var zone3 = new GameObject("Zone3"); zone3.transform.position = new Vector3(30f, 0f, 70f); zone3.transform.localScale = new Vector3(15f, 1f, 15f);
            SetPrivate(es, "spawnZones", new Transform[] { zone1.transform, zone2.transform, zone3.transform });
            es.SpawnFloorEnemies();
            Debug.Log($"[QuickStart] Enemies spawned: {es.CountLivingEnemies()}");

            // --- GAME STATE ---
            if (GameManager.Instance == null) gameObject.AddComponent<GameManager>();
            GameManager.Instance.StartRaid(50);
            Debug.Log("[QuickStart] ✅ Ready! Yellow=Player, Flat floor, Red=Enemies");
        }

        void SetPrivate(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
        }
    }
}
