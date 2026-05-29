using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using DownloadManager;

/// <summary>
/// <para>
/// High-level orchestrator for media asset management in a Unity project.
/// This is the <b>download-logic layer</b>: it owns no UI references and renders
/// nothing on screen. Progress is published exclusively through C# events so that
/// any presentation layer (e.g. <see cref="DownloadProgressView"/>) can subscribe
/// without coupling the two together (separation of concern).
/// </para>
///
/// <para><b>Responsibilities (what this class does):</b></para>
/// <list type="bullet">
///   <item>Accepts a list of remote URLs and downloads them sequentially.</item>
///   <item>Persists downloaded files to <c>Application.persistentDataPath/MediaFiles</c>.</item>
///   <item>Publishes per-asset and overall progress via events.</item>
///   <item>Raises a completion event / callback when the whole batch finishes.</item>
///   <item>Delegates sprite creation to <see cref="ImageLoader"/> and video path
///         resolution to <see cref="VideoLoader"/>.</item>
/// </list>
///
/// <para><b>Non-responsibilities (what this class does NOT do):</b></para>
/// <list type="bullet">
///   <item>UI rendering / sliders / text — see <see cref="DownloadProgressView"/>.</item>
///   <item>Raw HTTP I/O — see <see cref="DownloadUtility"/>.</item>
///   <item>Texture/sprite creation — see <see cref="ImageLoader"/>.</item>
/// </list>
/// </summary>
public class MediaManager : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Progress events — the public contract consumed by the UI layer
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Raised continuously as the <i>current</i> asset downloads.</summary>
    public event Action<AssetDownloadProgress> AssetProgressChanged;

    /// <summary>Raised continuously as the <i>overall</i> batch progresses.</summary>
    public event Action<OverallDownloadProgress> OverallProgressChanged;

    /// <summary>
    /// Raised exactly once when the whole batch finishes.
    /// The bool is <c>true</c> only when every asset downloaded successfully.
    /// </summary>
    public event Action<bool> AllAssetsDownloaded;

    // ──────────────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ──────────────────────────────────────────────────────────────────────────

    [Tooltip("Pre-populated via AssignDownloadableUrls() at runtime.")]
    [SerializeField] private List<string> _assetUrlList = new();

    [Tooltip("Max assets downloading at once. 0 = Auto (recommended). Downloads are network I/O-bound, " +
             "so the optimal value depends on bandwidth and the server's per-host connection cap (~6-8), " +
             "NOT on CPU cores. Auto targets that sweet spot. Set 1 for sequential.")]
    [Range(0, 16)]
    [SerializeField] private int _maxConcurrentDownloads = 0;

    // ──────────────────────────────────────────────────────────────────────────
    //  Public state
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Catalogue of all files that have been successfully downloaded.</summary>
    public MediaRegistry Registry { get; } = new MediaRegistry();

    /// <summary>True while a batch download is in progress.</summary>
    public bool IsDownloading { get; private set; }

    /// <summary>Number of assets queued for the current/last batch.</summary>
    public int TotalAssets => _assetUrlList?.Count ?? 0;

    // ──────────────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────────────

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

    private void OnDestroy()
    {
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
    /// case-insensitively and dropping blanks.
    /// </summary>
    public void AssignDownloadableUrls(IEnumerable<string> urls)
    {
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

    /// <summary>
    /// Downloads all URLs assigned via <see cref="AssignDownloadableUrls"/>
    /// <b>concurrently</b> (up to <see cref="_maxConcurrentDownloads"/> at a time),
    /// which is the key time-saver: while one asset waits on the network, others
    /// download in parallel.
    /// <para>
    /// Progress is published through <see cref="AssetProgressChanged"/> (the most
    /// recently active asset) and <see cref="OverallProgressChanged"/> (a smooth
    /// aggregate of every asset's progress); completion through
    /// <see cref="AllAssetsDownloaded"/> and the <paramref name="onCompleted"/> callback.
    /// </para>
    /// </summary>
    /// <param name="onCompleted">
    /// Invoked on the main thread after ALL downloads finish (or are skipped).
    /// The boolean argument is <c>true</c> when every download succeeded.
    /// </param>
    public async Task DownloadAllAsync(Action<bool> onCompleted = null)
    {
        int total = _assetUrlList?.Count ?? 0;

        if (total == 0)
        {
            Debug.LogWarning("[MediaManager] DownloadAllAsync called with no URLs assigned.");
            RaiseOverall(0, 0, 1f);
            AllAssetsDownloaded?.Invoke(true);
            onCompleted?.Invoke(true);
            return;
        }

        // Cancel any previous batch before starting a new one.
        _batchCts?.Cancel();
        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();
        CancellationToken ct = _batchCts.Token;

        Registry.Clear();
        IsDownloading = true;

        // Create the root + Images/Videos/Models structure up front.
        string rootDirectory = GetOrCreateMediaDirectory();
        EnsureCategoryFolders(rootDirectory);

        // Per-file progress in [0,1]; the overall bar is the average across all files.
        // Every mutation below happens on the main thread (Progress<T> + Unity's sync
        // context marshal continuations back here), so no locking is required.
        float[] fileProgress = new float[total];
        int completed = 0;
        bool allSuccess = true;

        RaiseOverall(0, total, 0f);

        // Bounds the number of simultaneous network requests so we never open all
        // connections at once — fast, but not abusive.
        int concurrency = ResolveConcurrency(total);
        using var throttler = new SemaphoreSlim(concurrency, concurrency);
        Debug.Log($"[MediaManager] Downloading {total} assets, concurrency = {concurrency} " +
                  $"({(_maxConcurrentDownloads <= 0 ? "Auto" : "manual")}).");

        // Local async function: download a single asset under the throttle.
        async Task DownloadOneAsync(int index)
        {
            string url = _assetUrlList[index];
            string assetName = DownloadUtility.SanitizeFileName(url);
            int assetNumber = index + 1;

            try
            {
                await throttler.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                allSuccess = false;
                return;
            }

            try
            {
                // Route this file into its category sub-folder (Images/Videos/Models/Other).
                string fileDirectory = GetOrCreateCategoryDirectory(rootDirectory, url);

                var progress = new Progress<float>(p =>
                {
                    fileProgress[index] = p;
                    // Slider 1 reflects the most recently active asset.
                    RaiseAsset(assetName, url, assetNumber, total, p);
                    // Slider 2 = smooth aggregate of every file's progress.
                    RaiseOverall(completed, total, AverageProgress(fileProgress));
                });

                DownloadResult result =
                    await DownloadUtility.DownloadAssetAsync(url, fileDirectory, progress, ct);

                if (result.IsSuccess)
                    RegisterDownloadedFile(result.LocalFilePath, url);
                else
                {
                    allSuccess = false;
                    Debug.LogError($"[MediaManager] Download failed for '{url}': {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                allSuccess = false;
                Debug.LogError($"[MediaManager] Unexpected error downloading '{url}': {ex.Message}");
            }
            finally
            {
                // Count the asset as processed (success or failure) and snap its
                // slice to 100% so the overall bar can reach completion.
                fileProgress[index] = 1f;
                completed++;
                RaiseAsset(assetName, url, assetNumber, total, 1f);
                RaiseOverall(completed, total, AverageProgress(fileProgress));
                throttler.Release();
            }
        }

        try
        {
            var tasks = new List<Task>(total);
            for (int i = 0; i < total; i++)
                tasks.Add(DownloadOneAsync(i));

            await Task.WhenAll(tasks);
        }
        finally
        {
            IsDownloading = false;
        }

        AllAssetsDownloaded?.Invoke(allSuccess);
        onCompleted?.Invoke(allSuccess);
    }

    /// <summary>
    /// Resolves the effective concurrency. A configured value of 0 (or less) means
    /// <b>Auto</b>. Asset downloads are network I/O-bound, so the best degree of
    /// parallelism is governed by available bandwidth and the server's per-host
    /// connection limit (most CDNs / S3 allow ~6-8 before throttling) — not by CPU
    /// core count. Auto therefore targets 8, capped by the batch size.
    /// </summary>
    private int ResolveConcurrency(int total)
    {
        const int autoPerHostLimit = 8;
        int requested = _maxConcurrentDownloads > 0 ? _maxConcurrentDownloads : autoPerHostLimit;
        return Mathf.Clamp(Mathf.Min(requested, total), 1, 32);
    }

    /// <summary>Mean of all per-file progress values, in [0, 1].</summary>
    private static float AverageProgress(float[] perFile)
    {
        if (perFile == null || perFile.Length == 0)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < perFile.Length; i++)
            sum += perFile[i];
        return sum / perFile.Length;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Single image load (download + sprite creation)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <paramref name="url"/> is downloaded, then loads it as a
    /// <see cref="Sprite"/> via <see cref="ImageLoader"/>.
    /// </summary>
    public async Task LoadSpriteAsync(string url, Action<Sprite> onLoaded = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogError("[MediaManager] LoadSpriteAsync: URL is null or empty.");
            onLoaded?.Invoke(null);
            return;
        }

        if (_imageLoader.IsCached(url))
        {
            onLoaded?.Invoke(_imageLoader.GetCached(url));
            return;
        }

        string localPath = await EnsureDownloadedAsync(url);
        if (localPath == null)
        {
            onLoaded?.Invoke(null);
            return;
        }

        Sprite sprite = await _imageLoader.LoadSpriteAsync(url, localPath);
        onLoaded?.Invoke(sprite);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Single video load (download + path resolution)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <paramref name="url"/> is downloaded, then resolves the local file
    /// path via <see cref="VideoLoader"/>.
    /// </summary>
    public async Task LoadVideoAsync(string url, Action<string> onResolved = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogError("[MediaManager] LoadVideoAsync: URL is null or empty.");
            onResolved?.Invoke(null);
            return;
        }

        string localPath = await EnsureDownloadedAsync(url);
        if (localPath == null)
        {
            onResolved?.Invoke(null);
            return;
        }

        VideoResolveResult result = _videoLoader.Resolve(url, Registry);
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
    public void ClearAllLocalData()
    {
        Registry.Clear();
        _imageLoader.ClearCache();

        string path = GetMediaDirectoryPath();
        if (!Directory.Exists(path))
        {
            Debug.LogWarning("[MediaManager] ClearAllLocalData: Directory does not exist.");
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            Debug.Log($"[MediaManager] <color=red>Local data cleared:</color> {path}");
        }
        catch (Exception ex)
        {
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
    private async Task<string> EnsureDownloadedAsync(string url, CancellationToken ct = default)
    {
        string fileName = DownloadUtility.SanitizeFileName(url);
        MediaFile cached = Registry.Find(fileName);
        if (cached != null && cached.ExistsOnDisk)
            return cached.LocalPath;

        // Route into the matching category sub-folder (Images/Videos/Models/Other).
        string rootDirectory = GetOrCreateMediaDirectory();
        string directory = GetOrCreateCategoryDirectory(rootDirectory, url);

        var progress = new Progress<float>(p =>
        {
            RaiseAsset(fileName, url, 1, 1, p);
            RaiseOverall(0, 1, p);
        });

        DownloadResult result = await DownloadUtility.DownloadAssetAsync(url, directory, progress, ct);

        if (!result.IsSuccess)
        {
            Debug.LogError($"[MediaManager] EnsureDownloadedAsync failed for '{url}': {result.ErrorMessage}");
            return null;
        }

        RegisterDownloadedFile(result.LocalFilePath, url);
        RaiseAsset(fileName, url, 1, 1, 1f);
        RaiseOverall(1, 1, 1f);
        return result.LocalFilePath;
    }

    /// <summary>
    /// Creates a <see cref="MediaFile"/> from a downloaded file path and adds it
    /// to the <see cref="Registry"/>.
    /// </summary>
    private void RegisterDownloadedFile(string localFilePath, string sourceUrl)
    {
        var mediaFile = new MediaFile(localFilePath, sourceUrl);
        Registry.Register(mediaFile);
    }

    // ── Event raising (DRY: one place each) ────────────────────────────────────

    private void RaiseAsset(string name, string url, int number, int total, float progress01)
        => AssetProgressChanged?.Invoke(new AssetDownloadProgress(name, url, number, total, progress01));

    private void RaiseOverall(int downloaded, int total, float progress01)
        => OverallProgressChanged?.Invoke(new OverallDownloadProgress(downloaded, total, progress01));

    // ── File system ────────────────────────────────────────────────────────────

    private static string GetMediaDirectoryPath()
        => Path.Combine(Application.persistentDataPath, "MediaFiles");

    /// <summary>Returns the root media directory path, creating it if it does not exist.</summary>
    private static string GetOrCreateMediaDirectory()
    {
        string path = GetMediaDirectoryPath();
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Pre-creates the <c>Images</c>, <c>Videos</c> and <c>Models</c> sub-folders
    /// under <paramref name="rootDir"/> so the structure is always complete, even
    /// when a category has no files in the current batch.
    /// </summary>
    private static void EnsureCategoryFolders(string rootDir)
    {
        foreach (string folder in MediaCategory.AllFolders)
        {
            string path = Path.Combine(rootDir, folder);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Returns the category sub-folder (Images/Videos/Models/Other) for
    /// <paramref name="urlOrFileName"/> under <paramref name="rootDir"/>, creating it.
    /// </summary>
    private static string GetOrCreateCategoryDirectory(string rootDir, string urlOrFileName)
    {
        string path = Path.Combine(rootDir, MediaCategory.ForFileName(urlOrFileName));
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }
}
