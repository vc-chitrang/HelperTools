using System.Collections.Generic;
using System.Text;
using DataManagement.Demo;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataManagement
{
    /// <summary>
    /// Runtime controller for the Data Manager demo scene.
    /// Demonstrates CSV parsing, filtering, and SO Database querying.
    /// </summary>
    [AddComponentMenu("HelperTools/Data/Data Demo Controller")]
    public sealed class DataDemoController : MonoBehaviour
    {
        // ── CSV panel ──────────────────────────────────────────────────────────
        [Header("CSV Panel")]
        [SerializeField] private TMP_Text  _csvTableText;
        [SerializeField] private TMP_Text  _csvStatsText;
        [SerializeField] private TMP_InputField _csvFilterInput;
        [SerializeField] private Button    _csvFilterButton;
        [SerializeField] private Button    _csvClearFilterButton;

        // ── Excel panel ────────────────────────────────────────────────────────
        [Header("Excel Panel")]
        [SerializeField] private TMP_Text  _excelStatusText;
        [SerializeField] private TMP_Text  _excelTableText;

        // ── SO Database panel ──────────────────────────────────────────────────
        [Header("SO Database Panel")]
        [SerializeField] private DemoItemDatabase _itemDatabase;
        [SerializeField] private TMP_Text         _soTableText;
        [SerializeField] private TMP_Text         _soStatsText;
        [SerializeField] private TMP_InputField   _soFilterInput;
        [SerializeField] private Button           _soFilterButton;
        [SerializeField] private Button           _soClearFilterButton;

        // ── Private state ──────────────────────────────────────────────────────
        private List<Dictionary<string, string>> _csvRows;
        private const string CsvResourcePath = "Demo/DemoItems";

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            LoadCSV();
            LoadSODatabase();
            RefreshExcelPanel();
            WireButtons();
        }

        // ── CSV ────────────────────────────────────────────────────────────────

        private void LoadCSV()
        {
            _csvRows = CSVReader.LoadFromResourcesWithHeaders(CsvResourcePath);

            if (_csvRows == null || _csvRows.Count == 0)
            {
                SetText(_csvTableText, "<color=#FF6666>DemoItems.csv not found in Resources/Demo/</color>");
                SetText(_csvStatsText, "0 rows");
                return;
            }
            RenderCSVTable(_csvRows);
        }

        private void RenderCSVTable(List<Dictionary<string, string>> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                SetText(_csvTableText, "<color=#AAAAAA>No rows to display.</color>");
                SetText(_csvStatsText, "0 rows");
                return;
            }

            var headers = new List<string>(rows[0].Keys);
            var sb      = new StringBuilder();

            // Header row
            sb.Append("<color=#88CCFF><b>");
            foreach (var h in headers) sb.Append($"{h,-14}");
            sb.AppendLine("</b></color>");

            // Separator
            sb.AppendLine(new string('─', headers.Count * 14));

            // Data rows — alternate colors
            for (int i = 0; i < rows.Count; i++)
            {
                string rowColor = i % 2 == 0 ? "#DDDDDD" : "#BBBBBB";
                sb.Append($"<color={rowColor}>");
                foreach (var h in headers)
                    sb.Append($"{Truncate(rows[i].TryGetValue(h, out var v) ? v : "", 13),-14}");
                sb.AppendLine("</color>");
            }

            SetText(_csvTableText, sb.ToString());
            SetText(_csvStatsText, $"{rows.Count} row{(rows.Count != 1 ? "s" : "")}");
        }

        private void OnCSVFilter()
        {
            string query = _csvFilterInput != null ? _csvFilterInput.text.Trim() : "";
            if (string.IsNullOrEmpty(query) || _csvRows == null) { RenderCSVTable(_csvRows); return; }

            var filtered = _csvRows.FindAll(row =>
            {
                foreach (var v in row.Values)
                    if (v.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            });
            RenderCSVTable(filtered);
            SetText(_csvStatsText, $"{filtered.Count} / {_csvRows.Count} rows  (filter: \"{query}\")");
        }

        private void OnCSVClearFilter()
        {
            if (_csvFilterInput != null) _csvFilterInput.text = string.Empty;
            RenderCSVTable(_csvRows);
        }

        // ── Excel ──────────────────────────────────────────────────────────────

        private void RefreshExcelPanel()
        {
            if (!ExcelReader.IsSupported)
            {
                SetText(_excelStatusText,
                    "<color=#FFAA44>Excel reading is not supported on WebGL at runtime.</color>\n" +
                    "Use the Editor importer to convert .xlsx to CSV.");
                return;
            }

            const string xlsxRelPath = "DataManagerDemo/Sample.xlsx";
            string full = System.IO.Path.Combine(Application.streamingAssetsPath, xlsxRelPath);

            if (!System.IO.File.Exists(full))
            {
                SetText(_excelStatusText,
                    "<color=#AAAAAA>No sample .xlsx found.</color>\n\n" +
                    "To test Excel reading:\n" +
                    "  1. Place any <b>.xlsx</b> file at:\n" +
                    $"     <color=#88CCFF>StreamingAssets/{xlsxRelPath}</color>\n" +
                    "  2. Re-enter Play mode.\n\n" +
                    "At runtime call:\n" +
                    "  <color=#88FFCC>ExcelReader.ReadFromStreamingAssets(path)</color>\n" +
                    "  <color=#88FFCC>ExcelReader.ReadFile(absolutePath)</color>\n" +
                    "  <color=#88FFCC>ExcelReader.Read(byteArray)</color>");
                SetText(_excelTableText, "");
                return;
            }

            var rows = ExcelReader.ReadWithHeaders(full);
            if (rows == null || rows.Count == 0)
            {
                SetText(_excelStatusText, "<color=#FFAA44>File found but no data could be read.</color>");
                return;
            }

            SetText(_excelStatusText,
                $"<color=#AAFFAA>Loaded {rows.Count} row(s) from:</color>\n{xlsxRelPath}");
            RenderExcelTable(rows);
        }

        private void RenderExcelTable(List<Dictionary<string, string>> rows)
        {
            var headers = new List<string>(rows[0].Keys);
            var sb      = new StringBuilder();
            sb.Append("<color=#FFCC88><b>");
            foreach (var h in headers) sb.Append($"{h,-14}");
            sb.AppendLine("</b></color>");
            sb.AppendLine(new string('─', headers.Count * 14));
            foreach (var row in rows)
            {
                foreach (var h in headers)
                    sb.Append($"{Truncate(row.TryGetValue(h, out var v) ? v : "", 13),-14}");
                sb.AppendLine();
            }
            SetText(_excelTableText, sb.ToString());
        }

        // ── SO Database ────────────────────────────────────────────────────────

        private void LoadSODatabase()
        {
            if (_itemDatabase == null)
            {
                SetText(_soTableText,
                    "<color=#AAAAAA>No DemoItemDatabase assigned.\n\n" +
                    "Assign the DemoItemDatabase.asset to this controller\n" +
                    "or run  Tools → Data Manager → Build Demo Scene  to rebuild.</color>");
                SetText(_soStatsText, "—");
                return;
            }
            RenderSOTable(_itemDatabase.Records);
        }

        private void RenderSOTable(IReadOnlyList<DemoItemRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                SetText(_soTableText, "<color=#AAAAAA>Database is empty.</color>");
                SetText(_soStatsText, "0 records");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<color=#AAFFCC><b>Id            Name          Type          Lv   Power</b></color>");
            sb.AppendLine(new string('─', 72));

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null) continue;
                string color = i % 2 == 0 ? "#DDDDDD" : "#BBBBBB";
                sb.AppendLine(
                    $"<color={color}>" +
                    $"{Truncate(r.Id, 13),-14}" +
                    $"{Truncate(r.ItemName, 13),-14}" +
                    $"{Truncate(r.ItemType, 13),-14}" +
                    $"{r.Level,-5}" +
                    $"{r.Power,-6:F1}" +
                    "</color>");
            }

            SetText(_soTableText, sb.ToString());
            SetText(_soStatsText, $"{records.Count} record{(records.Count != 1 ? "s" : "")}");
        }

        private void OnSOFilter()
        {
            if (_itemDatabase == null) return;
            string query = _soFilterInput != null ? _soFilterInput.text.Trim() : "";
            if (string.IsNullOrEmpty(query)) { RenderSOTable(_itemDatabase.Records); return; }

            var results = _itemDatabase.FindAll(r =>
                r.Id.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.ItemName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.ItemType.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);

            RenderSOTable(results);
            SetText(_soStatsText,
                $"{results.Count} / {_itemDatabase.Count} records  (filter: \"{query}\")");
        }

        private void OnSOClearFilter()
        {
            if (_soFilterInput != null) _soFilterInput.text = string.Empty;
            if (_itemDatabase != null) RenderSOTable(_itemDatabase.Records);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void WireButtons()
        {
            _csvFilterButton?.onClick.AddListener(OnCSVFilter);
            _csvClearFilterButton?.onClick.AddListener(OnCSVClearFilter);
            _soFilterButton?.onClick.AddListener(OnSOFilter);
            _soClearFilterButton?.onClick.AddListener(OnSOClearFilter);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        private static string Truncate(string s, int max) =>
            s != null && s.Length > max ? s.Substring(0, max - 1) + "…" : s ?? "";
    }
}
