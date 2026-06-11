using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace DataManagement
{
    /// <summary>
    /// Reflection-based helper that maps CSV column values to public fields on a DataRecord.
    /// Handles the common Unity-serializable types automatically.
    /// </summary>
    public static class DataRecordReflectionHelper
    {
        /// <summary>
        /// Sets public fields on <paramref name="record"/> by matching CSV column names to
        /// field names (case-insensitive). Supported types: string, int, float, double,
        /// bool, long, Vector2, Vector3, Color.
        /// </summary>
        public static void PopulateFromRow(DataRecord record,
                                           Dictionary<string, string> row)
        {
            if (record == null || row == null) return;

            var type   = record.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Find a matching column by name (case-insensitive)
                string colName = null;
                foreach (var key in row.Keys)
                    if (string.Equals(key.Trim(), field.Name,
                                      StringComparison.OrdinalIgnoreCase))
                    { colName = key; break; }

                if (colName == null) continue;

                object value = ConvertValue(row[colName], field.FieldType);
                if (value != null)
                    field.SetValue(record, value);
            }
        }

        // ── Type conversion ────────────────────────────────────────────────────

        private static object ConvertValue(string raw, Type targetType)
        {
            if (raw == null) return null;
            raw = raw.Trim();

            try
            {
                if (targetType == typeof(string))  return raw;
                if (targetType == typeof(int))     return int.Parse(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(float))   return float.Parse(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(double))  return double.Parse(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(long))    return long.Parse(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(bool))    return ParseBool(raw);
                if (targetType == typeof(Vector2)) return ParseVector2(raw);
                if (targetType == typeof(Vector3)) return ParseVector3(raw);
                if (targetType == typeof(Color))   return ParseColor(raw);
                if (targetType.IsEnum) return Enum.Parse(targetType, raw, ignoreCase: true);
                // Generic fallback via IConvertible
                return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null; // leave field at its default
            }
        }

        private static bool ParseBool(string s)
        {
            s = s.ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes" || s == "on";
        }

        // Accepts "x,y" or "(x,y)"
        private static Vector2 ParseVector2(string s)
        {
            s = s.Trim('(', ')');
            var parts = s.Split(',');
            return new Vector2(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
        }

        // Accepts "x,y,z" or "(x,y,z)"
        private static Vector3 ParseVector3(string s)
        {
            s = s.Trim('(', ')');
            var parts = s.Split(',');
            return new Vector3(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture));
        }

        // Accepts "#RRGGBB", "#RRGGBBAA", or "r,g,b" / "r,g,b,a" (0-1 floats)
        private static Color ParseColor(string s)
        {
            s = s.Trim();
            if (s.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(s, out Color c);
                return c;
            }
            var parts = s.Split(',');
            float r = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            float g = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            float b = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
            float a = parts.Length > 3 ? float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture) : 1f;
            return new Color(r, g, b, a);
        }
    }
}
