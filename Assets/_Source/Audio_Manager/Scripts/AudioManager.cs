using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  AUDIO MANAGER  —  Drop this prefab into any scene once.
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  The single, persistent facade for all audio in the project.
    ///  Mark "Persist Across Scenes" to keep it alive across scene loads
    ///  (recommended — place it in your first/bootstrap scene).
    ///
    ///  ┌─────────────────────────────────────────────────────────┐
    ///  │  HOW TO USE FROM CODE                                   │
    ///  │                                                         │
    ///  │  // Background Music                                    │
    ///  │  AudioManager.Instance.PlayBGM(clip);                   │
    ///  │  AudioManager.Instance.StopBGM();                       │
    ///  │  AudioManager.Instance.PauseBGM();                      │
    ///  │  AudioManager.Instance.ResumeBGM();                     │
    ///  │                                                         │
    ///  │  // Sound Effects  (pooled, auto-returns, callback)     │
    ///  │  AudioManager.Instance.PlaySFX(clip, onComplete, vol);  │
    ///  │  AudioManager.Instance.PlaySfxLooping(clip);            │
    ///  │  AudioManager.Instance.StopSfx(handle);                 │
    ///  │                                                         │
    ///  │  // Voice-Over  (dedicated pool + VO mixer channel)     │
    ///  │  AudioManager.Instance.PlayVO(clip, onComplete, vol);   │
    ///  │                                                         │
    ///  │  // Volume (0-1) and Mute per channel                   │
    ///  │  AudioManager.Instance.SetVolume(AudioChannel.BGM, .5f);│
    ///  │  AudioManager.Instance.ToggleMute(AudioChannel.SFX);    │
    ///  │  AudioManager.Instance.IsMuted(AudioChannel.VO);        │
    ///  └─────────────────────────────────────────────────────────┘
    ///
    ///  Separation of concern:
    ///   - AudioManager  = logic only, no UI.
    ///   - AudioVolumePanel = UI only, talks only to AudioManager.
    ///   - GameAudioMixer   = asset, routes audio to OS.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HelperTools/Audio/AudioManager")]
    public class AudioManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Singleton
        // ──────────────────────────────────────────────────────────────────────

        public static AudioManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        //  Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("─── Audio Mixer ───────────────────────────────────────────────")]
        [Tooltip("The GameAudioMixer asset (Assets/_Source/Audio_Manager/Audio/).\n" +
                 "Contains Master ▸ BGM / SFX / VO sub-groups.\n" +
                 "Volume params exposed: MasterVolume, BGMVolume, SFXVolume, VOVolume.")]
        [SerializeField] private AudioMixer _mixer;

        [Tooltip("Mixer group that routes background music.\n" +
                 "All BGM AudioSources output here.\n" +
                 "Expose 'BGMVolume' on this group in the mixer asset.")]
        [SerializeField] private AudioMixerGroup _bgmGroup;

        [Tooltip("Mixer group that routes sound effects.\n" +
                 "All pooled SFX AudioSources output here.\n" +
                 "Expose 'SFXVolume' on this group in the mixer asset.")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("Mixer group that routes voice-over lines.\n" +
                 "All pooled VO AudioSources output here.\n" +
                 "Expose 'VOVolume' on this group in the mixer asset.")]
        [SerializeField] private AudioMixerGroup _voGroup;

        [Header("─── BGM Settings ────────────────────────────────────────────────")]
        [Tooltip("Default duration (seconds) for BGM crossfade and fade-out.\n" +
                 "Override per-call: AudioManager.Instance.PlayBGM(clip, fadeDuration: 2f).\n" +
                 "Uses unscaledDeltaTime so fades work while Time.timeScale = 0.")]
        [SerializeField, Min(0f)] private float _bgmFadeDuration = 1f;

        [Header("─── SFX Pool ────────────────────────────────────────────────────")]
        [Tooltip("Number of SFX AudioSources pre-created at startup.\n" +
                 "Set to the typical maximum simultaneous SFX count.\n" +
                 "Pool auto-expands up to Max Pool Size if demand spikes.")]
        [SerializeField, Min(1)] private int _sfxInitialPoolSize = 10;

        [Tooltip("Hard ceiling on the SFX pool size.\n" +
                 "When exhausted a warning is logged and PlaySFX returns null.\n" +
                 "Raise if you hear sounds getting dropped.")]
        [SerializeField, Min(1)] private int _sfxMaxPoolSize = 32;

        [Header("─── VO Pool ─────────────────────────────────────────────────────")]
        [Tooltip("Number of VO AudioSources pre-created at startup.\n" +
                 "VO lines rarely overlap, so a small pool (2–4) is usually fine.")]
        [SerializeField, Min(1)] private int _voInitialPoolSize = 2;

        [Tooltip("Hard ceiling on the VO pool size.\n" +
                 "Raise if multiple simultaneous voice-overs are needed.")]
        [SerializeField, Min(1)] private int _voMaxPoolSize = 6;

        [Header("─── Lifecycle ───────────────────────────────────────────────────")]
        [Tooltip("When true: DontDestroyOnLoad — the AudioManager persists across scene loads.\n" +
                 "Recommended: place ONE instance in your bootstrap/first scene.\n" +
                 "When false: destroyed with the scene (useful for scene-scoped audio).")]
        [SerializeField] private bool _persistAcrossScenes = true;

        [Tooltip("When true: restores each channel's volume and mute state from\n" +
                 "PlayerPrefs on startup (keys: Audio_Vol_<channel>, Audio_Mute_<channel>).\n" +
                 "When false: every channel starts at full volume, unmuted.")]
        [SerializeField] private bool _loadSavedSettings = true;

        // ──────────────────────────────────────────────────────────────────────
        //  Public state
        // ──────────────────────────────────────────────────────────────────────

        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _activeBgm;
        private Coroutine _bgmFadeRoutine;

        private SfxAudioSourcePool _sfxPool;
        private SfxAudioSourcePool _voPool;

        private sealed class PlayingHandle
        {
            public AudioSource source;
            public SfxAudioSourcePool pool;
            public Action onComplete;
            public bool autoRelease;
        }
        private readonly List<PlayingHandle> _playing = new();

        private readonly Dictionary<AudioChannel, float> _volume = new();
        private readonly Dictionary<AudioChannel, bool>  _muted  = new();

        private const float MuteEpsilon = 0.0001f;

        private static readonly AudioChannel[] AllChannels =
            { AudioChannel.Master, AudioChannel.BGM, AudioChannel.SFX, AudioChannel.VO };

        // ──────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_persistAcrossScenes) DontDestroyOnLoad(gameObject);

            _bgmA = CreateBgmSource("BGM_Source_A");
            _bgmB = CreateBgmSource("BGM_Source_B");

            _sfxPool = new SfxAudioSourcePool(transform, _sfxGroup, _sfxInitialPoolSize, _sfxMaxPoolSize);
            _voPool  = new SfxAudioSourcePool(transform, _voGroup,  _voInitialPoolSize,  _voMaxPoolSize);

            // Load in Awake so AudioVolumePanel.Start() reads correct values.
            LoadSettings();
        }

        private void Start()
        {
            // Re-apply to the mixer after the first frame (more reliable on some platforms).
            foreach (AudioChannel ch in AllChannels) ApplyToMixer(ch);
        }

        private void Update()
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                PlayingHandle h = _playing[i];
                if (h.source == null) { _playing.RemoveAt(i); continue; }
                if (h.autoRelease && !h.source.isPlaying)
                {
                    _playing.RemoveAt(i);
                    try   { h.onComplete?.Invoke(); }
                    catch (Exception e) { Debug.LogException(e); }
                    h.pool.Release(h.source);
                }
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ──────────────────────────────────────────────────────────────────────
        //  BGM
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Plays <paramref name="clip"/> as BGM, crossfading from the current track.</summary>
        public void PlayBGM(AudioClip clip, bool loop = true, float? fadeDuration = null)
        {
            if (clip == null) { Debug.LogWarning("[AudioManager] PlayBGM: null clip."); return; }
            float dur = Mathf.Max(0f, fadeDuration ?? _bgmFadeDuration);
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(CrossfadeBgm(clip, loop, dur));
        }

        /// <summary>Fades out and stops the current BGM.</summary>
        public void StopBGM(float? fadeDuration = null)
        {
            float dur = Mathf.Max(0f, fadeDuration ?? _bgmFadeDuration);
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(FadeOutBgm(dur));
        }

        public void PauseBGM()  => _activeBgm?.Pause();
        public void ResumeBGM() => _activeBgm?.UnPause();

        private IEnumerator CrossfadeBgm(AudioClip clip, bool loop, float duration)
        {
            AudioSource from = _activeBgm;
            AudioSource to   = (from == _bgmA) ? _bgmB : _bgmA;
            to.clip = clip; to.loop = loop; to.volume = 0f; to.Play();
            float fromStart = from != null ? from.volume : 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
                to.volume = k;
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                yield return null;
            }
            to.volume = 1f;
            if (from != null) { from.Stop(); from.volume = 1f; }
            _activeBgm = to; _bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgm(float duration)
        {
            AudioSource src = _activeBgm;
            if (src == null) yield break;
            float start = src.volume; float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, 0f, duration <= 0f ? 1f : t / duration);
                yield return null;
            }
            src.Stop(); src.volume = 1f; _activeBgm = null; _bgmFadeRoutine = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  SFX / VO
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One-shot SFX — pooled, auto-returns when finished, fires <paramref name="onComplete"/>.
        /// Call multiple times for overlapping playback.
        /// </summary>
        public AudioSource PlaySFX(AudioClip clip, Action onComplete = null, float volume = 1f, float pitch = 1f)
            => PlayPooled(_sfxPool, _sfxGroup, "SFX", clip, onComplete, volume, pitch, loop: false);

        /// <summary>
        /// One-shot Voice-Over — pooled, auto-returns when finished, fires <paramref name="onComplete"/>.
        /// Routed to the VO mixer channel (separate volume from SFX).
        /// </summary>
        public AudioSource PlayVO(AudioClip clip, Action onComplete = null, float volume = 1f, float pitch = 1f)
            => PlayPooled(_voPool, _voGroup, "VO", clip, onComplete, volume, pitch, loop: false);

        /// <summary>Looping SFX — lives until <see cref="StopSfx"/> is called. Returns a handle.</summary>
        public AudioSource PlaySfxLooping(AudioClip clip, float volume = 1f, float pitch = 1f)
            => PlayPooled(_sfxPool, _sfxGroup, "SFX", clip, null, volume, pitch, loop: true);

        /// <summary>Stops a handle returned by any Play method and returns it to its pool.</summary>
        public void StopSfx(AudioSource handle)
        {
            if (handle == null) return;
            for (int i = 0; i < _playing.Count; i++)
            {
                if (_playing[i].source != handle) continue;
                var pool = _playing[i].pool; _playing.RemoveAt(i); pool.Release(handle); return;
            }
        }

        private AudioSource PlayPooled(SfxAudioSourcePool pool, AudioMixerGroup group, string label,
                                       AudioClip clip, Action onComplete, float volume, float pitch, bool loop)
        {
            if (clip == null) return null;
            if (pool == null) { Debug.LogWarning($"[AudioManager] {label} pool not initialised."); return null; }
            AudioSource src = pool.Get();
            if (src == null) { Debug.LogWarning($"[AudioManager] {label} pool exhausted — raise max pool size."); return null; }
            src.outputAudioMixerGroup = group;
            src.clip = clip; src.loop = loop;
            src.volume = Mathf.Clamp01(volume); src.pitch = pitch;
            src.spatialBlend = 0f; src.Play();
            _playing.Add(new PlayingHandle { source = src, pool = pool, onComplete = onComplete, autoRelease = !loop });
            return src;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Volume & Mute
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets a channel's volume (0–1). Saved to PlayerPrefs immediately.
        /// Dragging to 0 implicitly mutes; any value above 0 implicitly unmutes.
        /// </summary>
        public void SetVolume(AudioChannel channel, float linear01)
        {
            float v = Mathf.Clamp01(linear01);
            if (v > MuteEpsilon) { _volume[channel] = v; _muted[channel] = false; }
            else                 { _muted[channel] = true; }
            ApplyToMixer(channel); Persist(channel);
        }

        /// <summary>Returns effective volume for the UI/slider: 0 while muted, stored level otherwise.</summary>
        public float GetVolume(AudioChannel channel) => IsMuted(channel) ? 0f : Level(channel);

        /// <summary>Explicitly mutes or unmutes. Stored level is preserved for unmute.</summary>
        public void SetMute(AudioChannel channel, bool mute)
        {
            _muted[channel] = mute; ApplyToMixer(channel); Persist(channel);
        }

        /// <summary>True when the channel is muted. Used by <see cref="AudioVolumePanel"/> to drive the icon.</summary>
        public bool IsMuted(AudioChannel channel) => _muted.TryGetValue(channel, out bool m) && m;

        /// <summary>Flips the current mute state of a channel.</summary>
        public void ToggleMute(AudioChannel channel) => SetMute(channel, !IsMuted(channel));

        // ──────────────────────────────────────────────────────────────────────
        //  Internals
        // ──────────────────────────────────────────────────────────────────────

        private void LoadSettings()
        {
            foreach (AudioChannel ch in AllChannels)
            {
                _volume[ch] = _loadSavedSettings ? Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey(ch), 1f)) : 1f;
                _muted[ch]  = _loadSavedSettings && PlayerPrefs.GetInt(MuteKey(ch), 0) == 1;
                ApplyToMixer(ch);
            }
        }

        private float Level(AudioChannel ch) => _volume.TryGetValue(ch, out float v) ? v : 1f;

        private void ApplyToMixer(AudioChannel ch)
        {
            if (_mixer == null) return;
            float effective = IsMuted(ch) ? 0f : Level(ch);
            _mixer.SetFloat(AudioMixerParameters.For(ch), AudioVolume.LinearToDecibels(effective));
        }

        private void Persist(AudioChannel ch)
        {
            PlayerPrefs.SetFloat(VolumeKey(ch), Level(ch));
            PlayerPrefs.SetInt(MuteKey(ch), IsMuted(ch) ? 1 : 0);
            PlayerPrefs.Save();
        }

        private AudioSource CreateBgmSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = true;
            src.spatialBlend = 0f; src.outputAudioMixerGroup = _bgmGroup;
            return src;
        }

        private static string VolumeKey(AudioChannel ch) => $"Audio_Vol_{ch}";
        private static string MuteKey(AudioChannel ch)   => $"Audio_Mute_{ch}";
    }
}
