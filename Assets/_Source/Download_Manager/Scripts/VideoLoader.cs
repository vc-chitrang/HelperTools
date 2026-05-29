using System.IO;
using UnityEngine;

/// <summary>
/// Responsible for resolving the local disk path of a downloaded video asset so
/// that callers (e.g. a VideoPlayer component) can point directly to the file.
///
/// This class performs no network I/O.  The download layer (<see cref="MediaManager"/>)
/// must ensure the file is on disk before calling <see cref="Resolve"/>.
/// </summary>
public class VideoLoader
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to resolve the local file path for a video asset identified by
    /// <paramref name="sourceUrl"/> inside the provided <see cref="MediaRegistry"/>.
    /// </summary>
    /// <param name="sourceUrl">Remote URL of the video asset.</param>
    /// <param name="registry">Registry populated by the download layer.</param>
    /// <returns>
    /// A <see cref="VideoResolveResult"/> that is either successful (with a local
    /// path) or failed (with a reason).
    /// </returns>
    public VideoResolveResult Resolve(string sourceUrl, MediaRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return VideoResolveResult.Failure("Source URL is null or empty.");

        if (registry == null)
            return VideoResolveResult.Failure("Registry is null.");

        string fileName = DownloadUtility.SanitizeFileName(sourceUrl);
        MediaFile file  = registry.Find(fileName);

        // ── File not yet downloaded ───────────────────────────────────────────
        if (file == null)
        {
            string reason = $"No registry entry found for '{fileName}'. " +
                            $"Ensure the asset was downloaded before calling Resolve.";
            Debug.LogWarning($"[VideoLoader] {reason}");
            return VideoResolveResult.Failure(reason);
        }

        // ── Registry entry exists but the physical file is gone ───────────────
        if (!file.ExistsOnDisk)
        {
            string reason = $"Registry entry exists for '{fileName}' but file " +
                            $"is missing at '{file.LocalPath}'.";
            Debug.LogError($"[VideoLoader] {reason}");
            return VideoResolveResult.Failure(reason);
        }

        return VideoResolveResult.Success(file.LocalPath);
    }
}

// ────────────────────────────────────────────────────────────────────────────
//  Result type
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable result of <see cref="VideoLoader.Resolve"/>.
/// Check <see cref="IsSuccess"/> before reading <see cref="LocalFilePath"/>.
/// </summary>
public readonly struct VideoResolveResult
{
    /// <summary>True when a valid local path was resolved.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Absolute local file path to the video.
    /// Only valid when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public string LocalFilePath { get; }

    /// <summary>Reason for failure. Only meaningful when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public string ErrorMessage { get; }

    private VideoResolveResult(bool isSuccess, string localFilePath, string errorMessage)
    {
        IsSuccess     = isSuccess;
        LocalFilePath = localFilePath;
        ErrorMessage  = errorMessage;
    }

    public static VideoResolveResult Success(string localFilePath)
        => new VideoResolveResult(true, localFilePath, null);

    public static VideoResolveResult Failure(string errorMessage)
        => new VideoResolveResult(false, null, errorMessage);

    public override string ToString()
        => IsSuccess ? $"[OK] {LocalFilePath}" : $"[FAIL] {ErrorMessage}";
}
