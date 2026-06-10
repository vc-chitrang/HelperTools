using System;
using System.IO;
using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  IMAGE COMPRESSION UTILITY — encode, compress, save, load
    /// ══════════════════════════════════════════════════════════════
    ///
    ///    byte[] jpg = tex.EncodeJpg(quality: 75);     // lossy, small
    ///    byte[] png = tex.EncodePng();                // lossless
    ///    Texture2D smaller = tex.CompressLossy(60);   // re-encode in memory
    ///    tex.CompressInPlace();                       // GPU DXT/ETC compression
    ///
    ///    string path = tex.SaveToFile("thumb.jpg");   // persistentDataPath
    ///    Texture2D loaded = ImageCompressionUtility.LoadFromFile(path);
    ///
    ///  All encoders run a readable-copy pass first, so they work on
    ///  compressed / non-readable source textures.
    /// </summary>
    public static class ImageCompressionUtility
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Encoding
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Encodes to JPG. <paramref name="quality"/> 1–100 (75 is a good size/quality
        /// trade-off). JPG discards the alpha channel — use PNG when alpha matters.
        /// </summary>
        public static byte[] EncodeJpg(this Texture2D texture, int quality = 75)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            quality = Mathf.Clamp(quality, 1, 100);

            Texture2D readable = texture.GetReadableCopy();
            byte[] bytes = readable.EncodeToJPG(quality);
            TextureDisposer.Dispose(readable);
            return bytes;
        }

        /// <summary>Encodes to PNG (lossless, keeps alpha; larger than JPG).</summary>
        public static byte[] EncodePng(this Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            Texture2D readable = texture.GetReadableCopy();
            byte[] bytes = readable.EncodeToPNG();
            TextureDisposer.Dispose(readable);
            return bytes;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  In-memory compression
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a NEW texture re-encoded through JPG at the given quality —
        /// trades visual fidelity for a much smaller in-memory footprint after a
        /// download or screenshot. Caller owns the returned texture.
        /// </summary>
        public static Texture2D CompressLossy(this Texture2D texture, int quality = 60)
        {
            byte[] jpg = texture.EncodeJpg(quality);
            var result = new Texture2D(2, 2, TextureFormat.RGB24, false);
            result.LoadImage(jpg);
            return result;
        }

        /// <summary>
        /// Applies Unity's built-in GPU texture compression (DXT/ETC/ASTC depending
        /// on platform) IN PLACE, roughly quartering GPU memory. Requires the texture
        /// dimensions to be multiples of 4; logs a warning and does nothing otherwise.
        /// </summary>
        public static void CompressInPlace(this Texture2D texture, bool highQuality = false)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            if (texture.width % 4 != 0 || texture.height % 4 != 0)
            {
                Debug.LogWarning($"[ImageCompressionUtility] CompressInPlace skipped: " +
                                 $"{texture.width}x{texture.height} is not a multiple of 4.");
                return;
            }

            texture.Compress(highQuality);
            texture.Apply(false, true); // makeNoLongerReadable: frees the CPU copy
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Disk I/O
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Saves the texture under <c>Application.persistentDataPath/Images/</c>
        /// (or an absolute path if <paramref name="fileName"/> is rooted).
        /// Extension decides the format: .jpg/.jpeg → JPG, anything else → PNG.
        /// Returns the absolute path written, or null on failure.
        /// </summary>
        public static string SaveToFile(this Texture2D texture, string fileName, int jpgQuality = 75)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is null or empty.", nameof(fileName));

            string path = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(Application.persistentDataPath, "Images", fileName);

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string ext = Path.GetExtension(path).ToLowerInvariant();
                byte[] bytes = (ext == ".jpg" || ext == ".jpeg")
                    ? texture.EncodeJpg(jpgQuality)
                    : texture.EncodePng();

                File.WriteAllBytes(path, bytes);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ImageCompressionUtility] SaveToFile failed for '{path}': {ex.Message}");
                return null;
            }
        }

        /// <summary>Loads a PNG/JPG file from disk into a new texture. Returns null on failure.</summary>
        public static Texture2D LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[ImageCompressionUtility] LoadFromFile: file not found at '{path}'.");
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return texture.LoadImage(bytes) ? texture : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ImageCompressionUtility] LoadFromFile failed for '{path}': {ex.Message}");
                return null;
            }
        }
    }
}
