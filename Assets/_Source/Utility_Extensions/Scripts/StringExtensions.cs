using System;
using System.Text.RegularExpressions;

namespace HelperTools
{
    /// <summary>String extension methods.</summary>
    public static class StringExtensions
    {
        // ── Null / empty guards ────────────────────────────────────────────

        public static bool IsNullOrEmpty(this string s)           => string.IsNullOrEmpty(s);
        public static bool IsNullOrWhiteSpace(this string s)      => string.IsNullOrWhiteSpace(s);
        public static bool HasValue(this string s)                => !string.IsNullOrWhiteSpace(s);

        // ── Case conversion ────────────────────────────────────────────────

        /// <summary>Converts "helloWorld" or "HelloWorld" → "Hello World".</summary>
        public static string ToTitleCase(this string s)
        {
            if (s.IsNullOrEmpty()) return s;
            return Regex.Replace(s, @"([a-z])([A-Z])", "$1 $2")
                        .Replace("_", " ")
                        .Trim();
        }

        /// <summary>Converts "Hello World" → "hello_world".</summary>
        public static string ToSnakeCase(this string s)
        {
            if (s.IsNullOrEmpty()) return s;
            return Regex.Replace(s.Trim(), @"\s+", "_").ToLowerInvariant();
        }

        /// <summary>Converts "hello_world" or "Hello World" → "helloWorld".</summary>
        public static string ToCamelCase(this string s)
        {
            if (s.IsNullOrEmpty()) return s;
            var parts = s.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return s;
            var result = parts[0].ToLowerInvariant();
            for (int i = 1; i < parts.Length; i++)
                result += char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
            return result;
        }

        // ── Formatting ─────────────────────────────────────────────────────

        /// <summary>Truncates to <paramref name="maxLength"/> chars, appending "…" if cut.</summary>
        public static string Truncate(this string s, int maxLength, string ellipsis = "…")
        {
            if (s == null || s.Length <= maxLength) return s;
            return s[..(maxLength - ellipsis.Length)] + ellipsis;
        }

        /// <summary>Repeats the string <paramref name="count"/> times.</summary>
        public static string Repeat(this string s, int count) =>
            count <= 0 ? string.Empty : string.Concat(System.Linq.Enumerable.Repeat(s, count));

        // ── Parsing helpers ────────────────────────────────────────────────

        /// <summary>Parses to int, returning <paramref name="fallback"/> on failure.</summary>
        public static int ToInt(this string s, int fallback = 0) =>
            int.TryParse(s, out int v) ? v : fallback;

        /// <summary>Parses to float, returning <paramref name="fallback"/> on failure.</summary>
        public static float ToFloat(this string s, float fallback = 0f) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

        // ── Validation ─────────────────────────────────────────────────────

        /// <summary>Checks if the string is a valid email address pattern.</summary>
        public static bool IsValidEmail(this string s)
        {
            if (s.IsNullOrWhiteSpace()) return false;
            return Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        }

        /// <summary>Checks if the string contains only digits.</summary>
        public static bool IsNumeric(this string s) =>
            !s.IsNullOrEmpty() && Regex.IsMatch(s, @"^\d+$");

        // ── Unity rich text ────────────────────────────────────────────────

        /// <summary>Wraps the string in a Unity rich-text color tag.</summary>
        public static string Colorize(this string s, UnityEngine.Color c) =>
            $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        /// <summary>Wraps the string in a Unity rich-text bold tag.</summary>
        public static string Bold(this string s) => $"<b>{s}</b>";

        /// <summary>Wraps the string in a Unity rich-text italic tag.</summary>
        public static string Italic(this string s) => $"<i>{s}</i>";
    }
}
