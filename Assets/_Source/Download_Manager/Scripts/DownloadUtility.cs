using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Pure download utility — responsible only for fetching remote assets to local disk.
/// Has no knowledge of asset type, caching strategies, or Unity-specific asset creation.
/// All callers receive results via the <see cref="DownloadResult"/> return value.
/// </summary>
public static class DownloadUtility
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads a single remote asset to <paramref name="destinationDirectory"/>.
    /// If the file already exists on disk the download is skipped and the cached
    /// path is returned immediately (no network request is made).
    /// </summary>
    /// <param name="url">Remote URL of the asset to download.</param>
    /// <param name="destinationDirectory">Local directory where the file is saved.</param>
    /// <param name="onProgress">Optional per-frame progress callback (0 → 1).</param>
    /// <param name="cancellationToken">Token that can abort an in-flight download.</param>
    /// <returns>
    /// A <see cref="DownloadResult"/> that is either successful (with a local path)
    /// or failed (with an error message).
    /// </returns>
    public static async Task<DownloadResult> DownloadAssetAsync(
        string url,
        string destinationDirectory,
        IProgress<float> onProgress = null,
        CancellationToken cancellationToken = default)
    {
        // ── Guard: bad inputs ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(url))
            return DownloadResult.Failure("URL is null or empty.");

        if (string.IsNullOrWhiteSpace(destinationDirectory))
            return DownloadResult.Failure("Destination directory is null or empty.");

        // ── Resolve local file path ────────────────────────────────────────
        string fileName = SanitizeFileName(url);
        string localFilePath = Path.Combine(destinationDirectory, fileName);

        // Return immediately if the file is already cached on disk.
        if (File.Exists(localFilePath))
        {
            onProgress?.Report(1f);
            return DownloadResult.Success(localFilePath, fromCache: true);
        }

        // ── Ensure directory exists ────────────────────────────────────────
        EnsureDirectoryExists(destinationDirectory);

        // ── Perform network download ───────────────────────────────────────
        using UnityWebRequest request = UnityWebRequest.Get(url);
        UnityWebRequestAsyncOperation asyncOp = request.SendWebRequest();

        while (!asyncOp.isDone)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                request.Abort();
                return DownloadResult.Failure($"Download cancelled: {url}");
            }

            onProgress?.Report(asyncOp.progress);
            await Task.Delay(100, CancellationToken.None); // delay is internal; don't pass token here
        }

        // ── Handle failure ─────────────────────────────────────────────────
        if (request.result != UnityWebRequest.Result.Success)
            return DownloadResult.Failure($"Network error for '{url}': {request.error}");

        // ── Write to disk ──────────────────────────────────────────────────
        try
        {
            File.WriteAllBytes(localFilePath, request.downloadHandler.data);
        }
        catch (Exception ex)
        {
            return DownloadResult.Failure($"Failed to write file '{localFilePath}': {ex.Message}");
        }

        onProgress?.Report(1f);
        return DownloadResult.Success(localFilePath, fromCache: false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Converts a URL to a safe local file name by decoding %20 spaces.</summary>
    public static string SanitizeFileName(string url)
        => Path.GetFileName(url).Replace("%20", " ");

    /// <summary>Creates <paramref name="path"/> and all parent directories if missing.</summary>
    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}

// ────────────────────────────────────────────────────────────────────────────
//  Result type — avoids out-parameters and callback pyramids
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable result of a <see cref="DownloadUtility.DownloadAssetAsync"/> call.
/// Check <see cref="IsSuccess"/> before accessing <see cref="LocalFilePath"/>.
/// </summary>
public readonly struct DownloadResult
{
    /// <summary>True when the asset is available on disk.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Absolute local path to the downloaded file.
    /// Only valid when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public string LocalFilePath { get; }

    /// <summary>Human-readable error description when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public string ErrorMessage { get; }

    /// <summary>True when the file was already on disk and no network request was made.</summary>
    public bool FromCache { get; }

    private DownloadResult(bool isSuccess, string localFilePath, string errorMessage, bool fromCache)
    {
        IsSuccess = isSuccess;
        LocalFilePath = localFilePath;
        ErrorMessage = errorMessage;
        FromCache = fromCache;
    }

    /// <summary>Creates a successful result pointing to <paramref name="localFilePath"/>.</summary>
    public static DownloadResult Success(string localFilePath, bool fromCache = false)
        => new DownloadResult(true, localFilePath, null, fromCache);

    /// <summary>Creates a failed result carrying <paramref name="errorMessage"/>.</summary>
    public static DownloadResult Failure(string errorMessage)
        => new DownloadResult(false, null, errorMessage, false);

    public override string ToString()
        => IsSuccess ? $"[OK]{(FromCache ? "(cached)" : "")} {LocalFilePath}" : $"[FAIL] {ErrorMessage}";
}
