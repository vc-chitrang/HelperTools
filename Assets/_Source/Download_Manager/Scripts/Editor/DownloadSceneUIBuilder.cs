#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DownloadManager.EditorTools
{
    /// <summary>
    /// One-click editor utility that builds the 1920x1080 download UI (black
    /// background + two progress sliders + labels + completion message) into the
    /// active scene and wires it to <see cref="MediaManager"/> and
    /// <see cref="DownloadSceneController"/>.
    ///
    /// <para>
    /// This lives in an <c>Editor</c> folder so it is stripped from player builds.
    /// It only assembles scene objects; all runtime behaviour stays in the runtime
    /// scripts (separation of concern).
    /// </para>
    ///
    /// <para>Run via menu: <b>Tools ▸ Download Manager ▸ Build Download UI</b>.</para>
    /// </summary>
    public static class DownloadSceneUIBuilder
    {
        private const string CanvasName = "DownloadCanvas";
        private const string ScenePath = "Assets/_Source/Download_Manager/Scenes/DownloadScene.unity";
        private const string JsonAbsolutePathForTesting =
            @"C:\Users\Yash Halari\AppData\LocalLow\ViitorCloud\dual-gallery-interactive-display\SHM_AppData.json";

        [MenuItem("Tools/Download Manager/Build Download UI")]
        public static void BuildDownloadUI()
        {
            // Always target DownloadScene by path so the build can never be written
            // into a different (or phantom) active scene and silently lost.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return; // user cancelled the save prompt
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // Remove any previous build so the action is idempotent.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == CanvasName)
                    Object.DestroyImmediate(root);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── Canvas (1920x1080 reference) ───────────────────────────────────
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGO, scene);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            Transform canvasTr = canvasGO.transform;

            // ── Black full-screen background ───────────────────────────────────
            RectTransform bgRt = NewRect("Background", canvasTr);
            Stretch(bgRt);
            bgRt.gameObject.AddComponent<Image>().color = Color.black;

            // ── Build widgets ──────────────────────────────────────────────────
            MakeText(canvasTr, font, "Title", 320f, 1400f, 100f, 54).text = "Asset Downloader";
            Text assetLabel = MakeText(canvasTr, font, "AssetLabel", 150f, 1500f, 60f, 34);
            assetLabel.text = "-";
            Slider assetSlider = MakeSlider(canvasTr, "AssetSlider", 90f, 1200f, 40f);
            Text overallLabel = MakeText(canvasTr, font, "OverallLabel", -40f, 1500f, 60f, 34);
            overallLabel.text = "0/0 (0%)";
            Slider overallSlider = MakeSlider(canvasTr, "OverallSlider", -100f, 1200f, 40f);
            Text message = MakeText(canvasTr, font, "Message", -260f, 1600f, 80f, 40);
            message.text = "Preparing downloads...";

            // ── View (presentation layer) ──────────────────────────────────────
            var view = canvasGO.AddComponent<DownloadProgressView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_assetSlider").objectReferenceValue = assetSlider;
            viewSo.FindProperty("_assetLabel").objectReferenceValue = assetLabel;
            viewSo.FindProperty("_overallSlider").objectReferenceValue = overallSlider;
            viewSo.FindProperty("_overallLabel").objectReferenceValue = overallLabel;
            viewSo.FindProperty("_messageLabel").objectReferenceValue = message;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Controller (wiring layer) ──────────────────────────────────────
            var mediaManager = Object.FindAnyObjectByType<MediaManager>();
            GameObject ctrlGO = mediaManager != null ? mediaManager.gameObject : new GameObject("DownloadController");
            if (mediaManager == null)
                SceneManager.MoveGameObjectToScene(ctrlGO, scene);

            var controller = ctrlGO.GetComponent<DownloadSceneController>();
            if (controller == null)
                controller = ctrlGO.AddComponent<DownloadSceneController>();

            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("_mediaManager").objectReferenceValue = mediaManager;
            ctrlSo.FindProperty("_view").objectReferenceValue = view;
            ctrlSo.FindProperty("_absoluteJsonPathOverride").stringValue = JsonAbsolutePathForTesting;
            ctrlSo.FindProperty("_downloadOnStart").boolValue = true;
            ctrlSo.FindProperty("_maxAssets").intValue = 0;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[DownloadSceneUIBuilder] Built download UI in scene '{scene.name}'. " +
                      $"MediaManager wired: {mediaManager != null}.");
            Selection.activeGameObject = canvasGO;
        }

        // ── Reusable prefab ─────────────────────────────────────────────────────

        private const string PrefabFolder = "Assets/_Source/Download_Manager/Prefabs";
        private const string PrefabPath = PrefabFolder + "/DownloadManager.prefab";

        /// <summary>
        /// Builds a fully self-contained, drag-and-drop downloader prefab: a single
        /// root GameObject carrying <see cref="MediaManager"/> +
        /// <see cref="DownloadSceneController"/>, with the 1920x1080 Canvas UI nested
        /// underneath and every reference pre-wired. Drop it into any scene/project
        /// and it works with zero manual setup.
        /// <para>Run via menu: <b>Tools ▸ Download Manager ▸ Build Reusable Prefab</b>.</para>
        /// </summary>
        [MenuItem("Tools/Download Manager/Build Reusable Prefab")]
        public static void BuildReusablePrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Source/Download_Manager", "Prefabs");

            GameObject root = BuildConsolidatedRoot();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root); // remove the temp instance from the scene
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DownloadSceneUIBuilder] Reusable prefab {(success ? "saved" : "FAILED")}: {PrefabPath}");
            if (success)
                Selection.activeObject = prefab;
        }

        /// <summary>
        /// Assembles the single-root downloader hierarchy used by the prefab.
        /// Generic, project-agnostic defaults are applied (no machine-specific paths).
        /// </summary>
        private static GameObject BuildConsolidatedRoot()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("DownloadManager");

            // ── Canvas (1920x1080) nested under the root ───────────────────────
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
            canvasGO.transform.SetParent(root.transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            Transform canvasTr = canvasGO.transform;

            RectTransform bgRt = NewRect("Background", canvasTr);
            Stretch(bgRt);
            bgRt.gameObject.AddComponent<Image>().color = Color.black;

            MakeText(canvasTr, font, "Title", 320f, 1400f, 100f, 54).text = "Asset Downloader";
            Text assetLabel = MakeText(canvasTr, font, "AssetLabel", 150f, 1500f, 60f, 34);
            assetLabel.text = "-";
            Slider assetSlider = MakeSlider(canvasTr, "AssetSlider", 90f, 1200f, 40f);
            Text overallLabel = MakeText(canvasTr, font, "OverallLabel", -40f, 1500f, 60f, 34);
            overallLabel.text = "0/0 (0%)";
            Slider overallSlider = MakeSlider(canvasTr, "OverallSlider", -100f, 1200f, 40f);
            Text message = MakeText(canvasTr, font, "Message", -260f, 1600f, 80f, 40);
            message.text = "Preparing downloads...";

            // ── View (presentation) on the Canvas ──────────────────────────────
            var view = canvasGO.AddComponent<DownloadProgressView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_assetSlider").objectReferenceValue = assetSlider;
            viewSo.FindProperty("_assetLabel").objectReferenceValue = assetLabel;
            viewSo.FindProperty("_overallSlider").objectReferenceValue = overallSlider;
            viewSo.FindProperty("_overallLabel").objectReferenceValue = overallLabel;
            viewSo.FindProperty("_messageLabel").objectReferenceValue = message;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Logic + wiring on the root ─────────────────────────────────────
            var manager = root.AddComponent<MediaManager>();
            var controller = root.AddComponent<DownloadSceneController>();
            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("_mediaManager").objectReferenceValue = manager;
            ctrlSo.FindProperty("_view").objectReferenceValue = view;
            ctrlSo.FindProperty("_absoluteJsonPathOverride").stringValue = string.Empty; // generic
            ctrlSo.FindProperty("_jsonFileName").stringValue = "SHM_AppData.json";
            ctrlSo.FindProperty("_downloadOnStart").boolValue = true;
            ctrlSo.FindProperty("_maxAssets").intValue = 0;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ── Helpers (DRY) ──────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void CenterBox(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0, y);
        }

        private static Text MakeText(Transform parent, Font font, string name, float y, float w, float h, int size)
        {
            RectTransform rt = NewRect(name, parent);
            CenterBox(rt, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Slider MakeSlider(Transform parent, string name, float y, float w, float h)
        {
            RectTransform rt = NewRect(name, parent);
            CenterBox(rt, y, w, h);
            var slider = rt.gameObject.AddComponent<Slider>();

            RectTransform sbgRt = NewRect("Background", rt);
            Stretch(sbgRt);
            var sbg = sbgRt.gameObject.AddComponent<Image>();
            sbg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            RectTransform faRt = NewRect("Fill Area", rt);
            Stretch(faRt);

            RectTransform fillRt = NewRect("Fill", faRt);
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.color = new Color(0.30f, 0.65f, 1f, 1f);

            slider.fillRect = fillRt;
            slider.handleRect = null;
            slider.targetGraphic = sbg;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.wholeNumbers = false;
            slider.value = 0;
            return slider;
        }
    }
}
#endif
