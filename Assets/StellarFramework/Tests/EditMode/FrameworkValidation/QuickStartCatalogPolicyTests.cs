using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class QuickStartCatalogPolicyTests
    {
        [Test]
        public void QuickStartModuleDefinesHappyPathInFixedOrder()
        {
            string source = ReadQuickStartSource();

            Assert.That(source, Does.Contain("Title = \"1. 构建样例\""));
            Assert.That(source, Does.Contain("UIKitScenePath"));
            Assert.That(source, Does.Contain("ResKitScenePath"));
            Assert.That(source, Does.Not.Contain("Title = \"2. 打开 FrameworkValidation\""));
        }

        [Test]
        public void QuickStartModuleDefinesWelcomePortalEntry()
        {
            string source = ReadQuickStartSource();

            Assert.That(source, Does.Contain("欢迎使用 StellarFramework"));
            Assert.That(source, Does.Contain("进入 30 分钟上手"));
        }

        [Test]
        public void QuickStartBuildSamplesIsQueuedOutsideOnGui()
        {
            string source = ReadQuickStartSource();

            Assert.That(source, Does.Contain("QueueSampleBuild()"));
            Assert.That(source, Does.Contain("EditorApplication.delayCall"));
            Assert.That(source, Does.Contain("_sampleBuildQueued"));
            Assert.That(source, Does.Contain("_sampleBuildRunning"));
        }

        [Test]
        public void QuickStartEnvironmentChecksStayReadOnly()
        {
            string source = ReadQuickStartSource();

            Assert.That(source, Does.Not.Contain("ResKitRuntimeSettings.LoadOrCreateDefault()"));
            Assert.That(source, Does.Not.Contain("UIKitSettings.LoadOrCreateDefault()"));
            Assert.That(source, Does.Not.Contain("HotUpdateSettings.LoadOrCreateDefault()"));
            Assert.That(source, Does.Not.Contain("Resources.Load<ResKitRuntimeSettings>"));
            Assert.That(source, Does.Not.Contain("Resources.Load(\"HotUpdateSettings\""));
            Assert.That(source, Does.Contain("AssetDatabase.FindAssets"));
        }

        [Test]
        public void QuickStartWelcomePortalDoesNotAutoRefreshEnvironmentChecks()
        {
            string source = ReadQuickStartSource();
            int methodStart = source.IndexOf("private void DrawWelcomePortal()", System.StringComparison.Ordinal);
            int nextMethod = source.IndexOf("private void DrawGroupedEntries", methodStart, System.StringComparison.Ordinal);
            string methodSource = source.Substring(methodStart, nextMethod - methodStart);

            Assert.That(methodSource, Does.Not.Contain("QueueEnvironmentCheckRefresh()"));
            Assert.That(methodSource, Does.Contain("_showWelcomePortal = false;"));
        }

        [Test]
        public void ToolsHubDefinesExplicitPreferredGroupOrder()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Core/StellarFrameworkTools.cs");

            Assert.That(source, Does.Contain("PreferredGroupOrder"));
            AssertInOrder(
                source,
                "\"Start Here\"",
                "\"资源管理\"",
                "\"框架核心\"",
                "\"热更新\"",
                "\"样例支持\"",
                "\"生产力\"",
                "\"常用工具\"");
        }

        [Test]
        public void ToolsHubHostUsesUiToolkitCreateGui()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Core/StellarFrameworkTools.cs");

            Assert.That(source, Does.Contain("CreateGUI()"));
            Assert.That(source, Does.Contain("rootVisualElement"));
            Assert.That(source, Does.Contain("TwoPaneSplitView"));
            Assert.That(source, Does.Contain("ToolbarSearchField"));
        }

        [Test]
        public void ResourceManagementModulesUseFixedOrderWeights()
        {
            string assetBundleSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs");
            string addressablesSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs");
            string resKitSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ResKit/ResKitAuditHubModule.cs");

            Assert.That(assetBundleSource, Does.Contain("[StellarTool(\"资源打包 (AssetBundle)\", \"资源管理\", 0)]"));
            Assert.That(addressablesSource, Does.Contain("[StellarTool(\"AA 配置与发布\", \"资源管理\", 1)]"));
            Assert.That(resKitSource, Does.Contain("[StellarTool(\"ResKit 资源审计\", \"资源管理\", 2)]"));
        }

        [Test]
        public void AddressablesToolDisplaysCompatibilityStatusInUi()
        {
            string addressablesSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs");

            Assert.That(addressablesSource, Does.Contain("AAWorkflowCompatibilityStatus"));
            Assert.That(addressablesSource, Does.Contain("CompatibilityLabel"));
            Assert.That(addressablesSource, Does.Contain("CompatibilityDetail"));
            Assert.That(addressablesSource, Does.Contain("兼容性"));
            Assert.That(addressablesSource, Does.Contain("当前组合已通过框架兼容矩阵"));
        }

        [Test]
        public void QuickStartReferencedPathsExistOnDisk()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Scenes/ResKit_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/快速开始.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Samples_Index.md")), Is.True);
            AssertDualTrackKitDocsExist();
            AssertNonKitSourceDocsExist();
        }

        [Test]
        public void DocumentationDoesNotContainOutdatedAAWorkflowGuidance()
        {
            string docs = ReadFrameworkMarkdown();

            Assert.That(docs, Does.Not.Contain("Tools Hub 不重复实现 AA 打包窗口"));
            Assert.That(docs, Does.Not.Contain("StellarFramework 不在 ToolHub 中提供 AA"));
            Assert.That(docs, Does.Not.Contain("RegisterCustomLoader(\"YooAsset\", () =>"));
        }

        [Test]
        public void ToolsHubUserGuideCoversCoreToolsAndHotUpdateButtons()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md");

            Assert.That(source, Does.Contain("Quick Start"));
            Assert.That(source, Does.Contain("文档中心"));
            Assert.That(source, Does.Contain("AA 配置与发布"));
            Assert.That(source, Does.Contain("一键本地内置构建"));
            Assert.That(source, Does.Contain("一键远端热更发布"));
            Assert.That(source, Does.Contain("HybridCLR DLL 导出"));
            Assert.That(source, Does.Contain("资源打包 (AssetBundle)"));
            Assert.That(source, Does.Contain("ResKit 资源审计"));
        }

        [Test]
        public void ToolsHubUserGuideDocumentsSidebarGroupOrderAndQuickStartPortal()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md");

            Assert.That(source, Does.Contain("左侧分组固定顺序"));
            AssertInOrder(
                source,
                "`Start Here`",
                "`资源管理`",
                "`框架核心`",
                "`热更新`",
                "`样例支持`",
                "`生产力`",
                "`常用工具`");
            AssertInOrder(
                source,
                "`资源打包 (AssetBundle)`",
                "`AA 配置与发布`",
                "`ResKit 资源审计`");
            Assert.That(source, Does.Contain("欢迎使用 StellarFramework"));
            Assert.That(source, Does.Contain("进入 30 分钟上手"));
            Assert.That(source, Does.Contain("返回欢迎页"));
        }

        [Test]
        public void ToolsHubGroupsMoveAssetPipelinesIntoResourceManagement()
        {
            string assetBundleSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs");
            string addressablesSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs");
            string resKitSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ResKit/ResKitAuditHubModule.cs");
            string hybridClrSource = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/HybridCLRHotUpdateAssetExporter.cs");

            Assert.That(assetBundleSource, Does.Contain("[StellarTool(\"资源打包 (AssetBundle)\", \"资源管理\""));
            Assert.That(addressablesSource, Does.Contain("[StellarTool(\"AA 配置与发布\", \"资源管理\""));
            Assert.That(resKitSource, Does.Contain("[StellarTool(\"ResKit 资源审计\", \"资源管理\""));
            Assert.That(hybridClrSource, Does.Contain("[StellarTool(\"HybridCLR DLL 导出\", \"热更新\""));
        }

        [Test]
        public void AssetBundleToolDefinesInitializationGate()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs");

            Assert.That(source, Does.Contain("初始化AB"));
            Assert.That(source, Does.Contain("AssetMap"));
            Assert.That(source, Does.Contain("TestCapsule_AB.prefab"));
            Assert.That(source, Does.Contain("StreamingAssets/AssetBundles"));
            Assert.That(source, Does.Contain("HasManifestBundle"));
            Assert.That(source, Does.Contain("当前平台 AssetBundle Manifest"));
            Assert.That(source, Does.Contain("BuildBundles(revealInFinder: false, showDialogOnFailure: false)"));
            Assert.That(source, Does.Contain("已构建默认 AssetBundle 和当前平台 Manifest"));
        }

        [Test]
        public void DocumentationHubGroupsDocumentsByAudienceAndPurpose()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DocumentationHubModule.cs");

            Assert.That(source, Does.Contain("快速开始和 README"));
            Assert.That(source, Does.Contain("Kit 说明文档"));
            Assert.That(source, Does.Contain("Kit 源码文档"));
            Assert.That(source, Does.Contain("架构/Runtime 源码文档"));
            Assert.That(source, Does.Contain("ToolsHub 文档"));
            Assert.That(source, Does.Contain("Samples/Tests/Generated/Resources 文档"));
        }

        [Test]
        public void SourceGuideCoversMainSourceReadingRoutes()
        {
            string source = ReadAssetText("Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-源码文档-Guide.md")
                + ReadAssetText("Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-源码文档-Guide.md")
                + ReadAssetText("Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-源码文档-Guide.md")
                + ReadAssetText("Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-源码文档-Guide.md")
                + ReadAssetText("Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构源码文档-Guide.md")
                + ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md");

            Assert.That(source, Does.Contain("Architecture<T>"));
            Assert.That(source, Does.Contain("ResKit"));
            Assert.That(source, Does.Contain("IResLoader"));
            Assert.That(source, Does.Contain("ResLoader"));
            Assert.That(source, Does.Contain("AddressableHotUpdateManager"));
            Assert.That(source, Does.Contain("HotUpdateKit"));
            Assert.That(source, Does.Contain("HotUpdateManifest"));
            Assert.That(source, Does.Contain("UIKit"));
            Assert.That(source, Does.Contain("SettingsKit"));
            Assert.That(source, Does.Contain("ToolModule"));
            Assert.That(source, Does.Contain("StellarToolAttribute"));
        }

        [Test]
        public void SourceGuidesUseSourceLevelStandardSections()
        {
            string[] sourceGuidePaths =
            {
                "Assets/StellarFramework/Runtime/Kits/ActionKit/ActionKit-动作系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/AudioKit/AudioKit-音频系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/BindableKit/BindableKit-数据绑定-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/ConfigKit/ConfigKit-配置系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/EventKit/EventKit-事件系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/FSMKit/FSMKit-状态机-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/HttpKit/HttpKit-网络请求-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/LogKit/LogKit-PerformanceKit-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKit-对象池-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonKit-单例系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构源码文档-Guide.md",
                "Assets/StellarFramework/Runtime/Extensions/RuntimeExtensions-源码文档-Guide.md",
                "Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md",
                "Assets/StellarFramework/Samples/Samples-源码文档-Guide.md",
                "Assets/StellarFramework/Tests/Tests-源码文档-Guide.md",
                "Assets/StellarFramework/Generated/Generated-源码文档-Guide.md",
                "Assets/StellarFramework/Resources/Resources-说明与源码文档-Guide.md",
            };

            foreach (string path in sourceGuidePaths)
            {
                string source = ReadAssetText(path);
                Assert.That(source, Does.Contain("## 源码位置"), path);
                Assert.That(source, Does.Contain("## 核心类型"), path);
                Assert.That(source, Does.Contain("## 关键方法"), path);
                Assert.That(source, Does.Contain("## 数据流"), path);
                Assert.That(source, Does.Contain("## 依赖关系"), path);
                Assert.That(source, Does.Contain("## 扩展点"), path);
                Assert.That(source, Does.Contain("## 测试入口"), path);
            }
        }

        [Test]
        public void QuickStartMarkdownLinksPointToExistingFiles()
        {
            string quickStartPath = ToAbsoluteAssetPath("Assets/StellarFramework/快速开始.md");
            string quickStart = File.ReadAllText(quickStartPath);
            string quickStartDirectory = Path.GetDirectoryName(quickStartPath);

            MatchCollection links = Regex.Matches(quickStart, @"\[[^\]]+\]\(([^)#]+\.md)\)");
            Assert.That(links.Count, Is.GreaterThan(0));

            foreach (Match link in links)
            {
                string relativeLink = link.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                string target = Path.GetFullPath(Path.Combine(quickStartDirectory, relativeLink));
                Assert.That(File.Exists(target), Is.True, $"QuickStart link target is missing: {link.Groups[1].Value}");
            }
        }

        private static string ReadQuickStartSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StellarFramework/Editor/StellarToolsHub/Modules/QuickStartHubModule.cs");
            return File.ReadAllText(path);
        }

        private static void AssertDualTrackKitDocsExist()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/ActionKit/ActionKit-动作系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/ActionKit/ActionKit-动作系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/AudioKit/AudioKit-音频系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/AudioKit/AudioKit-音频系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/BindableKit/BindableKit-数据绑定-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/BindableKit/BindableKit-数据绑定-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/ConfigKit/ConfigKit-配置系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/ConfigKit/ConfigKit-配置系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/EventKit/EventKit-事件系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/EventKit/EventKit-事件系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/FSMKit/FSMKit-状态机-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/FSMKit/FSMKit-状态机-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/HttpKit/HttpKit-网络请求-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/HttpKit/HttpKit-网络请求-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/LogKit/LogKit-PerformanceKit-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/LogKit/LogKit-PerformanceKit-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKit-对象池-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKit-对象池-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonKit-单例系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonKit-单例系统-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-源码文档-Guide.md")), Is.True);
        }

        private static void AssertNonKitSourceDocsExist()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构说明文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Extensions/RuntimeExtensions-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-扩展开发-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/Samples-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Generated/Generated-源码文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Resources/Resources-说明与源码文档-Guide.md")), Is.True);
        }

        private static string ReadFrameworkMarkdown()
        {
            string root = Path.Combine(Application.dataPath, "StellarFramework");
            string combined = string.Empty;
            foreach (string path in Directory.GetFiles(root, "*.md", SearchOption.AllDirectories))
            {
                combined += File.ReadAllText(path);
            }

            return combined;
        }

        private static string ReadAssetText(string assetPath)
        {
            return File.ReadAllText(ToAbsoluteAssetPath(assetPath));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AssertInOrder(string source, params string[] fragments)
        {
            int previousIndex = -1;
            foreach (string fragment in fragments)
            {
                int currentIndex = source.IndexOf(fragment, previousIndex + 1, System.StringComparison.Ordinal);
                Assert.That(currentIndex, Is.GreaterThan(previousIndex), $"Expected fragment order to include {fragment}");
                previousIndex = currentIndex;
            }
        }
    }
}


