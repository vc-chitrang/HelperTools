using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JsonUtilities
{
    /// <summary>
    /// Demo controller for the JSON Utilities scene.
    /// Shows serialize/deserialize, file I/O, PlayerPrefs storage, and JSON validation.
    /// </summary>
    [AddComponentMenu("HelperTools/JSON Utilities/JsonDemoController")]
    public class JsonDemoController : MonoBehaviour
    {
        [Header("Serialize Panel")]
        [SerializeField] private TMP_Text  _serializeOutput;
        [SerializeField] private Toggle    _prettyPrintToggle;

        [Header("File I/O Panel")]
        [SerializeField] private TMP_Text  _fileOutput;

        [Header("PlayerPrefs Panel")]
        [SerializeField] private TMP_Text  _prefsOutput;

        [Header("Validate Panel")]
        [SerializeField] private TMP_InputField _validateInput;
        [SerializeField] private TMP_Text        _validateOutput;

        private const string FileName = "demo_player";
        private const string PrefsKey = "demo_player";

        // ── Sample data type ──────────────────────────────────────────────

        [Serializable]
        private class DemoPlayer
        {
            public string playerName = "Chitrang";
            public int    level      = 10;
            public float  health     = 95.5f;
            public int[]  topScores  = { 1000, 800, 600 };
        }

        // ── Serialize / Deserialize ───────────────────────────────────────

        public void OnSerialize()
        {
            bool pretty = _prettyPrintToggle != null && _prettyPrintToggle.isOn;
            string json = JsonHelper.ToJson(new DemoPlayer(), pretty);
            Show(_serializeOutput, json);
        }

        public void OnDeserialize()
        {
            if (_serializeOutput == null) return;

            string json = _serializeOutput.text.Trim();
            if (!JsonHelper.TryFromJson<DemoPlayer>(json, out var player))
            {
                Show(_serializeOutput, Err("Invalid JSON — press Serialize first."));
                return;
            }

            Show(_serializeOutput,
                $"<b>Deserialized!</b>\n" +
                $"Name:   <color=#88FF88>{player.playerName}</color>\n" +
                $"Level:  <color=#FFAA44>{player.level}</color>\n" +
                $"Health: <color=#AAAAFF>{player.health}</color>\n" +
                $"Scores: <color=#FFDD66>{string.Join(", ", player.topScores)}</color>");
        }

        // ── File I/O ──────────────────────────────────────────────────────

        public void OnSaveFile()
        {
            JsonFileManager.Save(new DemoPlayer(), FileName, prettyPrint: true);
            Show(_fileOutput,
                $"<color=#88FF88><b>Saved!</b></color>\n" +
                $"<size=70%>{JsonFileManager.GetPath(FileName)}</size>");
        }

        public void OnLoadFile()
        {
            if (!JsonFileManager.Exists(FileName))
            {
                Show(_fileOutput, Err("File not found — press Save first."));
                return;
            }

            var player = JsonFileManager.Load<DemoPlayer>(FileName);
            Show(_fileOutput,
                $"<color=#88FF88><b>Loaded!</b></color>\n" +
                $"Name:  {player.playerName}\n" +
                $"Level: {player.level}");
        }

        // ── PlayerPrefs ───────────────────────────────────────────────────

        public void OnSavePrefs()
        {
            JsonPrefsManager.Set(PrefsKey, new DemoPlayer());
            JsonPrefsManager.Flush();
            Show(_prefsOutput,
                $"<color=#88FF88><b>Saved!</b></color>\n" +
                $"Key: <color=#AAAAFF>\"{JsonPrefsManager.KeyPrefix}{PrefsKey}\"</color>");
        }

        public void OnLoadPrefs()
        {
            if (!JsonPrefsManager.HasKey(PrefsKey))
            {
                Show(_prefsOutput, Err("Key not found — press Save first."));
                return;
            }

            var player = JsonPrefsManager.Get<DemoPlayer>(PrefsKey);
            Show(_prefsOutput,
                $"<color=#88FF88><b>Loaded!</b></color>\n" +
                $"Name:  {player.playerName}\n" +
                $"Level: {player.level}");
        }

        // ── Validate ──────────────────────────────────────────────────────

        public void OnValidate()
        {
            if (_validateInput == null) return;

            string json = _validateInput.text.Trim();
            if (string.IsNullOrEmpty(json))
            {
                Show(_validateOutput, "<color=#FFAA44>Enter JSON in the field above.</color>");
                return;
            }

            bool valid = JsonHelper.IsValidJson(json);
            if (valid)
            {
                string type = JsonHelper.IsJsonObject(json) ? "Object  { }" : "Array  [ ]";
                Show(_validateOutput,
                    $"<color=#88FF88><b>Valid JSON</b></color>\n" +
                    $"Root type: {type}\n" +
                    $"Length: {json.Length} chars");
            }
            else
            {
                Show(_validateOutput,
                    $"<color=#FF6666><b>Invalid JSON</b></color>\n" +
                    "Must begin and end with\n{{ }} or [ ]");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void Show(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        private static string Err(string msg) => $"<color=#FF6666>{msg}</color>";
    }
}
