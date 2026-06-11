#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JsonUtilities.Editor
{
    /// <summary>Editor menu items for the JSON Utilities system.</summary>
    public static class JsonUtilitiesMenu
    {
        private const string Root = "Tools/JSON Utilities/";

        [MenuItem(Root + "Open Demo Scene")]
        private static void OpenDemoScene()
        {
            const string path = "Assets/_Source/Json_Manager/Scenes/JsonUtilitiesScene.unity";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[JsonUtilities] Demo scene not found. Run Build Demo Scene first.");
                return;
            }
            EditorSceneManager.OpenScene(path);
        }

        [MenuItem(Root + "Open README")]
        private static void OpenReadme()
        {
            const string path = "Assets/_Source/Json_Manager/README.md";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[JsonUtilities] README.md not found.");
        }

        [MenuItem(Root + "Select JsonDemoController in Scene")]
        private static void SelectController()
        {
            var ctrl = Object.FindFirstObjectByType<JsonDemoController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[JsonUtilities] No JsonDemoController found in the open scene.");
                return;
            }
            Selection.activeGameObject = ctrl.gameObject;
            EditorGUIUtility.PingObject(ctrl.gameObject);
        }
    }
}
#endif
