using UnityEngine;
using UnityEngine.UI;

namespace DownloadManager
{
    /// <summary>
    /// <para>
    /// Pure <b>presentation layer</b> for download progress. It knows how to render
    /// progress data onto two sliders, their labels, and a completion message — and
    /// nothing else. It performs <i>no</i> networking, file I/O, or download
    /// orchestration, so it stays fully decoupled from <see cref="MediaManager"/>
    /// (separation of concern).
    /// </para>
    /// <para>
    /// It is driven entirely through its public methods, which accept the plain
    /// progress records (<see cref="AssetDownloadProgress"/> /
    /// <see cref="OverallDownloadProgress"/>). A controller wires those to the
    /// download layer's events.
    /// </para>
    /// </summary>
    public class DownloadProgressView : MonoBehaviour
    {
        // ── Individual asset (slider 1) ────────────────────────────────────────
        [Header("Individual Asset Progress")]
        [Tooltip("Slider that fills with the CURRENT asset's download progress.")]
        [SerializeField] private Slider _assetSlider;

        [Tooltip("Label rendered as: {assetName} ----- {percent}%")]
        [SerializeField] private Text _assetLabel;

        // ── Overall batch (slider 2) ───────────────────────────────────────────
        [Header("Overall Batch Progress")]
        [Tooltip("Slider that fills with the WHOLE batch's combined progress.")]
        [SerializeField] private Slider _overallSlider;

        [Tooltip("Label rendered as: {downloaded}/{total} ({percent}%)")]
        [SerializeField] private Text _overallLabel;

        // ── Completion message ─────────────────────────────────────────────────
        [Header("Completion Message")]
        [Tooltip("Status / completion message shown to the user.")]
        [SerializeField] private Text _messageLabel;

        [SerializeField] private string _idleMessage      = "Preparing downloads…";
        [SerializeField] private string _downloadingText  = "Downloading assets…";
        [SerializeField] private string _successMessage    = "All assets downloaded successfully!";
        [SerializeField] private string _failureMessage    = "Some assets failed to download.";

        [Header("Colors")]
        [SerializeField] private Color _successColor = new Color(0.30f, 0.85f, 0.40f);
        [SerializeField] private Color _failureColor = new Color(0.90f, 0.35f, 0.35f);
        [SerializeField] private Color _neutralColor = Color.white;

        /// <summary>Last overall progress seen — used to make the completion message specific.</summary>
        private OverallDownloadProgress _lastOverall;

        // ──────────────────────────────────────────────────────────────────────
        //  Public API — called by the controller
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Resets both sliders/labels to a clean "about to start" state.</summary>
        public void ResetView()
        {
            SetSlider(_assetSlider, 0f);
            SetSlider(_overallSlider, 0f);

            SetText(_assetLabel, "—");
            SetText(_overallLabel, "0/0 (0%)");
            SetMessage(_idleMessage, _neutralColor);
        }

        /// <summary>Updates slider 1 with the current asset's progress.</summary>
        public void ShowAssetProgress(AssetDownloadProgress p)
        {
            SetSlider(_assetSlider, p.Progress01);
            // e.g.  "hero.png   —————   72%"
            SetText(_assetLabel, $"{p.AssetName}   —————   {p.Percent}%");
            SetMessage(_downloadingText, _neutralColor);
        }

        /// <summary>Updates slider 2 with the overall batch progress.</summary>
        public void ShowOverallProgress(OverallDownloadProgress p)
        {
            _lastOverall = p;
            SetSlider(_overallSlider, p.Progress01);
            // e.g.  "5/44 (11%)"
            SetText(_overallLabel, $"{p.Downloaded}/{p.Total} ({p.Percent}%)");
        }

        /// <summary>Displays the final, specific completion message on screen.</summary>
        public void ShowCompleted(bool success)
        {
            int total = _lastOverall.Total;

            if (success)
            {
                SetSlider(_assetSlider, 1f);
                SetSlider(_overallSlider, 1f);
                SetText(_assetLabel, "Done");
                SetText(_overallLabel, $"{total}/{total} (100%)");
                SetMessage($"✓ {_successMessage}  ({total}/{total})", _successColor);
            }
            else
            {
                SetMessage($"✗ {_failureMessage}  See console for details.", _failureColor);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Private helpers (DRY: null-safe setters)
        // ──────────────────────────────────────────────────────────────────────

        private static void SetSlider(Slider slider, float value01)
        {
            if (slider != null)
                slider.value = Mathf.Clamp01(value01);
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
                label.text = text;
        }

        private void SetMessage(string text, Color color)
        {
            if (_messageLabel == null)
                return;

            _messageLabel.text = text;
            _messageLabel.color = color;
        }
    }
}
