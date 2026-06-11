using System;
using System.Collections.Generic;
using UnityEngine;

namespace JsonUtilities
{
    /// <summary>
    /// Core JSON serialization helpers built on UnityEngine.JsonUtility.
    /// Adds array/list support, deep clone, partial merge, and validation.
    /// </summary>
    public static class JsonHelper
    {
        // Wrapper required because JsonUtility cannot serialize a top-level array.
        [Serializable]
        private class ArrayWrapper<T> { public T[] items; }

        // ── Serialize ─────────────────────────────────────────────────────

        /// <summary>Serializes <paramref name="obj"/> to a JSON string.</summary>
        public static string ToJson<T>(T obj, bool prettyPrint = false)
            => JsonUtility.ToJson(obj, prettyPrint);

        /// <summary>
        /// Serializes an array to a JSON array string <c>[...]</c>.
        /// Handles the JsonUtility top-level-array limitation internally.
        /// </summary>
        public static string ToJsonArray<T>(T[] array, bool prettyPrint = false)
        {
            if (array == null) return "[]";
            string raw = JsonUtility.ToJson(new ArrayWrapper<T> { items = array }, prettyPrint);
            return StripWrapper(raw);
        }

        /// <summary>Serializes a <see cref="List{T}"/> to a JSON array string.</summary>
        public static string ToJsonList<T>(List<T> list, bool prettyPrint = false)
            => ToJsonArray(list?.ToArray() ?? Array.Empty<T>(), prettyPrint);

        // ── Deserialize ───────────────────────────────────────────────────

        /// <summary>Deserializes a JSON string. Throws on malformed input.</summary>
        public static T FromJson<T>(string json)
            => JsonUtility.FromJson<T>(json);

        /// <summary>
        /// Tries to deserialize a JSON string.
        /// Returns <c>false</c> and sets <paramref name="result"/> to <c>default</c> on failure.
        /// </summary>
        public static bool TryFromJson<T>(string json, out T result)
        {
            result = default;
            if (!IsValidJson(json)) return false;
            try
            {
                result = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonHelper] TryFromJson<{typeof(T).Name}> failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Deserializes a JSON array string <c>[...]</c> to an array.</summary>
        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<T>();
            string wrapped = $"{{\"items\":{json}}}";
            var wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(wrapped);
            return wrapper?.items ?? Array.Empty<T>();
        }

        /// <summary>Deserializes a JSON array string <c>[...]</c> to a <see cref="List{T}"/>.</summary>
        public static List<T> FromJsonList<T>(string json)
            => new List<T>(FromJsonArray<T>(json));

        // ── Clone ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a deep copy of <paramref name="obj"/> via a JSON round-trip.
        /// <typeparamref name="T"/> must be <c>[Serializable]</c>.
        /// </summary>
        public static T Clone<T>(T obj) => FromJson<T>(ToJson(obj));

        // ── Merge ─────────────────────────────────────────────────────────

        /// <summary>
        /// Applies fields present in <paramref name="partialJson"/> onto <paramref name="target"/>.
        /// Fields absent from the partial JSON keep their original values.
        /// Best suited for class (reference) types.
        /// </summary>
        public static T Merge<T>(T target, string partialJson)
        {
            JsonUtility.FromJsonOverwrite(partialJson, target);
            return target;
        }

        // ── Validation ────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if <paramref name="json"/> looks like a valid JSON object or array.</summary>
        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            json = json.Trim();
            return (json.StartsWith("{", StringComparison.Ordinal) && json.EndsWith("}", StringComparison.Ordinal))
                || (json.StartsWith("[", StringComparison.Ordinal) && json.EndsWith("]", StringComparison.Ordinal));
        }

        /// <summary>Returns <c>true</c> when the JSON root is an object <c>{}</c>.</summary>
        public static bool IsJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            json = json.Trim();
            return json.StartsWith("{", StringComparison.Ordinal)
                && json.EndsWith("}",   StringComparison.Ordinal);
        }

        /// <summary>Returns <c>true</c> when the JSON root is an array <c>[]</c>.</summary>
        public static bool IsJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            json = json.Trim();
            return json.StartsWith("[", StringComparison.Ordinal)
                && json.EndsWith("]",   StringComparison.Ordinal);
        }

        // ── Private helpers ───────────────────────────────────────────────

        // Extracts the array value from {"items":[...]} → [...]
        private static string StripWrapper(string wrapped)
        {
            const string key = "\"items\":";
            int start = wrapped.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return "[]";
            start += key.Length;
            int end = wrapped.LastIndexOf('}');
            return end > start ? wrapped.Substring(start, end - start) : "[]";
        }
    }
}
