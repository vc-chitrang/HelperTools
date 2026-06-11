#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DataManagement.Editor
{
    /// <summary>
    /// Editor window that imports a CSV file into an <see cref="SODatabase{T}"/>,
    /// creating one <see cref="DataRecord"/> .asset per CSV row.
    ///
    /// Access via:  Tools → Data Manager → Import CSV to SO Database
    /// </summary>
    public class SODatabaseImporter : EditorWindow
    {
        private ScriptableObject _targetDatabase;
        private string           _csvPath         = "";
        private string           _outputFolder    = "Assets/_Source/Data_Manager/SOData";
        private char             _delimiter       = ',';
        private bool             _clearExisting   = true;
        private string           _lastStatus      = "";
        private MessageType      _lastStatusType  = MessageType.None;

        [MenuItem("Tools/Data Manager/Import CSV to SO Database (Window)")]
        public static void ShowWindow()
        {
            var w = GetWindow<SODatabaseImporter>("CSV → SO Database");
            w.minSize = new Vector2(500, 380);
        }

        private void OnGUI()
        {
            GUILayout.Label("Import CSV into a ScriptableObject Database",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            // ── Target database ──
            EditorGUILayout.LabelField("1.  Target SODatabase asset", EditorStyles.miniBoldLabel);
            _targetDatabase = (ScriptableObject)EditorGUILayout.ObjectField(
                "Database Asset", _targetDatabase, typeof(ScriptableObject), allowSceneObjects: false);
            EditorGUILayout.Space(4);

            // ── CSV file ──
            EditorGUILayout.LabelField("2.  Source CSV file", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _csvPath = EditorGUILayout.TextField("CSV Path", _csvPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFilePanel("Select CSV file", "", "csv");
                if (!string.IsNullOrEmpty(picked)) _csvPath = picked;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // ── Output folder ──
            EditorGUILayout.LabelField("3.  Output folder for .asset files", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Folder (Assets/…)", _outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select output folder",
                    Application.dataPath, "");
                if (!string.IsNullOrEmpty(picked))
                    _outputFolder = "Assets" + picked.Replace(Application.dataPath, "");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // ── Options ──
            EditorGUILayout.LabelField("4.  Options", EditorStyles.miniBoldLabel);
            string delimStr = EditorGUILayout.TextField("Delimiter (char)", _delimiter.ToString());
            if (delimStr.Length > 0) _delimiter = delimStr[0];
            _clearExisting = EditorGUILayout.Toggle(
                new GUIContent("Clear Existing Records",
                    "Remove all existing records from the database before importing."),
                _clearExisting);
            EditorGUILayout.Space(10);

            // ── Run button ──
            bool canRun = _targetDatabase != null && !string.IsNullOrEmpty(_csvPath) &&
                          !string.IsNullOrEmpty(_outputFolder);
            GUI.enabled = canRun;
            if (GUILayout.Button("Import Now", GUILayout.Height(34)))
                RunImport();
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_lastStatus, _lastStatusType);
            }
        }

        private void RunImport()
        {
            try
            {
                // ── Read CSV ──
                if (!File.Exists(_csvPath))
                { ShowStatus($"CSV not found: {_csvPath}", MessageType.Error); return; }

                string csvText = File.ReadAllText(_csvPath, System.Text.Encoding.UTF8);
                var rows = CSVParser.ParseWithHeaders(csvText, _delimiter);

                if (rows.Count == 0)
                { ShowStatus("CSV has no data rows.", MessageType.Warning); return; }

                // ── Resolve record type ──
                Type dbType     = _targetDatabase.GetType();
                Type recordType = ResolveRecordType(dbType);
                if (recordType == null)
                {
                    ShowStatus(
                        $"Cannot determine record type for '{dbType.Name}'.\n" +
                        $"Make sure it inherits from SODatabase<T> where T is a DataRecord subclass.",
                        MessageType.Error);
                    return;
                }

                // ── Ensure output folder ──
                EnsureFolder(_outputFolder);

                // ── Clear existing if requested ──
                var dbSO      = new SerializedObject(_targetDatabase);
                var recordsP  = dbSO.FindProperty("_records");
                if (recordsP == null)
                {
                    ShowStatus("Could not find '_records' SerializedProperty on the database. " +
                               "Make sure it inherits from SODatabase<T>.", MessageType.Error);
                    return;
                }

                if (_clearExisting)
                {
                    recordsP.ClearArray();
                    dbSO.ApplyModifiedPropertiesWithoutUndo();
                }

                // ── Create and populate records (batched to avoid per-asset imports) ──
                int created = 0;
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var row in rows)
                    {
                        var record = ScriptableObject.CreateInstance(recordType) as DataRecord;
                        if (record == null) continue;

                        record.PopulateFromCSVRow(row);

                        if (string.IsNullOrEmpty(record.Id))
                            record.Id = $"record_{created:D4}";

                        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                            $"{_outputFolder}/{recordType.Name}_{record.Id}.asset");
                        AssetDatabase.CreateAsset(record, assetPath);

                        int idx = recordsP.arraySize;
                        recordsP.InsertArrayElementAtIndex(idx);
                        recordsP.GetArrayElementAtIndex(idx).objectReferenceValue = record;
                        created++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                dbSO.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ShowStatus($"Import complete: {created} record(s) created in '{_outputFolder}'.",
                           MessageType.Info);
            }
            catch (Exception e)
            {
                ShowStatus($"Import failed: {e.Message}", MessageType.Error);
                Debug.LogException(e);
            }
        }

        // ── Static helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Programmatic import from code. Useful in builder scripts.
        /// Returns the number of records created.
        /// </summary>
        public static int Import(ScriptableObject database, string csvText,
                                  Type recordType, string outputFolder,
                                  char delimiter = ',', bool clearExisting = true)
        {
            var rows = CSVParser.ParseWithHeaders(csvText, delimiter);
            if (rows.Count == 0) return 0;

            EnsureFolder(outputFolder);

            var dbSO     = new SerializedObject(database);
            var recordsP = dbSO.FindProperty("_records");
            if (recordsP == null) return 0;

            if (clearExisting)
            {
                recordsP.ClearArray();
                dbSO.ApplyModifiedPropertiesWithoutUndo();
            }

            int created = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var row in rows)
                {
                    var record = ScriptableObject.CreateInstance(recordType) as DataRecord;
                    if (record == null) continue;

                    record.PopulateFromCSVRow(row);
                    if (string.IsNullOrEmpty(record.Id)) record.Id = $"record_{created:D4}";

                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{outputFolder}/{recordType.Name}_{record.Id}.asset");
                    AssetDatabase.CreateAsset(record, path);

                    int idx = recordsP.arraySize;
                    recordsP.InsertArrayElementAtIndex(idx);
                    recordsP.GetArrayElementAtIndex(idx).objectReferenceValue = record;
                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            dbSO.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return created;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static Type ResolveRecordType(Type dbType)
        {
            for (var t = dbType; t != null; t = t.BaseType)
            {
                if (!t.IsGenericType) continue;
                var def = t.GetGenericTypeDefinition();
                if (def == typeof(SODatabase<>))
                    return t.GetGenericArguments()[0];
            }
            return null;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string   cur   = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private void ShowStatus(string msg, MessageType type)
        {
            _lastStatus     = msg;
            _lastStatusType = type;
            Repaint();
        }
    }
}
#endif
