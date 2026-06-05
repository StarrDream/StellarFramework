# HotUpdateKit / 热更新源码文档

## 源码位置

- `Runtime/Kits/HotUpdateKit/HybridCLRHook.cs`：`HybridCLRHook`、`HotUpdateKit`、策略接口、AA Runner。
- `Runtime/Kits/HotUpdateKit/HotUpdateManifest.cs`：Manifest 数据结构、解析和来源链。
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableHotUpdateManager.cs`：Addressables catalog、下载和缓存流程。
- `Editor/StellarToolsHub/Modules/HybridCLRHotUpdateAssetExporter.cs`：DLL、metadata、Manifest 导出工具。
- `Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs`：AA 发布工作流。

## 核心类型

- `HybridCLRHook`：HybridCLR 代码加载入口，维护热更状态和最后错误。
- `HotUpdateState`：代码热更状态。
- `HybridCLRAAHotUpdateRunner`：从 AA 加载 Manifest、metadata、DLL 并启动热更。
- `HybridCLRAAHotUpdateResult`：AA 代码热更结果。
- `HotUpdateKit`：资源和代码热更统一门面。
- `IResourceHotUpdateStrategy`：资源热更策略接口。
- `ICodeHotUpdateStrategy`：代码热更策略接口。
- `AddressablesHotUpdateStrategy`：用 `AddressableHotUpdateManager` 实现资源热更。
- `HybridCLRCodeHotUpdateStrategy`：用 `HybridCLRHook` 实现代码热更。
- `HotUpdateManifest`：记录 DLL key、SHA256、入口类、入口方法、metadata keys、catalog 地址。
- `IHotUpdateManifestSource`：Manifest 来源接口。
- `RemoteHotUpdateManifestSource`、`StreamingAssetsHotUpdateManifestSource`、`AddressablesHotUpdateManifestSource`：Manifest 来源实现。
- `HotUpdateManifestSourceChain`：按优先级读取 Manifest。

## 关键方法

- `HotUpdateKit.Configure(...)`：替换资源或代码热更策略。
- `HotUpdateKit.InitializeAsync(...)`：初始化 Addressables。
- `HotUpdateKit.CheckResourceUpdatesAsync(...)`：调用资源策略检查 catalog 和下载大小。
- `HotUpdateKit.DownloadResourceUpdatesAsync(...)`：下载资源依赖。
- `HotUpdateKit.RunCodeHotUpdateAsync(...)`：运行代码热更策略。
- `HotUpdateKit.RunStartupHotUpdateAsync(...)`：启动期检查、下载、加载代码的组合流程。
- `HybridCLRHook.LoadMetadataForAOTAssembliesAsync(...)`：加载 AOT metadata。
- `HybridCLRHook.LoadAndStartHotUpdateAssembly(...)`：加载 DLL，反射入口类和入口方法并执行。
- `HybridCLRAAHotUpdateRunner.RunAsync(...)`：AA 代码热更完整 Runner。
- `HotUpdateManifest.FromJson(...)`：解析 JSON，包含 BOM 防护。

## 数据流

1. Player 启动读取 `ResKitRuntimeSettings`，获得 Manifest 地址和 fallback 策略。
2. `HotUpdateManifestSourceChain` 优先读远端 Manifest，也可按配置回退到 StreamingAssets 或 Addressables。
3. `HotUpdateKit.InitializeAsync` 初始化 Addressables。
4. `AddressableHotUpdateManager` 检查 catalog/hash，必要时 `UpdateCatalogs`。
5. 资源策略根据 keys 计算下载大小并下载依赖。
6. `HybridCLRAAHotUpdateRunner` 按 Manifest 加载 AOT metadata 和 `HotUpdate.dll.bytes`。
7. `HybridCLRHook` 校验 DLL SHA256，加载程序集，调用入口类静态方法。

## 依赖关系

- 资源热更依赖 Addressables。
- 代码热更依赖 HybridCLR。
- Manifest 和运行时配置依赖 ResKit 的 `ResKitRuntimeSettings`。
- 发布流程依赖 ToolsHub 的 AA 和 HybridCLR 导出工具。

## 扩展点

- 新增资源热更后端：实现 `IResourceHotUpdateStrategy`。
- 新增代码热更后端：实现 `ICodeHotUpdateStrategy`。
- 新增 Manifest 来源：实现 `IHotUpdateManifestSource` 并加入 `HotUpdateManifestSourceChain`。
- 修改发布字段：同步更新 `HotUpdateManifest`、导出工具、AA 发布工具和测试。

## 测试入口

- `HotUpdateManifestTests`：Manifest 解析、BOM、字段校验。
- `AAHotUpdatePublishToolTests`：AA 工作流路径和发布校验。
- `AddressablesCanLoadHybridClrDllBytesAndMetadata`：远端 AA 下 DLL bytes 和 metadata 加载。
- 修改运行链路后应跑 HotUpdate/AA 相关 EditMode 测试，并在需要时打开外置验证区的 FrameworkValidation 场景验证启动期热更。
