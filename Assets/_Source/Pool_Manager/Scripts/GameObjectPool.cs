using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  GAME OBJECT POOL — prefab-based MonoBehaviour pool
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Inspector usage (drag-and-drop):
    ///    1. Add GameObjectPool to a scene GameObject.
    ///    2. Assign Prefab, set Pre Warm Count and Max Size.
    ///    3. Call Spawn / Despawn from code.
    ///
    ///  Code-only usage (e.g. via PoolManager):
    ///    var pool = gameObject.AddComponent&lt;GameObjectPool&gt;();
    ///    pool.Initialize(bulletPrefab, preWarm: 20, maxSize: 100);
    ///    GameObject b = pool.Spawn(firePoint.position, firePoint.rotation);
    ///    pool.Despawn(b);
    ///
    ///  Auto-despawn (fire-and-forget):
    ///    pool.Spawn(position, rotation, despawnAfter: 2f);
    ///
    ///  IPoolable — any component on the prefab (or its children) that
    ///  implements IPoolable gets OnSpawn/OnDespawn callbacks automatically.
    /// </summary>
    [AddComponentMenu("HelperTools/Pool/GameObjectPool")]
    public class GameObjectPool : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────
        [SerializeField, Tooltip("Prefab to instantiate. Can also be set via Initialize().")]
        private GameObject _prefab;

        [SerializeField, Min(0), Tooltip("Objects created on Awake (0 = lazy).")]
        private int _preWarmCount = 10;

        [SerializeField, Min(0), Tooltip("Max inactive objects held. 0 = unlimited.")]
        private int _maxSize = 100;

        // ── Runtime state ──────────────────────────────────────────────────
        private readonly Stack<GameObject>   _inactive = new();
        private readonly HashSet<GameObject> _active   = new();
        private Transform _container;

        // ── Stats ──────────────────────────────────────────────────────────
        public int CountActive   => _active.Count;
        public int CountInactive => _inactive.Count;
        public int CountAll      => CountActive + CountInactive;

        // ── Events ─────────────────────────────────────────────────────────
        public event Action<GameObject> OnSpawned;
        public event Action<GameObject> OnDespawned;

        // ── Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _container = new GameObject($"[Pool]{(_prefab ? " " + _prefab.name : "")}").transform;
            _container.SetParent(transform);

            if (_prefab != null && _preWarmCount > 0)
                PreWarm(_preWarmCount);
        }

        // ── Setup ──────────────────────────────────────────────────────────

        /// <summary>
        /// Configure the pool from code (call once after AddComponent).
        /// <br/>Safe to call in Awake of the owner — runs after Awake creates the container.
        /// </summary>
        public void Initialize(GameObject prefab, int preWarm = 10, int maxSize = 100)
        {
            _prefab  = prefab;
            _maxSize = maxSize;

            if (_container == null)
            {
                _container = new GameObject($"[Pool] {prefab.name}").transform;
                _container.SetParent(transform);
            }
            else
            {
                _container.name = $"[Pool] {prefab.name}";
            }

            if (preWarm > 0)
                PreWarm(preWarm);
        }

        /// <summary>Creates <paramref name="count"/> inactive instances up front.</summary>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = Manufacture();
                if (obj != null) _inactive.Push(obj);
            }
        }

        // ── Spawn ──────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves an object from the pool (or instantiates one if the pool is empty)
        /// and places it at <paramref name="position"/> / <paramref name="rotation"/>.
        /// Returns <c>null</c> if the pool is at max capacity and not expandable.
        /// </summary>
        public GameObject Spawn(Vector3 position = default, Quaternion rotation = default,
            Transform parent = null)
        {
            GameObject obj = GetInactive();
            if (obj == null) return null;

            obj.transform.SetParent(parent);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            _active.Add(obj);

            NotifyPoolable(obj, spawn: true);
            OnSpawned?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Spawns an object and automatically despawns it after <paramref name="despawnAfter"/> seconds.
        /// </summary>
        public GameObject Spawn(Vector3 position, Quaternion rotation, float despawnAfter,
            Transform parent = null)
        {
            GameObject obj = Spawn(position, rotation, parent);
            if (obj != null)
                StartCoroutine(DespawnAfterDelay(obj, despawnAfter));
            return obj;
        }

        // ── Despawn ────────────────────────────────────────────────────────

        /// <summary>Returns <paramref name="obj"/> to the pool. Silently ignores unknown objects.</summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null || !_active.Contains(obj)) return;

            NotifyPoolable(obj, spawn: false);
            _active.Remove(obj);
            obj.SetActive(false);
            obj.transform.SetParent(_container);

            if (_maxSize > 0 && _inactive.Count >= _maxSize)
                Destroy(obj);       // over cap → destroy instead of pool
            else
                _inactive.Push(obj);

            OnDespawned?.Invoke(obj);
        }

        /// <summary>Returns every active object to the pool in one call.</summary>
        public void DespawnAll()
        {
            foreach (var obj in new List<GameObject>(_active))
                Despawn(obj);
        }

        // ── Internal ───────────────────────────────────────────────────────

        private GameObject GetInactive()
        {
            if (_inactive.Count > 0) return _inactive.Pop();

            bool atMax = _maxSize > 0 && CountAll >= _maxSize;
            if (atMax)
            {
                Debug.LogWarning($"[GameObjectPool] '{_prefab?.name}' pool is at max ({_maxSize}). " +
                                 "Increase MaxSize or call DespawnAll.");
                return null;
            }

            return Manufacture();
        }

        private GameObject Manufacture()
        {
            if (_prefab == null)
            {
                Debug.LogError("[GameObjectPool] Prefab is null. Assign it in the Inspector or call Initialize().");
                return null;
            }
            var obj = Instantiate(_prefab, _container);
            obj.SetActive(false);
            return obj;
        }

        private static void NotifyPoolable(GameObject obj, bool spawn)
        {
            foreach (var p in obj.GetComponentsInChildren<IPoolable>(includeInactive: true))
            {
                if (spawn) p.OnSpawn();
                else       p.OnDespawn();
            }
        }

        private IEnumerator DespawnAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null && _active.Contains(obj))
                Despawn(obj);
        }
    }
}
