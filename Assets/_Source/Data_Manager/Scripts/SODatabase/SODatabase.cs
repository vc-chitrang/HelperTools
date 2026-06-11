using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataManagement
{
    /// <summary>
    /// Abstract generic ScriptableObject that acts as a designer-friendly lookup table
    /// of <see cref="DataRecord"/> instances.
    ///
    /// <b>Usage — create your own database type:</b>
    /// <code>
    /// [CreateAssetMenu(menuName = "MyGame/Item Database")]
    /// public class ItemDatabase : SODatabase&lt;ItemRecord&gt; { }
    /// </code>
    ///
    /// Then create a <c>.asset</c> file via the menu and populate it in the Inspector
    /// or by running the CSV importer (<c>Tools → Data Manager → Import CSV to SO Database</c>).
    ///
    /// <b>Runtime query example:</b>
    /// <code>
    /// ItemRecord sword = itemDb.FindById("sword_01");
    /// var weapons = itemDb.FindAll(r => r.Type == "Weapon");
    /// </code>
    /// </summary>
    public abstract class SODatabase<T> : ScriptableObject where T : DataRecord
    {
        [SerializeField] private List<T> _records = new List<T>();

        // ── Read ───────────────────────────────────────────────────────────────

        /// <summary>All records in this database (read-only).</summary>
        public IReadOnlyList<T> Records => _records;

        /// <summary>Number of records.</summary>
        public int Count => _records.Count;

        /// <summary>Returns the record with the given Id, or null if not found.</summary>
        public T FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var r in _records)
                if (r != null && r.Id == id) return r;
            return null;
        }

        /// <summary>Returns the first record that satisfies the predicate, or null.</summary>
        public T Find(Predicate<T> match)
        {
            if (match == null) return null;
            foreach (var r in _records)
                if (r != null && match(r)) return r;
            return null;
        }

        /// <summary>Returns all records that satisfy the predicate.</summary>
        public List<T> FindAll(Predicate<T> match)
        {
            var result = new List<T>();
            if (match == null) return result;
            foreach (var r in _records)
                if (r != null && match(r)) result.Add(r);
            return result;
        }

        /// <summary>Returns true if any record has the given Id.</summary>
        public bool ContainsId(string id)
        {
            foreach (var r in _records)
                if (r != null && r.Id == id) return true;
            return false;
        }

        /// <summary>Returns the record at the given index, or null if out of range.</summary>
        public T GetAt(int index) =>
            index >= 0 && index < _records.Count ? _records[index] : null;

        // ── Runtime mutation (use editor importer for batch changes) ───────────

        /// <summary>Adds a record. Does nothing if the record is null or already present.</summary>
        public void Add(T record)
        {
            if (record != null && !_records.Contains(record))
                _records.Add(record);
        }

        /// <summary>Removes the record with the given Id. Returns true if removed.</summary>
        public bool RemoveById(string id)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
                if (_records[i] != null && _records[i].Id == id)
                { _records.RemoveAt(i); return true; }
            return false;
        }

        /// <summary>Removes a specific record instance. Returns true if removed.</summary>
        public bool Remove(T record) => _records.Remove(record);

        /// <summary>Clears all records from this database.</summary>
        public void Clear() => _records.Clear();

        /// <summary>Sorts the records in-place using the given comparison delegate.</summary>
        public void Sort(Comparison<T> comparison) => _records.Sort(comparison);

        // ── Enumeration ────────────────────────────────────────────────────────

        /// <summary>Iterates all non-null records.</summary>
        public IEnumerable<T> Enumerate()
        {
            foreach (var r in _records)
                if (r != null) yield return r;
        }

        // ── Validation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Logs a warning for any null entries or records with duplicate Ids.
        /// Call from an editor validation button or on Start() during development.
        /// </summary>
        public void Validate()
        {
            var seenIds = new HashSet<string>();
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i] == null)
                {
                    Debug.LogWarning($"[SODatabase<{typeof(T).Name}>] Null entry at index {i}", this);
                    continue;
                }
                if (!seenIds.Add(_records[i].Id))
                    Debug.LogWarning(
                        $"[SODatabase<{typeof(T).Name}>] Duplicate Id '{_records[i].Id}' at index {i}", this);
            }
        }
    }
}
