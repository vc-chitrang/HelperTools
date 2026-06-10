namespace PoolSystem
{
    /// <summary>
    /// Optional interface for components that need lifecycle callbacks when
    /// spawned from or returned to a <see cref="GameObjectPool"/>.
    ///
    ///   OnSpawn  — called after the object is activated and positioned.
    ///   OnDespawn — called just before the object is deactivated and pooled.
    ///
    /// Implement on any component in the prefab hierarchy (including children).
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
