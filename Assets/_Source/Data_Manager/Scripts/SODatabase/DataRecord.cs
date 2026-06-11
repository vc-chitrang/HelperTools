using UnityEngine;

namespace DataManagement
{
    /// <summary>
    /// Base class for all ScriptableObject data records.
    /// Subclass this and add your data fields, then store instances inside an
    /// <see cref="SODatabase{T}"/>.
    ///
    /// <b>Typical usage:</b>
    /// <code>
    /// [CreateAssetMenu(menuName = "MyGame/Item Record")]
    /// public class ItemRecord : DataRecord
    /// {
    ///     public string ItemName;
    ///     public int    Level;
    ///     public float  Power;
    /// }
    /// </code>
    /// </summary>
    public abstract class DataRecord : ScriptableObject
    {
        [Tooltip("Unique identifier used for database lookups. " +
                 "Must be unique within its SODatabase.")]
        public string Id;

        /// <summary>
        /// Called by <see cref="SODatabaseImporter"/> when populating this record from a CSV row.
        /// Override to apply custom parsing logic; the default implementation uses reflection
        /// to match CSV column names to public fields by name (case-insensitive).
        /// </summary>
        /// <param name="row">Dictionary of column name → cell value for this row.</param>
        public virtual void PopulateFromCSVRow(System.Collections.Generic.Dictionary<string, string> row)
        {
            // Default: reflection-based mapping. Subclasses can override for custom logic.
            DataRecordReflectionHelper.PopulateFromRow(this, row);
        }
    }
}
