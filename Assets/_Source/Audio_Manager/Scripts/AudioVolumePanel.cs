using System;
using UnityEngine;
using UnityEngine.UI;

namespace AudioSystem
{
    /// <summary>
    /// <para>
    /// Presentation-only volume panel. Each <see cref="ChannelRow"/> binds a UI
    /// slider (0–100%) and a mute toggle button to one <see cref="AudioChannel"/>.
    /// The mute button swaps its icon between two sprites.
    /// </para>
    /// <para>
    /// This component contains <b>no audio logic</b> — it only reads/writes through
    /// the <see cref="AudioManager"/> facade, keeping UI and audio fully decoupled
    /// (separation of concern).
    /// </para>
    /// </summary>
    public class AudioVolumePanel : MonoBehaviour
    {
        /// <summary>Binds one channel to its slider + mute button widgets.</summary>
        [Serializable]
        public class ChannelRow
        {
            public AudioChannel channel;
            public Slider volumeSlider;
            public Button muteButton;
            [Tooltip("Image whose sprite is swapped between mute/unmute. Usually the muteButton's image.")]
            public Image muteIcon;
            [Tooltip("Optional label that shows the current volume as a percentage.")]
            public Text percentLabel;
        }

        [SerializeField] private ChannelRow[] _rows;

        [Tooltip("Index 0 = MUTE sprite (shown when muted), Index 1 = UNMUTE sprite (shown when audible).")]
        [SerializeField] private Sprite[] _toggleButtonSprites;

        private void Start()
        {
            if (AudioManager.Instance == null)
                Debug.LogWarning("[AudioVolumePanel] No AudioManager in scene; the panel will be inert.");

            if (_rows == null) return;

            foreach (ChannelRow row in _rows)
            {
                if (row == null) continue;
                BindRow(row);
                RefreshRow(row);
            }
        }

        private void BindRow(ChannelRow row)
        {
            if (row.volumeSlider != null)
            {
                row.volumeSlider.minValue = 0f;
                row.volumeSlider.maxValue = 1f;
                row.volumeSlider.wholeNumbers = false;
                row.volumeSlider.SetValueWithoutNotify(GetVolume(row.channel));
                // Capture the row so each listener targets the right channel.
                ChannelRow captured = row;
                row.volumeSlider.onValueChanged.AddListener(v => OnVolumeChanged(captured, v));
            }

            if (row.muteButton != null)
            {
                ChannelRow captured = row;
                row.muteButton.onClick.AddListener(() => OnMuteClicked(captured));
            }
        }

        private void OnVolumeChanged(ChannelRow row, float value)
        {
            AudioManager.Instance?.SetVolume(row.channel, value);
            UpdatePercent(row, value);
        }

        private void OnMuteClicked(ChannelRow row)
        {
            AudioManager.Instance?.ToggleMute(row.channel);
            RefreshRow(row);
        }

        /// <summary>Syncs a row's widgets with the current AudioManager state.</summary>
        private void RefreshRow(ChannelRow row)
        {
            float volume = GetVolume(row.channel);
            if (row.volumeSlider != null)
                row.volumeSlider.SetValueWithoutNotify(volume);

            UpdatePercent(row, volume);
            UpdateMuteIcon(row);
        }

        private void UpdateMuteIcon(ChannelRow row)
        {
            if (row.muteIcon == null || _toggleButtonSprites == null || _toggleButtonSprites.Length < 2)
                return;

            bool muted = AudioManager.Instance != null && AudioManager.Instance.IsMuted(row.channel);
            row.muteIcon.sprite = muted ? _toggleButtonSprites[0] : _toggleButtonSprites[1];
        }

        private void UpdatePercent(ChannelRow row, float linear01)
        {
            if (row.percentLabel != null)
                row.percentLabel.text = Mathf.RoundToInt(Mathf.Clamp01(linear01) * 100f) + "%";
        }

        private static float GetVolume(AudioChannel channel)
            => AudioManager.Instance != null ? AudioManager.Instance.GetVolume(channel) : 1f;
    }
}
