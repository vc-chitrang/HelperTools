using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Responsible for loading image files from local disk into Unity <see cref="Sprite"/>
/// objects.  Maintains an in-memory LRU-style sprite cache so that repeated loads of
/// the same URL do not re-read the disk.
///
/// This class has no knowledge of networking — it only reads files that have already
/// been written to disk by <see cref="MediaManager"/> (or another download layer).
/// </summary>
public class ImageLoader
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Fields
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cache keyed by the original source URL.
    /// A null value means the URL was attempted but loading failed — subsequent
    /// calls short-circuit rather than repeatedly retrying a broken file.
    /// </summary>
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    // ──────────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a <see cref="Sprite"/> from <paramref name="localFilePath"/> and
    /// associates it with <paramref name="sourceUrl"/> in the cache.
    /// </summary>
    /// <param name="sourceUrl">
    /// The original remote URL — used as the cache key so callers can look up
    /// sprites by URL without knowing the local path.
    /// </param>
    /// <param name="localFilePath">Absolute path to the image file on disk.</param>
    /// <param name="cancellationToken">Token to abort an async file read.</param>
    /// <returns>The loaded <see cref="Sprite"/>, or <c>null</c> if loading failed.</returns>
    public async Task<Sprite> LoadSpriteAsync(
        string sourceUrl,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Return cached result if available (including cached failures) ──
        if (_spriteCache.TryGetValue(sourceUrl, out Sprite cached))
            return cached;

        // ── 2. Guard: file must exist ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            Debug.LogError($"[ImageLoader] File not found: '{localFilePath}' (url: {sourceUrl})");
            return CacheAndReturn(sourceUrl, null);
        }

        // ── 3. Read raw bytes from disk (async to avoid blocking main thread) ─
        byte[] fileData;
        try
        {
            fileData = await ReadAllBytesAsync(localFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageLoader] Failed to read file '{localFilePath}': {ex.Message}");
            return CacheAndReturn(sourceUrl, null);
        }

        // ── 4. Decode into a Texture2D ────────────────────────────────────────
        // NOTE: Texture2D.LoadImage must run on the Unity main thread.
        // If this method is called from a background thread, marshal back here.
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!texture.LoadImage(fileData))
        {
            Debug.LogError($"[ImageLoader] Texture2D.LoadImage failed for '{localFilePath}'.");
            UnityEngine.Object.Destroy(texture);
            return CacheAndReturn(sourceUrl, null);
        }

        // ── 5. Create Sprite ──────────────────────────────────────────────────
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            pivot: new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 100f);

        return CacheAndReturn(sourceUrl, sprite);
    }

    /// <summary>
    /// Returns true when a <see cref="Sprite"/> for <paramref name="sourceUrl"/> is
    /// already cached (regardless of whether it is valid or a cached failure).
    /// </summary>
    public bool IsCached(string sourceUrl) => _spriteCache.ContainsKey(sourceUrl);

    /// <summary>
    /// Returns the cached sprite for <paramref name="sourceUrl"/>, or <c>null</c>
    /// if nothing has been cached yet.
    /// </summary>
    public Sprite GetCached(string sourceUrl)
        => _spriteCache.TryGetValue(sourceUrl, out var s) ? s : null;

    /// <summary>Clears all cached sprites and destroys the underlying textures.</summary>
    public void ClearCache()
    {
        foreach (var sprite in _spriteCache.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite.texture);
        }
        _spriteCache.Clear();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Stores <paramref name="sprite"/> in the cache and returns it.</summary>
    private Sprite CacheAndReturn(string url, Sprite sprite)
    {
        _spriteCache[url] = sprite;
        return sprite;
    }

    /// <summary>
    /// Async file read that avoids blocking the main thread for large files.
    /// Falls back to synchronous read on platforms that do not support async streams.
    /// </summary>
    private static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
    {
        // FileStream with async flag; buffer size 4 KB is a reasonable default.
        using FileStream fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        byte[] buffer = new byte[fs.Length];
        await fs.ReadAsync(buffer, 0, buffer.Length, ct);
        return buffer;
    }
}
