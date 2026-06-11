using System;
using System.Collections.Generic;
using System.Text;

namespace DataManagement
{
    /// <summary>
    /// RFC 4180-compliant CSV parser and serializer.
    /// Handles quoted fields, embedded commas, embedded newlines, and escaped quotes ("").
    /// </summary>
    public static class CSVParser
    {
        // ── Parse ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses CSV text into a list of rows, each row being an array of field values.
        /// Supports comma, semicolon, or tab delimiters.
        /// </summary>
        public static List<string[]> Parse(string text, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(text)) return new List<string[]>();

            var result = new List<string[]>();
            int pos = 0;

            while (pos < text.Length)
            {
                var row = ParseRow(text, ref pos, delimiter);
                if (row != null) result.Add(row);
            }

            // Drop trailing empty rows caused by a final newline
            while (result.Count > 0 && IsEmptyRow(result[result.Count - 1]))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        /// <summary>
        /// Parses CSV text and uses the first row as column headers.
        /// Returns a list of dictionaries where keys are header names.
        /// </summary>
        public static List<Dictionary<string, string>> ParseWithHeaders(
            string text, char delimiter = ',')
        {
            var rows = Parse(text, delimiter);
            if (rows.Count == 0) return new List<Dictionary<string, string>>();

            var headers = rows[0];
            var result = new List<Dictionary<string, string>>(rows.Count - 1);

            for (int i = 1; i < rows.Count; i++)
            {
                var dict = new Dictionary<string, string>(headers.Length,
                    StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Length; j++)
                    dict[headers[j].Trim()] = j < rows[i].Length ? rows[i][j] : string.Empty;
                result.Add(dict);
            }
            return result;
        }

        // ── Serialize ──────────────────────────────────────────────────────────

        /// <summary>Serializes rows of fields back into RFC 4180-compliant CSV text.</summary>
        public static string Serialize(IEnumerable<string[]> rows, char delimiter = ',')
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    sb.Append(EscapeField(row[i] ?? string.Empty, delimiter));
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes a collection of objects to CSV using public fields and properties as columns.
        /// The first row is a header row with member names.
        /// </summary>
        public static string Serialize<T>(IEnumerable<T> objects, char delimiter = ',') where T : class
        {
            var type = typeof(T);
            var fields = type.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var props = type.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var rows = new List<string[]>();

            // Header row
            var headers = new string[fields.Length + props.Length];
            for (int i = 0; i < fields.Length; i++) headers[i]              = fields[i].Name;
            for (int i = 0; i < props.Length;  i++) headers[fields.Length + i] = props[i].Name;
            rows.Add(headers);

            // Data rows
            foreach (var obj in objects)
            {
                var values = new string[headers.Length];
                for (int i = 0; i < fields.Length; i++)
                    values[i] = fields[i].GetValue(obj)?.ToString() ?? string.Empty;
                for (int i = 0; i < props.Length; i++)
                    values[fields.Length + i] = props[i].CanRead
                        ? props[i].GetValue(obj)?.ToString() ?? string.Empty
                        : string.Empty;
                rows.Add(values);
            }
            return Serialize(rows, delimiter);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static string[] ParseRow(string text, ref int pos, char delimiter)
        {
            if (pos >= text.Length) return null;

            var fields = new List<string>();
            bool endOfLine = false;

            while (!endOfLine)
                fields.Add(ParseField(text, ref pos, delimiter, out endOfLine));

            return fields.ToArray();
        }

        private static string ParseField(string text, ref int pos, char delimiter, out bool endOfLine)
        {
            endOfLine = false;

            if (pos >= text.Length) { endOfLine = true; return string.Empty; }

            if (text[pos] == '"')
            {
                pos++; // skip opening quote
                var sb = new StringBuilder();
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c == '"')
                    {
                        pos++;
                        if (pos < text.Length && text[pos] == '"') { sb.Append('"'); pos++; continue; }
                        break; // closing quote
                    }
                    sb.Append(c);
                    pos++;
                }
                SkipDelimiterOrNewline(text, ref pos, delimiter, out endOfLine);
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                while (pos < text.Length &&
                       text[pos] != delimiter &&
                       text[pos] != '\r' &&
                       text[pos] != '\n')
                {
                    sb.Append(text[pos++]);
                }
                SkipDelimiterOrNewline(text, ref pos, delimiter, out endOfLine);
                return sb.ToString();
            }
        }

        private static void SkipDelimiterOrNewline(string text, ref int pos, char delimiter,
                                                    out bool endOfLine)
        {
            endOfLine = false;
            if (pos >= text.Length) { endOfLine = true; return; }

            if (text[pos] == delimiter) { pos++; }
            else if (text[pos] == '\r')
            {
                pos++;
                endOfLine = true;
                if (pos < text.Length && text[pos] == '\n') pos++;
            }
            else if (text[pos] == '\n') { pos++; endOfLine = true; }
            else endOfLine = true; // end of string without delimiter
        }

        // Wraps a field in quotes if it contains the delimiter, quotes, or newlines.
        private static string EscapeField(string field, char delimiter)
        {
            bool needsQuotes = field.IndexOf(delimiter) >= 0 ||
                               field.IndexOf('"') >= 0 ||
                               field.IndexOf('\n') >= 0 ||
                               field.IndexOf('\r') >= 0;
            return needsQuotes
                ? $"\"{field.Replace("\"", "\"\"")}\""
                : field;
        }

        private static bool IsEmptyRow(string[] row) =>
            row.Length == 0 || (row.Length == 1 && string.IsNullOrEmpty(row[0]));
    }
}
