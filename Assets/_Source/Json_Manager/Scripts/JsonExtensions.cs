using System.Collections.Generic;

namespace JsonUtilities
{
    /// <summary>
    /// Extension methods for convenient JSON operations directly on objects and strings.
    /// </summary>
    public static class JsonExtensions
    {
        // ── Object → JSON ─────────────────────────────────────────────────

        /// <summary>Serializes this object to a JSON string.</summary>
        public static string ToJson<T>(this T obj, bool prettyPrint = false)
            => JsonHelper.ToJson(obj, prettyPrint);

        /// <summary>Serializes this array to a JSON array string <c>[...]</c>.</summary>
        public static string ToJsonArray<T>(this T[] array, bool prettyPrint = false)
            => JsonHelper.ToJsonArray(array, prettyPrint);

        /// <summary>Serializes this list to a JSON array string <c>[...]</c>.</summary>
        public static string ToJsonList<T>(this List<T> list, bool prettyPrint = false)
            => JsonHelper.ToJsonList(list, prettyPrint);

        /// <summary>Returns a deep copy of this object via a JSON round-trip.</summary>
        public static T JsonClone<T>(this T obj)
            => JsonHelper.Clone(obj);

        // ── String → Object ───────────────────────────────────────────────

        /// <summary>Deserializes this JSON string to <typeparamref name="T"/>. Throws on malformed input.</summary>
        public static T FromJson<T>(this string json)
            => JsonHelper.FromJson<T>(json);

        /// <summary>
        /// Tries to deserialize this JSON string to <typeparamref name="T"/>.
        /// Returns <c>false</c> on failure without throwing.
        /// </summary>
        public static bool TryParseJson<T>(this string json, out T result)
            => JsonHelper.TryFromJson(json, out result);

        /// <summary>Deserializes this JSON array string to an array of <typeparamref name="T"/>.</summary>
        public static T[] FromJsonArray<T>(this string json)
            => JsonHelper.FromJsonArray<T>(json);

        /// <summary>Deserializes this JSON array string to a <see cref="List{T}"/>.</summary>
        public static List<T> FromJsonList<T>(this string json)
            => JsonHelper.FromJsonList<T>(json);

        // ── Validation ─────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if this string is a valid JSON object or array.</summary>
        public static bool IsValidJson(this string json)   => JsonHelper.IsValidJson(json);

        /// <summary>Returns <c>true</c> if this string is a JSON object <c>{}</c>.</summary>
        public static bool IsJsonObject(this string json)  => JsonHelper.IsJsonObject(json);

        /// <summary>Returns <c>true</c> if this string is a JSON array <c>[]</c>.</summary>
        public static bool IsJsonArray(this string json)   => JsonHelper.IsJsonArray(json);
    }
}
