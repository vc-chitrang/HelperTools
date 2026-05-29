using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace DownloadManager
{
    /// <summary>Inspector-friendly UnityEvent carrying the batch success flag.</summary>
    [Serializable]
    public class BoolUnityEvent : UnityEvent<bool> { }

    /// <summary>
    /// <para>
    /// Thin <b>controller</b> that glues the three concerns together without merging
    /// them:
    /// </para>
    /// <list type="number">
    ///   <item>Sources the asset URLs (parses <c>SHM_AppData.json</c>).</item>
    ///   <item>Feeds them to the download-logic layer (<see cref="MediaManager"/>).</item>
    ///   <item>Forwards the manager's progress events to the UI layer
    ///         (<see cref="DownloadProgressView"/>).</item>
    /// </list>
    /// <para>
    /// Neither the manager nor the view references the other — this controller is the
    /// single wiring point, and it exposes the <c>onAllAssetDownload Done</c> callback.
    /// </para>
    /// </summary>
    public class DownloadSceneController : MonoBehaviour
    {
        [Header("Layers (separation of concern)")]
        [Tooltip("Download-logic layer. No UI knowledge.")]
        [SerializeField] private MediaManager _mediaManager;

        [Tooltip("Presentation layer. No download knowledge.")]
        [SerializeField] private DownloadProgressView _view;

        [Header("Asset source (test data)")]
        [Tooltip("File name resolved against Application.persistentDataPath when no override/asset is set.")]
        [SerializeField] private string _jsonFileName = "SHM_AppData.json";

        [Tooltip("Optional absolute path to the JSON file. Used for editor testing; takes priority when the file exists.")]
        [SerializeField] private string _absoluteJsonPathOverride = "";

        [Tooltip("Optional JSON TextAsset fallback if no file is found on disk.")]
        [SerializeField] private TextAsset _jsonAssetFallback;

        [Tooltip("Last-resort fallback URLs if no JSON source is available.")]
        [SerializeField] private List<string> _manualUrlFallback = new();

        [Tooltip("Cap the number of assets downloaded (0 = download all). Useful to keep test runs short.")]
        [SerializeField] private int _maxAssets = 0;

        [Header("Behaviour")]
        [Tooltip("Begin downloading automatically when the scene starts.")]
        [SerializeField] private bool _downloadOnStart = true;

        [Tooltip("Designer hook fired once when the whole batch finishes (bool = all succeeded).")]
        [SerializeField] private BoolUnityEvent _onAllAssetsDownloaded = new();

        /// <summary>
        /// Code-level completion callback ("onAllAssetDownload Done").
        /// Mirrors <see cref="_onAllAssetsDownloaded"/> for non-inspector subscribers.
        /// </summary>
        public event Action<bool> AllAssetsDownloaded;

        // ──────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (!ValidateReferences())
                return;

            // Wire download-logic events → UI. This controller is the ONLY place
            // the two layers meet.
            _mediaManager.AssetProgressChanged   += _view.ShowAssetProgress;
            _mediaManager.OverallProgressChanged += _view.ShowOverallProgress;
            _mediaManager.AllAssetsDownloaded    += HandleAllAssetsDownloaded;

            _view.ResetView();
        }

        private void OnDestroy()
        {
            if (_mediaManager == null)
                return;

            _mediaManager.AssetProgressChanged   -= _view.ShowAssetProgress;
            _mediaManager.OverallProgressChanged -= _view.ShowOverallProgress;
            _mediaManager.AllAssetsDownloaded    -= HandleAllAssetsDownloaded;
        }

        private async void Start()
        {
            if (!_downloadOnStart || _mediaManager == null)
                return;

            await BeginDownloadAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sources the URLs and kicks off the batch download. Safe to call manually
        /// (e.g. from a button) when <see cref="_downloadOnStart"/> is disabled.
        /// </summary>
        public async System.Threading.Tasks.Task BeginDownloadAsync()
        {
            List<string> urls = ResolveUrls();
            if (urls.Count == 0)
            {
                Debug.LogWarning("[DownloadSceneController] No URLs resolved — nothing to download.");
                _view.ResetView();
                HandleAllAssetsDownloaded(true);
                return;
            }

            if (_maxAssets > 0 && urls.Count > _maxAssets)
            {
                Debug.Log($"[DownloadSceneController] Capping batch to first {_maxAssets} of {urls.Count} assets.");
                urls = urls.GetRange(0, _maxAssets);
            }

            Debug.Log($"[DownloadSceneController] Starting download of {urls.Count} asset(s).");
            _mediaManager.AssignDownloadableUrls(urls);
            _view.ResetView();

            await _mediaManager.DownloadAllAsync();
        }

        /// <summary>
        /// Code entry point for consuming the prefab from any project: assigns
        /// <paramref name="urls"/> and starts downloading immediately. The optional
        /// <paramref name="onCompleted"/> fires exactly once when the batch finishes
        /// (true = every asset succeeded) — this is the "onAllAssetDownload Done" hook.
        /// </summary>
        /// <example>
        /// <code>
        /// downloader.StartDownload(myUrls, success =>
        ///     Debug.Log(success ? "All done!" : "Some failed"));
        /// </code>
        /// </example>
        public async System.Threading.Tasks.Task StartDownload(
            IEnumerable<string> urls, Action<bool> onCompleted = null)
        {
            if (!ValidateReferences())
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (onCompleted != null)
            {
                // One-shot: detach after the first completion so repeated calls don't stack.
                void OneShot(bool ok) { AllAssetsDownloaded -= OneShot; onCompleted(ok); }
                AllAssetsDownloaded += OneShot;
            }

            if (urls != null)
                _mediaManager.AssignDownloadableUrls(urls);

            _view.ResetView();
            await _mediaManager.DownloadAllAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Internals
        // ──────────────────────────────────────────────────────────────────────

        private void HandleAllAssetsDownloaded(bool success)
        {
            _view.ShowCompleted(success);

            Debug.Log(success
                ? "[DownloadSceneController] ✓ onAllAssetDownload Done — all assets downloaded."
                : "[DownloadSceneController] ✗ onAllAssetDownload Done — completed with failures.");

            _onAllAssetsDownloaded?.Invoke(success);
            AllAssetsDownloaded?.Invoke(success);
        }

        /// <summary>
        /// Resolves the asset URL list from the first available source:
        /// absolute override file → persistentDataPath file → TextAsset → manual list.
        /// </summary>
        private List<string> ResolveUrls()
        {
            string json = ReadJson();
            if (!string.IsNullOrWhiteSpace(json))
            {
                List<string> extracted = AppDataUrlExtractor.ExtractUrls(json);
                if (extracted.Count > 0)
                    return extracted;
            }

            Debug.LogWarning("[DownloadSceneController] Falling back to manual URL list.");
            return new List<string>(_manualUrlFallback);
        }

        private string ReadJson()
        {
            // 1) Explicit absolute override (editor testing).
            if (!string.IsNullOrWhiteSpace(_absoluteJsonPathOverride) && File.Exists(_absoluteJsonPathOverride))
                return SafeReadFile(_absoluteJsonPathOverride);

            // 2) persistentDataPath/<fileName> (production location).
            string persistentPath = Path.Combine(Application.persistentDataPath, _jsonFileName);
            if (File.Exists(persistentPath))
                return SafeReadFile(persistentPath);

            // 3) Bundled TextAsset fallback.
            if (_jsonAssetFallback != null)
                return _jsonAssetFallback.text;

            return null;
        }

        private static string SafeReadFile(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DownloadSceneController] Failed to read JSON at '{path}': {ex.Message}");
                return null;
            }
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            if (_mediaManager == null)
            {
                Debug.LogError("[DownloadSceneController] MediaManager reference is not assigned.", this);
                ok = false;
            }
            if (_view == null)
            {
                Debug.LogError("[DownloadSceneController] DownloadProgressView reference is not assigned.", this);
                ok = false;
            }
            return ok;
        }
    }
}
