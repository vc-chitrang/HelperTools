using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AudioSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  AUDIO VOLUME PANEL  —  UI-only volume controls.
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Pure presentation layer — contains NO audio logic.
    ///  Reads and writes through AudioManager.Instance only.
    ///  Drop the AudioVolumePanel prefab into any Canvas and it
    ///  connects automatically (requires AudioManager in the scene).
    ///
    ///  Hierarchy expected inside the prefab root:
    ///
    ///    AudioVolumePanel (this component + CanvasGroup)
    ///    └─ Panel  (background Image)
    ///       ├─ Title          (TMP_Text)
    ///       └─ [Channel]      (RectTransform container per channel)
    ///          ├─ Label       (TMP_Text  — channel name identifier)
    ///          ├─ Slider      (Slider    — 0–1, maps to 0–100%)
    ///          ├─ Percent     (TMP_Text  — shows e.g. "80%")
    ///          └─ Toggle      (Button + Image — mute/unmute icon)
    ///
    ///  Each row is configured via the _rows array in the Inspector.
    ///  The _toggleButtonSprites array keys sprites to channels:
    ///    sprites[0] = mute icon   (shown when channel is muted)
    ///    sprites[1] = unmute icon (shown when channel is audible)
    /// </summary>
    [AddComponentMenu("HelperTools/Audio/AudioVolumePanel")]
    public class AudioVolumePanel : MonoBehaviour
    {
        // ── Inner types ────────────────────────────────────────────────────────

        /// <summary>
        /// Binds one AudioChannel to its three UI widgets.
        /// Each field maps directly to a child of the channel's container.
        /// </summary>
        [Serializable]
        public class ChannelRow
        {
            [Tooltip("Which audio channel this row controls.\n" +
                     "Must match one of the AudioChannel enum values:\n" +
                     "Master, BGM, SFX, VO.")]
            public AudioChannel channel;

            [Tooltip("The Slider component that controls volume 0–1.\n" +
                     "Child path inside container: [Channel]/Slider\n" +
                     "Listener: calls AudioManager.SetVolume on value change.\n" +
                     "Slider range: min=0, max=1, wholeNumbers=false.")]
            public Slider volumeSlider;

            [Tooltip("The Button on the mute/unmute toggle icon.\n" +
                     "Child path inside container: [Channel]/Toggle\n" +
                     "OnClick: calls AudioManager.ToggleMute, then refreshes the icon.")]
            public Button muteButton;

            [Tooltip("The Image component whose sprite is swapped between mute and unmute.\n" +
                     "Child path inside container: [Channel]/Toggle  (same GO as muteButton)\n" +
                     "Sprite driven by _toggleButtonSprites[channel]:\n" +
                     "  index 0 = mute   sprite (when muted)\n" +
                     "  index 1 = unmute sprite (when audible)")]
            public Image muteIcon;

            [Tooltip("TextMeshPro label that shows the live volume percentage.\n" +
                     "Child path inside container: [Channel]/Percent\n" +
                     "Format: '80%'. Updated every time the slider or mute changes.")]
            public TMP_Text percentLabel;
        }

        /// <summary>
        /// Per-channel mute/unmute sprite pair (keyed by AudioChannel enum).
        /// Each entry supplies two sprites for one channel's toggle button.
        /// </summary>
        [Serializable]
        public class ChannelToggleSprites
        {
            [Tooltip("The channel these sprites belong to.\n" +
                     "Must match the channel field in the corresponding ChannelRow.")]
            public AudioChannel channel;

            [Tooltip("Two sprites for the mute toggle button of this channel.\n" +
                     "Index 0 → Mute   icon: displayed when the channel IS muted.\n" +
                     "Index 1 → Unmute icon: displayed when the channel is audible.\n\n" +
                     "Sprite folders: Assets/_Source/Audio_Manager/Sprites/{Channel}/\n" +
                     "Naming: {Channel}_Mute.png  and  {Channel}_UnMute.png\n" +
                     "Replace placeholders with final art and re-run:\n" +
                     "  Tools ▸ Audio Manager ▸ Build Audio Scene")]
            public Sprite[] sprites = new Sprite[2];
        }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Tooltip("One entry per audio channel (Master, BGM, SFX, VO).\n" +
                 "Each row binds a channel to its Slider, Button, Icon, and Percent label.\n" +
                 "Auto-populated by the editor builder (Tools ▸ Audio Manager ▸ Build Audio Scene).\n" +
                 "Manual setup: assign all four fields per row in the Inspector.")]
        [SerializeField] private ChannelRow[] _rows;

        [Tooltip("Per-channel mute/unmute sprite pairs.\n" +
                 "One entry per channel that has a custom icon (Master, BGM, SFX, VO).\n" +
                 "Keyed by the channel enum — the panel looks up by channel, not by array index.\n\n" +
                 "sprites[0] = Mute   icon (slider == 0 or mute button pressed)\n" +
                 "sprites[1] = UnMute icon (slider  > 0 and channel is audible)\n\n" +
                 "Sprite source: Assets/_Source/Audio_Manager/Sprites/{Channel}/\n" +
                 "Auto-assigned by the editor builder.")]
        [SerializeField] private ChannelToggleSprites[] _toggleButtonSprites;

        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (AudioManager.Instance == null)
                Debug.LogWarning("[AudioVolumePanel] AudioManager not found in scene. " +
                                 "Add the AudioManager prefab to this scene or ensure it persists from a previous scene.");
            if (_rows == null) return;
            foreach (ChannelRow row in _rows)
            {
                if (row == null) continue;
                BindRow(row);
                RefreshRow(row);
            }
        }

        // ── Binding ─────────────────────────────────────────────────────────────

        private void BindRow(ChannelRow row)
        {
            if (row.volumeSlider != null)
            {
                row.volumeSlider.minValue = 0f;
                row.volumeSlider.maxValue = 1f;
                row.volumeSlider.wholeNumbers = false;
                row.volumeSlider.SetValueWithoutNotify(GetVolume(row.channel));
                ChannelRow captured = row;
                row.volumeSlider.onValueChanged.AddListener(v => OnVolumeChanged(captured, v));
            }
            if (row.muteButton != null)
            {
                ChannelRow captured = row;
                row.muteButton.onClick.AddListener(() => OnMuteClicked(captured));
            }
        }

        // ── Callbacks ───────────────────────────────────────────────────────────

        private void OnVolumeChanged(ChannelRow row, float value)
        {
            AudioManager.Instance?.SetVolume(row.channel, value);
            UpdateMuteIcon(row);
            UpdatePercent(row, value);
        }

        private void OnMuteClicked(ChannelRow row)
        {
            AudioManager.Instance?.ToggleMute(row.channel);
            RefreshRow(row);
        }

        // ── Refresh ─────────────────────────────────────────────────────────────

        private void RefreshRow(ChannelRow row)
        {
            float volume = GetVolume(row.channel);
            row.volumeSlider?.SetValueWithoutNotify(volume);
            UpdateMuteIcon(row);
            UpdatePercent(row, volume);
        }

        private void UpdateMuteIcon(ChannelRow row)
        {
            if (row.muteIcon == null) return;
            Sprite[] sprites = FindSprites(row.channel);
            if (sprites == null || sprites.Length < 2) return;
            bool muted = AudioManager.Instance != null && AudioManager.Instance.IsMuted(row.channel);
            Sprite target = muted ? sprites[0] : sprites[1];
            if (target != null) row.muteIcon.sprite = target;
        }

        private void UpdatePercent(ChannelRow row, float linear01)
        {
            if (row.percentLabel != null)
                row.percentLabel.text = Mathf.RoundToInt(Mathf.Clamp01(linear01) * 100f) + "%";
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private Sprite[] FindSprites(AudioChannel channel)
        {
            if (_toggleButtonSprites == null) return null;
            foreach (var entry in _toggleButtonSprites)
                if (entry != null && entry.channel == channel) return entry.sprites;
            return null;
        }

        private static float GetVolume(AudioChannel channel)
            => AudioManager.Instance != null ? AudioManager.Instance.GetVolume(channel) : 1f;
    }
}
