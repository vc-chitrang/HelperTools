using System;
using System.Collections.Generic;
using UnityEngine;

namespace DownloadManager
{
    // ────────────────────────────────────────────────────────────────────────────
    //  Serializable models — mirror the structure of SHM_AppData.json
    //  (Only the fields we need are declared; JsonUtility ignores the rest.)
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>Root object of <c>SHM_AppData.json</c>.</summary>
    [Serializable]
    public class AppData
    {
        public string version;
        public GalleryData dusGurusData;
        public GalleryData guruNanakData;
    }

    /// <summary>A single gallery section containing one video and a list of media items.</summary>
    [Serializable]
    public class GalleryData
    {
        public GalleryVideo galleryVideo;
        public List<MediaItem> mediaList = new();
    }

    /// <summary>The trailer/intro video attached to a gallery.</summary>
    [Serializable]
    public class GalleryVideo
    {
        public string videoUrl;
        public string videoDescription;
    }

    /// <summary>One artwork entry and its associated downloadable image URLs.</summary>
    [Serializable]
    public class MediaItem
    {
        public int id;
        public string title;
        public string headerSpriteUrl;
        public string thumbnailSpriteUrl;
        public string thumbnailGraySpriteUrl;
    }

    // ────────────────────────────────────────────────────────────────────────────
    //  Extractor — turns parsed app data into a flat, deduplicated URL list
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stateless helper that parses <c>SHM_AppData.json</c> and flattens every
    /// downloadable asset URL (videos + images) into a single ordered list.
    /// </summary>
    public static class AppDataUrlExtractor
    {
        /// <summary>
        /// Parses <paramref name="json"/> and returns every downloadable URL it
        /// contains. Videos are emitted first (they are the largest assets), then
        /// image URLs, in document order. Duplicates and blanks are removed.
        /// </summary>
        /// <param name="json">Raw JSON text matching the <see cref="AppData"/> schema.</param>
        /// <returns>Ordered, deduplicated list of asset URLs (never null).</returns>
        public static List<string> ExtractUrls(string json)
        {
            var urls = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[AppDataUrlExtractor] Empty JSON supplied; returning no URLs.");
                return urls;
            }

            AppData data;
            try
            {
                data = JsonUtility.FromJson<AppData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppDataUrlExtractor] Failed to parse JSON: {ex.Message}");
                return urls;
            }

            if (data == null)
                return urls;

            // Videos first so the heavy assets surface early in the batch.
            CollectVideoUrls(data.dusGurusData, urls);
            CollectVideoUrls(data.guruNanakData, urls);

            CollectImageUrls(data.dusGurusData, urls);
            CollectImageUrls(data.guruNanakData, urls);

            return Deduplicate(urls);
        }

        // ── Private collectors (DRY: one routine per concern) ──────────────────

        private static void CollectVideoUrls(GalleryData gallery, List<string> sink)
        {
            if (gallery?.galleryVideo != null)
                AddIfValid(gallery.galleryVideo.videoUrl, sink);
        }

        private static void CollectImageUrls(GalleryData gallery, List<string> sink)
        {
            if (gallery?.mediaList == null)
                return;

            foreach (MediaItem item in gallery.mediaList)
            {
                if (item == null)
                    continue;

                AddIfValid(item.headerSpriteUrl, sink);
                AddIfValid(item.thumbnailSpriteUrl, sink);
                AddIfValid(item.thumbnailGraySpriteUrl, sink);
            }
        }

        private static void AddIfValid(string url, List<string> sink)
        {
            if (!string.IsNullOrWhiteSpace(url))
                sink.Add(url.Trim());
        }

        private static List<string> Deduplicate(List<string> urls)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(urls.Count);

            foreach (string url in urls)
            {
                if (seen.Add(url))
                    result.Add(url);
            }

            return result;
        }
    }
}
