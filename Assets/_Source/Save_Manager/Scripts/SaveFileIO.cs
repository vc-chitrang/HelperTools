using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  SAVE FILE IO — atomic writes, auto-backup, AES-256 encryption
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Write pattern: write → .tmp, then rename over .json (atomic).
    ///  Old .json becomes .bak before replacement — auto-restored on
    ///  read if the primary file is corrupt.
    ///
    ///  Encryption is AES-256-CBC with a fixed key/IV (tamper-resistance,
    ///  not cryptographic security). Swap the key bytes before shipping.
    /// </summary>
    public static class SaveFileIO
    {
        // ── AES key material — change before shipping ──────────────────────
        // Key  : 32 bytes = 256-bit AES
        // IV   : 16 bytes = AES block size
        // Note : a fixed IV leaks whether two save files are identical
        //        (not a problem for single-player saves, but worth noting).
        private static readonly byte[] AesKey =
            Encoding.UTF8.GetBytes("HelperTools_SaveKey_32BytesPad!!");   // 32 chars
        private static readonly byte[] AesIV  =
            Encoding.UTF8.GetBytes("HelperTools_IV16");                   // 16 chars

        // ──────────────────────────────────────────────────────────────────
        //  Write
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Atomically writes <paramref name="json"/> to <paramref name="path"/>.
        /// The previous file (if any) is preserved as <c>.bak</c>.
        /// Optionally AES-256 encrypts the bytes before writing.
        /// </summary>
        public static void Write(string path, string json, bool encrypt = false)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            byte[] bytes = encrypt ? Encrypt(json) : Encoding.UTF8.GetBytes(json);

            string tmpPath = path + ".tmp";
            File.WriteAllBytes(tmpPath, bytes);

            // Atomic rename: old file → .bak, tmp → primary
            if (File.Exists(path))
                File.Replace(tmpPath, path, path + ".bak");
            else
                File.Move(tmpPath, path);
        }

        // ──────────────────────────────────────────────────────────────────
        //  Read
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads JSON from <paramref name="path"/>. On failure falls back to
        /// <c>.bak</c>. Returns <c>null</c> if the file does not exist or
        /// both primary and backup are unreadable.
        /// </summary>
        public static string Read(string path, bool encrypted = false)
        {
            if (!File.Exists(path)) return null;

            string result = TryRead(path, encrypted);
            if (result != null) return result;

            // Primary corrupt — try backup
            string bakPath = path + ".bak";
            if (File.Exists(bakPath))
            {
                Debug.LogWarning($"[SaveFileIO] Primary save corrupt at '{path}' — restoring backup.");
                result = TryRead(bakPath, encrypted);
                if (result != null)
                {
                    // Promote backup to primary
                    try { File.Copy(bakPath, path, overwrite: true); } catch { }
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Delete / Exists
        // ──────────────────────────────────────────────────────────────────

        /// <summary>Deletes the save file and its .bak / .tmp companions.</summary>
        public static void Delete(string path)
        {
            TryDelete(path);
            TryDelete(path + ".bak");
            TryDelete(path + ".tmp");
        }

        /// <summary>Returns true if the primary save file exists.</summary>
        public static bool Exists(string path) => File.Exists(path);

        // ──────────────────────────────────────────────────────────────────
        //  AES-256-CBC helpers
        // ──────────────────────────────────────────────────────────────────

        private static byte[] Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key     = AesKey;
            aes.IV      = AesIV;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] plain = Encoding.UTF8.GetBytes(plainText);
                cs.Write(plain, 0, plain.Length);
                cs.FlushFinalBlock();
            }
            return ms.ToArray();
        }

        private static string Decrypt(byte[] cipherBytes)
        {
            using var aes = Aes.Create();
            aes.Key     = AesKey;
            aes.IV      = AesIV;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms     = new MemoryStream(cipherBytes);
            using var cs     = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // ──────────────────────────────────────────────────────────────────
        //  Private helpers
        // ──────────────────────────────────────────────────────────────────

        private static string TryRead(string path, bool encrypted)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return encrypted ? Decrypt(bytes) : Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveFileIO] Read failed '{path}': {ex.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.LogWarning($"[SaveFileIO] Delete failed '{path}': {ex.Message}"); }
        }
    }
}
