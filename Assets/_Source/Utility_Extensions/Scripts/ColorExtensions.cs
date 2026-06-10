using UnityEngine;

namespace HelperTools
{
    /// <summary>Color extension methods.</summary>
    public static class ColorExtensions
    {
        // ── Component setters ──────────────────────────────────────────────

        public static Color WithR(this Color c, float r) => new Color(r, c.g, c.b, c.a);
        public static Color WithG(this Color c, float g) => new Color(c.r, g, c.b, c.a);
        public static Color WithB(this Color c, float b) => new Color(c.r, c.g, b, c.a);
        public static Color WithA(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ── Derivation ─────────────────────────────────────────────────────

        /// <summary>Returns the same color at the given opacity (0–1).</summary>
        public static Color WithAlpha(this Color c, float alpha) =>
            new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));

        /// <summary>Linearly blends toward white by <paramref name="amount"/> (0 = no change, 1 = white).</summary>
        public static Color Lighten(this Color c, float amount) =>
            Color.Lerp(c, Color.white, Mathf.Clamp01(amount));

        /// <summary>Linearly blends toward black by <paramref name="amount"/> (0 = no change, 1 = black).</summary>
        public static Color Darken(this Color c, float amount) =>
            Color.Lerp(c, Color.black, Mathf.Clamp01(amount));

        /// <summary>Returns the complementary (180°-hue-shift) color.</summary>
        public static Color Complement(this Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB((h + 0.5f) % 1f, s, v).WithA(c.a);
        }

        // ── Conversion ─────────────────────────────────────────────────────

        /// <summary>Converts to a hex string such as "FF8844" (no leading #).</summary>
        public static string ToHex(this Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Converts to a hex string such as "FF884480" (RGBA, no leading #).</summary>
        public static string ToHexA(this Color c) => ColorUtility.ToHtmlStringRGBA(c);

        /// <summary>Parses a hex color string (with or without #). Returns <c>Color.white</c> on failure.</summary>
        public static Color FromHex(string hex)
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var col) ? col : Color.white;
        }

        // ── Comparison ─────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if all RGBA channels are within <paramref name="tolerance"/>.</summary>
        public static bool ApproximatelyEquals(this Color a, Color b, float tolerance = 0.01f) =>
            Mathf.Abs(a.r - b.r) <= tolerance &&
            Mathf.Abs(a.g - b.g) <= tolerance &&
            Mathf.Abs(a.b - b.b) <= tolerance &&
            Mathf.Abs(a.a - b.a) <= tolerance;

        // ── Grayscale ──────────────────────────────────────────────────────

        /// <summary>Returns the perceived luminance (0.299 R + 0.587 G + 0.114 B).</summary>
        public static float Luminance(this Color c) =>
            0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>Returns <c>true</c> if the color is "dark" (luminance < 0.5).</summary>
        public static bool IsDark(this Color c) => c.Luminance() < 0.5f;
    }
}
