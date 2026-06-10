using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PoolSystem
{
    /// <summary>
    /// Demo controller for the Pool demo scene.
    /// Creates a primitive sphere pool at runtime and exposes Spawn /
    /// SpawnAutoDespawn / DespawnAll buttons + live stat display.
    /// </summary>
    [AddComponentMenu("HelperTools/Pool/PoolDemoController")]
    public class PoolDemoController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text _statsLabel;
        [SerializeField] private Button   _spawnButton;
        [SerializeField] private Button   _spawnAutoButton;
        [SerializeField] private Button   _despawnAllButton;

        [Header("Demo Settings")]
        [SerializeField] private int   _preWarm  = 10;
        [SerializeField] private int   _maxSize  = 50;
        [SerializeField] private float _spawnRadius = 4f;
        [SerializeField] private float _autoDespawnDelay = 2f;

        private GameObjectPool _pool;
        private GameObject     _prefabTemplate;

        private void Start()
        {
            // Build a sphere prefab template at runtime (no asset required)
            _prefabTemplate = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _prefabTemplate.name = "PooledSphere_Template";
            _prefabTemplate.transform.SetParent(transform);
            _prefabTemplate.SetActive(false);

            var poolGO = new GameObject("SpherePool");
            poolGO.transform.SetParent(transform);
            _pool = poolGO.AddComponent<GameObjectPool>();
            _pool.Initialize(_prefabTemplate, _preWarm, _maxSize);

            if (_spawnButton    != null) _spawnButton.onClick.AddListener(SpawnOne);
            if (_spawnAutoButton != null) _spawnAutoButton.onClick.AddListener(SpawnAuto);
            if (_despawnAllButton != null) _despawnAllButton.onClick.AddListener(DespawnAll);

            RefreshStats();
        }

        private void Update() => RefreshStats();

        private void SpawnOne()
        {
            _pool?.Spawn(RandomPos(), Quaternion.identity);
        }

        private void SpawnAuto()
        {
            _pool?.Spawn(RandomPos(), Quaternion.identity, _autoDespawnDelay);
        }

        private void DespawnAll()
        {
            _pool?.DespawnAll();
        }

        private void RefreshStats()
        {
            if (_statsLabel == null || _pool == null) return;
            _statsLabel.text =
                $"Active: <color=#88FF88>{_pool.CountActive}</color>   " +
                $"Available: <color=#FFAA44>{_pool.CountInactive}</color>   " +
                $"Total: <color=#AAAAFF>{_pool.CountAll}</color>";
        }

        private Vector3 RandomPos() =>
            new Vector3(
                Random.Range(-_spawnRadius, _spawnRadius),
                Random.Range(-_spawnRadius * 0.4f, _spawnRadius * 0.4f),
                Random.Range(3f, 8f));
    }
}
