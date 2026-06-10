using System;
using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  IMAGE BLUR UTILITY — CPU gaussian-approximation blur
    /// ══════════════════════════════════════════════════════════════
    ///
    ///    Texture2D blurred = tex.Blur(radius: 8);                // 3 box passes ≈ gaussian
    ///    Texture2D soft    = tex.Blur(radius: 4, iterations: 1); // single fast box blur
    ///
    ///  Implementation: separable box blur with a sliding-window accumulator —
    ///  cost is O(pixels) per pass regardless of radius. Three iterated box
    ///  blurs closely approximate a true gaussian (central limit theorem)
    ///  without shaders, so it works identically on every platform.
    ///
    ///  Performance note: this is CPU work on the main thread. For repeated
    ///  full-screen blurs every frame prefer a shader; for one-off use
    ///  (frosted-glass popups, thumbnails, photo effects) this is ideal —
    ///  blur a DOWNSCALED copy first for big speedups:
    ///
    ///    var blurredBg = tex.CreateThumbnail(512).Blur(6);
    /// </summary>
    public static class ImageBlurUtility
    {
        /// <summary>
        /// Returns a NEW blurred copy of <paramref name="source"/> (the source is
        /// untouched; works on non-readable textures). Caller owns the result.
        /// </summary>
        /// <param name="source">Texture to blur.</param>
        /// <param name="radius">Blur radius in pixels (1–64). Bigger = blurrier.</param>
        /// <param name="iterations">
        /// Box-blur passes. 1 = fast box blur (slightly "blocky"),
        /// 3 = near-gaussian quality (default).
        /// </param>
        public static Texture2D Blur(this Texture source, int radius, int iterations = 3)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            radius = Mathf.Clamp(radius, 1, 64);
            iterations = Mathf.Clamp(iterations, 1, 5);

            Texture2D readable = source.GetReadableCopy();
            int width = readable.width;
            int height = readable.height;

            Color32[] front = readable.GetPixels32();
            var back = new Color32[front.Length];

            for (int i = 0; i < iterations; i++)
            {
                BlurPassHorizontal(front, back, width, height, radius);
                BlurPassVertical(back, front, width, height, radius);
            }

            readable.SetPixels32(front);
            readable.Apply();
            return readable;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Separable passes — sliding window keeps each O(width*height)
        // ──────────────────────────────────────────────────────────────────────

        private static void BlurPassHorizontal(Color32[] src, Color32[] dst, int width, int height, int radius)
        {
            float norm = 1f / (radius * 2 + 1);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int r = 0, g = 0, b = 0, a = 0;

                // Prime the window centred on x = 0 (edges clamp).
                for (int i = -radius; i <= radius; i++)
                {
                    Color32 c = src[row + Mathf.Clamp(i, 0, width - 1)];
                    r += c.r; g += c.g; b += c.b; a += c.a;
                }

                for (int x = 0; x < width; x++)
                {
                    dst[row + x] = new Color32(
                        (byte)(r * norm + 0.5f), (byte)(g * norm + 0.5f),
                        (byte)(b * norm + 0.5f), (byte)(a * norm + 0.5f));

                    // Slide the window one pixel right.
                    Color32 add = src[row + Mathf.Clamp(x + radius + 1, 0, width - 1)];
                    Color32 sub = src[row + Mathf.Clamp(x - radius, 0, width - 1)];
                    r += add.r - sub.r; g += add.g - sub.g;
                    b += add.b - sub.b; a += add.a - sub.a;
                }
            }
        }

        private static void BlurPassVertical(Color32[] src, Color32[] dst, int width, int height, int radius)
        {
            float norm = 1f / (radius * 2 + 1);

            for (int x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    Color32 c = src[Mathf.Clamp(i, 0, height - 1) * width + x];
                    r += c.r; g += c.g; b += c.b; a += c.a;
                }

                for (int y = 0; y < height; y++)
                {
                    dst[y * width + x] = new Color32(
                        (byte)(r * norm + 0.5f), (byte)(g * norm + 0.5f),
                        (byte)(b * norm + 0.5f), (byte)(a * norm + 0.5f));

                    Color32 add = src[Mathf.Clamp(y + radius + 1, 0, height - 1) * width + x];
                    Color32 sub = src[Mathf.Clamp(y - radius, 0, height - 1) * width + x];
                    r += add.r - sub.r; g += add.g - sub.g;
                    b += add.b - sub.b; a += add.a - sub.a;
                }
            }
        }
    }
}
