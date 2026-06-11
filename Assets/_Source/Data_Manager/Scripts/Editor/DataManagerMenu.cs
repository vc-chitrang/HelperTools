#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DataManagement.Editor
{
    internal static class DataManagerMenu
    {
        private const string ScenePath  = "Assets/_Source/Data_Manager/Scenes/DataManagerScene.unity";
        private const string ReadmePath = "Assets/_Source/Data_Manager/README.md";

        [MenuItem("Tools/Data Manager/Open Demo Scene")]
        private static void OpenScene()
        {
            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(
                        Application.dataPath.Replace("Assets", ""), ScenePath)))
            {
                EditorUtility.DisplayDialog("Data Manager",
                    "Demo scene not found.\n\nRun:  Tools → Data Manager → Build Demo Scene  first.",
                    "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Data Manager/Open README")]
        private static void OpenReadme()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            if (asset != null) AssetDatabase.OpenAsset(asset);
            else EditorUtility.DisplayDialog("Data Manager", "README.md not found.", "OK");
        }

        [MenuItem("Tools/Data Manager/Build Demo Scene")]
        private static void BuildScene()
        {
            EditorApplication.ExecuteMenuItem("Tools/Data Manager/Build Demo Scene (Execute)");
        }

        [MenuItem("Tools/Data Manager/Import CSV to SO Database")]
        private static void ImportCSV()
        {
            EditorApplication.ExecuteMenuItem(
                "Tools/Data Manager/Import CSV to SO Database (Window)");
        }
    }
}
#endif
