using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DataManagement
{
    /// <summary>
    /// Loads CSV files from Unity Resources, StreamingAssets, or an absolute path
    /// and returns parsed data via <see cref="CSVParser"/>.
    /// </summary>
    public static class CSVReader
    {
        // ── Resources ──────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a CSV file from a Resources folder (omit the .csv extension).
        /// Example: <c>CSVReader.LoadFromResources("Data/Items")</c>
        /// </summary>
        public static List<string[]> LoadFromResources(string resourcePath, char delimiter = ',')
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[CSVReader] Resource not found: {resourcePath}");
                return new List<string[]>();
            }
            return CSVParser.Parse(asset.text, delimiter);
        }

        /// <summary>
        /// Loads a CSV from Resources and uses the first row as headers.
        /// </summary>
        public static List<Dictionary<string, string>> LoadFromResourcesWithHeaders(
            string resourcePath, char delimiter = ',')
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[CSVReader] Resource not found: {resourcePath}");
                return new List<Dictionary<string, string>>();
            }
            return CSVParser.ParseWithHeaders(asset.text, delimiter);
        }

        // ── StreamingAssets ────────────────────────────────────────────────────

        /// <summary>
        /// Loads a CSV from StreamingAssets (synchronous; not supported on WebGL at runtime).
        /// Path is relative to StreamingAssets, e.g. <c>"Data/Items.csv"</c>.
        /// </summary>
        public static List<string[]> LoadFromStreamingAssets(
            string relativePath, char delimiter = ',')
        {
            string text = ReadStreamingAssetsText(relativePath);
            return text == null ? new List<string[]>() : CSVParser.Parse(text, delimiter);
        }

        /// <summary>
        /// Loads a CSV from StreamingAssets and uses the first row as headers.
        /// </summary>
        public static List<Dictionary<string, string>> LoadFromStreamingAssetsWithHeaders(
            string relativePath, char delimiter = ',')
        {
            string text = ReadStreamingAssetsText(relativePath);
            return text == null
                ? new List<Dictionary<string, string>>()
                : CSVParser.ParseWithHeaders(text, delimiter);
        }

        // ── Absolute path ──────────────────────────────────────────────────────

        /// <summary>
        /// Loads a CSV from an absolute file-system path (Editor or standalone platforms).
        /// </summary>
        public static List<string[]> LoadFromPath(string absolutePath, char delimiter = ',')
        {
            string text = ReadFileText(absolutePath);
            return text == null ? new List<string[]>() : CSVParser.Parse(text, delimiter);
        }

        /// <summary>
        /// Loads a CSV from an absolute path and uses the first row as headers.
        /// </summary>
        public static List<Dictionary<string, string>> LoadFromPathWithHeaders(
            string absolutePath, char delimiter = ',')
        {
            string text = ReadFileText(absolutePath);
            return text == null
                ? new List<Dictionary<string, string>>()
                : CSVParser.ParseWithHeaders(text, delimiter);
        }

        // ── Raw text ───────────────────────────────────────────────────────────

        /// <summary>Parses raw CSV text directly (when you already have the content).</summary>
        public static List<string[]> Parse(string csvText, char delimiter = ',')
            => CSVParser.Parse(csvText, delimiter);

        /// <summary>Parses raw CSV text and uses the first row as headers.</summary>
        public static List<Dictionary<string, string>> ParseWithHeaders(
            string csvText, char delimiter = ',')
            => CSVParser.ParseWithHeaders(csvText, delimiter);

        // ── Private helpers ────────────────────────────────────────────────────

        private static string ReadStreamingAssetsText(string relativePath)
        {
            string full = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(full))
            {
                Debug.LogWarning($"[CSVReader] StreamingAssets file not found: {full}");
                return null;
            }
            try { return File.ReadAllText(full, System.Text.Encoding.UTF8); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CSVReader] Failed to read '{full}': {e.Message}");
                return null;
            }
        }

        private static string ReadFileText(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CSVReader] File not found: {path}");
                return null;
            }
            try { return File.ReadAllText(path, System.Text.Encoding.UTF8); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CSVReader] Failed to read '{path}': {e.Message}");
                return null;
            }
        }
    }
}
