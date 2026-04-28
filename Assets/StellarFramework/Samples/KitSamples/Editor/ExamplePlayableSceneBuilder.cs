#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using StellarFramework.Examples;
using StellarFramework.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    /// <summary>
    /// 批量生成 Example Playable 场景与最小依赖资源。
    /// </summary>
    public static class ExamplePlayableSceneBuilder
    {
        private const string BuildRequestFile = "Assets/StellarFramework/Samples/KitSamples/Editor/.build_playable_scenes";
        private const string ScenesFolder = "Assets/StellarFramework/Samples/KitSamples/Scenes";
        private const string GeneratedFolder = "Assets/StellarFramework/Samples/KitSamples/Generated";
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
        private const string ExampleSettingsScriptPath = "Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/Example_SettingsKit.cs";
        private const string SettingsOverlayScriptPath = "Assets/StellarFramework/Runtime/Kits/SettingsKit/Core/SettingsMenuOverlay.cs";

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
            BuildPlayableScenes();
        }

        public static void BuildPlayableScenes()
        {
            EnsureFolders();
            EnsureSupportAssets();
            BuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ExamplePlayableSceneBuilder] Example Playable 场景与依赖资源生成完成。");
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(ScenesFolder);
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

            EnsureDirectory(ScenesFolder);
            EnsureDirectory(GeneratedFolder);
            EnsureDirectory(GeneratedMaterialsFolder);
            EnsureDirectory(GeneratedPrefabsFolder);
            EnsureDirectory(GeneratedAnimFolder);
            EnsureDirectory(GeneratedAudioFolder + "/BGM");
            EnsureDirectory(GeneratedAudioFolder + "/SFX");
            EnsureDirectory(GeneratedResFolder);
            EnsureDirectory(GeneratedUiFolder);
            EnsureDirectory(StreamingAssetsFolder + "/Configs/Normal");
            EnsureDirectory(StreamingAssetsFolder + "/Configs/Net");
            EnsureDirectory(ResKitStreamingFolder);
            EnsureDirectory(ResKitArtFolder);
            EnsureDirectory(AddressableSourceFolder);
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

            EnsureExamplePanelPrefab();
            EnsureConfigFiles();
            EnsureRawTextFile();
            EnsureAudioClips();
            EnsureResKitPrefabs();
            BuildResKitAssetBundle();
        }

        private static void BuildScenes()
        {
            BuildActionKitScene();
            BuildAudioKitScene();
            BuildBindableKitScene();
            BuildConfigKitScene();
            BuildSettingsKitScene();
            BuildEventKitScene();
            BuildFsmKitScene();
            BuildHotUpdateKitScene();
            BuildHttpKitScene();
            BuildLogKitScene();
            BuildPoolKitScene();
            BuildResKitScene();
            BuildSingletonKitScene();
            BuildUIKitScene();
        }

        private static void BuildActionKitScene()
        {
            Scene scene = NewScene("ActionKit_Playable");
            CreateGround("Ground", Vector3.zero, new Vector3(10f, 0.1f, 10f), _greenMaterial);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "AnimatedCube";
            cube.transform.position = new Vector3(-4f, 0.6f, 0f);
            cube.GetComponent<Renderer>().sharedMaterial = _redMaterial;

            Canvas canvas = CreateScreenCanvas("ActionCanvas");
            GameObject panel = CreateUiPanel(canvas.transform, "FadePanel", new Vector2(320f, 120f),
                new Color(0.15f, 0.19f, 0.26f, 0.82f));
            panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -170f);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            Text text = CreateText(panel.transform, "HintText", "按 A 播放序列动画\n按 S 取消当前动画",
                24, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(20f, 20f), new Vector2(-20f, -20f));

            GameObject runner = new GameObject("Example_ActionKit_Runner");
            Example_ActionKit example = runner.AddComponent<Example_ActionKit>();
            example.cubeTransform = cube.transform;
            example.uiGroup = group;

            AddGuide("ActionKit Playable",
                "演示链式动作、延迟、并行动作与手动取消。按键触发后会驱动方块移动、缩放，并同步淡出 UI 面板。",
                "A: 播放一段新的动作序列\nS: 手动取消当前序列",
                "无额外资源依赖。直接运行即可观察 ActionKit 的最小闭环。",
                "首次按 A 后，方块会从左侧移动到右侧，再并行缩放方块并改变 UI 透明度。按 S 会立即中断当前链。",
                "该场景强调的是调用链写法和生命周期绑定，不是完整演出系统。");

            SaveScene(scene, "ActionKit_Playable");
        }

        private static void BuildAudioKitScene()
        {
            Scene scene = NewScene("AudioKit_Playable");
            CreateGround("Ground", Vector3.zero, new Vector3(14f, 0.1f, 14f), _greenMaterial);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "MovingTarget";
            target.transform.position = new Vector3(2.5f, 0.6f, 0f);
            target.GetComponent<Renderer>().sharedMaterial = _blueMaterial;
            ExamplePingPongMover mover = target.AddComponent<ExamplePingPongMover>();
            mover.localOffset = new Vector3(0f, 0f, 5f);
            mover.speed = 0.7f;

            GameObject runner = new GameObject("Example_AudioKit_Runner");
            runner.transform.position = new Vector3(-2f, 0f, 0f);
            Example_AudioKit example = runner.AddComponent<Example_AudioKit>();

            SerializedObject serialized = new SerializedObject(example);
            serialized.FindProperty("_mainMixer").objectReferenceValue = _defaultAudioMixer;
            serialized.FindProperty("_movingTarget").objectReferenceValue = target.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddGuide("AudioKit Playable",
                "演示 AudioKit 初始化、BGM 切换、2D/3D 音效与全局音量控制。默认会使用框架自带的 DefaultAudioMixer 和自动生成的测试音频资源。",
                "1: 播放 2D UI 音效\n2: 在当前物体位置播放 3D 爆炸音效\n3: 播放跟随 MovingTarget 的 3D 音效\n4: 切换 BattleTheme\n5: 切换音效静音\n6: 循环调整 BGM 音量",
                "场景已自动绑定 DefaultAudioMixer，并生成 MainTheme / BattleTheme / UI_Click / Explosion / Footstep 五个最小音频资源。",
                "进入场景后会自动播放 MainTheme。按键触发时可以听到 BGM 和 SFX 变化，并在 Console 看到相应日志。",
                "这是 API 验证入口。若你要接入真实项目，请替换为项目自己的 Mixer、分组参数和音频素材。");

            SaveScene(scene, "AudioKit_Playable");
        }

        private static void BuildBindableKitScene()
        {
            Scene scene = NewScene("BindableKit_Playable");
            GameObject runner = new GameObject("Example_BindableKit_Runner");
            runner.AddComponent<Example_BindableKit>();

            AddGuide("BindableKit Playable",
                "演示 BindableProperty、BindableList 和 BindableDictionary 的标准注册与回调链路。",
                "Q: HP -10\nW: 背包新增一个物品\nE: 更新任务状态",
                "无额外资源依赖。所有结果都会输出到 Console。",
                "开始运行后会先触发一次 HP 的初始化回调。之后每次按键都会看到对应的绑定事件日志。",
                "该场景主要验证数据绑定语义，不包含 UI 刷新层。");

            SaveScene(scene, "BindableKit_Playable");
        }

        private static void BuildConfigKitScene()
        {
            Scene scene = NewScene("ConfigKit_Playable");
            GameObject runner = new GameObject("Example_ConfigKit_Runner");
            runner.AddComponent<Example_ConfigKit>();

            AddGuide("ConfigKit Playable",
                "演示 NormalConfig 与 NetConfig 的异步加载、读写、保存，以及参数化路由拼接。",
                "运行后观察左上角 OnGUI 面板。\n点击按钮可以修改 BGMVolume，并触发 Save() 保存到 PersistentDataPath。",
                "构建器已预置 StreamingAssets/Configs/Normal/TestGameConfig.json 与 StreamingAssets/Configs/Net/TestApiConfig.json。",
                "进入场景后会先输出配置加载结果，再在面板中展示当前音量并允许修改和保存。",
                "若你此前手动保存过本地 Overlay，读取到的值会优先来自 PersistentDataPath，而不是包内默认配置。",
                true);

            SaveScene(scene, "ConfigKit_Playable");
        }

        private static void BuildEventKitScene()
        {
            Scene scene = NewScene("EventKit_Playable");
            GameObject runner = new GameObject("Example_EventKit_Runner");
            runner.AddComponent<Example_EventKit>();

            AddGuide("EventKit Playable",
                "演示枚举事件和强类型结构体事件的注册、广播与生命周期解绑。",
                "Z: 广播 LevelUp 枚举事件\nX: 广播 PlayerHit 结构体事件",
                "无额外依赖，直接运行即可。",
                "每次按键都会在 Console 中看到对应事件的接收日志。",
                "这个场景强调的是轻量事件流，而不是完整消息总线。");

            SaveScene(scene, "EventKit_Playable");
        }

        private static void BuildSettingsKitScene()
        {
            Scene scene = NewScene("SettingsKit_Playable");
            CreateGround("Ground", Vector3.zero, new Vector3(14f, 0.1f, 14f), _greenMaterial);

            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 2.8f, -7.2f);
                camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            }

            Light previewLight = Object.FindObjectOfType<Light>();
            if (previewLight != null)
            {
                previewLight.intensity = 1.2f;
            }

            GameObject previewObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            previewObject.name = "SettingsPreviewActor";
            previewObject.transform.position = new Vector3(0f, 1.1f, 0f);
            previewObject.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
            previewObject.GetComponent<Renderer>().sharedMaterial = _blueMaterial;

            GameObject sideMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideMarker.name = "PreviewMarker";
            sideMarker.transform.position = new Vector3(2.2f, 0.55f, 0f);
            sideMarker.transform.localScale = new Vector3(0.8f, 1.1f, 0.8f);
            sideMarker.GetComponent<Renderer>().sharedMaterial = _redMaterial;

            GameObject runner = new GameObject("Example_SettingsKit_Runner");
            MonoScript overlayScript = AssetDatabase.LoadAssetAtPath<MonoScript>(SettingsOverlayScriptPath);
            Type overlayType = overlayScript?.GetClass();
            if (overlayType != null && typeof(MonoBehaviour).IsAssignableFrom(overlayType))
            {
                MonoBehaviour overlay = (MonoBehaviour)runner.AddComponent(overlayType);
                SerializedObject overlaySerialized = new SerializedObject(overlay);
                overlaySerialized.FindProperty("title").stringValue = "SettingsKit Sample";
                overlaySerialized.FindProperty("visibleOnStart").boolValue = true;
                overlaySerialized.FindProperty("toggleKey").intValue = (int)KeyCode.F10;
                overlaySerialized.FindProperty("windowRect").rectValue = new Rect(36f, 36f, 860f, 620f);
                overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            MonoScript exampleScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ExampleSettingsScriptPath);
            Type exampleType = exampleScript?.GetClass();
            if (exampleType != null && typeof(MonoBehaviour).IsAssignableFrom(exampleType))
            {
                MonoBehaviour example = (MonoBehaviour)runner.AddComponent(exampleType);
                SerializedObject serialized = new SerializedObject(example);
                serialized.FindProperty("_mainMixer").objectReferenceValue = _defaultAudioMixer;
                serialized.FindProperty("_previewLight").objectReferenceValue = previewLight;
                serialized.FindProperty("_previewRenderer").objectReferenceValue = previewObject.GetComponent<Renderer>();
                if (overlayType != null)
                {
                    Component overlayComponent = runner.GetComponent(overlayType);
                    if (overlayComponent != null)
                    {
                        serialized.FindProperty("_overlay").objectReferenceValue = overlayComponent;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError("[ExamplePlayableSceneBuilder] Could not resolve Example_SettingsKit from script asset.");
            }

            AddGuide("SettingsKit Playable",
                "演示 SettingsKit 的完整链路：设置定义、存储、即时应用、页面扩展、策略解耦，以及可替换 UI。样例默认使用 OnGUI 版 SettingsMenuOverlay 作为参考实现。",
                "F10: 打开或关闭设置菜单\n1: 播放 UI_Click 测试音效\n2: 播放 Explosion 3D 测试音效\nF5: 保存当前设置\nF9: 恢复全部默认设置",
                "场景会自动初始化 AudioKit、安装默认的 Gameplay / Audio / Graphics / Input / Language 页面，并追加一个 Example 扩展页。默认 AudioMixer 与测试音频资源由构建器自动补齐。",
                "修改语言、键位、音量、画面和扩展设置后，可以立即看到中心准星、底部字幕、右上角状态面板和场景预览物体的变化。保存后再次进入场景会读回 PlayerPrefs 中的设置值。",
                "这个样例刻意不依赖 UIKit。若你的项目已有正式 UI，只需要复用 SettingsKit 的定义层、存储层和 ApplyStrategy，即可替换掉当前的 OnGUI 外观。");

            SaveScene(scene, "SettingsKit_Playable");
        }

        private static void BuildFsmKitScene()
        {
            Scene scene = NewScene("FSMKit_Playable");
            CreateGround("Ground", Vector3.zero, new Vector3(12f, 0.1f, 12f), _greenMaterial);

            GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actor.name = "FSMActor";
            actor.transform.position = new Vector3(0f, 1f, 0f);
            actor.GetComponent<Renderer>().sharedMaterial = _redMaterial;
            Animator animator = actor.AddComponent<Animator>();
            animator.runtimeAnimatorController = _fsmController;

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "EnemyTarget";
            target.transform.position = new Vector3(3.5f, 0.8f, 0f);
            target.GetComponent<Renderer>().sharedMaterial = _blueMaterial;
            ExamplePingPongMover mover = target.AddComponent<ExamplePingPongMover>();
            mover.localOffset = new Vector3(5f, 0f, 0f);
            mover.speed = 0.45f;

            ExampleFsmActorService service = actor.AddComponent<ExampleFsmActorService>();
            service.EnemyTarget = target.transform;
            actor.AddComponent<ExampleFsmActorView>();

            AddGuide("FSMKit Playable",
                "演示 MSV 思路下的轻量状态机：Service 负责逻辑，Model 分发表现请求，View 驱动 Animator。",
                "无需按键。观察目标物靠近与远离时，FSMActor 在 Idle / Chase 两个状态间切换。",
                "场景已自动生成一个最小 AnimatorController，包含 Idle 与 Run 两个状态，满足 Example_FSMKit 的状态哈希要求。",
                "当 EnemyTarget 靠近 5 米内时，FSMActor 会进入 Chase 并向目标移动；距离重新拉开后回到 Idle。",
                "这个 Animator 只是最小验证资产，真实项目请替换为正式角色动画。");

            SaveScene(scene, "FSMKit_Playable");
        }

        private static void BuildHotUpdateKitScene()
        {
            Scene scene = NewScene("HotUpdateKit_Playable");
            GameObject runner = new GameObject("Example_HotUpdateKit_Runner");
            runner.AddComponent<Example_HotUpdateKit>();

            AddGuide("HotUpdateKit Playable",
                "演示 HybridCLRHook 当前状态读取、入口配置打印，以及基于 TextAsset 的热更加载链路验证。",
                "运行后使用 OnGUI 面板：\n打印当前配置\n尝试用 TextAsset 验证装载链路",
                "该场景不会伪造热更 DLL。若要走完整流程，你仍需手动提供 HotUpdate DLL 与 AOT 元数据 TextAsset。",
                "在未提供 DLL 的情况下，场景会明确告诉你当前只能验证配置与入口状态展示。",
                "这是接入辅助示例，不代表完整热更发布流程。",
                true);

            SaveScene(scene, "HotUpdateKit_Playable");
        }

        private static void BuildHttpKitScene()
        {
            Scene scene = NewScene("HttpKit_Playable");
            Canvas canvas = CreateScreenCanvas("HttpCanvas");
            GameObject avatarRoot = CreateUiPanel(canvas.transform, "AvatarFrame", new Vector2(180f, 180f),
                new Color(0.16f, 0.20f, 0.28f, 0.9f));
            avatarRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(270f, 140f);
            Image avatar = avatarRoot.GetComponent<Image>();
            avatar.color = new Color(0.84f, 0.88f, 0.94f, 1f);

            GameObject bannerRoot = CreateUiPanel(canvas.transform, "BannerFrame", new Vector2(360f, 140f),
                new Color(0.16f, 0.20f, 0.28f, 0.9f));
            bannerRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(180f, -70f);
            Object.DestroyImmediate(bannerRoot.GetComponent<Image>());
            RawImage banner = bannerRoot.AddComponent<RawImage>();
            banner.color = new Color(0.30f, 0.34f, 0.42f, 1f);

            GameObject runner = new GameObject("Example_HttpKit_Runner");
            Example_HttpKit example = runner.AddComponent<Example_HttpKit>();
            example.userAvatarImage = avatar;
            example.bannerRawImage = banner;
            example.apiBaseUrl = "https://dummyjson.com";

            AddGuide("HttpKit Playable",
                "演示 POST 登录、Token 注入、GET 资料拉取、图片下载、大文件下载与请求取消入口。",
                "进入场景后会自动执行登录流程。\n默认测试地址为 DummyJSON，可直接验证。\n若需要调用补丁下载，请从外部脚本或按钮调用 DownloadGamePatchAsync。",
                "当前场景默认使用 https://dummyjson.com 的 auth/login 与 auth/me 公开测试接口，并通过其动态图片接口加载 banner。",
                "当接口可达时，头像和横幅会自动填充；失败时会在 Console 中输出精确错误。",
                "如果你要改成自己的服务，只需替换 apiBaseUrl 与返回结构。",
                true);

            SaveScene(scene, "HttpKit_Playable");
        }

        private static void BuildLogKitScene()
        {
            Scene scene = NewScene("LogKit_Playable");
            GameObject runner = new GameObject("Example_LogKit_Runner");
            runner.AddComponent<Example_LogKit>();

            AddGuide("LogKit Playable",
                "演示日志分级输出、耗时测量与内存快照打印。",
                "Space: 触发一次参数为空的业务逻辑，观察精准错误日志\nG: 强制 GC 并重新打印内存信息",
                "无额外依赖。直接运行即可。",
                "进入场景后会自动输出基础日志、性能测量结果和内存使用情况。按键会继续追加验证日志。",
                "Force GC 只适合在黑屏切场或受控节点使用，不应在常规帧循环里滥用。");

            SaveScene(scene, "LogKit_Playable");
        }

        private static void BuildPoolKitScene()
        {
            Scene scene = NewScene("PoolKit_Playable");
            CreateGround("Ground", Vector3.zero, new Vector3(16f, 0.1f, 16f), _greenMaterial);

            GameObject spawn = new GameObject("Example_PoolKit_Runner");
            spawn.transform.position = new Vector3(0f, 1f, -3f);
            Example_PoolKit example = spawn.AddComponent<Example_PoolKit>();
            SerializedObject serialized = new SerializedObject(example);
            serialized.FindProperty("_bulletPrefab").objectReferenceValue = _bulletPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddGuide("PoolKit Playable",
                "演示纯 C# 对象池与 GameObject 工厂对象池的双通道使用方式。",
                "Space: 分配并回收一个 PlayerMoveMsg\nF: 从对象池取出一枚 Bullet\nR: 批量回收当前所有 Bullet",
                "场景已自动生成一个最小 Bullet 预制体，并为其附加简单前进表现，便于观察分配与回收。",
                "按 Space 会打印一次消息对象的借出与归还。按 F 会不断出池子弹；按 R 会将当前激活子弹全部回收。",
                "这个 Bullet Prefab 只是池化验证用资产，真实项目要自行补充命中、生命周期与回收条件。");

            SaveScene(scene, "PoolKit_Playable");
        }

        private static void BuildResKitScene()
        {
            Scene scene = NewScene("ResKit_Playable");
            GameObject runner = new GameObject("Example_ResKit_Runner");
            runner.AddComponent<Example_ResKit>();

            AddGuide("ResKit Playable",
                "演示 Resources、AssetBundle、Addressables 与自定义 RawTextLoader 的统一加载入口。",
                "运行后使用 OnGUI 面板：\n1. 初始化 AB 管理器\n2. 加载 AssetBundle 资源\n3. 加载 Addressables 资源\n4. 加载 Resources 资源\n5. 加载 RawText\n6. 清理实例和加载器",
                "构建器已补齐 Example_ResKit 目录中的 Resources / AssetBundle / Addressables 源资源，并在 StreamingAssets/StellarFramework/Samples/KitSamples/Example_ResKit/ 下生成 RawText 测试文件。\nAddressables 仍取决于工程是否安装了对应包。",
                "Resources、RawText 与 AB 测试可直接验证。AA 仍需要你先安装并构建 Addressables。",
                "该示例强调真实物理构建环境，而不是 Editor 下的假加载。",
                true);

            SaveScene(scene, "ResKit_Playable");
        }

        private static void BuildSingletonKitScene()
        {
            Scene scene = NewScene("SingletonKit_Playable");
            GameObject director = new GameObject("LevelDirector");
            director.AddComponent<ExampleSceneLevelDirector>();

            GameObject runner = new GameObject("Example_SingletonKit_Runner");
            runner.AddComponent<Example_SingletonKit>();

            AddGuide("SingletonKit Playable",
                "演示全局 MonoSingleton、场景 MonoSingleton 与纯 C# Singleton 三种生命周期策略。",
                "无按键操作。直接运行后观察 Console。",
                "场景已经预先挂好 LevelDirector，因此 Scene 单例访问不会触发缺失报错。",
                "运行后会自动创建 GlobalNetworkManager、初始化 LevelDirector，并打印纯 C# 单例计算伤害结果。",
                "如果你移除了场景里的 LevelDirector，再访问其 Instance，就会看到框架故意给出的精准错误日志。");

            SaveScene(scene, "SingletonKit_Playable");
        }

        private static void BuildUIKitScene()
        {
            Scene scene = NewScene("UIKit_Playable");
            GameObject runner = new GameObject("Example_UIKit_Runner");
            runner.AddComponent<Example_UIKit>();

            AddGuide("UIKit Playable",
                "演示 UIKit 的异步初始化、UIRoot 自动加载、强类型面板数据传入与面板打开流程。",
                "进入场景后会自动执行 UIKit.InitAsync() 并尝试打开 ExamplePanel。\n点击面板上的确认按钮会关闭该面板。",
                "构建器已补齐 Resources/UIPanel/ExamplePanel.prefab，并复用现有 UIRoot.prefab。",
                "若场景运行正常，会自动弹出 ExamplePanel，显示标题与奖励数量，并能响应关闭按钮。",
                "该场景验证的是 UIKit 接线方式，不涉及复杂导航栈和多层 UI 业务。");

            SaveScene(scene, "UIKit_Playable");
        }

        private static Scene NewScene(string sceneName)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject light = new GameObject("Directional Light");
            Light directionalLight = light.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            GameObject camera = new GameObject("Main Camera");
            Camera mainCamera = camera.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.11f, 0.13f, 0.17f);
            camera.transform.position = new Vector3(0f, 4.2f, -9f);
            camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            return scene;
        }

        private static void SaveScene(Scene scene, string fileName)
        {
            EditorSceneManager.SaveScene(scene, $"{ScenesFolder}/{fileName}.unity");
        }

        private static void AddGuide(string title, string summary, string controls, string setup, string expected,
            string notes, bool anchorRight = false)
        {
            GameObject guideObject = new GameObject("ExampleSceneGuide");
            ExampleSceneGuide guide = guideObject.AddComponent<ExampleSceneGuide>();
            guide.title = title;
            guide.summary = summary;
            guide.controls = controls;
            guide.setup = setup;
            guide.expected = expected;
            guide.notes = notes;
            guide.anchorRight = anchorRight;
            guide.area = new Rect(12f, 12f, 470f, 690f);
        }

        private static GameObject CreateGround(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.transform.position = position;
            ground.transform.localScale = scale;
            ground.GetComponent<Renderer>().sharedMaterial = material;
            return ground;
        }

        private static Canvas CreateScreenCanvas(string name)
        {
            GameObject canvasObject = new GameObject(name);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreateUiPanel(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return panel;
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

        private static void EnsureExamplePanelPrefab()
        {
            const string path = GeneratedUiFolder + "/ExamplePanel.prefab";
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
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

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
