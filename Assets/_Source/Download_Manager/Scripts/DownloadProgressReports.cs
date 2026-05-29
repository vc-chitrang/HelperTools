using System;
using UnityEngine;

/// <summary>
/// <para>
/// Plain, immutable value types describing download progress.  These records form
/// the <b>contract between the download-logic layer and the UI layer</b>: the logic
/// (<see cref="MediaManager"/>) emits them, and the presentation layer
/// (<see cref="DownloadProgressView"/>) consumes them.
/// </para>
/// <para>
/// They carry <i>data only</i> — no Unity types, no networking, no rendering — so
/// neither side needs to know how the other is implemented (separation of concern).
/// </para>
/// </summary>
namespace DownloadManager
{
    /// <summary>
    /// Progress of the <b>single asset</b> currently being downloaded.
    /// Drives the per-asset slider: <c>{AssetName} ----- {Percent}%</c>.
    /// </summary>
    [Serializable]
    public readonly struct AssetDownloadProgress
    {
        /// <summary>Human-readable file name of the asset in flight (e.g. "hero.png").</summary>
        public string AssetName { get; }

        /// <summary>Remote URL of the asset in flight.</summary>
        public string Url { get; }

        /// <summary>1-based index of this asset within the batch (e.g. asset 3 of 10).</summary>
        public int AssetNumber { get; }

        /// <summary>Total number of assets in the batch.</summary>
        public int TotalAssets { get; }

        /// <summary>Download progress of <i>this</i> asset, normalized to [0, 1].</summary>
        public float Progress01 { get; }

        public AssetDownloadProgress(string assetName, string url, int assetNumber, int totalAssets, float progress01)
        {
            AssetName   = assetName;
            Url         = url;
            AssetNumber = assetNumber;
            TotalAssets = totalAssets;
            Progress01  = progress01;
        }

        /// <summary>Progress of this asset as a whole percentage [0, 100].</summary>
        public int Percent => Mathf.RoundToInt(Mathf.Clamp01(Progress01) * 100f);
    }

    /// <summary>
    /// Aggregate progress across the <b>whole batch</b>.
    /// Drives the overall slider: <c>{Downloaded}/{Total} ({Percent}%)</c>.
    /// </summary>
    [Serializable]
    public readonly struct OverallDownloadProgress
    {
        /// <summary>Number of assets fully completed so far.</summary>
        public int Downloaded { get; }

        /// <summary>Total number of assets in the batch.</summary>
        public int Total { get; }

        /// <summary>
        /// Combined progress across the batch, normalized to [0, 1].
        /// Includes the fractional progress of the in-flight asset so the bar
        /// advances smoothly rather than only on completion.
        /// </summary>
        public float Progress01 { get; }

        public OverallDownloadProgress(int downloaded, int total, float progress01)
        {
            Downloaded = downloaded;
            Total      = total;
            Progress01 = progress01;
        }

        /// <summary>Overall progress as a whole percentage [0, 100].</summary>
        public int Percent => Mathf.RoundToInt(Mathf.Clamp01(Progress01) * 100f);
    }
}
