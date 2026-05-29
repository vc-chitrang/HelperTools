using System;
using System.Collections.Generic;
using System.IO;

// ────────────────────────────────────────────────────────────────────────────
//  File-extension constants
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Known media file extensions used throughout the system.</summary>
public static class MediaExtension
{
    public const string Mp4  = ".mp4";
    public const string Jpg  = ".jpg";
    public const string Jpeg = ".jpeg";
    public const string Png  = ".png";
    public const string Json = ".json";
}

// ────────────────────────────────────────────────────────────────────────────
//  MediaCategory — maps a file extension to a storage sub-folder
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Single source of truth for classifying a media file by extension and deciding
/// which sub-folder it belongs in (Images / Videos / Models / Other). Keeping this
/// in one place means the download layer, loaders, and any future tooling all agree
/// on where a given file type lives (DRY).
/// </summary>
public static class MediaCategory
{
    // Sub-folder names under the root media directory.
    public const string Images = "Images";
    public const string Videos = "Videos";
    public const string Models = "Models";
    public const string Other  = "Other";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".webp" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".webm", ".mkv", ".avi", ".m4v" };

    private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".glb", ".gltf", ".obj", ".fbx", ".dae", ".ply", ".stl" };

    /// <summary>All category sub-folders that should always exist (so the structure
    /// is complete even when a category currently has no files).</summary>
    public static readonly string[] AllFolders = { Images, Videos, Models };

    /// <summary>Returns the sub-folder name for a file extension (e.g. ".png" → "Images").</summary>
    public static string ForExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return Other;
        if (ImageExtensions.Contains(extension)) return Images;
        if (VideoExtensions.Contains(extension)) return Videos;
        if (ModelExtensions.Contains(extension)) return Models;
        return Other;
    }

    /// <summary>Returns the sub-folder name for a file name or URL (uses its extension).</summary>
    public static string ForFileName(string fileNameOrUrl)
        => ForExtension(Path.GetExtension(fileNameOrUrl ?? string.Empty));
}

// ────────────────────────────────────────────────────────────────────────────
//  MediaFile — immutable data record
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents metadata for a single downloaded media asset.
/// Instances are created after a successful download and are considered immutable.
/// </summary>
[Serializable]
public class MediaFile
{
    /// <summary>File name including extension, e.g. "hero_banner.jpg".</summary>
    public string FileName { get; }

    /// <summary>Extension including the dot, e.g. ".jpg".</summary>
    public string FileType { get; }

    /// <summary>Absolute local path to the file on disk.</summary>
    public string LocalPath { get; }

    /// <summary>Original remote URL the asset was downloaded from.</summary>
    public string SourceUrl { get; }

    public MediaFile(string localPath, string sourceUrl)
    {
        LocalPath = localPath;
        SourceUrl = sourceUrl;
        FileName  = Path.GetFileName(localPath);
        FileType  = Path.GetExtension(localPath).ToLowerInvariant();
    }

    // ── Type helpers ──────────────────────────────────────────────────────

    /// <summary>Returns true for .mp4 files.</summary>
    public bool IsVideo  => FileType == MediaExtension.Mp4;

    /// <summary>Returns true for .jpg / .jpeg / .png files.</summary>
    public bool IsImage  => FileType is MediaExtension.Jpg
                                      or MediaExtension.Jpeg
                                      or MediaExtension.Png;

    /// <summary>The storage sub-folder this file belongs to (Images / Videos / Models / Other).</summary>
    public string Category => MediaCategory.ForExtension(FileType);

    /// <summary>Returns true if the file physically exists on disk.</summary>
    public bool ExistsOnDisk => File.Exists(LocalPath);
}

// ────────────────────────────────────────────────────────────────────────────
//  MediaRegistry — in-memory catalogue of downloaded files
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Thread-safe catalogue that maps a sanitized file name to its <see cref="MediaFile"/>
/// record.  All lookups are O(1).
/// </summary>
public class MediaRegistry
{
    // Key: sanitized file name (e.g. "hero banner.jpg")
    private readonly Dictionary<string, MediaFile> _entries = new();

    // ── Mutation ──────────────────────────────────────────────────────────

    /// <summary>Adds or replaces a record.  Keyed by <see cref="MediaFile.FileName"/>.</summary>
    public void Register(MediaFile file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        _entries[file.FileName] = file;
    }

    /// <summary>Removes all entries from the registry.</summary>
    public void Clear() => _entries.Clear();

    // ── Queries ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to find a <see cref="MediaFile"/> by its file name.
    /// Returns <c>null</c> when not found.
    /// </summary>
    public MediaFile Find(string fileName)
        => _entries.TryGetValue(fileName, out var file) ? file : null;

    /// <summary>Returns true when a record for <paramref name="fileName"/> exists.</summary>
    public bool Contains(string fileName) => _entries.ContainsKey(fileName);

    /// <summary>Total number of registered files.</summary>
    public int Count => _entries.Count;

    /// <summary>Read-only snapshot of all registered files.</summary>
    public IReadOnlyDictionary<string, MediaFile> All => _entries;
}
