#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PoolSystem;

namespace PoolSystem.Editor
{
    /// <summary>Editor menu items for the Pool Manager system.</summary>
    public static class PoolManagerMenu
    {
        private const string MenuRoot = "Tools/Pool Manager/";

        [MenuItem(MenuRoot + "Print Pool Stats")]
        private static void PrintPoolStats()
        {
            var pm = Object.FindFirstObjectByType<PoolManager>();
            if (pm == null) { Debug.Log("[PoolManager] No PoolManager found in scene."); return; }

            var pools = pm.GetComponentsInChildren<GameObjectPool>(includeInactive: true);
            if (pools.Length == 0) { Debug.Log("[PoolManager] No pools registered."); return; }

            Debug.Log($"[PoolManager] {pools.Length} pool(s):");
            foreach (var p in pools)
                Debug.Log($"  Pool '{p.name}'  Active={p.CountActive}  Inactive={p.CountInactive}  Total={p.CountAll}");
        }

        [MenuItem(MenuRoot + "Despawn All (Play Mode)")]
        private static void DespawnAll()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[PoolManager] Enter Play Mode first."); return; }
            var pm = PoolManager.Instance;
            if (pm == null) { Debug.LogWarning("[PoolManager] No PoolManager instance."); return; }
            pm.DespawnAll();
            Debug.Log("[PoolManager] All pools despawned.");
        }

        [MenuItem(MenuRoot + "Despawn All (Play Mode)", validate = true)]
        private static bool DespawnAllValidate() => Application.isPlaying;

        [MenuItem(MenuRoot + "Select PoolManager in Hierarchy")]
        private static void SelectPoolManager()
        {
            var pm = Object.FindFirstObjectByType<PoolManager>();
            if (pm == null) { Debug.LogWarning("[PoolManager] No PoolManager found in scene."); return; }
            Selection.activeGameObject = pm.gameObject;
            EditorGUIUtility.PingObject(pm.gameObject);
        }

        [MenuItem(MenuRoot + "Open README")]
        private static void OpenReadme()
        {
            var guids = AssetDatabase.FindAssets("t:TextAsset Pool_Manager_README");
            if (guids.Length == 0)
            {
                // fall back to path search
                var path = "Assets/_Source/Pool_Manager/README.md";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) { EditorUtility.OpenWithDefaultApp(path); return; }
                Debug.LogWarning("[PoolManager] README.md not found.");
                return;
            }
            var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            EditorUtility.OpenWithDefaultApp(assetPath);
        }
    }
}
#endif
