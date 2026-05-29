#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public enum QuickStartActionKind
    {
        BuildSamples,
        OpenScene,
        OpenDoc,
        ValidateEnvironment
    }

    [Serializable]
    public sealed class QuickStartEntry
    {
        public string Title;
        public string Description;
        public QuickStartActionKind ActionKind;
        public string TargetPath;
        public string Group;
        public int Order;
    }

    public static class FrameworkQuickStartCatalog
    {
        public const string FrameworkValidationScenePath =
            "Assets/StellarFramework/Samples/KitSamples/Scenes/FrameworkValidation_Playable.unity";

        public const string UIKitScenePath =
            "Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity";

        public const string ResKitScenePath =
            "Assets/StellarFramework/Samples/KitSamples/Scenes/ResKit_Playable.unity";

        public const string QuickStartDocPath = "Assets/StellarFramework/快速开始.md";
        public const string SamplesIndexDocPath = "Assets/StellarFramework/Samples/KitSamples/Samples_Index.md";
        public const string UIKitGuidePath = "Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-Guide.md";
        public const string ResKitGuidePath = "Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-Guide.md";
        public const string HotUpdateGuidePath =
            "Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md";

        public static IReadOnlyList<QuickStartEntry> BuildDefaultEntries()
        {
            return new[]
            {
                new QuickStartEntry
                {
                    Title = "1. 构建样例",
                    Description = "生成 KitSamples 场景、UIRoot、示例资源和验证入口。",
                    ActionKind = QuickStartActionKind.BuildSamples,
                    Group = "30 分钟上手",
                    Order = 0
                },
                new QuickStartEntry
                {
                    Title = "2. 打开 FrameworkValidation",
                    Description = "先跑总验证场景，确认 ResKit / UIKit / HotUpdateKit 主链路可用。",
                    ActionKind = QuickStartActionKind.OpenScene,
                    TargetPath = FrameworkValidationScenePath,
                    Group = "30 分钟上手",
                    Order = 1
                },
                new QuickStartEntry
                {
                    Title = "3. 打开 UIKit_Playable",
                    Description = "学习唯一 UI 门户：OpenAsync / PushAsync / Pop / Close / ClearStack。",
                    ActionKind = QuickStartActionKind.OpenScene,
                    TargetPath = UIKitScenePath,
                    Group = "30 分钟上手",
                    Order = 2
                },
                new QuickStartEntry
                {
                    Title = "4. 打开 ResKit_Playable",
                    Description = "学习统一资源门户，以及 Resources / AA / AB 的适用边界。",
                    ActionKind = QuickStartActionKind.OpenScene,
                    TargetPath = ResKitScenePath,
                    Group = "30 分钟上手",
                    Order = 3
                },
                new QuickStartEntry
                {
                    Title = "环境检查",
                    Description = "检查样例资源、Addressables、AB 产物和 HybridCLR 开关是否就绪。",
                    ActionKind = QuickStartActionKind.ValidateEnvironment,
                    Group = "常用入口",
                    Order = 10
                },
                new QuickStartEntry
                {
                    Title = "快速开始",
                    Description = "打开快速开始文档，按业务场景复制模板跑通框架。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = QuickStartDocPath,
                    Group = "常用入口",
                    Order = 11
                },
                new QuickStartEntry
                {
                    Title = "样例索引",
                    Description = "查看所有 Playable 场景、前置条件与验收顺序。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = SamplesIndexDocPath,
                    Group = "常用入口",
                    Order = 12
                },
                new QuickStartEntry
                {
                    Title = "UIKit Guide",
                    Description = "唯一 UI 门户、堆栈能力、自动绑定与排错。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = UIKitGuidePath,
                    Group = "常用入口",
                    Order = 13
                },
                new QuickStartEntry
                {
                    Title = "ResKit Guide",
                    Description = "统一资源门户与后端选择规则。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = ResKitGuidePath,
                    Group = "常用入口",
                    Order = 14
                },
                new QuickStartEntry
                {
                    Title = "HotUpdateKit Guide",
                    Description = "资源热更门户、代码热更门户与 HybridCLR 接线。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = HotUpdateGuidePath,
                    Group = "常用入口",
                    Order = 15
                }
            };
        }
    }

    [StellarTool("Quick Start", "Start Here", -1000)]
    public sealed class QuickStartHubModule : ToolModule
    {
        private sealed class EnvironmentCheckResult
        {
            public string Name;
            public bool Passed;
            public string Details;
        }

        private readonly List<QuickStartEntry> _entries = new List<QuickStartEntry>();
        private readonly List<EnvironmentCheckResult> _checks = new List<EnvironmentCheckResult>();

        public override string Icon => "d_UnityEditor.ConsoleWindow";
        public override string Description => "新人第一入口：构建样例、打开主链路场景、查看推荐路线并检查环境。";

        public override void OnEnable()
        {
            _entries.Clear();
            _entries.AddRange(FrameworkQuickStartCatalog.BuildDefaultEntries().OrderBy(entry => entry.Order));
            RefreshEnvironmentChecks();
        }

        public override void OnGUI()
        {
            Section("30 分钟上手");
            DrawGroupedEntries("30 分钟上手");

            Section("官方推荐路线");
            EditorGUILayout.HelpBox(
                "本地轻量资源：Resources\n" +
                "生产资源热更：Addressables\n" +
                "显式包资源 / 既有打包管线：AssetBundle\n" +
                "第三方资源系统：Custom Loader\n" +
                "UI 唯一入口：UIKit\n" +
                "代码热更：HotUpdateKit + HybridCLR startup-only",
                MessageType.Info);

            Section("环境检查");
            DrawEnvironmentChecks();

            Section("常用入口");
            DrawGroupedEntries("常用入口");
        }

        private void DrawGroupedEntries(string group)
        {
            foreach (QuickStartEntry entry in _entries.Where(item => string.Equals(item.Group, group, StringComparison.Ordinal)))
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(entry.Title, EditorStyles.boldLabel);
                    GUILayout.Label(entry.Description, EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(4f);

                    if (PrimaryButton(GetActionLabel(entry.ActionKind), GUILayout.Height(28)))
                    {
                        ExecuteEntry(entry);
                    }
                }
            }
        }

        private void DrawEnvironmentChecks()
        {
            if (PrimaryButton("刷新环境检查", GUILayout.Height(28)))
            {
                RefreshEnvironmentChecks();
            }

            GUILayout.Space(6f);

            foreach (EnvironmentCheckResult check in _checks)
            {
                MessageType messageType = check.Passed ? MessageType.Info : MessageType.Warning;
                string status = check.Passed ? "通过" : "待处理";
                EditorGUILayout.HelpBox($"[{status}] {check.Name}\n{check.Details}", messageType);
            }
        }

        private void ExecuteEntry(QuickStartEntry entry)
        {
            switch (entry.ActionKind)
            {
                case QuickStartActionKind.BuildSamples:
                    if (!TryInvokeSampleSceneBuilder(out string error))
                    {
                        Debug.LogError(error);
                        Window.ShowNotification(new GUIContent("样例构建器不可用"));
                        return;
                    }

                    RefreshEnvironmentChecks();
                    Window.ShowNotification(new GUIContent("KitSamples 构建完成"));
                    return;

                case QuickStartActionKind.OpenScene:
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        return;
                    }

                    if (!File.Exists(ToAbsoluteProjectPath(entry.TargetPath)))
                    {
                        Debug.LogError($"[QuickStart] 找不到场景: {entry.TargetPath}");
                        return;
                    }

                    EditorSceneManager.OpenScene(entry.TargetPath, OpenSceneMode.Single);
                    return;

                case QuickStartActionKind.OpenDoc:
                    UnityEngine.Object docAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.TargetPath);
                    if (docAsset == null)
                    {
                        Debug.LogError($"[QuickStart] 找不到文档: {entry.TargetPath}");
                        return;
                    }

                    Selection.activeObject = docAsset;
                    EditorGUIUtility.PingObject(docAsset);
                    return;

                case QuickStartActionKind.ValidateEnvironment:
                    RefreshEnvironmentChecks();
                    return;
            }
        }

        private void RefreshEnvironmentChecks()
        {
            _checks.Clear();

            AddPathCheck("样例场景已生成",
                FrameworkQuickStartCatalog.FrameworkValidationScenePath,
                "FrameworkValidation_Playable 用于主链路回归与真机前检查。");
            AddPathCheck("UIRoot.prefab 已存在",
                "Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab",
                "UIKit 默认入口依赖这个 UIRoot。可通过样例构建器或 UIKit 工具重新生成。");
            AddPathCheck("ExamplePanel.prefab 已存在",
                "Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab",
                "UIKit_Playable 与自动绑定示例都会用到这个 Panel。");

            ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();
            bool hasRuntimeSettingsAsset = Resources.Load<ResKitRuntimeSettings>(ResKitRuntimeSettings.DefaultResourcesPath) != null;
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "ResKitRuntimeSettings 可读取",
                Passed = settings != null,
                Details = hasRuntimeSettingsAsset
                    ? "已找到 Resources/ResKitRuntimeSettings.asset，可直接驱动 ResKit / HotUpdateKit 默认配置。"
                    : "当前使用运行时默认值。建议创建 Resources/ResKitRuntimeSettings.asset 固化资源与热更配置。"
            });

            bool addressablesAvailable = Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables") != null;
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "Addressables 包状态",
                Passed = addressablesAvailable,
                Details = addressablesAvailable
                    ? "已检测到 Unity.Addressables。AA 的模拟、Build 与 Content Update 请使用官方 Groups / Profiles / Build。"
                    : "未检测到 Unity.Addressables。AA 相关入口会返回不可用提示。"
            });

            string abPlatformFolder = GetCurrentAssetBundleOutputPath();
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "AssetBundle 产物目录",
                Passed = Directory.Exists(ToAbsoluteProjectPath(abPlatformFolder)),
                Details = Directory.Exists(ToAbsoluteProjectPath(abPlatformFolder))
                    ? $"已检测到 {abPlatformFolder}。可直接验证 ResKit 的 AB 链路。"
                    : $"尚未检测到 {abPlatformFolder}。需要通过 Tools Hub 的 AssetBundle 构建模块生成产物。"
            });

            bool hybridClrEnabled = IsScriptingDefineEnabled("HYBRIDCLR_ENABLE");
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "HybridCLR 开关状态",
                Passed = true,
                Details = hybridClrEnabled
                    ? "HYBRIDCLR_ENABLE 已开启。HotUpdateKit 可进入启动期代码热更链路。"
                    : "HYBRIDCLR_ENABLE 未开启。HotUpdateKit 仍可编译运行，但代码热更入口会返回明确不可用提示。"
            });
        }

        private void AddPathCheck(string name, string assetPath, string details)
        {
            bool exists = File.Exists(ToAbsoluteProjectPath(assetPath));
            _checks.Add(new EnvironmentCheckResult
            {
                Name = name,
                Passed = exists,
                Details = exists
                    ? $"{assetPath}\n{details}"
                    : $"{assetPath}\n缺失时请先运行 Quick Start 的“构建样例”或对应工具补齐。"
            });
        }

        private static string GetActionLabel(QuickStartActionKind actionKind)
        {
            switch (actionKind)
            {
                case QuickStartActionKind.BuildSamples:
                    return "立即构建";
                case QuickStartActionKind.OpenScene:
                    return "打开场景";
                case QuickStartActionKind.OpenDoc:
                    return "定位文档";
                case QuickStartActionKind.ValidateEnvironment:
                    return "刷新检查";
                default:
                    return "执行";
            }
        }

        private static string GetCurrentAssetBundleOutputPath()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string platformFolder;
            switch (target)
            {
                case BuildTarget.Android:
                    platformFolder = "Android";
                    break;
                case BuildTarget.iOS:
                    platformFolder = "iOS";
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    platformFolder = "Windows";
                    break;
                case BuildTarget.StandaloneOSX:
                    platformFolder = "OSX";
                    break;
                case BuildTarget.WebGL:
                    platformFolder = "WebGL";
                    break;
                default:
                    platformFolder = "Unknown";
                    break;
            }

            return $"Assets/StreamingAssets/AssetBundles/{platformFolder}";
        }

        private static bool IsScriptingDefineEnabled(string define)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(item => string.Equals(item.Trim(), define, StringComparison.Ordinal));
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string normalizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalizedPath);
        }

        private static bool TryInvokeSampleSceneBuilder(out string error)
        {
            error = null;

            Assembly sampleAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "StellarFramework.Samples.Editor");
            if (sampleAssembly == null)
            {
                error = "[QuickStart] 找不到程序集 StellarFramework.Samples.Editor。";
                return false;
            }

            Type builderType = sampleAssembly.GetType("StellarFramework.Editor.ExamplePlayableSceneBuilder");
            MethodInfo buildMethod = builderType?.GetMethod("BuildPlayableScenes", BindingFlags.Public | BindingFlags.Static);
            if (buildMethod == null)
            {
                error = "[QuickStart] 找不到 ExamplePlayableSceneBuilder.BuildPlayableScenes()。";
                return false;
            }

            buildMethod.Invoke(null, null);
            return true;
        }
    }
}
#endif

