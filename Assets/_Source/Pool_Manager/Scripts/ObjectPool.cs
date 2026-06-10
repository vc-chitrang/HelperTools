using System;
using System.Collections.Generic;

namespace PoolSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  OBJECT POOL — generic stack-based pool for any C# type
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Use for plain C# objects, audio sources, particle handles, etc.
    ///  For Unity GameObject/prefab pools use <see cref="GameObjectPool"/>.
    ///
    ///    var pool = new ObjectPool&lt;Bullet&gt;(
    ///        factory:   () => new Bullet(),
    ///        onGet:     b  => b.Reset(),
    ///        onRelease: b  => b.Clear(),
    ///        maxSize: 64);
    ///
    ///    Bullet b = pool.Get();
    ///    pool.Release(b);
    /// </summary>
    public class ObjectPool<T>
    {
        private readonly Stack<T>  _pool;
        private readonly Func<T>   _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int       _maxSize;

        /// <summary>Total objects ever created (active + inactive).</summary>
        public int CountAll { get; private set; }

        /// <summary>Objects currently sitting idle in the pool.</summary>
        public int CountInactive => _pool.Count;

        /// <summary>Objects currently checked out and in use.</summary>
        public int CountActive => CountAll - CountInactive;

        /// <param name="factory">Creates a new instance when the pool is empty.</param>
        /// <param name="onGet">Called just before an object is handed out (reset / activate).</param>
        /// <param name="onRelease">Called just before an object is returned (clear / deactivate).</param>
        /// <param name="onDestroy">Called when an over-capacity object is discarded (dispose / destroy).</param>
        /// <param name="defaultCapacity">Initial stack capacity — avoids early re-allocations.</param>
        /// <param name="maxSize">Maximum inactive objects held. Excess are passed to <paramref name="onDestroy"/>.</param>
        public ObjectPool(
            Func<T>   factory,
            Action<T> onGet           = null,
            Action<T> onRelease       = null,
            Action<T> onDestroy       = null,
            int       defaultCapacity = 16,
            int       maxSize         = int.MaxValue)
        {
            _factory   = factory  ?? throw new ArgumentNullException(nameof(factory));
            _onGet     = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize   = maxSize;
            _pool      = new Stack<T>(defaultCapacity);
        }

        // ── Core operations ────────────────────────────────────────────────

        /// <summary>Gets an object from the pool, creating one if the pool is empty.</summary>
        public T Get()
        {
            T item = _pool.Count > 0 ? _pool.Pop() : Manufacture();
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>Returns an object to the pool for future reuse.</summary>
        public void Release(T item)
        {
            if (_pool.Count >= _maxSize)
                _onDestroy?.Invoke(item);
            else
            {
                _onRelease?.Invoke(item);
                _pool.Push(item);
            }
        }

        // ── Warm-up ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates <paramref name="count"/> objects and places them in the pool
        /// so the first N calls to <see cref="Get"/> never allocate.
        /// </summary>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T item = Manufacture();
                _onRelease?.Invoke(item);
                _pool.Push(item);
            }
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        /// <summary>
        /// Empties the pool, calling <c>onDestroy</c> on every remaining inactive item.
        /// Does NOT track or affect currently active (checked-out) objects.
        /// </summary>
        public void Clear()
        {
            if (_onDestroy != null)
                while (_pool.Count > 0)
                    _onDestroy(_pool.Pop());
            else
                _pool.Clear();

            CountAll = 0;
        }

        // ── Internal ───────────────────────────────────────────────────────

        private T Manufacture()
        {
            CountAll++;
            return _factory();
        }
    }
}
