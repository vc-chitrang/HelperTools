using UnityEngine;
using DataManagement;

namespace DataManagement.Demo
{
    /// <summary>Sample DataRecord for the Data Manager demo scene.</summary>
    [CreateAssetMenu(
        fileName = "DemoItemRecord",
        menuName  = "HelperTools/Demo/Item Record",
        order     = 200)]
    public class DemoItemRecord : DataRecord
    {
        public string ItemName;
        public string ItemType;
        public int    Level;
        public float  Power;
        public string Description;
    }
}
