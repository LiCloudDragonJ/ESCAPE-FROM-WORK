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
            // --- CAMERA: one camera, one follow script ---
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            // Remove any duplicate cameras.
            var allCams = FindObjectsOfType<Camera>();
            foreach (var c in allCams)
            {
                if (c != _cam) Destroy(c.gameObject);
            }
            if (_cam.GetComponent<AudioListener>() == null)
                _cam.gameObject.AddComponent<AudioListener>();

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
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = false;
            // Disable physics collision so the player can move freely between tiles.
            // Enemies use triggers for detection, so this doesn't break combat.
            var playerCol = _player.GetComponent<Collider>();
            if (playerCol != null) playerCol.isTrigger = true;
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
            var cf = _cam.GetComponent<SimpleCameraFollow>();
            if (cf == null) cf = _cam.gameObject.AddComponent<SimpleCameraFollow>();
            SetPrivate(cf, "target", _player.transform);
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

            // Make all non-player, non-enemy colliders triggers so walls don't block movement.
            foreach (var col in FindObjectsOfType<Collider>())
            {
                if (col.CompareTag("Player") || col.CompareTag("Enemy")) continue;
                col.isTrigger = true;
            }

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

            // --- FLOOR MANAGER ---
            var fm = gameObject.AddComponent<FloorManager>();
            SetPrivate(fm, "floorGenerator", fg);
            SetPrivate(fm, "enemySpawner", es);
            fm.InitializeFloor(50, Random.Range(0, 99999));
            Debug.Log("[QuickStart] FloorManager initialized");

            // --- EXTRACTION TRIGGERS ---
            CreateExtractionTrigger("Extraction_Stairs", new Vector3(10f, 1f, 10f), new Vector3(5f, 2f, 5f), false);
            CreateExtractionTrigger("Extraction_FireEscape", new Vector3(90f, 1f, 90f), new Vector3(5f, 2f, 5f), true);

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

        void CreateExtractionTrigger(string name, Vector3 pos, Vector3 scale, bool useFireEscape)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            col.isTrigger = true;
            var trigger = go.AddComponent<ExtractionTrigger>();
            SetPrivate(trigger, "useFireEscape", useFireEscape);
            // Make it visible: green for stairs, orange for fire escape.
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mr.material.color = useFireEscape ? new Color(1f, 0.5f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
            Debug.Log($"[QuickStart] Created {name} at {pos}");
        }
    }
}
