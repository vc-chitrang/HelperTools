using UnityEngine;
using DataManagement;

namespace DataManagement.Demo
{
    /// <summary>Sample SODatabase for the Data Manager demo scene.</summary>
    [CreateAssetMenu(
        fileName = "DemoItemDatabase",
        menuName  = "HelperTools/Demo/Item Database",
        order     = 201)]
    public class DemoItemDatabase : SODatabase<DemoItemRecord> { }
}
