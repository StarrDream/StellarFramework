#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StellarFramework.Demo;
using StellarFramework.Examples;
using StellarFramework.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    /// <summary>
    /// Generates all sample support assets and materializes sample scenes from editor-only templates.
    /// </summary>
    public static class ExamplePlayableSceneBuilder
    {
        private const string BuildRequestFile = "Assets/StellarFramework/Samples/KitSamples/Editor/.build_playable_scenes";
        private const string TemplateRootFolder = "Assets/StellarFramework/Samples/KitSamples/Editor/SampleTemplates";
        private const string KitSampleTemplateFolder = TemplateRootFolder + "/KitSamples";
        private const string ArchitectureDemoTemplateFolder = TemplateRootFolder + "/ArchitectureDemo";
        private const string ScenesFolder = "Assets/StellarFramework/Samples/KitSamples/Scenes";
        private const string GeneratedFolder = "Assets/StellarFramework/Samples/KitSamples/Generated";
        private const string ArchitectureDemoSceneFolder = "Assets/StellarFramework/Samples/ArchitectureDemo/Scene";
        private const string ArchitectureDemoPanelFolder = "Assets/StellarFramework/Samples/ArchitectureDemo/Resources/UIPanel";
        private const string GeneratedMaterialsFolder = GeneratedFolder + "/Materials";
        private const string GeneratedPrefabsFolder = GeneratedFolder + "/Prefabs";
        private const string GeneratedAnimFolder = GeneratedFolder + "/Animations";
        private const string GeneratedAudioFolder = "Assets/StellarFramework/Resources/Audio";
        private const string GeneratedResFolder = "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest";
        private const string GeneratedUiFolder = "Assets/StellarFramework/Resources/UIPanel";
        private const string StreamingAssetsFolder = "Assets/StreamingAssets";
        private const string ResKitStreamingFolder = StreamingAssetsFolder + "/StellarFramework/Samples/KitSamples/Example_ResKit";
        private const string ResKitArtFolder = "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle";
        private const string AddressableSourceFolder = "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables";

        private const string ExampleSettingsScriptPath =
            "Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/Example_SettingsKit.cs";

        private const string SettingsOverlayScriptPath =
            "Assets/StellarFramework/Runtime/Kits/SettingsKit/Core/SettingsMenuOverlay.cs";

        // GUIDs from the development-scene templates. They get replaced with the GUIDs of freshly generated assets.
        private const string TemplateGuidRedMaterial = "2e79d560d23fc354395f5747b936ab1c";
        private const string TemplateGuidGreenMaterial = "16e62bb8a5f930f4a8be099d35b34f95";
        private const string TemplateGuidBlueMaterial = "1a75180894048894bba9cf870ae470db";
        private const string TemplateGuidBulletPrefab = "711ec48668c7491488ac961d36833480";
        private const string TemplateGuidFsmController = "6a91e7c3ed0287f49be0b56e26e5add2";

        private static Material _redMaterial;
        private static Material _greenMaterial;
        private static Material _blueMaterial;
        private static RuntimeAnimatorController _fsmController;
        private static Bullet _bulletPrefab;
        private static AudioMixer _defaultAudioMixer;

        [InitializeOnLoadMethod]
        private static void AutoBuildIfRequested()
        {
            if (!File.Exists(BuildRequestFile))
            {
                return;
            }

            File.Delete(BuildRequestFile);
            BuildAllSamples();
        }

        public static void BuildPlayableScenes()
        {
            BuildAllSamples();
        }

        public static void EnsureSampleSupportAssetsForCurrentPipeline()
        {
            EnsureFolders();
            EnsureSupportAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildAllSamples()
        {
            EnsureFolders();
            EnsureSupportAssets();
            BuildKitSampleScenesFromTemplates();
            BuildArchitectureDemoFromTemplate();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ExamplePlayableSceneBuilder] 全部样例场景与依赖资源生成完成。");
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(ScenesFolder);
            EnsureAssetFolder(ArchitectureDemoSceneFolder);
            EnsureAssetFolder(ArchitectureDemoPanelFolder);
            EnsureAssetFolder(GeneratedFolder);
            EnsureAssetFolder(GeneratedMaterialsFolder);
            EnsureAssetFolder(GeneratedPrefabsFolder);
            EnsureAssetFolder(GeneratedAnimFolder);
            EnsureAssetFolder(GeneratedAudioFolder);
            EnsureAssetFolder(GeneratedAudioFolder + "/BGM");
            EnsureAssetFolder(GeneratedAudioFolder + "/SFX");
            EnsureAssetFolder(GeneratedResFolder);
            EnsureAssetFolder(GeneratedUiFolder);
            EnsureAssetFolder("Assets/StreamingAssets");
            EnsureAssetFolder(StreamingAssetsFolder + "/Configs");
            EnsureAssetFolder(StreamingAssetsFolder + "/Configs/Normal");
            EnsureAssetFolder(StreamingAssetsFolder + "/Configs/Net");
            EnsureAssetFolder(ResKitStreamingFolder);
            EnsureAssetFolder(ResKitArtFolder);
            EnsureAssetFolder(AddressableSourceFolder);
        }

        private static void EnsureSupportAssets()
        {
            _redMaterial = LoadOrCreateMaterial(GeneratedMaterialsFolder + "/Example_Red.mat", new Color(0.89f, 0.30f, 0.28f));
            _greenMaterial = LoadOrCreateMaterial(GeneratedMaterialsFolder + "/Example_Green.mat", new Color(0.28f, 0.75f, 0.41f));
            _blueMaterial = LoadOrCreateMaterial(GeneratedMaterialsFolder + "/Example_Blue.mat", new Color(0.27f, 0.55f, 0.92f));
            _fsmController = LoadOrCreateFsmController();
            _bulletPrefab = LoadOrCreateBulletPrefab();
            _defaultAudioMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
                "Assets/StellarFramework/Runtime/Kits/AudioKit/DefaultAudioMixer.mixer");

            EnsureUIRootPrefab();
            EnsureExamplePanelPrefab();
            EnsureArchitectureDemoPanelPrefab();
            EnsureConfigFiles();
            EnsureRawTextFile();
            EnsureAudioClips();
            EnsureResKitPrefabs();
        }

        private static void BuildKitSampleScenesFromTemplates()
        {
            MaterializeTemplateFolder(KitSampleTemplateFolder, ScenesFolder);
        }

        private static void BuildArchitectureDemoFromTemplate()
        {
            MaterializeTemplateFolder(ArchitectureDemoTemplateFolder, ArchitectureDemoSceneFolder);
        }

        private static void MaterializeTemplateFolder(string templateFolder, string outputFolder)
        {
            string templateRoot = ToAbsolutePath(templateFolder);
            if (!Directory.Exists(templateRoot))
            {
                Debug.LogError($"[ExamplePlayableSceneBuilder] Sample template folder not found: {templateFolder}");
                return;
            }

            string[] templateFiles = Directory.GetFiles(templateRoot, "*.unity.txt", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < templateFiles.Length; i++)
            {
                string outputAssetPath = $"{outputFolder}/{Path.GetFileName(templateFiles[i]).Replace(".unity.txt", ".unity")}";
                MaterializeSceneTemplate(templateFiles[i], outputAssetPath);
            }
        }

        private static void MaterializeSceneTemplate(string templateAbsolutePath, string outputAssetPath)
        {
            string templateText = File.ReadAllText(templateAbsolutePath);
            string resolvedText = ApplyTemplateGuidReplacements(templateText);
            string outputAbsolutePath = ToAbsolutePath(outputAssetPath);
            EnsureDirectory(Path.GetDirectoryName(outputAbsolutePath));
            File.WriteAllText(outputAbsolutePath, resolvedText, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ApplyTemplateGuidReplacements(string templateText)
        {
            if (string.IsNullOrEmpty(templateText))
            {
                return string.Empty;
            }

            return templateText
                .Replace(TemplateGuidRedMaterial, AssetDatabase.AssetPathToGUID(GeneratedMaterialsFolder + "/Example_Red.mat"))
                .Replace(TemplateGuidGreenMaterial, AssetDatabase.AssetPathToGUID(GeneratedMaterialsFolder + "/Example_Green.mat"))
                .Replace(TemplateGuidBlueMaterial, AssetDatabase.AssetPathToGUID(GeneratedMaterialsFolder + "/Example_Blue.mat"))
                .Replace(TemplateGuidBulletPrefab, AssetDatabase.AssetPathToGUID(GeneratedPrefabsFolder + "/ExampleBullet.prefab"))
                .Replace(TemplateGuidFsmController, AssetDatabase.AssetPathToGUID(GeneratedAnimFolder + "/Example_FSM.controller"));
        }

        private static void EnsureUIRootPrefab()
        {
            const string path = GeneratedUiFolder + "/UIRoot.prefab";

            // 已存在则跳过，绝不覆盖用户对 UIRoot 的定制（Canvas 排序、引用等）。
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject root = new GameObject("UIRoot", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(root.transform, false);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            CreateUiRoleCanvas(root.transform, "StaticCanvas", 0);
            CreateUiRoleCanvas(root.transform, "DynamicCanvas", 100);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateUiRoleCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject roleRoot = new GameObject(name, typeof(RectTransform));
            roleRoot.layer = LayerMask.NameToLayer("UI");
            roleRoot.transform.SetParent(parent, false);

            RectTransform rect = roleRoot.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.zero);

            Canvas canvas = roleRoot.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            roleRoot.AddComponent<GraphicRaycaster>();

            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                CreateUiLayerNode(roleRoot.transform, layer);
            }
        }

        private static void CreateUiLayerNode(Transform parent, UIPanelBase.PanelLayer layer)
        {
            GameObject layerObject = new GameObject(layer.ToString(), typeof(RectTransform));
            layerObject.layer = LayerMask.NameToLayer("UI");
            layerObject.transform.SetParent(parent, false);

            RectTransform rect = layerObject.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.zero);

            if (layer == UIPanelBase.PanelLayer.Popup || layer == UIPanelBase.PanelLayer.System)
            {
                layerObject.AddComponent<CanvasGroup>();
            }
        }

        private static void EnsureExamplePanelPrefab()
        {
            const string path = GeneratedUiFolder + "/ExamplePanel.prefab";

            // 已存在则跳过，绝不覆盖用户定制。
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject panel = new GameObject("ExamplePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(720f, 420f);

            ExamplePanel panelComponent = panel.AddComponent<ExamplePanel>();

            GameObject root = new GameObject("root", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(panel.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(560f, 260f);
            rootRect.anchoredPosition = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.96f);

            Text title = CreateText(root.transform, "TitleText", "Example Panel", 28, TextAnchor.MiddleCenter);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(480f, 120f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);

            Button confirmButton = CreateButton(root.transform, "ConfirmBtn", "确认");
            RectTransform buttonRect = confirmButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(180f, 56f);
            buttonRect.anchoredPosition = new Vector2(0f, 28f);

            panelComponent.TitleText = title;
            panelComponent.ConfirmBtn = confirmButton;

            PrefabUtility.SaveAsPrefabAsset(panel, path);
            Object.DestroyImmediate(panel);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureArchitectureDemoPanelPrefab()
        {
            const string path = ArchitectureDemoPanelFolder + "/Panel_Main.prefab";

            // 已存在则跳过，绝不覆盖用户定制。
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject panel = new GameObject("Panel_Main", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            panel.layer = LayerMask.NameToLayer("UI");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(960f, 640f);

            Panel_Main panelComponent = panel.AddComponent<Panel_Main>();

            GameObject background = new GameObject("root", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.layer = LayerMask.NameToLayer("UI");
            background.transform.SetParent(panel.transform, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(760f, 420f);
            backgroundRect.anchoredPosition = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f, 0.96f);

            Text titleText = CreateText(background.transform, "titleTxt", "架构样例 / Architecture Demo", 40, TextAnchor.MiddleCenter);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(620f, 80f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);

            Text coinText = CreateText(background.transform, "coinText", "当前金币: 0", 34, TextAnchor.MiddleLeft);
            RectTransform coinRect = coinText.rectTransform;
            coinRect.anchorMin = new Vector2(0f, 1f);
            coinRect.anchorMax = new Vector2(0f, 1f);
            coinRect.pivot = new Vector2(0f, 1f);
            coinRect.sizeDelta = new Vector2(420f, 64f);
            coinRect.anchoredPosition = new Vector2(50f, -120f);
            coinText.color = new Color(0.95f, 0.77f, 0.06f, 1f);

            Text hintText = CreateText(background.transform, "hintTxt", "提示：点击“挖矿”按钮验证 Service -> Model -> View 更新链路。", 22, TextAnchor.MiddleLeft);
            RectTransform hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-80f, 40f);
            hintRect.anchoredPosition = new Vector2(0f, 40f);
            hintText.color = new Color(0.58f, 0.65f, 0.65f, 1f);

            Button mineButton = CreateButton(background.transform, "MineButton", "挖矿 +10");
            RectTransform mineRect = mineButton.GetComponent<RectTransform>();
            mineRect.anchorMin = new Vector2(0f, 0f);
            mineRect.anchorMax = new Vector2(0f, 0f);
            mineRect.pivot = new Vector2(0f, 0f);
            mineRect.sizeDelta = new Vector2(220f, 56f);
            mineRect.anchoredPosition = new Vector2(50f, 110f);

            Button closeButton = CreateButton(background.transform, "CloseButton", "关闭");
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0f);
            closeRect.anchorMax = new Vector2(1f, 0f);
            closeRect.pivot = new Vector2(1f, 0f);
            closeRect.sizeDelta = new Vector2(160f, 56f);
            closeRect.anchoredPosition = new Vector2(-50f, 110f);

            panelComponent.CoinText = coinText;
            panelComponent.MineButton = mineButton;
            panelComponent.CloseButton = closeButton;

            PrefabUtility.SaveAsPrefabAsset(panel, path);
            Object.DestroyImmediate(panel);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureConfigFiles()
        {
            WriteTextIfMissing(
                StreamingAssetsFolder + "/Configs/Normal/TestGameConfig.json",
                "{\n  \"Version\": \"1.0.0\",\n  \"MaxPlayers\": 100,\n  \"BGMVolume\": 0.8,\n  \"IsDebugOpen\": true\n}\n");

            WriteTextIfMissing(
                StreamingAssetsFolder + "/Configs/Net/TestApiConfig.json",
                "{\n  \"ActiveProfile\": \"Dev\",\n  \"Environments\": {\n    \"Dev\": {\n      \"GameApi\": \"https://api.example.com\"\n    },\n    \"Release\": {\n      \"GameApi\": \"https://release.example.com\"\n    }\n  },\n  \"Endpoints\": {\n    \"Auth.Login\": {\n      \"Service\": \"GameApi\",\n      \"Path\": \"/auth/login\"\n    },\n    \"Item.GetDetail\": {\n      \"Service\": \"GameApi\",\n      \"Path\": \"/item/{itemId}\"\n    },\n    \"Room.Join\": {\n      \"Service\": \"GameApi\",\n      \"Path\": \"/room/{roomId}/join/{uid}\"\n    }\n  }\n}\n");
        }

        private static void EnsureRawTextFile()
        {
            WriteTextIfMissing(
                ResKitStreamingFolder + "/TestText.txt",
                "Hello Physical World!\nThis file is generated for Example_ResKit.\n");
        }

        private static void EnsureAudioClips()
        {
            WriteToneWavIfMissing(GeneratedAudioFolder + "/BGM/MainTheme.wav", 261.63f, 1.6f);
            WriteToneWavIfMissing(GeneratedAudioFolder + "/BGM/BattleTheme.wav", 329.63f, 1.2f);
            WriteToneWavIfMissing(GeneratedAudioFolder + "/SFX/UI_Click.wav", 880f, 0.12f);
            WriteToneWavIfMissing(GeneratedAudioFolder + "/SFX/Explosion.wav", 110f, 0.5f);
            WriteToneWavIfMissing(GeneratedAudioFolder + "/SFX/Footstep.wav", 196f, 0.18f);
        }

        private static void EnsureResKitPrefabs()
        {
            CreatePrimitivePrefabIfMissing(GeneratedResFolder + "/TestCube_Res.prefab", PrimitiveType.Cube, _greenMaterial, Vector3.one);
            CreatePrimitivePrefabIfMissing(ResKitArtFolder + "/TestCapsule_AB.prefab", PrimitiveType.Capsule, _redMaterial, Vector3.one);
            CreatePrimitivePrefabIfMissing(AddressableSourceFolder + "/TestSphere_AA.prefab", PrimitiveType.Sphere, _blueMaterial, Vector3.one);
        }

        private static void BuildResKitAssetBundle()
        {
            const string assetPath = ResKitArtFolder + "/TestCapsule_AB.prefab";
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                return;
            }

            importer.assetBundleName = "art";
            importer.SaveAndReimport();

            string outputPath = $"{StreamingAssetsFolder}/AssetBundles/{GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget)}";
            EnsureAssetFolder($"{StreamingAssetsFolder}/AssetBundles");
            EnsureAssetFolder(outputPath);

            BuildPipeline.BuildAssetBundles(
                ToAbsolutePath(outputPath),
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget);
        }

        private static Bullet LoadOrCreateBulletPrefab()
        {
            const string path = GeneratedPrefabsFolder + "/ExampleBullet.prefab";
            Bullet existing = AssetDatabase.LoadAssetAtPath<Bullet>(path);
            if (existing != null)
            {
                // 已存在则不重置材质/缩放，保护用户定制。
                return existing;
            }

            GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "ExampleBullet";
            bullet.transform.localScale = Vector3.one * 0.35f;
            bullet.GetComponent<Renderer>().sharedMaterial = _blueMaterial;

            Bullet bulletComponent = bullet.AddComponent<ExamplePoolBullet>();
            bulletComponent.Speed = 12f;

            ExampleForwardMover mover = bullet.AddComponent<ExampleForwardMover>();
            mover.direction = Vector3.forward;
            mover.speed = 7f;
            mover.loopDistance = 14f;

            PrefabUtility.SaveAsPrefabAsset(bullet, path);
            Object.DestroyImmediate(bullet);
            return AssetDatabase.LoadAssetAtPath<Bullet>(path);
        }

        private static RuntimeAnimatorController LoadOrCreateFsmController()
        {
            const string controllerPath = GeneratedAnimFolder + "/Example_FSM.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller != null)
            {
                return controller;
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimationClip idleClip = CreateLoopClip(GeneratedAnimFolder + "/Idle.anim", true);
            AnimationClip runClip = CreateLoopClip(GeneratedAnimFolder + "/Run.anim", false);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idleClip;
            AnimatorState runState = stateMachine.AddState("Run");
            runState.motion = runClip;
            stateMachine.defaultState = idleState;

            return controller;
        }

        private static AnimationClip CreateLoopClip(string path, bool idle)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip();
            clip.frameRate = 30f;

            if (idle)
            {
                AnimationCurve bob = AnimationCurve.EaseInOut(0f, 1f, 0.7f, 1.08f);
                bob.AddKey(1.4f, 1f);
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.y", bob);
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.x", AnimationCurve.Constant(0f, 1.4f, 1f));
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, 1.4f, 1f));
            }
            else
            {
                AnimationCurve x = AnimationCurve.Linear(0f, 0.95f, 0.35f, 1.05f);
                x.AddKey(0.7f, 0.95f);
                AnimationCurve z = AnimationCurve.Linear(0f, 1.05f, 0.35f, 0.95f);
                z.AddKey(0.7f, 1.05f);
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.x", x);
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.z", z);
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.y", AnimationCurve.Constant(0f, 0.7f, 1f));
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void CreatePrimitivePrefabIfMissing(string path, PrimitiveType primitiveType, Material material, Vector3 scale)
        {
            EnsureAssetFolder(Path.GetDirectoryName(path).Replace("\\", "/"));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                // 已存在则不重置材质/缩放，保护用户定制。
                return;
            }

            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = Path.GetFileNameWithoutExtension(path);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = global::StellarFramework.RenderPipelineCompatibility.FindPreferredLitShader();
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }

                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // 已存在：仅当 shader 缺失（丢失引用）时修复，绝不覆盖用户自定义 shader 与颜色。
                if (shader != null && material.shader == null)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                }
            }

            return material;
        }

        private static void WriteToneWavIfMissing(string path, float frequency, float durationSeconds)
        {
            string absolutePath = ToAbsolutePath(path);
            if (File.Exists(absolutePath))
            {
                return;
            }

            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - (t / durationSeconds));
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
                samples[i] = (short)(sample * short.MaxValue);
            }

            byte[] wavBytes = BuildWavBytes(samples, sampleRate, 1);
            EnsureDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, wavBytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static byte[] BuildWavBytes(short[] samples, int sampleRate, short channels)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int byteRate = sampleRate * channels * sizeof(short);
                short blockAlign = (short)(channels * sizeof(short));
                int subChunk2Size = samples.Length * sizeof(short);

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + subChunk2Size);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    writer.Write(samples[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteTextIfMissing(string path, string content)
        {
            string absolutePath = ToAbsolutePath(path);
            if (File.Exists(absolutePath))
            {
                return;
            }

            EnsureDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.47f, 0.88f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureDirectory(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            string absolutePath = IsAbsolutePath(relativePath) ? relativePath : ToAbsolutePath(relativePath);
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }
        }

        private static bool IsAbsolutePath(string path)
        {
            return Path.IsPathRooted(path);
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            string normalized = assetFolderPath.Replace("\\", "/").TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) || AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = Path.GetDirectoryName(normalized)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            string folderName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static string GetPlatformFolderName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSX:
                    return "OSX";
                case BuildTarget.WebGL:
                    return "WebGL";
                default:
                    return "Unknown";
            }
        }

        private static string ToAbsolutePath(string assetRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string normalized = assetRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalized);
        }
    }
}
#endif
