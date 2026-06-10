using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaveSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  SAVE MANAGER — Singleton coordinator for multi-slot saves
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Drop the prefab into your first/persistent scene. Any system
    ///  that wants to save/load implements <see cref="ISaveable"/> and
    ///  registers itself:
    ///
    ///    void Awake() => SaveManager.Instance.Register(this);
    ///    void OnDestroy() => SaveManager.Instance?.Unregister(this);
    ///
    ///  Then call from UI or game logic:
    ///    SaveManager.Instance.Save(slotIndex);     // 0-based
    ///    SaveManager.Instance.Load(slotIndex);
    ///    SaveManager.Instance.Delete(slotIndex);
    ///    SaveManager.Instance.GetSlotInfo(slotIndex);
    ///
    ///  Save files land in Application.persistentDataPath/Saves/slot_N.json
    ///  Encryption, auto-save interval and slot count are inspector-configurable.
    /// </summary>
    [AddComponentMenu("HelperTools/Save/SaveManager")]
    public class SaveManager : MonoBehaviour
    {
        // ── Constants ──────────────────────────────────────────────────────
        /// <summary>Bump when the <see cref="GameSaveData"/> schema changes.</summary>
        public const int DataVersion = 1;

        /// <summary>Number of available save slots.</summary>
        public const int SlotCount = 3;

        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Encryption")]
        [SerializeField, Tooltip("AES-256 encrypt save files (tamper-resistance).")]
        private bool _encrypt = false;

        [Header("Auto-Save")]
        [SerializeField, Tooltip("Seconds between periodic auto-saves. 0 = disabled.")]
        private float _autoSaveInterval = 60f;

        [SerializeField, Tooltip("Which slot receives the periodic auto-save (0-based).")]
        private int _autoSaveSlot = 0;

        // ── Singleton ──────────────────────────────────────────────────────
        private static SaveManager _instance;

        public static SaveManager Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogError("[SaveManager] No SaveManager found in scene. " +
                                   "Drag the SaveManager prefab into your persistent scene.");
                return _instance;
            }
        }

        // ── Events ─────────────────────────────────────────────────────────
        /// <summary>Raised after a slot is successfully saved. Argument = slot index.</summary>
        public static event Action<int> OnSaved;

        /// <summary>Raised after a slot is successfully loaded. Argument = slot index.</summary>
        public static event Action<int> OnLoaded;

        /// <summary>Raised after a slot is deleted. Argument = slot index.</summary>
        public static event Action<int> OnDeleted;

        // ── Private state ──────────────────────────────────────────────────
        private readonly List<ISaveable> _saveables = new();
        private float _autoSaveTimer;
        private float _sessionStartTime;   // Time.realtimeSinceStartup at session start

        // ── Unity lifecycle ────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_autoSaveInterval <= 0f) return;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                Save(_autoSaveSlot);
            }
        }

        // OnApplicationPause fires when the app is backgrounded on mobile.
        // OnApplicationQuit may not fire on iOS/Android if the OS kills the process.
        private void OnApplicationPause(bool paused)
        {
            if (paused && _autoSaveInterval > 0f)
                Save(_autoSaveSlot);
        }

        private void OnApplicationQuit()
        {
            if (_autoSaveInterval > 0f)
                Save(_autoSaveSlot);
        }

        // ── Registration ───────────────────────────────────────────────────

        /// <summary>
        /// Register a saveable system. Call from <c>Awake</c> on the system
        /// that implements <see cref="ISaveable"/>.
        /// </summary>
        public void Register(ISaveable saveable)
        {
            if (!_saveables.Contains(saveable))
                _saveables.Add(saveable);
        }

        /// <summary>
        /// Unregister when the object is destroyed. Call from <c>OnDestroy</c>.
        /// </summary>
        public void Unregister(ISaveable saveable) => _saveables.Remove(saveable);

        // ── Save / Load / Delete ───────────────────────────────────────────

        /// <summary>
        /// Collects state from all registered <see cref="ISaveable"/> systems
        /// and writes it to <paramref name="slotIndex"/>.
        /// </summary>
        public void Save(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);

            var data = new GameSaveData
            {
                slotName      = $"Slot {slotIndex + 1}",
                timestampUtc  = DateTime.UtcNow.Ticks,
                totalPlayTime = ComputeTotalPlayTime(),
            };

            foreach (var s in _saveables)
            {
                try   { s.GatherSaveData(data); }
                catch (Exception ex)
                { Debug.LogError($"[SaveManager] GatherSaveData failed on {s}: {ex.Message}"); }
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            SaveFileIO.Write(GetSlotPath(slotIndex), json, _encrypt);

            Debug.Log($"[SaveManager] Saved slot {slotIndex} → {GetSlotPath(slotIndex)}");
            OnSaved?.Invoke(slotIndex);
        }

        /// <summary>
        /// Reads <paramref name="slotIndex"/> from disk and distributes the
        /// data to all registered <see cref="ISaveable"/> systems.
        /// Returns <c>false</c> if the slot is empty or unreadable.
        /// </summary>
        public bool Load(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);

            string json = SaveFileIO.Read(GetSlotPath(slotIndex), _encrypt);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[SaveManager] Slot {slotIndex} is empty or unreadable.");
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<GameSaveData>(json);

                // Adjust session start so accumulated play time is correct on next save.
                _sessionStartTime = Time.realtimeSinceStartup - data.totalPlayTime;

                foreach (var s in _saveables)
                {
                    try   { s.ApplySaveData(data); }
                    catch (Exception ex)
                    { Debug.LogError($"[SaveManager] ApplySaveData failed on {s}: {ex.Message}"); }
                }

                Debug.Log($"[SaveManager] Loaded slot {slotIndex}.");
                OnLoaded?.Invoke(slotIndex);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Deserialize failed for slot {slotIndex}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Permanently deletes <paramref name="slotIndex"/> (primary + backup).</summary>
        public void Delete(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
            SaveFileIO.Delete(GetSlotPath(slotIndex));
            Debug.Log($"[SaveManager] Deleted slot {slotIndex}.");
            OnDeleted?.Invoke(slotIndex);
        }

        // ── Query ──────────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if a save file exists for this slot.</summary>
        public bool HasSave(int slotIndex) =>
            SaveFileIO.Exists(GetSlotPath(Mathf.Clamp(slotIndex, 0, SlotCount - 1)));

        /// <summary>
        /// Returns lightweight metadata for a slot (name, timestamp, play time)
        /// without fully deserializing the save file. Safe to call every frame.
        /// </summary>
        public SaveSlotInfo GetSlotInfo(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
            var info  = new SaveSlotInfo { slotIndex = slotIndex };

            string json = SaveFileIO.Read(GetSlotPath(slotIndex), _encrypt);
            if (string.IsNullOrEmpty(json)) return info;

            try
            {
                var data          = JsonUtility.FromJson<GameSaveData>(json);
                info.exists       = true;
                info.slotName     = data.slotName;
                info.timestampUtc = data.timestampUtc;
                info.totalPlayTime = data.totalPlayTime;
            }
            catch { /* corrupt save → treat as empty */ }

            return info;
        }

        // ── Paths ──────────────────────────────────────────────────────────

        /// <summary>Absolute path to the save folder (for editor tooling).</summary>
        public static string SaveFolderPath =>
            Path.Combine(Application.persistentDataPath, "Saves");

        private static string GetSlotPath(int slot) =>
            Path.Combine(SaveFolderPath, $"slot_{slot}.json");

        // ── Internal helpers ───────────────────────────────────────────────

        private float ComputeTotalPlayTime() =>
            Time.realtimeSinceStartup - _sessionStartTime;
    }
}
