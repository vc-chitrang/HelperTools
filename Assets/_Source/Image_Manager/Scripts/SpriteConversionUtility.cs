using System;
using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// Destroys temporary textures safely in both play mode and edit mode
    /// (editor tooling reuses these utilities, where <c>Object.Destroy</c> throws).
    /// </summary>
    internal static class TextureDisposer
    {
        public static void Dispose(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  SPRITE CONVERSION UTILITY — Texture2D ⇄ Sprite ⇄ RenderTexture
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Stateless conversion helpers used across the framework:
    ///
    ///    Sprite   sprite = texture.ToSprite();
    ///    Texture2D tex   = sprite.ToTexture2D();        // atlas-safe
    ///    Texture2D tex   = renderTexture.ToTexture2D();
    ///    Texture2D copy  = anyTexture.GetReadableCopy(); // works on non-readable
    ///    string  base64  = texture.ToBase64Png();
    ///    Texture2D tex   = SpriteConversionUtility.FromBase64(base64);
    ///
    ///  Key detail: <see cref="GetReadableCopy"/> goes through a temporary
    ///  RenderTexture blit, so it works on compressed and non-readable
    ///  textures (where GetPixels would throw). All other helpers build on it.
    /// </summary>
    public static class SpriteConversionUtility
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Texture2D → Sprite
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Wraps <paramref name="texture"/> in a new <see cref="Sprite"/> covering the
        /// whole texture. No pixel data is copied — this is cheap.
        /// </summary>
        /// <param name="texture">Source texture (any readability).</param>
        /// <param name="pixelsPerUnit">Sprite PPU; 100 matches Unity's default import.</param>
        /// <param name="pivot">Normalized pivot; defaults to center (0.5, 0.5).</param>
        public static Sprite ToSprite(this Texture2D texture, float pixelsPerUnit = 100f, Vector2? pivot = null)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot ?? new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Sprite → Texture2D
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts a sprite's pixels into a standalone <see cref="Texture2D"/>.
        /// Atlas-safe: when the sprite is a sub-rect of a packed atlas, only that
        /// region is copied. Works on non-readable source textures.
        /// </summary>
        public static Texture2D ToTexture2D(this Sprite sprite)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));

            Rect rect = sprite.textureRect;
            Texture2D source = sprite.texture;

            // Fast path: the sprite covers the whole texture.
            bool coversWholeTexture =
                Mathf.Approximately(rect.width, source.width) &&
                Mathf.Approximately(rect.height, source.height);

            Texture2D readable = source.GetReadableCopy();
            if (coversWholeTexture)
                return readable;

            // Extract the atlas sub-region.
            var result = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            Color[] pixels = readable.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            result.SetPixels(pixels);
            result.Apply();

            TextureDisposer.Dispose(readable);
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  RenderTexture → Texture2D
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads a <see cref="RenderTexture"/> back into a CPU-side <see cref="Texture2D"/>.
        /// Useful for minimaps, camera captures, and post-processed images.
        /// </summary>
        public static Texture2D ToTexture2D(this RenderTexture renderTexture)
        {
            if (renderTexture == null) throw new ArgumentNullException(nameof(renderTexture));

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var result = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Readable copy (the workhorse)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns an uncompressed, CPU-readable RGBA32 copy of any texture —
        /// including compressed (DXT/ASTC) and non-readable imports — by blitting
        /// through a temporary RenderTexture on the GPU.
        /// Caller owns the returned texture (Destroy it when done).
        /// </summary>
        public static Texture2D GetReadableCopy(this Texture texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            RenderTexture tmp = RenderTexture.GetTemporary(
                texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

            Graphics.Blit(texture, tmp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return copy;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Base64 (string transport, e.g. JSON payloads)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Encodes a texture to a Base64 PNG string (for JSON/web transport).</summary>
        public static string ToBase64Png(this Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            Texture2D readable = texture.GetReadableCopy();
            string base64 = Convert.ToBase64String(readable.EncodeToPNG());
            TextureDisposer.Dispose(readable);
            return base64;
        }

        /// <summary>Decodes a Base64 PNG/JPG string back into a texture. Returns null on bad input.</summary>
        public static Texture2D FromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return texture.LoadImage(bytes) ? texture : null;
            }
            catch (FormatException)
            {
                Debug.LogError("[SpriteConversionUtility] FromBase64: input is not valid Base64.");
                return null;
            }
        }
    }
}
