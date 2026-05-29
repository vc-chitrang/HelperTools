using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// <para>
/// High-level orchestrator for media asset management in a Unity project.
/// </para>
///
/// <para><b>Responsibilities (what this class does):</b></para>
/// <list type="bullet">
///   <item>Accepts a list of remote URLs and triggers batch or single-file downloads.</item>
///   <item>Persists downloaded files to <c>Application.persistentDataPath/MediaFiles</c>.</item>
///   <item>Delegates sprite creation to <see cref="ImageLoader"/>.</item>
///   <item>Delegates video path resolution to <see cref="VideoLoader"/>.</item>
///   <item>Reports progress through <see cref="PopupManager"/>.</item>
/// </list>
///
/// <para><b>Non-responsibilities (what this class does NOT do):</b></para>
/// <list type="bullet">
///   <item>Texture/sprite creation logic — see <see cref="ImageLoader"/>.</item>
///   <item>Raw HTTP I/O — see <see cref="DownloadUtility"/>.</item>
///   <item>Video playback setup — see <see cref="VideoLoader"/>.</item>
/// </list>
/// </summary>
public class MediaManager:MonoBehaviour {
    // ──────────────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ──────────────────────────────────────────────────────────────────────────

    [Tooltip("Pre-populated via AssignDownloadableUrls() at runtime.")]
    [SerializeField] private List<string> _assetUrlList = new();

    // ──────────────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Catalogue of all files that have been successfully downloaded.</summary>
    public MediaRegistry Registry { get; } = new MediaRegistry();

    private readonly ImageLoader _imageLoader = new ImageLoader();
    private readonly VideoLoader _videoLoader = new VideoLoader();

    /// <summary>
    /// Token source used to cancel all in-flight downloads at once.
    /// Replaced each time a new batch is started.
    /// </summary>
    private CancellationTokenSource _batchCts;

    // ──────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDestroy() {
        // Cancel any in-flight downloads to prevent callbacks firing on a
        // destroyed MonoBehaviour.
        _batchCts?.Cancel();
        _batchCts?.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  URL management
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the current URL list with <paramref name="urls"/>, deduplicating
    /// case-insensitively.
    /// </summary>
    public void AssignDownloadableUrls(IEnumerable<string> urls) {
        if (urls == null)
            throw new ArgumentNullException(nameof(urls));

        _assetUrlList.Clear();
        _assetUrlList.AddRange(
            urls.Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Batch download
    // ──────────────────────────────────────────────────────────────────────────
    private void Start() {
    DownloadAllAsync((b) => { 
        if(b)
        {
            Debug.Log("All downloads completed successfully.");
        }
        else
        {
            Debug.LogError("Some downloads failed.");
        }
    });
}
    /// <summary>
    /// Downloads all URLs assigned via <see cref="AssignDownloadableUrls"/>.
    /// Progress is reported through <see cref="PopupManager"/>.
    /// </summary>
    /// <param name="onCompleted">
    /// Invoked on the main thread after ALL downloads finish (or are skipped).
    /// The boolean argument is <c>true</c> when every download succeeded.
    /// </param>
    public async Task DownloadAllAsync(Action<bool> onCompleted = null) {
        if (_assetUrlList == null || _assetUrlList.Count == 0) {
            Debug.LogWarning("[MediaManager] DownloadAllAsync called with no URLs assigned.");
            onCompleted?.Invoke(true);
            return;
        }

        // Cancel any previous batch before starting a new one.
        _batchCts?.Cancel();
        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();
        CancellationToken ct = _batchCts.Token;

        Registry.Clear();

        string directory = GetOrCreateMediaDirectory();
        bool allSuccess = true;
        int completed = 0;
        int total = _assetUrlList.Count;

        // Build and launch all download tasks concurrently.
        // Each task captures its own URL so the closure is correct.
        var tasks = _assetUrlList.Select(url => DownloadSingleUrlAsync(
            url,
            directory,
            onProgress: p => ReportProgress(completed,total,p),
            ct: ct,
            onDone: (result) => {
                if (result.IsSuccess)
                    RegisterDownloadedFile(result.LocalFilePath,url);
                else {
                    allSuccess = false;
                    Debug.LogError($"[MediaManager] Batch download failed: {result.ErrorMessage}");
                }

                completed++;
                ReportProgress(completed,total,1f);
            })).ToList();

        await Task.WhenAll(tasks);

        onCompleted?.Invoke(allSuccess);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Single image load (download + sprite creation)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <paramref name="url"/> is downloaded, then loads it as a
    /// <see cref="Sprite"/> via <see cref="ImageLoader"/>.
    /// </summary>
    /// <param name="url">Remote image URL.</param>
    /// <param name="onLoaded">
    /// Called with the resulting <see cref="Sprite"/> (may be <c>null</c> on failure).
    /// </param>
    public async Task LoadSpriteAsync(string url,Action<Sprite> onLoaded = null) {
        if (string.IsNullOrWhiteSpace(url)) {
            Debug.LogError("[MediaManager] LoadSpriteAsync: URL is null or empty.");
            onLoaded?.Invoke(null);
            return;
        }

        // Short-circuit if already cached by ImageLoader.
        if (_imageLoader.IsCached(url)) {
            onLoaded?.Invoke(_imageLoader.GetCached(url));
            return;
        }

        // Ensure the file is on disk (download if necessary).
        string localPath = await EnsureDownloadedAsync(url);
        if (localPath == null) {
            onLoaded?.Invoke(null);
            return;
        }

        Sprite sprite = await _imageLoader.LoadSpriteAsync(url,localPath);
        onLoaded?.Invoke(sprite);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Single video load (download + path resolution)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <paramref name="url"/> is downloaded, then resolves the local file
    /// path via <see cref="VideoLoader"/>.
    /// </summary>
    /// <param name="url">Remote video URL.</param>
    /// <param name="onResolved">
    /// Called with the absolute local path (may be <c>null</c> on failure).
    /// </param>
    public async Task LoadVideoAsync(string url,Action<string> onResolved = null) {
        if (string.IsNullOrWhiteSpace(url)) {
            Debug.LogError("[MediaManager] LoadVideoAsync: URL is null or empty.");
            onResolved?.Invoke(null);
            return;
        }

        // Ensure the file is on disk (download if necessary).
        string localPath = await EnsureDownloadedAsync(url);
        if (localPath == null) {
            onResolved?.Invoke(null);
            return;
        }

        VideoResolveResult result = _videoLoader.Resolve(url,Registry);
        if (!result.IsSuccess)
            Debug.LogError($"[MediaManager] Video resolve failed: {result.ErrorMessage}");

        onResolved?.Invoke(result.IsSuccess ? result.LocalFilePath : null);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Storage management
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the in-memory registry and sprite cache, then deletes all files in
    /// the local media directory from disk.
    /// </summary>
    public void ClearAllLocalData() {
        Registry.Clear();
        _imageLoader.ClearCache();

        string path = GetMediaDirectoryPath();
        if (!Directory.Exists(path)) {
            Debug.LogWarning("[MediaManager] ClearAllLocalData: Directory does not exist.");
            return;
        }

        try {
            // true = recursive: deletes the folder and all files inside.
            Directory.Delete(path,recursive: true);
            Debug.Log($"[MediaManager] <color=red>Local data cleared:</color> {path}");
        } catch (Exception ex) {
            Debug.LogError($"[MediaManager] Failed to clear local data: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads <paramref name="url"/> if it is not already on disk.
    /// Returns the local file path on success, or <c>null</c> on failure.
    /// </summary>
    private async Task<string> EnsureDownloadedAsync(
        string url,
        CancellationToken ct = default) {
        // Check registry first (file may have been downloaded in this session).
        string fileName = DownloadUtility.SanitizeFileName(url);
        MediaFile cached = Registry.Find(fileName);
        if (cached != null && cached.ExistsOnDisk)
            return cached.LocalPath;

        string directory = GetOrCreateMediaDirectory();

        // Use a simple progress reporter; for single-file loads the UI is
        // intentionally kept minimal.
        var progress = new Progress<float>(p => ReportProgress(0,1,p));
        DownloadResult result = await DownloadUtility.DownloadAssetAsync(url,directory,progress,ct);

        if (!result.IsSuccess) {
            Debug.LogError($"[MediaManager] EnsureDownloadedAsync failed for '{url}': {result.ErrorMessage}");
            return null;
        }

        RegisterDownloadedFile(result.LocalFilePath,url);
        ReportProgress(1,1,1f);
        return result.LocalFilePath;
    }

    /// <summary>
    /// Internal helper that downloads a single URL and calls
    /// <paramref name="onDone"/> when it finishes (success or failure).
    /// </summary>
    private async Task DownloadSingleUrlAsync(
        string url,
        string directory,
        Action<float> onProgress,
        CancellationToken ct,
        Action<DownloadResult> onDone) {
        var progress = new Progress<float>(onProgress);
        DownloadResult result = await DownloadUtility.DownloadAssetAsync(url,directory,progress,ct);
        onDone(result);
    }

    /// <summary>
    /// Creates a <see cref="MediaFile"/> from a downloaded file path and adds it
    /// to the <see cref="Registry"/>.
    /// </summary>
    private void RegisterDownloadedFile(string localFilePath,string sourceUrl) {
        var mediaFile = new MediaFile(localFilePath,sourceUrl);
        Registry.Register(mediaFile);
    }

    // ── File system ──────────────────────────────────────────────────────────

    private static string GetMediaDirectoryPath()
        => Path.Combine(Application.persistentDataPath,"MediaFiles");

    /// <summary>Returns the media directory path, creating it if it does not exist.</summary>
    private static string GetOrCreateMediaDirectory() {
        string path = GetMediaDirectoryPath();
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    // ── UI / Progress ────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes progress to both the loading popup UI and the Unity console.
    /// Console format: <c>{00%} (downloadedCount/TotalDownloadCount)</c>
    /// When <paramref name="total"/> is 1 (single-file loads) the file
    /// count is omitted from the UI text to avoid a misleading "0/1".
    /// </summary>
    /// <param name="downloaded">Number of files fully downloaded so far.</param>
    /// <param name="total">Total number of files in this batch.</param>
    /// <param name="progress">Per-file download progress in the range [0, 1].</param>
    private void ReportProgress(int downloaded,int total,float progress) {
        // Convert 0–1 float → 0–100 int so the output is never ambiguous,
        // regardless of platform culture or format-specifier parsing.
        // e.g.  [MediaManager] {75%} (3/4)
        int percent = Mathf.RoundToInt(progress * 100f);

        // ── Console log ───────────────────────────────────────────────────
        Debug.Log($"[MediaManager] {{{percent}%}} ({downloaded}/{total})");

        // ── Popup UI text ─────────────────────────────────────────────────
        string uiText = total > 1
            ? $"Loading...\n{downloaded}/{total} ({percent}%)"
            : $"Loading... ({percent}%)";
    }
}