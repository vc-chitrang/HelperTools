using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    /// <summary>
    /// A reusable pool of <see cref="AudioSource"/> components for sound effects.
    /// <para>
    /// Pooling avoids the cost of creating/destroying GameObjects per sound (the
    /// classic <c>PlayClipAtPoint</c> anti-pattern) and lets many SFX overlap.
    /// The pool prewarms a set of sources, hands them out on demand, auto-expands up
    /// to a hard cap, and reclaims them when the owner releases them.
    /// </para>
    /// <para>This is a plain C# class (no MonoBehaviour) — the owning
    /// <see cref="AudioManager"/> drives its lifecycle. Single responsibility:
    /// supply and recycle AudioSources.</para>
    /// </summary>
    public sealed class SfxAudioSourcePool
    {
        private readonly Transform _parent;
        private readonly AudioMixerGroup _output;
        private readonly int _maxSize;
        private readonly Stack<AudioSource> _available = new();

        private int _total;

        /// <summary>Sources currently free for reuse.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Total sources created so far (in use + available).</summary>
        public int TotalCount => _total;

        /// <summary>Hard ceiling on the number of pooled sources.</summary>
        public int MaxSize => _maxSize;

        public SfxAudioSourcePool(Transform parent, AudioMixerGroup output, int initialSize, int maxSize)
        {
            _parent  = parent;
            _output  = output;
            _maxSize = Mathf.Max(1, maxSize);

            int prewarm = Mathf.Clamp(initialSize, 0, _maxSize);
            for (int i = 0; i < prewarm; i++)
                _available.Push(CreateSource());
        }

        /// <summary>
        /// Returns a ready-to-use, active <see cref="AudioSource"/>, or <c>null</c>
        /// when the pool is exhausted (all sources busy and the cap is reached).
        /// </summary>
        public AudioSource Get()
        {
            AudioSource source;

            if (_available.Count > 0)
                source = _available.Pop();
            else if (_total < _maxSize)
                source = CreateSource();          // auto-expand
            else
                return null;                       // exhausted — caller decides what to do

            source.gameObject.SetActive(true);
            return source;
        }

        /// <summary>Stops <paramref name="source"/>, resets it, and returns it to the pool.</summary>
        public void Release(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip    = null;
            source.loop    = false;
            source.volume  = 1f;
            source.pitch   = 1f;
            source.spatialBlend = 0f;
            source.gameObject.SetActive(false);

            _available.Push(source);
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject($"SFX_Source_{_total}");
            go.transform.SetParent(_parent, false);
            go.SetActive(false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _output;
            source.spatialBlend = 0f; // 2D by default; callers can opt into 3D

            _total++;
            return source;
        }
    }
}
