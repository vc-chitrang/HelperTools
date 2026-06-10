using System;
using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  IMAGE RESIZE UTILITY — runtime resizing & thumbnails
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  GPU-based resizing (Graphics.Blit through a temporary RenderTexture):
    ///  fast, allocation-light, and works on non-readable / compressed sources.
    ///
    ///    Texture2D small  = tex.Resize(512, 512);                  // exact size (distorts)
    ///    Texture2D fitted = tex.ResizeToFit(1024, 1024);           // keeps aspect, fits inside
    ///    Texture2D cover  = tex.ResizeToCover(1024, 1024);         // keeps aspect, covers fully
    ///    Texture2D thumb  = tex.CreateThumbnail(128);              // longest edge = 128
    /// </summary>
    public static class ImageResizeUtility
    {
        /// <summary>
        /// Resizes <paramref name="source"/> to exactly <paramref name="width"/>×<paramref name="height"/>
        /// using bilinear GPU filtering. Aspect ratio is NOT preserved — pair with
        /// <see cref="ResizeToFit"/>/<see cref="ResizeToCover"/> when it must be.
        /// Caller owns the returned texture.
        /// </summary>
        public static Texture2D Resize(this Texture source, int width, int height)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            width  = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            RenderTexture tmp = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            tmp.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, tmp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return result;
        }

        /// <summary>
        /// Resizes keeping aspect ratio so the result fits INSIDE the given bounds
        /// (longest dimension touches the bounds — like <see cref="ImageFillMode.Fit"/>).
        /// </summary>
        public static Texture2D ResizeToFit(this Texture source, int maxWidth, int maxHeight)
            => ResizeWithMode(source, maxWidth, maxHeight, ImageFillMode.Fit);

        /// <summary>
        /// Resizes keeping aspect ratio so the result fully COVERS the given bounds
        /// (shortest dimension touches the bounds — like <see cref="ImageFillMode.Fill"/>).
        /// </summary>
        public static Texture2D ResizeToCover(this Texture source, int width, int height)
            => ResizeWithMode(source, width, height, ImageFillMode.Fill);

        /// <summary>
        /// Creates a small preview copy whose longest edge equals
        /// <paramref name="maxSize"/> pixels (aspect preserved). Never upscales —
        /// if the source is already smaller, a same-size copy is returned.
        /// </summary>
        public static Texture2D CreateThumbnail(this Texture source, int maxSize = 128)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            maxSize = Mathf.Max(1, maxSize);

            int longest = Mathf.Max(source.width, source.height);
            if (longest <= maxSize)
                return source.Resize(source.width, source.height); // readable same-size copy

            float scale = (float)maxSize / longest;
            return source.Resize(
                Mathf.Max(1, Mathf.RoundToInt(source.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(source.height * scale)));
        }

        // ── Shared implementation (DRY: fill-mode math lives in FillModeMath) ──

        private static Texture2D ResizeWithMode(Texture source, int boundsWidth, int boundsHeight, ImageFillMode mode)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            Vector2Int size = FillModeMath.GetPixelSize(
                new Vector2(boundsWidth, boundsHeight),
                new Vector2(source.width, source.height),
                mode);

            return source.Resize(size.x, size.y);
        }
    }
}
