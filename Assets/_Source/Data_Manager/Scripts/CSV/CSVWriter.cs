using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DataManagement
{
    /// <summary>
    /// Writes data to CSV files on disk.
    /// For in-memory serialization use <see cref="CSVParser.Serialize"/> directly.
    /// </summary>
    public static class CSVWriter
    {
        // ── Write rows ─────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a list of string-array rows to a CSV file.
        /// Creates the directory if it does not exist.
        /// </summary>
        public static bool Write(string absolutePath, IEnumerable<string[]> rows,
                                 char delimiter = ',')
        {
            try
            {
                EnsureDirectory(absolutePath);
                string csv = CSVParser.Serialize(rows, delimiter);
                File.WriteAllText(absolutePath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CSVWriter] Failed to write '{absolutePath}': {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Writes a list of rows where each row is a dictionary of column-name → value.
        /// Column names are taken from the keys of the first dictionary.
        /// </summary>
        public static bool Write(string absolutePath,
                                 IReadOnlyList<Dictionary<string, string>> rows,
                                 char delimiter = ',')
        {
            if (rows == null || rows.Count == 0)
                return Write(absolutePath, new List<string[]>(), delimiter);

            var headers = new List<string>(rows[0].Keys);
            var allRows = new List<string[]>(rows.Count + 1) { headers.ToArray() };
            foreach (var row in rows)
            {
                var values = new string[headers.Count];
                for (int i = 0; i < headers.Count; i++)
                    values[i] = row.TryGetValue(headers[i], out string v) ? v : string.Empty;
                allRows.Add(values);
            }
            return Write(absolutePath, allRows, delimiter);
        }

        /// <summary>
        /// Serializes a collection of objects to CSV and writes to a file.
        /// Column headers are derived from public field and property names.
        /// </summary>
        public static bool Write<T>(string absolutePath, IEnumerable<T> objects,
                                    char delimiter = ',') where T : class
        {
            try
            {
                EnsureDirectory(absolutePath);
                string csv = CSVParser.Serialize(objects, delimiter);
                File.WriteAllText(absolutePath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CSVWriter] Failed to write '{absolutePath}': {e.Message}");
                return false;
            }
        }

        // ── PersistentDataPath convenience ──────────────────────────────────────

        /// <summary>
        /// Writes a CSV to <c>Application.persistentDataPath/fileName</c>.
        /// </summary>
        public static bool WriteToPersistentData(string fileName, IEnumerable<string[]> rows,
                                                  char delimiter = ',')
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            return Write(path, rows, delimiter);
        }

        /// <summary>
        /// Writes object data to <c>Application.persistentDataPath/fileName</c>.
        /// </summary>
        public static bool WriteToPersistentData<T>(string fileName, IEnumerable<T> objects,
                                                     char delimiter = ',') where T : class
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            return Write(path, objects, delimiter);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
