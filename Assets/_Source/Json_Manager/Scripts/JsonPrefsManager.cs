using UnityEngine;

namespace JsonUtilities
{
    /// <summary>
    /// Stores and retrieves serializable objects as JSON strings inside <see cref="PlayerPrefs"/>.
    /// Ideal for lightweight settings that don't warrant a dedicated save file.
    /// All keys are automatically prefixed to avoid collisions.
    /// </summary>
    public static class JsonPrefsManager
    {
        private static string _keyPrefix = "json.";

        /// <summary>
        /// Prefix applied to every PlayerPrefs key (default <c>"json."</c>).
        /// Change before first use if you need a custom namespace.
        /// </summary>
        public static string KeyPrefix
        {
            get => _keyPrefix;
            set => _keyPrefix = value ?? string.Empty;
        }

        // ── Write ─────────────────────────────────────────────────────────

        /// <summary>Serializes <paramref name="data"/> and stores it under <paramref name="key"/>.</summary>
        public static void Set<T>(string key, T data, bool prettyPrint = false)
            => PlayerPrefs.SetString(BuildKey(key), JsonHelper.ToJson(data, prettyPrint));

        // ── Read ──────────────────────────────────────────────────────────

        /// <summary>
        /// Loads and deserializes the value stored under <paramref name="key"/>.
        /// Returns <c>new T()</c> if the key does not exist or the JSON is invalid.
        /// </summary>
        public static T Get<T>(string key) where T : new()
        {
            string raw = PlayerPrefs.GetString(BuildKey(key), string.Empty);
            if (string.IsNullOrEmpty(raw)) return new T();
            return JsonHelper.TryFromJson(raw, out T result) ? result : new T();
        }

        /// <summary>
        /// Tries to load and deserialize the value stored under <paramref name="key"/>.
        /// Returns <c>false</c> if the key is missing or the JSON is invalid.
        /// </summary>
        public static bool TryGet<T>(string key, out T result) where T : new()
        {
            result = new T();
            string raw = PlayerPrefs.GetString(BuildKey(key), string.Empty);
            return !string.IsNullOrEmpty(raw) && JsonHelper.TryFromJson(raw, out result);
        }

        // ── Exists / Delete ───────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if <paramref name="key"/> has a stored value.</summary>
        public static bool HasKey(string key) => PlayerPrefs.HasKey(BuildKey(key));

        /// <summary>Removes the stored value for <paramref name="key"/>.</summary>
        public static void Delete(string key) => PlayerPrefs.DeleteKey(BuildKey(key));

        /// <summary>
        /// Flushes all pending PlayerPrefs writes to disk.
        /// Called automatically on application quit; call manually after critical saves on mobile.
        /// </summary>
        public static void Flush() => PlayerPrefs.Save();

        // ── Private helpers ───────────────────────────────────────────────

        private static string BuildKey(string key) => _keyPrefix + key;
    }
}
