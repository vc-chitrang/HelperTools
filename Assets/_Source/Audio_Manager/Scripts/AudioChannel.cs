using UnityEngine;

namespace AudioSystem
{
    /// <summary>
    /// The logical audio buses exposed to gameplay and UI.
    /// Each maps 1:1 to an Audio Mixer group + exposed volume parameter.
    /// </summary>
    public enum AudioChannel
    {
        Master,
        BGM,
        SFX,
        VO
    }

    /// <summary>
    /// Single source of truth for the exposed Audio Mixer parameter names.
    /// These strings MUST match the parameters exposed on the mixer asset
    /// (created by the editor builder). Keeping them here avoids magic strings
    /// scattered across the code (DRY).
    /// </summary>
    public static class AudioMixerParameters
    {
        public const string Master = "MasterVolume";
        public const string BGM    = "BGMVolume";
        public const string SFX    = "SFXVolume";
        public const string VO     = "VOVolume";

        /// <summary>Returns the exposed mixer parameter name for a channel.</summary>
        public static string For(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => Master,
            AudioChannel.BGM    => BGM,
            AudioChannel.SFX    => SFX,
            AudioChannel.VO     => VO,
            _                   => Master
        };
    }

    /// <summary>
    /// Converts between a user-facing linear volume [0,1] and the decibel scale the
    /// Audio Mixer actually uses. Mixer parameters are in dB — setting them with raw
    /// linear values is a classic bug (0.5 linear ≈ -6 dB, not "half volume").
    /// </summary>
    public static class AudioVolume
    {
        /// <summary>Decibel value treated as "silent".</summary>
        public const float MinDecibels = -80f;

        private const float MinLinear = 0.0001f;

        /// <summary>Linear [0,1] → decibels [-80, 0].</summary>
        public static float LinearToDecibels(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            return linear01 <= MinLinear ? MinDecibels : Mathf.Log10(linear01) * 20f;
        }

        /// <summary>Decibels → linear [0,1].</summary>
        public static float DecibelsToLinear(float decibels)
            => Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
    }
}
