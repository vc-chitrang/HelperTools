using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  SAVE SLOT UI — one row / card per save slot
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Wire in the Inspector (or let the builder do it):
    ///    • _slotIndex        — which slot this card represents (0-based)
    ///    • _slotLabel        — shows slot name or "Slot N — Empty"
    ///    • _timestampLabel   — formatted save date/time
    ///    • _playTimeLabel    — formatted play time ("2h 05m")
    ///    • _saveButton       — calls SaveManager.Save
    ///    • _loadButton       — calls SaveManager.Load  (disabled when empty)
    ///    • _deleteButton     — calls SaveManager.Delete (disabled when empty)
    ///
    ///  Refreshes automatically via SaveManager events (OnSaved/OnLoaded/OnDeleted).
    /// </summary>
    [AddComponentMenu("HelperTools/Save/SaveSlotUI")]
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("Slot")]
        [SerializeField] private int _slotIndex;

        [Header("UI References")]
        [SerializeField] private TMP_Text _slotLabel;
        [SerializeField] private TMP_Text _timestampLabel;
        [SerializeField] private TMP_Text _playTimeLabel;
        [SerializeField] private Button   _saveButton;
        [SerializeField] private Button   _loadButton;
        [SerializeField] private Button   _deleteButton;

        // Optional: parent GameObjects toggled between empty / filled states
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _filledState;

        // ── Lifecycle ──────────────────────────────────────────────────────
        private void OnEnable()
        {
            SaveManager.OnSaved   += HandleSaveChanged;
            SaveManager.OnLoaded  += HandleSaveChanged;
            SaveManager.OnDeleted += HandleSaveChanged;
            Refresh();
        }

        private void OnDisable()
        {
            SaveManager.OnSaved   -= HandleSaveChanged;
            SaveManager.OnLoaded  -= HandleSaveChanged;
            SaveManager.OnDeleted -= HandleSaveChanged;
        }

        private void Start()
        {
            if (_saveButton   != null) _saveButton.onClick.AddListener(OnSaveClicked);
            if (_loadButton   != null) _loadButton.onClick.AddListener(OnLoadClicked);
            if (_deleteButton != null) _deleteButton.onClick.AddListener(OnDeleteClicked);
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Re-reads slot metadata and updates all labels and button states.</summary>
        public void Refresh()
        {
            if (SaveManager.Instance == null) return;

            var info    = SaveManager.Instance.GetSlotInfo(_slotIndex);
            bool filled = info.exists;

            if (_emptyState  != null) _emptyState.SetActive(!filled);
            if (_filledState != null) _filledState.SetActive(filled);

            if (_slotLabel != null)
                _slotLabel.text = filled ? info.slotName : $"Slot {_slotIndex + 1} — Empty";

            if (_timestampLabel != null)
                _timestampLabel.text = filled
                    ? info.Timestamp.ToString("yyyy-MM-dd  HH:mm")
                    : string.Empty;

            if (_playTimeLabel != null)
                _playTimeLabel.text = filled ? info.FormattedPlayTime : string.Empty;

            if (_loadButton   != null) _loadButton.interactable   = filled;
            if (_deleteButton != null) _deleteButton.interactable = filled;
        }

        // ── Button callbacks ───────────────────────────────────────────────
        private void HandleSaveChanged(int slot) { if (slot == _slotIndex) Refresh(); }
        private void OnSaveClicked()   => SaveManager.Instance?.Save(_slotIndex);
        private void OnLoadClicked()   => SaveManager.Instance?.Load(_slotIndex);
        private void OnDeleteClicked() => SaveManager.Instance?.Delete(_slotIndex);
    }
}
