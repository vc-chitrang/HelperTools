using System;
using System.IO;
using UnityEngine;

namespace JsonUtilities
{
    /// <summary>
    /// File-backed JSON storage at <see cref="Application.persistentDataPath"/>.
    /// Writes are atomic (temp → rename) to prevent data corruption on crash.
    /// </summary>
    public static class JsonFileManager
    {
        private static string _subFolder = string.Empty;

        /// <summary>
        /// Optional sub-folder inside <see cref="Application.persistentDataPath"/> (e.g. "saves").
        /// Set before calling any other method.
        /// </summary>
        public static string SubFolder
        {
            get => _subFolder;
            set => _subFolder = value ?? string.Empty;
        }

        // ── Path helpers ──────────────────────────────────────────────────

        /// <summary>Returns the full absolute path for the given file name.</summary>
        public static string GetPath(string fileName)
        {
            string dir = string.IsNullOrEmpty(_subFolder)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, _subFolder);
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";
            return Path.Combine(dir, fileName);
        }

        // ── Write ─────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes <paramref name="data"/> and writes it to
        /// <c>persistentDataPath/[SubFolder]/fileName.json</c> atomically.
        /// </summary>
        public static void Save<T>(T data, string fileName, bool prettyPrint = false)
        {
            string path = GetPath(fileName);
            EnsureDirectory(path);
            WriteAtomic(path, JsonHelper.ToJson(data, prettyPrint));
        }

        // ── Read ──────────────────────────────────────────────────────────

        /// <summary>
        /// Loads and deserializes a JSON file.
        /// Returns <c>new T()</c> if the file does not exist.
        /// </summary>
        public static T Load<T>(string fileName) where T : new()
        {
            string path = GetPath(fileName);
            if (!File.Exists(path)) return new T();
            return JsonHelper.FromJson<T>(File.ReadAllText(path));
        }

        /// <summary>
        /// Tries to load and deserialize a JSON file.
        /// Returns <c>false</c> if the file is missing or the JSON is invalid.
        /// </summary>
        public static bool TryLoad<T>(string fileName, out T result) where T : new()
        {
            result = new T();
            string path = GetPath(fileName);
            if (!File.Exists(path)) return false;
            try
            {
                return JsonHelper.TryFromJson(File.ReadAllText(path), out result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileManager] TryLoad '{fileName}' failed: {e.Message}");
                return false;
            }
        }

        // ── Exists / Delete / List ────────────────────────────────────────

        /// <summary>Returns <c>true</c> if the JSON file for <paramref name="fileName"/> exists.</summary>
        public static bool Exists(string fileName) => File.Exists(GetPath(fileName));

        /// <summary>Deletes the JSON file and its backup (.bak) if present.</summary>
        public static void Delete(string fileName)
        {
            string path = GetPath(fileName);
            if (File.Exists(path))       File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }

        /// <summary>Returns absolute paths of all JSON files in the storage folder.</summary>
        public static string[] ListFiles()
        {
            string dir = string.IsNullOrEmpty(_subFolder)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, _subFolder);
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json")
                : Array.Empty<string>();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        // Write to .tmp, then atomically rename over the target file.
        private static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak");
            else
                File.Move(tmp, path);
        }
    }
}
