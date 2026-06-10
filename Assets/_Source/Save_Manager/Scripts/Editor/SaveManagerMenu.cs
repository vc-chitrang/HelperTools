#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SaveSystem.EditorTools
{
    /// <summary>
    /// Editor shortcuts for the Save &amp; Load system, under
    /// <b>Tools ▸ Save Manager</b>:
    /// <list type="bullet">
    ///   <item><b>Open Save Folder</b> — reveals the persistent data save folder in Explorer/Finder.</item>
    ///   <item><b>Print Save Info</b> — logs file names, sizes, and timestamps to the console.</item>
    ///   <item><b>Delete All Save Files</b> — wipes all .json/.bak files (with confirmation).</item>
    /// </list>
    /// Editor-only; stripped from player builds.
    /// </summary>
    public static class SaveManagerMenu
    {
        private const string MenuRoot = "Tools/Save Manager/";

        // ── Open folder ───────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Open Save Folder")]
        public static void OpenSaveFolder()
        {
            string path = SaveManager.SaveFolderPath;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        // ── Print info ────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Print Save Info")]
        public static void PrintSaveInfo()
        {
            string path = SaveManager.SaveFolderPath;
            if (!Directory.Exists(path))
            {
                Debug.Log("[SaveManagerMenu] Save folder does not exist yet — no saves found.");
                return;
            }

            string[] files = Directory.GetFiles(path, "*.json");
            if (files.Length == 0)
            {
                Debug.Log("[SaveManagerMenu] No .json save files found.");
                return;
            }

            foreach (string f in files)
            {
                var fi = new FileInfo(f);
                Debug.Log($"[SaveManagerMenu] {fi.Name}  " +
                          $"{fi.Length / 1024f:F1} KB  " +
                          $"Last write: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            }
        }

        // ── Delete all ────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Delete All Save Files")]
        public static void DeleteAllSaves()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete All Saves",
                    $"Delete all save files in:\n{SaveManager.SaveFolderPath}\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            string path = SaveManager.SaveFolderPath;
            if (!Directory.Exists(path))
            {
                Debug.Log("[SaveManagerMenu] No saves folder found — nothing to delete.");
                return;
            }

            int count = 0;
            foreach (string ext in new[] { "*.json", "*.bak", "*.tmp" })
                foreach (string f in Directory.GetFiles(path, ext))
                {
                    File.Delete(f);
                    count++;
                }

            Debug.Log($"[SaveManagerMenu] Deleted {count} file(s) from {path}");
        }
    }
}
#endif
