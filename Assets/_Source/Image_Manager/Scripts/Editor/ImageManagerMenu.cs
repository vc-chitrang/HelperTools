#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ImageSystem.EditorTools
{
    /// <summary>
    /// Editor shortcuts for the Image &amp; Texture system, under
    /// <b>Tools ▸ Image Manager</b>:
    /// <list type="bullet">
    ///   <item><b>Capture Game View Screenshot</b> — saves a timestamped PNG into
    ///         <c>Screenshots/</c> at the project root.</item>
    ///   <item><b>Create Thumbnail From Selected Texture</b> — writes a 256px
    ///         thumbnail PNG next to the selected texture asset.</item>
    ///   <item><b>Blur Selected Texture</b> — writes a blurred PNG copy next to
    ///         the selected texture asset.</item>
    /// </list>
    /// Editor-only; stripped from player builds. All runtime behaviour lives in
    /// the runtime scripts (separation of concern).
    /// </summary>
    public static class ImageManagerMenu
    {
        private const string MenuRoot = "Tools/Image Manager/";

        // ──────────────────────────────────────────────────────────────────────
        //  Screenshot
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Capture Game View Screenshot")]
        public static void CaptureGameViewScreenshot()
        {
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            // Works in both edit mode and play mode; writes asynchronously after
            // the next Game View repaint.
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[ImageManagerMenu] Screenshot queued → {path}\n" +
                      "(The file appears after the Game view renders its next frame.)");
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Thumbnail from selection
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Create Thumbnail From Selected Texture")]
        public static void CreateThumbnailFromSelection()
        {
            ProcessSelectedTexture("thumb", tex => tex.CreateThumbnail(256));
        }

        [MenuItem(MenuRoot + "Create Thumbnail From Selected Texture", isValidateFunction: true)]
        private static bool ValidateThumbnail() => Selection.activeObject is Texture2D;

        // ──────────────────────────────────────────────────────────────────────
        //  Blur from selection
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Blur Selected Texture")]
        public static void BlurSelection()
        {
            ProcessSelectedTexture("blurred", tex => tex.Blur(radius: 8));
        }

        [MenuItem(MenuRoot + "Blur Selected Texture", isValidateFunction: true)]
        private static bool ValidateBlur() => Selection.activeObject is Texture2D;

        // ──────────────────────────────────────────────────────────────────────
        //  Shared pipeline (DRY): selected texture → transform → PNG next to it
        // ──────────────────────────────────────────────────────────────────────

        private static void ProcessSelectedTexture(string suffix, Func<Texture2D, Texture2D> transform)
        {
            if (Selection.activeObject is not Texture2D source)
            {
                Debug.LogWarning("[ImageManagerMenu] Select a Texture2D asset first.");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(source);
            string outputPath = string.IsNullOrEmpty(assetPath)
                ? $"Assets/{source.name}_{suffix}.png"
                : $"{Path.GetDirectoryName(assetPath)}/{Path.GetFileNameWithoutExtension(assetPath)}_{suffix}.png";

            Texture2D result = transform(source);
            try
            {
                File.WriteAllBytes(outputPath, result.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(result);
            }

            AssetDatabase.Refresh();
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
            if (imported != null)
                Selection.activeObject = imported;

            Debug.Log($"[ImageManagerMenu] Saved → {outputPath}");
        }
    }
}
#endif
