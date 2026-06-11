#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModelController.Editor
{
    /// <summary>Editor menu items for the Model Controller system.</summary>
    public static class ModelControllerMenu
    {
        private const string Root = "Tools/Model Controller/";

        [MenuItem(Root + "Select ModelViewController in Scene")]
        private static void Select()
        {
            var ctrl = Object.FindFirstObjectByType<ModelViewController>();
            if (ctrl == null) { Debug.LogWarning("[ModelController] No ModelViewController found in scene."); return; }
            Selection.activeGameObject = ctrl.gameObject;
            EditorGUIUtility.PingObject(ctrl.gameObject);
        }

        [MenuItem(Root + "Focus Camera on Selected Object")]
        private static void FocusOnSelected()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ModelController] Enter Play Mode first.");
                return;
            }
            if (Selection.activeTransform == null)
            {
                Debug.LogWarning("[ModelController] No object selected.");
                return;
            }
            var ctrl = Object.FindFirstObjectByType<ModelViewController>();
            if (ctrl == null) { Debug.LogWarning("[ModelController] No ModelViewController in scene."); return; }
            ctrl.FocusOn(Selection.activeTransform);
            Debug.Log($"[ModelController] Focused on '{Selection.activeTransform.name}'.");
        }

        [MenuItem(Root + "Focus Camera on Selected Object", validate = true)]
        private static bool FocusValidate() => Application.isPlaying && Selection.activeTransform != null;

        [MenuItem(Root + "Open README")]
        private static void OpenReadme()
        {
            const string path = "Assets/_Source/Model_Controller/README.md";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[ModelController] README.md not found.");
        }
    }
}
#endif
