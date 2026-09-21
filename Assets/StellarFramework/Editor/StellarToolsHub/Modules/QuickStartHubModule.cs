#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public enum QuickStartActionKind
    {
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
        public const string ArchitectureDemoScenePath =
            "Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity";

        public const string QuickStartDocPath = "Assets/StellarFramework/FrameworkDoc/00-Overview/快速开始.md";
        public const string FrameworkDocIndexPath = "Assets/StellarFramework/FrameworkDoc/README.md";
        public const string UIKitGuidePath =
            "Assets/StellarFramework/FrameworkDoc/02-Kits/UIKit/UIKit-界面系统-说明文档-Guide.md";
        public const string ResKitGuidePath =
            "Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md";
        public const string HybridCLRGuidePath =
            "Assets/StellarFramework/FrameworkDoc/02-Kits/HybridCLRKit/HybridCLRKit-代码热更新-说明文档-Guide.md";

        public static IReadOnlyList<QuickStartEntry> BuildDefaultEntries()
        {
            return new[]
            {
                new QuickStartEntry
                {
                    Title = "1. 打开 ArchitectureDemo",
                    Description = "运行唯一入门 Demo，观察 MSV、BindableKit、ActionKit、UIKit、LocalizationKit 与 LogKit 如何协作。",
                    ActionKind = QuickStartActionKind.OpenScene,
                    TargetPath = ArchitectureDemoScenePath,
                    Group = "30 分钟上手",
                    Order = 0
                },
                new QuickStartEntry
                {
                    Title = "2. 阅读快速开始",
                    Description = "按业务需求选择 Kit，并通过 Guide 中的最小代码片段接入。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = QuickStartDocPath,
                    Group = "30 分钟上手",
                    Order = 1
                },
                new QuickStartEntry
                {
                    Title = "3. 打开 FrameworkDoc",
                    Description = "查看完整 Kit / Adapter / ToolsHub 文档索引；各 Kit 不再维护独立 Sample 场景。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = FrameworkDocIndexPath,
                    Group = "30 分钟上手",
                    Order = 2
                },
                new QuickStartEntry
                {
                    Title = "环境检查",
                    Description = "检查基础运行资源、AB 产物，以及可选 Addressables / HybridCLR 扩展是否就绪。",
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
                    Title = "UIKit Guide",
                    Description = "唯一 UI 门户、堆栈能力、自动绑定与排错。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = UIKitGuidePath,
                    Group = "常用入口",
                    Order = 12
                },
                new QuickStartEntry
                {
                    Title = "ResKit Guide",
                    Description = "统一资源门户与后端选择规则。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = ResKitGuidePath,
                    Group = "常用入口",
                    Order = 13
                },
                new QuickStartEntry
                {
                    Title = "HybridCLRKit Guide",
                    Description = "可选代码热更扩展：通过 ResKit 读取 Manifest、DLL 与 AOT metadata，再进入 HybridCLR 热更程序集。",
                    ActionKind = QuickStartActionKind.OpenDoc,
                    TargetPath = HybridCLRGuidePath,
                    Group = "常用入口",
                    Order = 14
                }
            };
        }
    }

    [StellarTool("Quick Start", "Start Here", -1000)]
    public sealed class QuickStartHubModule : ToolModule
    {
        private const string WelcomeTitle = "欢迎使用 StellarFramework";
        private const string WelcomeButtonLabel = "进入 30 分钟上手";

        private sealed class EnvironmentCheckResult
        {
            public string Name;
            public bool Passed;
            public string Details;
        }

        private readonly List<QuickStartEntry> _entries = new List<QuickStartEntry>();
        private readonly List<EnvironmentCheckResult> _checks = new List<EnvironmentCheckResult>();
        private bool _showWelcomePortal = true;
        private EditorApplication.CallbackFunction _pendingEnvironmentRefreshAction;

        public override string Icon => "d_UnityEditor.ConsoleWindow";
        public override string Description => "新人第一入口：运行唯一入门 Demo、阅读完整文档并检查开发环境。";

        public override void OnEnable()
        {
            _entries.Clear();
            _entries.AddRange(FrameworkQuickStartCatalog.BuildDefaultEntries().OrderBy(entry => entry.Order));
            _checks.Clear();
            _showWelcomePortal = true;
        }

        public override void OnDisable()
        {
            if (_pendingEnvironmentRefreshAction != null)
            {
                EditorApplication.delayCall -= _pendingEnvironmentRefreshAction;
                _pendingEnvironmentRefreshAction = null;
            }
        }

        public override void OnGUI()
        {
            if (_showWelcomePortal)
            {
                DrawWelcomePortal();
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("返回欢迎页", GUILayout.Width(120), GUILayout.Height(24)))
                {
                    _showWelcomePortal = true;
                    GUI.FocusControl(null);
                    return;
                }
            }

            Section("30 分钟上手");
            DrawGroupedEntries("30 分钟上手");

            Section("官方推荐路线");
            EditorGUILayout.HelpBox(
                "本地轻量资源：Resources\n" +
                "本地/普通资源后端：Addressables\n" +
                "生产内容热更：YooAsset\n" +
                "显式包资源 / 既有打包管线：AssetBundle\n" +
                "第三方资源系统：Custom Loader\n" +
                "UI 唯一入口：UIKit\n" +
                "代码热更：HybridCLRKit（startup-only，可选扩展）",
                MessageType.Info);

            Section("环境检查");
            DrawEnvironmentChecks();

            Section("常用入口");
            DrawGroupedEntries("常用入口");
        }

        private void DrawWelcomePortal()
        {
            GUILayout.FlexibleSpace();

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(720)))
                {
                    GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 24,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                    GUIStyle bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter
                    };

                    GUILayout.Space(20f);
                    GUILayout.Label(WelcomeTitle, titleStyle);
                    GUILayout.Space(10f);
                    GUILayout.Label(
                        "这里是 StellarFramework 的统一上手门户。先运行唯一 ArchitectureDemo 理解框架协作方式，再按 Kit Guide 接入真实项目；不需要维护或生成一组独立 Sample 场景。",
                        bodyStyle);
                    GUILayout.Space(18f);

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (PrimaryButton(WelcomeButtonLabel, GUILayout.Width(280), GUILayout.Height(54)))
                        {
                            _showWelcomePortal = false;
                            GUI.FocusControl(null);
                        }

                        GUILayout.FlexibleSpace();
                    }

                    GUILayout.Space(12f);
                    EditorGUILayout.HelpBox(
                        "建议顺序：打开 ArchitectureDemo -> 阅读快速开始 -> 按需查看 Kit / Adapter Guide。",
                        MessageType.Info);
                    GUILayout.Space(8f);
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
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

                    bool targetMissing = (entry.ActionKind == QuickStartActionKind.OpenScene ||
                                          entry.ActionKind == QuickStartActionKind.OpenDoc) &&
                                         !File.Exists(ToAbsoluteProjectPath(entry.TargetPath));
                    using (new EditorGUI.DisabledScope(targetMissing))
                    {
                        string actionLabel = targetMissing ? "入口缺失" : GetActionLabel(entry.ActionKind);

                        if (PrimaryButton(actionLabel, GUILayout.Height(28)))
                        {
                            ExecuteEntry(entry);
                        }
                    }
                }
            }
        }

        private void DrawEnvironmentChecks()
        {
            if (PrimaryButton("刷新环境检查", GUILayout.Height(28)))
            {
                QueueEnvironmentCheckRefresh();
            }

            GUILayout.Space(6f);

            if (_checks.Count == 0)
            {
                EditorGUILayout.HelpBox("点击“刷新环境检查”或先进入 30 分钟上手后等待一帧，即可加载当前工程的只读环境检查结果。", MessageType.Info);
                return;
            }

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
                    QueueEnvironmentCheckRefresh();
                    return;
            }
        }

        private void QueueEnvironmentCheckRefresh()
        {
            if (_pendingEnvironmentRefreshAction != null)
            {
                return;
            }

            _pendingEnvironmentRefreshAction = () =>
            {
                EditorApplication.delayCall -= _pendingEnvironmentRefreshAction;
                _pendingEnvironmentRefreshAction = null;
                RefreshEnvironmentChecks();
                Window.Repaint();
            };

            EditorApplication.delayCall += _pendingEnvironmentRefreshAction;
        }

        private void RefreshEnvironmentChecks()
        {
            _checks.Clear();

            AddPathCheck("UIRoot.prefab 已存在",
                "Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab",
                "UIKit 默认入口依赖这个 UIRoot。");
            AddPathCheck("ArchitectureDemo 已存在",
                FrameworkQuickStartCatalog.ArchitectureDemoScenePath,
                "仓库唯一入门 Demo，用于观察基础 Kit 的组合方式。");

            string resKitSettingsAssetPath = FindResourcesAssetPath("ResKitRuntimeSettings");
            bool hasRuntimeSettingsAsset = !string.IsNullOrEmpty(resKitSettingsAssetPath);
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "ResKitRuntimeSettings 可读取",
                Passed = hasRuntimeSettingsAsset,
                Details = hasRuntimeSettingsAsset
                    ? $"已找到 {resKitSettingsAssetPath}，可直接驱动 ResKit 默认资源配置。"
                    : "当前未找到 Resources/ResKitRuntimeSettings.asset。请按需创建该配置资产。"
            });

            string hotUpdateSettingsAssetPath = FindResourcesAssetPath("HotUpdateSettings");
            bool hasHotUpdateSettingsAsset = !string.IsNullOrEmpty(hotUpdateSettingsAssetPath);
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "HotUpdateSettings 可读取",
                Passed = true,
                Details = hasHotUpdateSettingsAsset
                    ? $"已找到 {hotUpdateSettingsAssetPath}，用于指定 HybridCLR 的 ResKit 后端、Manifest key 与导出入口配置。"
                    : "当前未找到 HotUpdateSettings 资产。仅影响 HybridCLRKit；不影响基础 ResKit / UIKit 上手。"
            });

            bool addressablesAvailable = Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables") != null;
            _checks.Add(new EnvironmentCheckResult
            {
                Name = "Addressables 包状态",
                Passed = addressablesAvailable,
                Details = addressablesAvailable
                    ? "已检测到 Unity.Addressables。StellarFramework 只把 AA 作为 ResKit Load/Release 后端与本地构建入口。"
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
                    ? "HYBRIDCLR_ENABLE 已开启。HybridCLRKit 可进入启动期代码热更链路。"
                    : "HYBRIDCLR_ENABLE 未开启。HybridCLRKit 仍可编译，但运行代码热更会返回明确不可用结果。"
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
                    : $"{assetPath}\n该入口缺失，请检查工程完整性或重新导入对应框架资产。"
            });
        }

        private static string GetActionLabel(QuickStartActionKind actionKind)
        {
            switch (actionKind)
            {
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

        private static string FindResourcesAssetPath(string assetNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(assetNameWithoutExtension))
            {
                return string.Empty;
            }

            string[] guids = AssetDatabase.FindAssets(assetNameWithoutExtension);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string normalizedPath = assetPath.Replace('\\', '/');
                if (!normalizedPath.Contains("/Resources/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(
                        Path.GetFileNameWithoutExtension(normalizedPath),
                        assetNameWithoutExtension,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return normalizedPath;
            }

            return string.Empty;
        }

    }
}
#endif

