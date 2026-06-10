using System;
using UnityEngine;

namespace SaveSystem
{
    // ══════════════════════════════════════════════════════════════════════════
    //  SAVE DATA MODEL
    //  Plain C# DTOs (not MonoBehaviours) + ISaveable interface.
    //  Extend PlayerSaveData with your own fields; bump DataVersion when the
    //  format changes so future migration logic can detect old saves.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Implement on any MonoBehaviour that wants to participate in save/load.
    /// Call <c>SaveManager.Instance.Register(this)</c> in Awake and
    /// <c>Unregister(this)</c> in OnDestroy.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Write your state into the DTO before it is serialized to disk.</summary>
        void GatherSaveData(GameSaveData data);

        /// <summary>Read your state back from the DTO after it is deserialized from disk.</summary>
        void ApplySaveData(GameSaveData data);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Root save DTO
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root save file DTO. Must be a plain <c>[Serializable]</c> C# class.
    /// Add a <c>version</c> bump whenever the schema changes.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        // ── Metadata ──────────────────────────────────────────────────────────
        public int    version      = SaveManager.DataVersion;
        public string slotName     = "New Game";
        public long   timestampUtc;       // DateTime.UtcNow.Ticks at save time
        public float  totalPlayTime;      // accumulated seconds

        // ── Game state — extend as needed ─────────────────────────────────────
        public PlayerSaveData player = new PlayerSaveData();
    }

    /// <summary>
    /// Example player save block. Replace / extend with your own fields.
    /// Use primitives and arrays — JsonUtility does not serialise dictionaries.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public float posX, posY, posZ;   // Vector3 split for JSON compat
        public int   health  = 100;
        public int   level   = 1;
        public int   score   = 0;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

        public void SetPosition(Vector3 v)
        {
            posX = v.x;
            posY = v.y;
            posZ = v.z;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Slot metadata (lightweight read — no full deserialization needed)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight slot summary returned by <see cref="SaveManager.GetSlotInfo"/>.
    /// Derived from the full save but cheap to read for slot-selection UIs.
    /// </summary>
    [Serializable]
    public class SaveSlotInfo
    {
        public bool   exists;
        public int    slotIndex;
        public string slotName;
        public long   timestampUtc;
        public float  totalPlayTime;

        /// <summary>Save timestamp converted to local time. Returns <c>DateTime.MinValue</c> if slot is empty.</summary>
        public DateTime Timestamp => timestampUtc > 0
            ? new DateTime(timestampUtc, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;

        /// <summary>Human-readable play time, e.g. "2h 05m" or "45m".</summary>
        public string FormattedPlayTime
        {
            get
            {
                int h = (int)(totalPlayTime / 3600f);
                int m = (int)(totalPlayTime % 3600f / 60f);
                return h > 0 ? $"{h}h {m:D2}m" : $"{m}m";
            }
        }
    }
}
