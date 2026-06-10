#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ImageSystem.EditorTools
{
    /// <summary>
    /// One-click editor utilities for the Image &amp; Texture system:
    /// <list type="bullet">
    ///   <item><b>Build AdaptiveImage Prefab</b> — creates a 400×300 drag-and-drop
    ///         frame with <see cref="AdaptiveImage"/> in Fit mode.</item>
    ///   <item><b>Build Image Demo Scene</b> — assembles a scene showing all five
    ///         fill modes side by side with labels and descriptions.</item>
    /// </list>
    /// Editor-only; stripped from player builds.
    /// All runtime behaviour lives in the runtime scripts (separation of concern).
    /// </summary>
    public static class ImageSystemBuilder
    {
        private const string PrefabFolder   = "Assets/_Source/Image_Manager/Prefabs";
        private const string SceneFolder    = "Assets/_Source/Image_Manager/Scenes";
        private const string ScenePath      = SceneFolder + "/ImageManagerScene.unity";
        private const string AdaptivePrefab = PrefabFolder + "/AdaptiveImage.prefab";

        // ════════════════════════════════════════════════════════════════════════
        //  1. AdaptiveImage prefab
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the <b>AdaptiveImage prefab</b>: a 400×300 RectTransform with
        /// <see cref="AdaptiveImage"/> pre-set to <c>Fit</c> mode.
        /// Drop into any UI canvas, then call <c>SetSprite()</c> / <c>SetTexture()</c>.
        /// Menu: <b>Tools ▸ Image Manager ▸ Build AdaptiveImage Prefab</b>
        /// </summary>
        [MenuItem("Tools/Image Manager/Build AdaptiveImage Prefab")]
        public static void BuildAdaptiveImagePrefab()
        {
            EnsureFolder(PrefabFolder);

            var root = new GameObject("AdaptiveImage", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 300f);

            var adaptive = root.AddComponent<AdaptiveImage>();
            var so = new SerializedObject(adaptive);
            so.FindProperty("_fillMode").enumValueIndex = (int)ImageFillMode.Fit;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, AdaptivePrefab, "AdaptiveImage prefab");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. Demo scene
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a demo scene with five AdaptiveImage frames — one per fill mode —
        /// arranged side by side so you can compare them visually.
        /// Assign any Texture in the Inspector or via <c>SetTexture()</c> at runtime.
        /// Menu: <b>Tools ▸ Image Manager ▸ Build Image Demo Scene</b>
        /// </summary>
        [MenuItem("Tools/Image Manager/Build Image Demo Scene")]
        public static void BuildImageDemoScene()
        {
            EnsureFolder(SceneFolder);
            EnsureFolder(PrefabFolder);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Idempotent: remove any canvas built by a previous run.
            const string CanvasName = "ImageDemoCanvas";
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == CanvasName)
                    Object.DestroyImmediate(root);

            EnsureEventSystem(scene);

            // ── Canvas ──────────────────────────────────────────────────────────
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGO, scene);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen dark background
            var bg = NewRect("Background", canvasGO.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 1f);

            // Title
            var titleRT = NewRect("Title", canvasGO.transform);
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot     = new Vector2(0.5f, 1f);
            titleRT.sizeDelta = new Vector2(0f, 90f);
            titleRT.anchoredPosition = Vector2.zero;
            MakeText(titleRT.gameObject, "AdaptiveImage — Fill Mode Demo", 44, bold: true);

            // ── Five fill-mode demo cards ────────────────────────────────────────
            string[] modeLabels = { "Fill", "Fit", "Stretch", "Crop", "PreserveAspect" };
            string[] modeDescs  =
            {
                "Cover the frame\n(overflow visible)",
                "Letterbox inside\n(no cropping)",
                "Distort to fit\nexact size",
                "Cover + clip edges\n(CSS object-fit: cover)",
                "Native pixel size\nno scaling"
            };

            const float FrameW = 290f, FrameH = 210f;
            const float CardW  = FrameW + 24f;
            const float CardH  = FrameH + 96f;
            const float Gap    = 32f;
            float totalW = CardW * modeLabels.Length + Gap * (modeLabels.Length - 1);
            float startX = -totalW * 0.5f + CardW * 0.5f;

            for (int i = 0; i < modeLabels.Length; i++)
            {
                float x = startX + i * (CardW + Gap);

                // Card backing
                var card = NewRect(modeLabels[i] + "_Card", canvasGO.transform);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(CardW, CardH);
                card.anchoredPosition = new Vector2(x, -20f);
                card.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.17f, 1f);

                // AdaptiveImage frame (top part of the card)
                var frameGO = new GameObject("Frame_" + modeLabels[i], typeof(RectTransform));
                frameGO.transform.SetParent(card, false);
                var frameRT = frameGO.GetComponent<RectTransform>();
                frameRT.anchorMin = frameRT.anchorMax = frameRT.pivot = new Vector2(0.5f, 1f);
                frameRT.sizeDelta = new Vector2(FrameW, FrameH);
                frameRT.anchoredPosition = new Vector2(0f, -12f);
                // Subtle tint — visible when no texture is assigned yet
                frameGO.AddComponent<Image>().color = new Color(0.24f, 0.24f, 0.29f, 1f);

                var adaptive = frameGO.AddComponent<AdaptiveImage>();
                var fso = new SerializedObject(adaptive);
                fso.FindProperty("_fillMode").enumValueIndex = i; // matches Fill=0 … PreserveAspect=4
                fso.ApplyModifiedPropertiesWithoutUndo();

                // Mode name
                var nameRT = NewRect("ModeLabel", card);
                nameRT.anchorMin = nameRT.anchorMax = nameRT.pivot = new Vector2(0.5f, 0f);
                nameRT.sizeDelta = new Vector2(CardW - 16f, 36f);
                nameRT.anchoredPosition = new Vector2(0f, 52f);
                MakeText(nameRT.gameObject, modeLabels[i], 26, bold: true);

                // Description
                var descRT = NewRect("Desc", card);
                descRT.anchorMin = descRT.anchorMax = descRT.pivot = new Vector2(0.5f, 0f);
                descRT.sizeDelta = new Vector2(CardW - 16f, 48f);
                descRT.anchoredPosition = new Vector2(0f, 8f);
                MakeText(descRT.gameObject, modeDescs[i], 18, bold: false,
                    color: new Color(0.68f, 0.68f, 0.68f, 1f));
            }

            // Hint bar at the bottom
            var hintRT = NewRect("Hint", canvasGO.transform);
            hintRT.anchorMin = new Vector2(0f, 0f);
            hintRT.anchorMax = new Vector2(1f, 0f);
            hintRT.pivot     = new Vector2(0.5f, 0f);
            hintRT.sizeDelta = new Vector2(0f, 56f);
            hintRT.anchoredPosition = Vector2.zero;
            MakeText(hintRT.gameObject,
                "Drop  AdaptiveImage.prefab  into any Canvas  →  assign Sprite/Texture  →  pick a Fill Mode.",
                22, bold: false, color: new Color(0.50f, 0.50f, 0.50f, 1f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ImageSystemBuilder] Demo scene built → {ScenePath}");
            Selection.activeGameObject = canvasGO;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void EnsureEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(es, scene);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

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
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Text MakeText(GameObject go, string content, int size, bool bold,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null)
        {
            var t = go.AddComponent<Text>();
            t.text       = content;
            t.fontSize   = size;
            t.fontStyle  = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment  = anchor;
            t.color      = color ?? Color.white;
            t.supportRichText = false;
            return t;
        }

        private static void SavePrefab(GameObject root, string path, string label)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab != null)
            {
                Debug.Log($"[ImageSystemBuilder] {label} saved → {path}");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError($"[ImageSystemBuilder] Failed to save {label} at '{path}'.");
            }
        }
    }
}
#endif
