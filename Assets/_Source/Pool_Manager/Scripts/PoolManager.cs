using System.Collections.Generic;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  POOL MANAGER — Singleton registry of named GameObject pools
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Drop the prefab into your persistent scene. Create pools by name,
    ///  then spawn/despawn from anywhere in the project:
    ///
    ///    PoolManager.Instance.CreatePool("Bullet", bulletPrefab, preWarm: 20);
    ///    GameObject b = PoolManager.Instance.Spawn("Bullet", pos, rot);
    ///    PoolManager.Instance.Despawn("Bullet", b);
    ///
    ///  Auto-despawn (fire-and-forget):
    ///    PoolManager.Instance.Spawn("FX_Explosion", pos, rot, despawnAfter: 1.5f);
    ///
    ///  Direct pool access (for events / stats):
    ///    GameObjectPool pool = PoolManager.Instance.GetPool("Bullet");
    /// </summary>
    [AddComponentMenu("HelperTools/Pool/PoolManager")]
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager _instance;

        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogError("[PoolManager] No PoolManager in scene. " +
                                   "Drag the PoolManager prefab into your persistent scene.");
                return _instance;
            }
        }

        private readonly Dictionary<string, GameObjectPool> _pools = new();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Pool creation ──────────────────────────────────────────────────

        /// <summary>
        /// Creates and registers a named pool. Returns the existing pool if the
        /// key already exists (idempotent).
        /// </summary>
        public GameObjectPool CreatePool(string key, GameObject prefab,
            int preWarm = 10, int maxSize = 100)
        {
            if (_pools.TryGetValue(key, out var existing))
            {
                Debug.LogWarning($"[PoolManager] Pool '{key}' already exists — returning existing.");
                return existing;
            }

            var poolGO = new GameObject($"Pool_{key}");
            poolGO.transform.SetParent(transform);
            var pool = poolGO.AddComponent<GameObjectPool>();
            pool.Initialize(prefab, preWarm, maxSize);
            _pools[key] = pool;
            return pool;
        }

        /// <summary>Removes and destroys a named pool (including all its objects).</summary>
        public void DestroyPool(string key)
        {
            if (!_pools.TryGetValue(key, out var pool)) return;
            pool.DespawnAll();
            _pools.Remove(key);
            Destroy(pool.gameObject);
        }

        // ── Spawn ──────────────────────────────────────────────────────────

        /// <summary>Spawns an object from the named pool. Returns null if pool not found.</summary>
        public GameObject Spawn(string key, Vector3 position = default,
            Quaternion rotation = default, Transform parent = null)
        {
            if (!TryGetPool(key, out var pool)) return null;
            return pool.Spawn(position, rotation, parent);
        }

        /// <summary>Spawns and auto-despawns after <paramref name="despawnAfter"/> seconds.</summary>
        public GameObject Spawn(string key, Vector3 position, Quaternion rotation,
            float despawnAfter, Transform parent = null)
        {
            if (!TryGetPool(key, out var pool)) return null;
            return pool.Spawn(position, rotation, despawnAfter, parent);
        }

        // ── Despawn ────────────────────────────────────────────────────────

        /// <summary>Returns <paramref name="obj"/> to the named pool.</summary>
        public void Despawn(string key, GameObject obj)
        {
            if (TryGetPool(key, out var pool))
                pool.Despawn(obj);
        }

        /// <summary>Returns all active objects of the named pool.</summary>
        public void DespawnAll(string key)
        {
            if (TryGetPool(key, out var pool))
                pool.DespawnAll();
        }

        /// <summary>Returns all active objects across every registered pool.</summary>
        public void DespawnAll()
        {
            foreach (var pool in _pools.Values)
                pool.DespawnAll();
        }

        // ── Query ──────────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if a pool with this key has been registered.</summary>
        public bool HasPool(string key) => _pools.ContainsKey(key);

        /// <summary>Returns the <see cref="GameObjectPool"/> for direct access. Null if not found.</summary>
        public GameObjectPool GetPool(string key) =>
            _pools.TryGetValue(key, out var pool) ? pool : null;

        // ── Internal ───────────────────────────────────────────────────────
        private bool TryGetPool(string key, out GameObjectPool pool)
        {
            if (_pools.TryGetValue(key, out pool)) return true;
            Debug.LogError($"[PoolManager] Pool '{key}' not found. Call CreatePool(\"{key}\", ...) first.");
            return false;
        }
    }
}
