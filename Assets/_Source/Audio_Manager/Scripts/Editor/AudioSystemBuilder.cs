#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AudioSystem.EditorTools
{
    /// <summary>
    /// One-click editor utilities for the Audio Framework:
    /// <list type="bullet">
    ///   <item><b>Build Audio Mixer</b> — generates an Audio Mixer asset with
    ///         Master ▸ BGM / SFX groups and exposed volume parameters.</item>
    ///   <item><b>Build Audio Scene</b> — assembles the volume UI panel + AudioManager
    ///         into <c>AudioManagerScene</c> and wires everything to the mixer.</item>
    /// </list>
    /// Editor-only (stripped from player builds); it only assembles assets/scene
    /// objects — all runtime behaviour stays in the runtime scripts.
    /// </summary>
    public static class AudioSystemBuilder
    {
        private const string AudioFolder = "Assets/_Source/Audio_Manager/Audio";
        private const string MixerPath = AudioFolder + "/GameAudioMixer.mixer";
        private const string ScenePath = "Assets/_Source/Audio_Manager/Scenes/AudioManagerScene.unity";
        private const string CanvasName = "AudioCanvas";
        private const string ManagerName = "AudioManager";

        // ════════════════════════════════════════════════════════════════════════
        //  1. Audio Mixer
        // ════════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/Audio Manager/Build Audio Mixer")]
        public static AudioMixer BuildAudioMixer()
        {
            EnsureFolder(AudioFolder);

            // Rebuild cleanly so the action is idempotent.
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
                AssetDatabase.DeleteAsset(MixerPath);

            Assembly editorAsm = typeof(AssetDatabase).Assembly;
            Type ctrlType  = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
            Type groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            Type expType   = editorAsm.GetType("UnityEditor.Audio.ExposedAudioParameter");

            if (ctrlType == null || groupType == null || expType == null)
            {
                Debug.LogError("[AudioSystemBuilder] Could not resolve internal mixer types; aborting.");
                return null;
            }

            // AudioMixerController.CreateMixerControllerAtPath(path)
            var createMethod = ctrlType.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.Static);
            object mixer = createMethod.Invoke(null, new object[] { MixerPath });

            object master = ctrlType.GetProperty("masterGroup").GetValue(mixer);

            object bgm = CreateGroup(ctrlType, mixer, master, "BGM");
            object sfx = CreateGroup(ctrlType, mixer, master, "SFX");

            ExposeVolume(ctrlType, groupType, expType, mixer, master, AudioMixerParameters.Master);
            ExposeVolume(ctrlType, groupType, expType, mixer, bgm,    AudioMixerParameters.BGM);
            ExposeVolume(ctrlType, groupType, expType, mixer, sfx,    AudioMixerParameters.SFX);

            EditorUtility.SetDirty((UnityEngine.Object)mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Debug.Log($"[AudioSystemBuilder] Audio Mixer built at {MixerPath} " +
                      $"(exposed: Master/BGM/SFX volume).");
            return asset;
        }

        private static object CreateGroup(Type ctrlType, object mixer, object parent, string name)
        {
            object group = ctrlType.GetMethod("CreateNewGroup").Invoke(mixer, new object[] { name, false });

            // AddChildToParent(child, parent) — adapt to the method's parameter count.
            MethodInfo addChild = ctrlType.GetMethod("AddChildToParent");
            object[] args = addChild.GetParameters().Length == 3
                ? new object[] { group, parent, false }
                : new object[] { group, parent };
            addChild.Invoke(mixer, args);

            // Optional: make the group visible in the default view.
            try { ctrlType.GetMethod("AddGroupToCurrentView")?.Invoke(mixer, new object[] { group }); }
            catch { /* non-fatal cosmetic step */ }

            return group;
        }

        private static void ExposeVolume(Type ctrlType, Type groupType, Type expType,
                                         object mixer, object group, string paramName)
        {
            object guid = groupType.GetMethod("GetGUIDForVolume").Invoke(group, null);

            // Build an ExposedAudioParameter { guid, name } via reflection (field types
            // identify the targets, so we don't depend on exact field names).
            object exposed = Activator.CreateInstance(expType);
            foreach (FieldInfo f in expType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == guid.GetType()) f.SetValue(exposed, guid);
                else if (f.FieldType == typeof(string)) f.SetValue(exposed, paramName);
            }

            // Append to the controller's exposedParameters array. (AddExposedParameter
            // takes an AudioParameterPath, not an ExposedAudioParameter, so we set the
            // array directly — this is what the Inspector persists and what GetFloat reads.)
            PropertyInfo prop = ctrlType.GetProperty("exposedParameters");
            var current = (Array)prop.GetValue(mixer);
            int len = current?.Length ?? 0;
            Array grown = Array.CreateInstance(expType, len + 1);
            current?.CopyTo(grown, 0);
            grown.SetValue(exposed, len);
            prop.SetValue(mixer, grown);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. Scene
        // ════════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/Audio Manager/Build Audio Scene")]
        public static void BuildAudioScene()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                mixer = BuildAudioMixer();

            // Target AudioManagerScene by path so the build can't land in another scene.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // Idempotent: clear previous build roots.
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == CanvasName || root.name == ManagerName)
                    UnityEngine.Object.DestroyImmediate(root);

            EnsureAudioListener(scene);
            EnsureEventSystem();

            AudioMixerGroup bgmGroup = FirstGroup(mixer, "BGM");
            AudioMixerGroup sfxGroup = FirstGroup(mixer, "SFX");

            // ── AudioManager ───────────────────────────────────────────────────
            var managerGO = new GameObject(ManagerName);
            SceneManager.MoveGameObjectToScene(managerGO, scene);
            var manager = managerGO.AddComponent<AudioManager>();
            var amSo = new SerializedObject(manager);
            amSo.FindProperty("_mixer").objectReferenceValue = mixer;
            amSo.FindProperty("_bgmGroup").objectReferenceValue = bgmGroup;
            amSo.FindProperty("_sfxGroup").objectReferenceValue = sfxGroup;
            amSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Canvas + panel ───────────────────────────────────────────────────
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGO, scene);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dark panel
            RectTransform panel = NewRect("Panel", canvasGO.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900, 600);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            MakeText(panel, font, "Title", 250, 800, 70, 40).text = "Audio Settings";

            var view = panel.gameObject.AddComponent<AudioVolumePanel>();
            var rows = new (AudioChannel ch, Slider slider, Button btn, Image icon, Text pct)[3];
            AudioChannel[] channels = { AudioChannel.Master, AudioChannel.BGM, AudioChannel.SFX };

            float y = 120f;
            for (int i = 0; i < channels.Length; i++)
            {
                AudioChannel ch = channels[i];
                MakeText(panel, font, ch + "Label", y, 200, 60, 28, TextAnchor.MiddleLeft, -330);
                Slider slider = MakeSlider(panel, ch + "Slider", y, 460, 30, -40);
                Text pct = MakeText(panel, font, ch + "Percent", y, 90, 60, 26, TextAnchor.MiddleCenter, 250);
                pct.text = "100%";
                (Button btn, Image icon) = MakeMuteButton(panel, ch + "Mute", y, 60, 360);
                rows[i] = (ch, slider, btn, icon, pct);
                y -= 110f;
            }

            // Wire the panel rows.
            var viewSo = new SerializedObject(view);
            SerializedProperty rowsProp = viewSo.FindProperty("_rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty el = rowsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("channel").enumValueIndex = (int)rows[i].ch;
                el.FindPropertyRelative("volumeSlider").objectReferenceValue = rows[i].slider;
                el.FindPropertyRelative("muteButton").objectReferenceValue = rows[i].btn;
                el.FindPropertyRelative("muteIcon").objectReferenceValue = rows[i].icon;
                el.FindPropertyRelative("percentLabel").objectReferenceValue = rows[i].pct;
            }
            // _toggleButtonSprites left empty for the user to assign (0=mute,1=unmute).
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[AudioSystemBuilder] Built audio scene '{scene.name}' " +
                      $"(mixer wired: {mixer != null}, BGM: {bgmGroup != null}, SFX: {sfxGroup != null}).");
            Selection.activeGameObject = canvasGO;
        }

        // ── Scene helpers ────────────────────────────────────────────────────────

        private static void EnsureAudioListener(Scene scene)
        {
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null)
                return;

            Camera cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null) { cam.gameObject.AddComponent<AudioListener>(); return; }

            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            // Add an input module so the UI is interactive (best-effort across input backends).
            Type moduleType =
                Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem")
                ?? typeof(UnityEngine.EventSystems.StandaloneInputModule);
            es.AddComponent(moduleType);
        }

        private static AudioMixerGroup FirstGroup(AudioMixer mixer, string name)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(name);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        // ── UI builder helpers (DRY) ───────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Text MakeText(RectTransform parent, Font font, string name, float y, float w, float h,
                                     int size, TextAnchor anchor = TextAnchor.MiddleCenter, float x = 0)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Slider MakeSlider(RectTransform parent, string name, float y, float w, float h, float x)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var slider = rt.gameObject.AddComponent<Slider>();

            RectTransform bg = NewRect("Background", rt);
            Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.20f, 0.20f, 0.22f, 1f);

            RectTransform fillArea = NewRect("Fill Area", rt);
            Stretch(fillArea);
            RectTransform fill = NewRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(1, 1);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.70f, 1f, 1f);

            RectTransform handleArea = NewRect("Handle Slide Area", rt);
            Stretch(handleArea);
            RectTransform handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(24, 0);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }

        private static (Button, Image) MakeMuteButton(RectTransform parent, string name, float y, float size, float x)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(x, y);
            var icon = rt.gameObject.AddComponent<Image>();
            icon.color = Color.white; // tint; sprite assigned at runtime by the panel
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            return (button, icon);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
