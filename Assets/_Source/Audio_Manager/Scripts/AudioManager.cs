using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    /// <summary>
    /// <para>
    /// Central, reusable audio facade. A single persistent <see cref="AudioManager"/>
    /// handles every audio concern so the rest of the project talks to ONE API:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>BGM</b> — looping background music with crossfade / fade in-out
    ///         (two ping-pong sources for seamless transitions).</item>
    ///   <item><b>SFX</b> — <see cref="PlaySfxOneShot"/> (pooled, auto-returned when
    ///         finished), <see cref="PlaySfxLooping"/> (lives until stopped or app
    ///         exit), and overlapping/"multiple" playback via repeated one-shots.</item>
    ///   <item><b>Volume</b> — per-channel 0–1 control routed to the Audio Mixer in dB.</item>
    ///   <item><b>Mute</b> — per-channel mute that remembers the previous volume.</item>
    /// </list>
    /// <para>
    /// Mixer integration, pooling, and persistence live here; <b>no UI code</b> —
    /// the UI layer (<see cref="AudioVolumePanel"/>) only calls this facade
    /// (separation of concern).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Singleton
        // ──────────────────────────────────────────────────────────────────────

        public static AudioManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        //  Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Mixer")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("BGM")]
        [Tooltip("Default crossfade / fade duration in seconds.")]
        [SerializeField, Min(0f)] private float _bgmFadeDuration = 1f;

        [Header("SFX Pool")]
        [SerializeField, Min(1)] private int _sfxInitialPoolSize = 10;
        [SerializeField, Min(1)] private int _sfxMaxPoolSize = 32;

        [Header("Lifecycle")]
        [Tooltip("Survive scene loads (recommended for a global manager).")]
        [SerializeField] private bool _persistAcrossScenes = true;

        [Tooltip("Restore saved volume/mute from PlayerPrefs on startup.")]
        [SerializeField] private bool _loadSavedSettings = true;

        // ──────────────────────────────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────────────────────────────

        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _activeBgm;     // currently audible BGM source
        private Coroutine _bgmFadeRoutine;

        private SfxAudioSourcePool _sfxPool;
        private readonly List<AudioSource> _activeOneShots = new();

        private readonly Dictionary<AudioChannel, float> _volume = new();
        private readonly Dictionary<AudioChannel, bool> _muted = new();

        private static readonly AudioChannel[] AllChannels =
            { AudioChannel.Master, AudioChannel.BGM, AudioChannel.SFX };

        // ──────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            _bgmA = CreateBgmSource("BGM_Source_A");
            _bgmB = CreateBgmSource("BGM_Source_B");
            _activeBgm = null;

            _sfxPool = new SfxAudioSourcePool(transform, _sfxGroup, _sfxInitialPoolSize, _sfxMaxPoolSize);
        }

        private void Start()
        {
            // Mixer parameters are reliably settable after the first frame, so apply
            // initial/saved volumes here rather than in Awake.
            foreach (AudioChannel ch in AllChannels)
            {
                float vol = _loadSavedSettings ? PlayerPrefs.GetFloat(VolumeKey(ch), 1f) : 1f;
                bool mute = _loadSavedSettings && PlayerPrefs.GetInt(MuteKey(ch), 0) == 1;
                _volume[ch] = Mathf.Clamp01(vol);
                _muted[ch] = mute;
                ApplyToMixer(ch);
            }
        }

        private void Update()
        {
            // Reclaim one-shot SFX sources the moment they finish playing.
            for (int i = _activeOneShots.Count - 1; i >= 0; i--)
            {
                AudioSource src = _activeOneShots[i];
                if (src == null || !src.isPlaying)
                {
                    _activeOneShots.RemoveAt(i);
                    _sfxPool.Release(src);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  BGM (Dynamic Music)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays <paramref name="clip"/> as background music, crossfading from the
        /// current track. Pass <paramref name="fadeDuration"/> to override the default.
        /// </summary>
        public void PlayBGM(AudioClip clip, bool loop = true, float? fadeDuration = null)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlayBGM called with a null clip.");
                return;
            }

            float duration = Mathf.Max(0f, fadeDuration ?? _bgmFadeDuration);
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(CrossfadeBgm(clip, loop, duration));
        }

        /// <summary>Fades out and stops the current BGM.</summary>
        public void StopBGM(float? fadeDuration = null)
        {
            float duration = Mathf.Max(0f, fadeDuration ?? _bgmFadeDuration);
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(FadeOutBgm(duration));
        }

        /// <summary>Pauses the current BGM (remembers position).</summary>
        public void PauseBGM() => _activeBgm?.Pause();

        /// <summary>Resumes a paused BGM.</summary>
        public void ResumeBGM() => _activeBgm?.UnPause();

        private IEnumerator CrossfadeBgm(AudioClip clip, bool loop, float duration)
        {
            AudioSource from = _activeBgm;
            AudioSource to = (from == _bgmA) ? _bgmB : _bgmA;

            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();

            float fromStartVol = from != null ? from.volume : 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;   // unscaled so fades work while paused
                float k = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
                to.volume = k;
                if (from != null) from.volume = Mathf.Lerp(fromStartVol, 0f, k);
                yield return null;
            }

            to.volume = 1f;
            if (from != null)
            {
                from.Stop();
                from.volume = 1f;
            }

            _activeBgm = to;
            _bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgm(float duration)
        {
            AudioSource src = _activeBgm;
            if (src == null) yield break;

            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, duration <= 0f ? 1f : t / duration);
                yield return null;
            }

            src.Stop();
            src.volume = 1f;
            _activeBgm = null;
            _bgmFadeRoutine = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  SFX
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a one-shot sound effect from the pool. The source is automatically
        /// returned to the pool when the clip finishes. Call repeatedly for
        /// overlapping / "multiple" playback. Returns the source (or <c>null</c> if the
        /// pool is exhausted) — you usually don't need the return value.
        /// </summary>
        public AudioSource PlaySfxOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;

            AudioSource src = _sfxPool.Get();
            if (src == null)
            {
                Debug.LogWarning("[AudioManager] SFX pool exhausted; consider raising the max pool size.");
                return null;
            }

            src.outputAudioMixerGroup = _sfxGroup;
            src.clip = clip;
            src.loop = false;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();

            _activeOneShots.Add(src); // auto-released in Update when finished
            return src;
        }

        /// <summary>
        /// Plays a looping sound effect that lives until you call <see cref="StopSfx"/>
        /// (or the app closes). Use for ambient loops, engine hums, etc. The returned
        /// <see cref="AudioSource"/> is the handle — keep it to stop the sound later.
        /// </summary>
        public AudioSource PlaySfxLooping(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;

            AudioSource src = _sfxPool.Get();
            if (src == null)
            {
                Debug.LogWarning("[AudioManager] SFX pool exhausted; cannot start looping SFX.");
                return null;
            }

            src.outputAudioMixerGroup = _sfxGroup;
            src.clip = clip;
            src.loop = true;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();

            // Intentionally NOT tracked as a one-shot: it persists until StopSfx.
            return src;
        }

        /// <summary>Stops a handle returned by <see cref="PlaySfxLooping"/>/<see cref="PlaySfxOneShot"/> and recycles it.</summary>
        public void StopSfx(AudioSource handle)
        {
            if (handle == null) return;
            _activeOneShots.Remove(handle);
            _sfxPool.Release(handle);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Volume & Mute
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Sets a channel's volume (linear 0–1) and persists it.</summary>
        public void SetVolume(AudioChannel channel, float linear01)
        {
            _volume[channel] = Mathf.Clamp01(linear01);
            ApplyToMixer(channel);
            PlayerPrefs.SetFloat(VolumeKey(channel), _volume[channel]);
        }

        /// <summary>Returns a channel's volume (linear 0–1).</summary>
        public float GetVolume(AudioChannel channel)
            => _volume.TryGetValue(channel, out float v) ? v : 1f;

        /// <summary>Mutes / unmutes a channel (its volume is remembered).</summary>
        public void SetMute(AudioChannel channel, bool mute)
        {
            _muted[channel] = mute;
            ApplyToMixer(channel);
            PlayerPrefs.SetInt(MuteKey(channel), mute ? 1 : 0);
        }

        /// <summary>True when the channel is currently muted.</summary>
        public bool IsMuted(AudioChannel channel)
            => _muted.TryGetValue(channel, out bool m) && m;

        /// <summary>Flips the mute state of a channel.</summary>
        public void ToggleMute(AudioChannel channel) => SetMute(channel, !IsMuted(channel));

        // ──────────────────────────────────────────────────────────────────────
        //  Internals
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyToMixer(AudioChannel channel)
        {
            if (_mixer == null) return;

            float linear = IsMuted(channel) ? 0f : GetVolume(channel);
            _mixer.SetFloat(AudioMixerParameters.For(channel), AudioVolume.LinearToDecibels(linear));
        }

        private AudioSource CreateBgmSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = _bgmGroup;
            return src;
        }

        private static string VolumeKey(AudioChannel channel) => $"Audio_Vol_{channel}";
        private static string MuteKey(AudioChannel channel) => $"Audio_Mute_{channel}";
    }
}
