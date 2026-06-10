# Pool Manager — System #15

Reusable, modular object pooling for Unity 6.

---

## Structure

```
Pool_Manager/
├── Scripts/
│   ├── IPoolable.cs            # Lifecycle callbacks interface
│   ├── ObjectPool.cs           # Generic C# stack-based pool
│   ├── GameObjectPool.cs       # MonoBehaviour prefab pool
│   ├── PoolManager.cs          # Singleton registry
│   ├── PoolDemoController.cs   # Runtime demo UI controller
│   └── Editor/
│       ├── PoolManagerMenu.cs  # Tools > Pool Manager menu
│       └── PoolSystemBuilder.cs  # (DELETED after use)
├── Prefabs/
│   └── PoolManager.prefab      # Drag into persistent scene
├── Scenes/
│   └── PoolManagerScene.unity  # Live demo
└── README.md
```

---

## Quick Start

### 1 — Add the prefab to your scene

Drag **PoolManager.prefab** into your persistent (bootstrap) scene.

### 2 — Create a pool at runtime

```csharp
PoolManager.Instance.CreatePool("Bullet", bulletPrefab, preWarm: 20, maxSize: 100);
```

### 3 — Spawn / Despawn

```csharp
// Spawn
GameObject bullet = PoolManager.Instance.Spawn("Bullet", firePoint.position, firePoint.rotation);

// Auto-despawn after 3 s (fire-and-forget)
PoolManager.Instance.Spawn("FX_Explosion", pos, Quaternion.identity, despawnAfter: 3f);

// Manual despawn
PoolManager.Instance.Despawn("Bullet", bullet);

// Despawn everything in one pool
PoolManager.Instance.DespawnAll("Bullet");

// Despawn everything in all pools
PoolManager.Instance.DespawnAll();
```

---

## IPoolable

Any component on a pooled prefab (or its children) that implements `IPoolable`
receives lifecycle callbacks automatically:

```csharp
using PoolSystem;

public class Bullet : MonoBehaviour, IPoolable
{
    public void OnSpawn()   { /* reset trail, velocity, etc. */ }
    public void OnDespawn() { /* stop particles, clear state  */ }
}
```

---

## ObjectPool<T> — generic C# pool

For non-MonoBehaviour types (audio, particles, custom classes):

```csharp
var pool = new ObjectPool<AudioSource>(
    factory:   () => gameObject.AddComponent<AudioSource>(),
    onGet:     a  => a.enabled = true,
    onRelease: a  => a.enabled = false,
    maxSize: 32);

AudioSource src = pool.Get();
pool.Release(src);
pool.PreWarm(10);  // create 10 inactive instances up front
pool.Clear();      // empty + destroy all inactive
```

---

## GameObjectPool — standalone prefab pool

Use without the PoolManager when you need a local, scene-scoped pool:

```csharp
// Add to a GameObject in the scene
var pool = go.AddComponent<GameObjectPool>();
pool.Initialize(myPrefab, preWarm: 10, maxSize: 50);

// Or assign prefab in the Inspector and let Awake pre-warm it

GameObject obj = pool.Spawn(position, rotation);
pool.Spawn(position, rotation, despawnAfter: 2f);   // auto-despawn
pool.Despawn(obj);
pool.DespawnAll();

// Events
pool.OnSpawned  += HandleSpawned;
pool.OnDespawned += HandleDespawned;
```

---

## Editor Menu

| Item | Description |
|---|---|
| **Tools > Pool Manager > Print Pool Stats** | Logs Active/Inactive/Total per pool |
| **Tools > Pool Manager > Despawn All (Play Mode)** | Returns all active objects (play mode only) |
| **Tools > Pool Manager > Select PoolManager in Hierarchy** | Pings the PoolManager in the scene |

---

## Notes

- `PoolManager` is a **DontDestroyOnLoad** singleton — place it once in the bootstrap scene.
- Pools that exceed `maxSize` **destroy** overflow objects rather than pooling them.
- `DespawnAll()` is safe to call during scene unload for cleanup.
- The `PoolDemoController` builds its own sphere pool at runtime — no asset dependency.
