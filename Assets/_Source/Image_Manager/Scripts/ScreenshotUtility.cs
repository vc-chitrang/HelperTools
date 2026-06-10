using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  SCREENSHOT UTILITY — async captures with and without UI
    /// ══════════════════════════════════════════════════════════════
    ///
    ///    // Full screen, exactly what the player sees (includes UI):
    ///    Texture2D shot = await ScreenshotUtility.CaptureAsync();
    ///
    ///    // A sub-region of the screen:
    ///    Texture2D part = await ScreenshotUtility.CaptureRegionAsync(new RectInt(0, 0, 512, 512));
    ///
    ///    // One camera only — Screen Space Overlay UI is naturally EXCLUDED:
    ///    Texture2D clean = ScreenshotUtility.CaptureCamera(Camera.main, 1920, 1080);
    ///
    ///    // Capture and write to disk in one call:
    ///    string path = await ScreenshotUtility.SaveAsync("MyShot.png");
    ///
    ///  Screen captures must happen at end-of-frame, so those methods are
    ///  async (Unity 6 Awaitable) and must be awaited from the main thread.
    ///  Camera capture renders on demand and is synchronous.
    /// </summary>
    public static class ScreenshotUtility
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Full screen (includes UI overlays)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the whole screen as displayed — including Screen Space Overlay
        /// UI. Waits for end-of-frame so rendering is complete. Caller owns the
        /// returned texture.
        /// </summary>
        public static async Awaitable<Texture2D> CaptureAsync(CancellationToken cancellationToken = default)
        {
            await Awaitable.EndOfFrameAsync(cancellationToken);
            return ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>
        /// Captures a pixel region of the screen (origin = bottom-left).
        /// The region is clamped to the current screen size.
        /// </summary>
        public static async Awaitable<Texture2D> CaptureRegionAsync(
            RectInt region, CancellationToken cancellationToken = default)
        {
            await Awaitable.EndOfFrameAsync(cancellationToken);

            int x = Mathf.Clamp(region.x, 0, Screen.width - 1);
            int y = Mathf.Clamp(region.y, 0, Screen.height - 1);
            int w = Mathf.Clamp(region.width, 1, Screen.width - x);
            int h = Mathf.Clamp(region.height, 1, Screen.height - y);

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(x, y, w, h), 0, 0);
            texture.Apply();
            return texture;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Single camera (UI excluded)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders one camera into a texture at the requested resolution.
        /// Screen Space Overlay canvases never pass through a camera, so this is
        /// the "screenshot without UI" path. Also useful for photo modes and
        /// thumbnails at resolutions other than the screen's.
        /// </summary>
        public static Texture2D CaptureCamera(Camera camera, int width, int height)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            width  = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previousTarget;

            Texture2D result = rt.ToTexture2D(); // SpriteConversionUtility (DRY)
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Capture + save
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the full screen and writes it under
        /// <c>Application.persistentDataPath/Screenshots/</c> (PNG by default,
        /// .jpg extension switches to JPG). Pass null to auto-name with a timestamp.
        /// Returns the absolute file path, or null on failure.
        /// </summary>
        public static async Awaitable<string> SaveAsync(
            string fileName = null, CancellationToken cancellationToken = default)
        {
            Texture2D shot = await CaptureAsync(cancellationToken);
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

                string path = Path.IsPathRooted(fileName)
                    ? fileName
                    : Path.Combine(Application.persistentDataPath, "Screenshots", fileName);

                // Reuse the compression utility for encoding + directory handling (DRY).
                return shot.SaveToFile(path);
            }
            finally
            {
                TextureDisposer.Dispose(shot);
            }
        }
    }
}
