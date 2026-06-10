using UnityEngine;

namespace HelperTools
{
    /// <summary>Numeric extension methods for int and float.</summary>
    public static class NumberExtensions
    {
        // ── Float helpers ──────────────────────────────────────────────────

        /// <summary>Clamps the value between 0 and 1.</summary>
        public static float Clamp01(this float v) => Mathf.Clamp01(v);

        /// <summary>Clamps the value between <paramref name="min"/> and <paramref name="max"/>.</summary>
        public static float Clamp(this float v, float min, float max) => Mathf.Clamp(v, min, max);

        /// <summary>Rounds to the nearest integer.</summary>
        public static float Round(this float v) => Mathf.Round(v);

        /// <summary>Rounds down to the nearest integer.</summary>
        public static float Floor(this float v) => Mathf.Floor(v);

        /// <summary>Rounds up to the nearest integer.</summary>
        public static float Ceil(this float v) => Mathf.Ceil(v);

        /// <summary>Absolute value.</summary>
        public static float Abs(this float v) => Mathf.Abs(v);

        /// <summary>True if within ±<paramref name="epsilon"/> of <paramref name="other"/>.</summary>
        public static bool Approximately(this float v, float other, float epsilon = 0.0001f) =>
            Mathf.Abs(v - other) <= epsilon;

        /// <summary>Remaps from [<paramref name="inMin"/>, <paramref name="inMax"/>] to [<paramref name="outMin"/>, <paramref name="outMax"/>].</summary>
        public static float Remap(this float v, float inMin, float inMax, float outMin, float outMax) =>
            Mathf.Lerp(outMin, outMax, Mathf.InverseLerp(inMin, inMax, v));

        /// <summary>Linear interpolation toward <paramref name="target"/> by t.</summary>
        public static float LerpTo(this float v, float target, float t) => Mathf.Lerp(v, target, t);

        // ── Int helpers ────────────────────────────────────────────────────

        /// <summary>Clamps between <paramref name="min"/> and <paramref name="max"/>.</summary>
        public static int Clamp(this int v, int min, int max) => Mathf.Clamp(v, min, max);

        /// <summary>Absolute value.</summary>
        public static int Abs(this int v) => Mathf.Abs(v);

        /// <summary>True if the integer is between <paramref name="min"/> (inclusive) and <paramref name="max"/> (exclusive).</summary>
        public static bool InRange(this int v, int min, int max) => v >= min && v < max;

        /// <summary>True if the value is even.</summary>
        public static bool IsEven(this int v) => (v & 1) == 0;

        /// <summary>True if the value is odd.</summary>
        public static bool IsOdd(this int v) => (v & 1) != 0;

        // ── Angle helpers ──────────────────────────────────────────────────

        /// <summary>Converts degrees to radians.</summary>
        public static float ToRadians(this float degrees) => degrees * Mathf.Deg2Rad;

        /// <summary>Converts radians to degrees.</summary>
        public static float ToDegrees(this float radians) => radians * Mathf.Rad2Deg;

        /// <summary>Wraps an angle to the range [0, 360).</summary>
        public static float WrapAngle(this float angle) => ((angle % 360f) + 360f) % 360f;

        // ── Formatting ─────────────────────────────────────────────────────

        /// <summary>Formats seconds as "m:ss" (e.g. 75 → "1:15").</summary>
        public static string ToTimeString(this float totalSeconds)
        {
            int minutes = (int)(totalSeconds / 60f);
            int seconds = (int)(totalSeconds % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        /// <summary>Formats seconds as "h:mm:ss" when >= 1 hour, otherwise "m:ss".</summary>
        public static string ToTimeStringLong(this float totalSeconds)
        {
            int hours   = (int)(totalSeconds / 3600f);
            int minutes = (int)(totalSeconds % 3600f / 60f);
            int seconds = (int)(totalSeconds % 60f);
            return hours > 0 ? $"{hours}:{minutes:D2}:{seconds:D2}" : $"{minutes}:{seconds:D2}";
        }
    }
}
