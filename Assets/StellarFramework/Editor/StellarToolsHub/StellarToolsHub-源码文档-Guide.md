# StellarToolsHub / 源码文档

ToolsHub 是 StellarFramework 的统一编辑器工具入口。源码阅读重点不是每个按钮的 IMGUI 细节，而是理解工具如何注册、如何分组、如何持久化配置、如何调用构建/发布逻辑。

## 源码位置

- `Editor/StellarToolsHub/StellarFrameworkTools.cs`：主窗口、菜单入口、工具发现和布局。
- `Editor/StellarToolsHub/ToolModule.cs`：所有工具模块的基类。
- `Editor/StellarToolsHub/StellarToolAttribute.cs`：工具自动注册特性。
- `Editor/StellarToolsHub/Modules/DocumentationHubModule.cs`：文档中心。
- `Editor/StellarToolsHub/Modules/QuickStartHubModule.cs`：新人入口和样例构建导航。
- `Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs`：AA 本地内置和远端热更发布闭环。
- `Editor/StellarToolsHub/Modules/HybridCLRHotUpdateAssetExporter.cs`：HybridCLR DLL、metadata、Manifest 导出。
- `Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs`：AB 规则、构建、AssetMap 生成。
- `Editor/StellarToolsHub/Modules/*HubModule.cs`：UIKit、ConfigKit、SettingsKit、AudioKit、EventKit、Samples 等工具模块。

## 核心类型

- `StellarFrameworkTools`：EditorWindow 主体，负责扫描 `ToolModule`、绘制左侧分组、绘制右侧工具内容。
- `ToolModule`：工具模块基类，定义 `Icon`、`Description`、`OnEnable()`、`OnDisable()`、`OnGUI()`。
- `StellarToolAttribute`：声明工具显示名、分组和排序。
- `DocumentationHubModule`：扫描 Markdown，按用途分组，渲染文档。
- `QuickStartHubModule`：提供新手固定路径、样例构建和关键场景入口。
- `AAWorkflowConfig` / `AAWorkflowConfigSet` / `AAWorkflowConfigStore`：AA 工作流配置模型和持久化。
- `AAHotUpdatePublishLogic`：AA 配置、构建、发布、校验的核心逻辑层。
- `AAHotUpdatePublishToolModule`：AA 发布工具的界面层。
- `HybridCLRHotUpdateAssetExporter`：导出热更 DLL、AOT metadata 和 `HotUpdateManifest.json`。
- `AssetBundleToolModule`：AB 构建工具，负责规则应用、构建、清理和 AssetMap 生成。

## 关键方法

- `StellarFrameworkTools.ShowWindow()`：菜单打开 ToolsHub。
- `StellarFrameworkTools.DiscoverModules()`：通过反射查找带 `StellarToolAttribute` 的 `ToolModule` 类型。
- `ToolModule.OnGUI()`：每个工具的绘制入口。
- `DocumentationHubModule.RefreshDocs()`：扫描 `Assets/StellarFramework` 下的 Markdown 文档。
- `DocumentationHubModule.BuildCategory(...)`：根据路径和文件名给文档分类。
- `QuickStartHubModule.OnGUI()`：绘制新人路线和样例入口。
- `AAHotUpdatePublishLogic.RunLocalBuiltInBuild(...)`：本地内置 AA 一键构建流程。
- `AAHotUpdatePublishLogic.RunRemoteHotUpdatePublish(...)`：远端热更 AA 一键发布流程。
- `HybridCLRHotUpdateAssetExporter.Export(...)`：复制 DLL、metadata 并生成 Manifest。
- `AssetBundleToolModule.BuildAssetBundles()`：执行 AB 构建。

## 数据流

1. Unity 菜单打开 `StellarFrameworkTools`。
2. 主窗口通过反射扫描所有 `[StellarTool]` 模块。
3. 模块按 category 和 order 排序，显示在左侧。
4. 选中工具后调用该模块 `OnGUI()`。
5. 工具界面读取配置资产、EditorPrefs 或项目目录。
6. 用户点击按钮后，工具调用独立逻辑类执行构建、发布、生成或校验。
7. 结果写回配置、Generated 目录、StreamingAssets、Samples 或 Console。

## 依赖关系

- 依赖 UnityEditor 和 UnityEngine IMGUI。
- AA 发布工具依赖 Addressables 编辑器 API 和 HotUpdateKit 文档约定。
- HybridCLR 导出工具在启用 HybridCLR 时读取生成目录。
- AB 工具依赖 Unity AssetBundle 构建 API。
- 文档中心只依赖 Markdown 文件扫描和本地渲染，不依赖 Runtime。

## 扩展点

- 新增工具：新增 `ToolModule` 派生类并添加 `[StellarTool]`。
- 新增文档分类：修改 `DocumentationHubModule.BuildCategory(...)`。
- 新增构建流程：把业务逻辑放进独立静态类或服务类，UI 只负责参数和按钮。
- 新增配置：优先使用 ScriptableObject 资产保存团队配置，EditorPrefs 只保存个人偏好。
- 新增生成物：写入 `Generated` 或工具专属目录，并补源码文档和测试。

## 测试入口

- `QuickStartCatalogPolicyTests`：README 链接、双轨文档、文档中心分类、旧 AA 文案防回归。
- `OnboardingSurfacePolicyTests`：新人入口策略。
- `AAHotUpdatePublishToolTests`：AA 工具路径、安全校验和发布逻辑。
- `HotUpdateManifestTests`：Manifest 解析和 BOM 防回归。
- 修改 ToolsHub 后应刷新 Unity，跑相关 EditMode 测试，并手动打开窗口确认分组、按钮和文档中心可读。
