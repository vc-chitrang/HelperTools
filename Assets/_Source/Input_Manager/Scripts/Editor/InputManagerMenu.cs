#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InputFramework.Editor
{
    internal static class InputManagerMenu
    {
        private const string ScenePath  = "Assets/_Source/Input_Manager/Scenes/InputManagerScene.unity";
        private const string ReadmePath = "Assets/_Source/Input_Manager/README.md";

        [MenuItem("Tools/Input Manager/Open Demo Scene")]
        private static void OpenDemoScene()
        {
            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(Application.dataPath.Replace("Assets",""), ScenePath)))
            {
                EditorUtility.DisplayDialog(
                    "Input Manager",
                    "Demo scene not found.\n\nRun:  Tools → Input Manager → Build Demo Scene  first.",
                    "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Input Manager/Open README")]
        private static void OpenReadme()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            if (asset != null) AssetDatabase.OpenAsset(asset);
            else EditorUtility.DisplayDialog("Input Manager", "README.md not found.", "OK");
        }

        [MenuItem("Tools/Input Manager/Select InputManager in Scene")]
        private static void SelectInputManager()
        {
            var go = Object.FindFirstObjectByType<InputManager>();
            if (go != null)
            {
                Selection.activeGameObject = go.gameObject;
                EditorGUIUtility.PingObject(go.gameObject);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Input Manager",
                    "No InputManager found in the current scene.\n\n" +
                    "Drag InputManager.prefab from  Assets/_Source/Input_Manager/Prefabs/  into the Hierarchy.",
                    "OK");
            }
        }

        [MenuItem("Tools/Input Manager/Build Demo Scene")]
        private static void BuildDemoScene()
        {
            EditorApplication.ExecuteMenuItem("Tools/Input Manager/Build Demo Scene (Execute)");
        }
    }
}
#endif
