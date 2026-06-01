#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
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
            object vo  = CreateGroup(ctrlType, mixer, master, "VO");

            ExposeVolume(ctrlType, groupType, expType, mixer, master, AudioMixerParameters.Master);
            ExposeVolume(ctrlType, groupType, expType, mixer, bgm,    AudioMixerParameters.BGM);
            ExposeVolume(ctrlType, groupType, expType, mixer, sfx,    AudioMixerParameters.SFX);
            ExposeVolume(ctrlType, groupType, expType, mixer, vo,     AudioMixerParameters.VO);

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
            AudioMixerGroup voGroup  = FirstGroup(mixer, "VO");

            // ── AudioManager ───────────────────────────────────────────────────
            var managerGO = new GameObject(ManagerName);
            SceneManager.MoveGameObjectToScene(managerGO, scene);
            var manager = managerGO.AddComponent<AudioManager>();
            var amSo = new SerializedObject(manager);
            amSo.FindProperty("_mixer").objectReferenceValue = mixer;
            amSo.FindProperty("_bgmGroup").objectReferenceValue = bgmGroup;
            amSo.FindProperty("_sfxGroup").objectReferenceValue = sfxGroup;
            amSo.FindProperty("_voGroup").objectReferenceValue = voGroup;
            amSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Canvas + panel ───────────────────────────────────────────────────
            EnsureTmpEssentials();
            EnsurePlaceholderSprites();

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
            panel.sizeDelta = new Vector2(900, 720);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            MakeTMP(panel, "Title", 0, 300, 800, 70, 40, TextAlignmentOptions.Center).text = "Audio Settings";

            var view = panel.gameObject.AddComponent<AudioVolumePanel>();
            AudioChannel[] channels = { AudioChannel.Master, AudioChannel.BGM, AudioChannel.SFX, AudioChannel.VO };
            var rows = new (AudioChannel ch, Slider slider, Button btn, Image icon, TMP_Text pct)[channels.Length];

            float y = 180f;
            for (int i = 0; i < channels.Length; i++)
            {
                AudioChannel ch = channels[i];

                // One empty RectTransform container per channel, holding all its widgets.
                RectTransform container = NewRect(ch.ToString(), panel);
                container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
                container.sizeDelta = new Vector2(820, 90);
                container.anchoredPosition = new Vector2(0, y);

                // Children laid out left→right inside the container (y = 0, centered).
                MakeTMP(container, "Label", -340, 0, 180, 60, 30, TextAlignmentOptions.Left).text = ch.ToString();
                Slider slider = MakeSlider(container, "Slider", 0, 440, 30, -30);
                TMP_Text pct = MakeTMP(container, "Percent", 230, 0, 110, 60, 28, TextAlignmentOptions.Center);
                pct.text = "100%";
                (Button btn, Image icon) = MakeMuteButton(container, "Toggle", 0, 64, 350);

                rows[i] = (ch, slider, btn, icon, pct);
                y -= 120f;
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
            AssignChannelToggleSprites(viewSo, channels);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[AudioSystemBuilder] Built audio scene '{scene.name}' " +
                      $"(mixer wired: {mixer != null}, BGM: {bgmGroup != null}, SFX: {sfxGroup != null}).");
            Selection.activeGameObject = canvasGO;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. Prefabs
        // ════════════════════════════════════════════════════════════════════════

        private const string PrefabFolder       = "Assets/_Source/Audio_Manager/Prefabs";
        private const string ManagerPrefabPath  = PrefabFolder + "/AudioManager.prefab";
        private const string PanelPrefabPath    = PrefabFolder + "/AudioVolumePanel.prefab";

        /// <summary>
        /// Builds the <b>AudioManager prefab</b>: logic-only root with
        /// <see cref="AudioManager"/> pre-wired to the mixer and all four groups.
        /// Drop into any scene once and call from code.
        /// Menu: <b>Tools ▸ Audio Manager ▸ Build AudioManager Prefab</b>
        /// </summary>
        [MenuItem("Tools/Audio Manager/Build AudioManager Prefab")]
        public static void BuildManagerPrefab()
        {
            EnsureFolder(PrefabFolder);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) ?? BuildAudioMixer();

            GameObject root = new GameObject("AudioManager");
            var manager = root.AddComponent<AudioManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_mixer").objectReferenceValue = mixer;
            so.FindProperty("_bgmGroup").objectReferenceValue = FirstGroup(mixer, "BGM");
            so.FindProperty("_sfxGroup").objectReferenceValue = FirstGroup(mixer, "SFX");
            so.FindProperty("_voGroup").objectReferenceValue  = FirstGroup(mixer, "VO");
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, ManagerPrefabPath, "AudioManager prefab");
        }

        /// <summary>
        /// Builds the <b>AudioVolumePanel prefab</b>: self-contained Canvas UI panel
        /// with four channel rows (Master, BGM, SFX, VO), each containing a
        /// container RectTransform ▸ Label (TMP) / Slider / Percent (TMP) / Toggle button.
        /// Drop into any Canvas — requires <see cref="AudioManager"/> in the scene.
        /// Menu: <b>Tools ▸ Audio Manager ▸ Build AudioVolumePanel Prefab</b>
        /// </summary>
        [MenuItem("Tools/Audio Manager/Build AudioVolumePanel Prefab")]
        public static void BuildPanelPrefab()
        {
            EnsureFolder(PrefabFolder);
            EnsureTmpEssentials();
            EnsurePlaceholderSprites();

            // Root: just a RectTransform — user drops this into their own Canvas.
            var root = new GameObject("AudioVolumePanel", typeof(RectTransform));

            // Dark background panel.
            RectTransform panel = NewRect("Panel", root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900, 720);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            MakeTMP(panel, "Title", 0, 300, 800, 70, 40, TextAlignmentOptions.Center).text = "Audio Settings";

            AudioChannel[] channels = { AudioChannel.Master, AudioChannel.BGM, AudioChannel.SFX, AudioChannel.VO };
            var rows = new (AudioChannel ch, Slider slider, Button btn, Image icon, TMP_Text pct)[channels.Length];

            float y = 180f;
            for (int i = 0; i < channels.Length; i++)
            {
                AudioChannel ch = channels[i];

                // Per-channel empty container (Master / BGM / SFX / VO).
                RectTransform container = NewRect(ch.ToString(), panel);
                container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
                container.sizeDelta = new Vector2(820, 90);
                container.anchoredPosition = new Vector2(0, y);

                MakeTMP(container, "Label", -340, 0, 180, 60, 30, TextAlignmentOptions.Left).text = ch.ToString();
                Slider   slider = MakeSlider(container, "Slider",  0, 440, 30, -30);
                TMP_Text pct    = MakeTMP(container, "Percent", 230, 0, 110, 60, 28, TextAlignmentOptions.Center);
                pct.text = "100%";
                (Button btn, Image icon) = MakeMuteButton(container, "Toggle", 0, 64, 350);

                rows[i] = (ch, slider, btn, icon, pct);
                y -= 120f;
            }

            // Wire AudioVolumePanel component.
            var view   = panel.gameObject.AddComponent<AudioVolumePanel>();
            var viewSo = new SerializedObject(view);
            var rowsProp = viewSo.FindProperty("_rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty el = rowsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("channel").enumValueIndex              = (int)rows[i].ch;
                el.FindPropertyRelative("volumeSlider").objectReferenceValue   = rows[i].slider;
                el.FindPropertyRelative("muteButton").objectReferenceValue     = rows[i].btn;
                el.FindPropertyRelative("muteIcon").objectReferenceValue       = rows[i].icon;
                el.FindPropertyRelative("percentLabel").objectReferenceValue   = rows[i].pct;
            }
            AssignChannelToggleSprites(viewSo, channels);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PanelPrefabPath, "AudioVolumePanel prefab");
        }

        private static void SavePrefab(GameObject instance, string path, string label)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool ok);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudioSystemBuilder] {label} {(ok ? "saved" : "FAILED")}: {path}");
            if (ok) Selection.activeObject = prefab;
        }

        private const string SpritesFolder = "Assets/_Source/Audio_Manager/Sprites";

        /// <summary>
        /// Populates <c>_toggleButtonSprites</c> (a per-channel array) from the Sprites
        /// folder by naming convention: <c>Sprites/{Channel}/{Channel}_Mute.png</c> and
        /// <c>{Channel}_UnMute.png</c>. Channels without sprites are skipped. Each PNG is
        /// ensured to import as a Sprite.
        /// </summary>
        private static void AssignChannelToggleSprites(SerializedObject viewSo, AudioChannel[] channels)
        {
            SerializedProperty arr = viewSo.FindProperty("_toggleButtonSprites");
            arr.ClearArray();

            int index = 0;
            foreach (AudioChannel ch in channels)
            {
                Sprite mute   = LoadChannelSprite(ch, "Mute");
                Sprite unmute = LoadChannelSprite(ch, "UnMute");
                if (mute == null && unmute == null)
                    continue; // no icons for this channel (e.g. Master) — leave it out

                arr.InsertArrayElementAtIndex(index);
                SerializedProperty el = arr.GetArrayElementAtIndex(index);
                el.FindPropertyRelative("channel").enumValueIndex = (int)ch;
                SerializedProperty sprites = el.FindPropertyRelative("sprites");
                sprites.arraySize = 2;
                sprites.GetArrayElementAtIndex(0).objectReferenceValue = mute;   // 0 = mute
                sprites.GetArrayElementAtIndex(1).objectReferenceValue = unmute; // 1 = unmute
                index++;
            }
        }

        private static Sprite LoadChannelSprite(AudioChannel channel, string suffix)
        {
            string path = $"{SpritesFolder}/{channel}/{channel}_{suffix}.png";
            EnsureSpriteImport(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Ensures a texture at <paramref name="path"/> imports as a Sprite.</summary>
        private static void EnsureSpriteImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;
            if (importer.textureType == TextureImporterType.Sprite)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        /// <summary>Imports TextMeshPro Essential Resources (settings + default font) if missing.</summary>
        private static void EnsureTmpEssentials()
        {
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
                return;

            Type t = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType("TMPro.TMP_PackageResourceImporter");
                if (t != null) break;
            }
            if (t == null)
            {
                Debug.LogWarning("[AudioSystemBuilder] TMP importer not found; TMP labels may be blank until " +
                                 "you run Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
                return;
            }

            try
            {
                object importer = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
                t.GetMethod("ImportResources")?.Invoke(importer, new object[] { true, false, false });
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioSystemBuilder] TMP essentials import failed: " + e.Message);
            }
        }

        /// <summary>
        /// Ensures the VO and Master channels have toggle sprites by copying the SFX
        /// sprites into their folders as placeholders (swap them for real art later).
        /// </summary>
        private static void EnsurePlaceholderSprites()
        {
            SeedChannelSprites(AudioChannel.VO);
            SeedChannelSprites(AudioChannel.Master);
            AssetDatabase.Refresh();
        }

        private static void SeedChannelSprites(AudioChannel channel)
        {
            string folder = $"{SpritesFolder}/{channel}";
            EnsureFolder(folder);
            CopyIfMissing($"{SpritesFolder}/SFX/SFX_Mute.png",   $"{folder}/{channel}_Mute.png");
            CopyIfMissing($"{SpritesFolder}/SFX/SFX_UnMute.png", $"{folder}/{channel}_UnMute.png");
        }

        private static void CopyIfMissing(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) != null) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(source) == null) return;
            AssetDatabase.CopyAsset(source, destination);
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

        private static TMP_Text MakeTMP(RectTransform parent, string name, float x, float y, float w, float h,
                                        int size, TextAlignmentOptions align)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
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
