using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Note: System.IO.Compression is part of .NET Standard 2.0 and is available in Unity 2020+.
// WebGL does NOT support ZipArchive at runtime — use Editor-side conversion to CSV instead.

namespace DataManagement
{
    /// <summary>
    /// Reads Excel .xlsx files at runtime without any third-party package.
    /// Supports basic cell types: numbers, strings (shared and inline), booleans.
    /// Does NOT support formulas, charts, merged cells, or rich text — values are read as-is.
    ///
    /// <b>Platform support:</b> Editor, Windows, macOS, Android, iOS.
    /// WebGL is NOT supported (no ZipArchive). Check <see cref="IsSupported"/> before calling.
    /// </summary>
    public static class ExcelReader
    {
        private static readonly XNamespace NS =
            "http://schemas.openxmlformats.org/spreadsheetml/sheet";

        // ── Platform guard ────────────────────────────────────────────────────

        /// <summary>Returns true on platforms where .xlsx reading is supported.</summary>
        public static bool IsSupported =>
#if UNITY_WEBGL && !UNITY_EDITOR
            false;
#else
            true;
#endif

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Reads all rows from the first (or specified) sheet of an .xlsx file.
        /// Returns null on failure.
        /// </summary>
        public static List<string[]> ReadFile(string absolutePath, int sheetIndex = 0)
        {
            if (!CheckSupport()) return null;
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning($"[ExcelReader] File not found: {absolutePath}");
                return null;
            }
            try { return Read(File.ReadAllBytes(absolutePath), sheetIndex); }
            catch (Exception e)
            {
                Debug.LogError($"[ExcelReader] Failed to read '{absolutePath}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads from StreamingAssets (path relative to StreamingAssets folder).
        /// </summary>
        public static List<string[]> ReadFromStreamingAssets(string relativePath,
                                                              int sheetIndex = 0)
        {
            if (!CheckSupport()) return null;
            string full = Path.Combine(Application.streamingAssetsPath, relativePath);
            return ReadFile(full, sheetIndex);
        }

        /// <summary>
        /// Reads all rows using the first row as column headers.
        /// Returns an empty list on failure.
        /// </summary>
        public static List<Dictionary<string, string>> ReadWithHeaders(string absolutePath,
                                                                        int sheetIndex = 0)
        {
            var rows = ReadFile(absolutePath, sheetIndex);
            return rows != null ? RowsToDict(rows) : new List<Dictionary<string, string>>();
        }

        /// <summary>Reads from bytes (e.g. downloaded via UnityWebRequest).</summary>
        public static List<string[]> Read(byte[] xlsxBytes, int sheetIndex = 0)
        {
            if (!CheckSupport()) return null;
            using (var ms = new MemoryStream(xlsxBytes))
            using (var zip = new System.IO.Compression.ZipArchive(ms,
                       System.IO.Compression.ZipArchiveMode.Read))
            {
                var sharedStrings = ReadSharedStrings(zip);
                var entry = GetSheetEntry(zip, sheetIndex);
                if (entry == null)
                {
                    Debug.LogWarning($"[ExcelReader] Sheet index {sheetIndex} not found.");
                    return null;
                }
                return ParseSheet(entry, sharedStrings);
            }
        }

        // ── Private — ZIP / XML parsing ────────────────────────────────────────

        private static List<string> ReadSharedStrings(System.IO.Compression.ZipArchive zip)
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();

            using (var stream = entry.Open())
            {
                var doc = XDocument.Load(stream);
                return doc.Root?.Elements(NS + "si")
                    .Select(si =>
                    {
                        // Try simple <t> first, then concatenate all <t> inside <r> (rich text)
                        var t = si.Element(NS + "t");
                        if (t != null) return t.Value;
                        return string.Concat(si.Descendants(NS + "t").Select(x => x.Value));
                    })
                    .ToList() ?? new List<string>();
            }
        }

        private static System.IO.Compression.ZipArchiveEntry GetSheetEntry(
            System.IO.Compression.ZipArchive zip, int sheetIndex)
        {
            // Standard path: xl/worksheets/sheet{n+1}.xml
            var entry = zip.GetEntry($"xl/worksheets/sheet{sheetIndex + 1}.xml");
            if (entry != null) return entry;

            // Fallback: find any sheet entry sorted by name
            return zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet") &&
                            e.FullName.EndsWith(".xml"))
                .OrderBy(e => e.FullName)
                .Skip(sheetIndex)
                .FirstOrDefault();
        }

        private static List<string[]> ParseSheet(System.IO.Compression.ZipArchiveEntry entry,
                                                   List<string> sharedStrings)
        {
            using (var stream = entry.Open())
            {
                var doc  = XDocument.Load(stream);
                var rows = doc.Descendants(NS + "row")
                              .OrderBy(r => ParseInt((string)r.Attribute("r")))
                              .ToList();

                if (!rows.Any()) return new List<string[]>();

                // Find the max column index across all rows
                int maxCol = rows
                    .SelectMany(r => r.Elements(NS + "c"))
                    .Select(c => ColIndexFromRef((string)c.Attribute("r")))
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                var result    = new List<string[]>();
                int prevRowNo = 0;

                foreach (var rowEl in rows)
                {
                    int rowNo = ParseInt((string)rowEl.Attribute("r"));

                    // Fill skipped rows with empty arrays
                    while (prevRowNo < rowNo - 1)
                    {
                        result.Add(new string[maxCol]);
                        prevRowNo++;
                    }

                    var cells = new string[maxCol];
                    foreach (var cellEl in rowEl.Elements(NS + "c"))
                    {
                        int colIdx = ColIndexFromRef((string)cellEl.Attribute("r"));
                        if (colIdx < 0 || colIdx >= maxCol) continue;

                        string cellType = (string)cellEl.Attribute("t") ?? string.Empty;
                        string rawValue = cellEl.Element(NS + "v")?.Value ?? string.Empty;

                        cells[colIdx] = cellType switch
                        {
                            "s"    => SharedString(sharedStrings, rawValue),    // shared string
                            "str"  => cellEl.Element(NS + "v")?.Value ?? "",    // inline string formula result
                            "inlineStr" => cellEl.Element(NS + "is")?.Element(NS + "t")?.Value ?? "",
                            "b"    => rawValue == "1" ? "TRUE" : "FALSE",       // boolean
                            "e"    => rawValue,                                  // error
                            _      => FormatNumber(rawValue),                   // number (default)
                        };
                    }

                    result.Add(cells);
                    prevRowNo = rowNo;
                }
                return result;
            }
        }

        // ── Private — cell reference helpers ──────────────────────────────────

        // "C5" → col index 2; "AA10" → col index 26
        private static int ColIndexFromRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return 0;
            int col = 0;
            foreach (char c in cellRef)
            {
                if (!char.IsLetter(c)) break;
                col = col * 26 + (c - 'A' + 1);
            }
            return col - 1;
        }

        private static int ParseInt(string s) =>
            int.TryParse(s, out int v) ? v : 0;

        private static string SharedString(List<string> table, string raw) =>
            int.TryParse(raw, out int idx) && idx < table.Count ? table[idx] : raw;

        // Trims unnecessary trailing zeros from numeric strings
        private static string FormatNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            if (raw.Contains('.') || raw.Contains(','))
            {
                if (decimal.TryParse(raw,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                    return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return raw;
        }

        private static List<Dictionary<string, string>> RowsToDict(List<string[]> rows)
        {
            if (rows.Count == 0) return new List<Dictionary<string, string>>();
            var headers = rows[0];
            var result  = new List<Dictionary<string, string>>(rows.Count - 1);
            for (int i = 1; i < rows.Count; i++)
            {
                var dict = new Dictionary<string, string>(headers.Length,
                    StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Length; j++)
                    dict[headers[j]?.Trim() ?? string.Empty] =
                        j < rows[i].Length ? rows[i][j] ?? string.Empty : string.Empty;
                result.Add(dict);
            }
            return result;
        }

        private static bool CheckSupport()
        {
            if (!IsSupported)
            {
                Debug.LogWarning("[ExcelReader] .xlsx reading is not supported on WebGL. " +
                                 "Convert to CSV in the Editor instead.");
                return false;
            }
            return true;
        }
    }
}
