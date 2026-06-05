# ResKit / 统一资源源码文档

## 源码位置

- `Runtime/Kits/Reskit/ResKit.cs`：统一入口和 Custom loader 注册。
- `Runtime/Kits/Reskit/Core/IResLoader.cs`：加载器接口。
- `Runtime/Kits/Reskit/Core/ResLoader.cs`：加载器基类、引用计数和回收逻辑。
- `Runtime/Kits/Reskit/Core/ResMgr.cs`：全局资源驻留和引用管理。
- `Runtime/Kits/Reskit/Data/ResData.cs`：资源数据记录。
- `Runtime/Kits/Reskit/Data/ResKitRuntimeSettings.cs`：运行时配置。
- `Runtime/Kits/Reskit/Loaders/ResourceLoader/ResourceLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleManager.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableHotUpdateManager.cs`

## 核心类型

- `ResKit`：资源系统门面，负责分配 loader、回收 loader、注册第三方 loader。
- `ResLoaderRequest`：描述后端、owner、CustomKey 等分配参数。
- `ResLoadBackend`：Resources、AssetBundle、Custom 后端枚举。
- `IResLoader`：业务层看到的加载、卸载、回收接口。
- `ResLoader`：所有 loader 的基类，管理已加载路径、引用计数和 `RecycleToPool()`。
- `ResMgr`：全局资源缓存和引用计数管理器。
- `ResData`：单个资源的引用计数、对象、来源等状态。
- `ResKitRuntimeSettings`：AA Manifest、fallback、AB 卸载策略等运行时配置。
- `ResourceLoader`：基于 `Resources.Load` 的后端。
- `AddressableLoader`：热更拓展包中的 Addressables custom loader。
- `AddressableHotUpdateManager`：热更拓展包中的 AA catalog、hash、下载、缓存和清理管理。
- `AssetBundleLoader`：基于 AssetMap 和 AssetBundleManager 的后端。
- `AssetBundleManager`：AB manifest、依赖、bundle 缓存和卸载策略。

## 关键方法

- `ResKit.Allocate(...)`：根据 request 或泛型分配 loader。
- `ResKit.RegisterCustomLoader(...)`：注册第三方 loader 工厂，签名为 `request => loader`。
- `ResKit.Recycle(loader)`：把 loader 持有的资源释放并回收到池。
- `ResLoader.Load<T>` / `LoadAsync<T>`：模板方法，调用具体后端的真实加载。
- `ResLoader.Unload(path)` / `UnloadAll()`：减少 loader 和全局资源引用。
- `ResLoader.RecycleToPool()`：每个自定义 loader 必须覆写或确保可回收。
- `ResMgr.Retain(...)` / `Release(...)`：维护全局引用计数。
- `AssetBundleManager.InitAsync()`：加载 AB manifest 并准备依赖表。
- `AddressableHotUpdateManager.InitializeAsync()`：初始化 Addressables。
- `AddressableHotUpdateManager.CheckForUpdatesAsync()`：检查远端 catalog 和下载大小。
- `AddressableHotUpdateManager.DownloadDependenciesAsync()`：下载缺失或变化 bundle。

## 数据流

业务调用 `ResKit.Allocate` 得到 `IResLoader`。加载时，loader 先检查自己是否已持有该路径，再委托具体后端加载。加载成功后，`ResMgr` 保存全局 `ResData` 并增加引用计数。业务卸载路径或回收 loader 时，loader 释放自己的持有关系，`ResMgr` 在全局引用归零后调用后端释放。

Addressables 远端热更的数据流属于热更拓展包：`AddressableHotUpdateManager` 初始化 AA，检查 catalog/hash，更新 catalog，按 key 计算下载大小，下载依赖并让 AA 管缓存。ResKit 只负责通过 `CustomKey=Addressables` 把加载 API 统一给业务层。

## 依赖关系

- Resources 后端依赖 Unity Resources。
- AB 后端依赖 `Generated/AssetMap` 和 Unity AssetBundle API。
- Addressables custom loader 依赖 Addressables，并位于热更拓展包。
- HotUpdateKit 基础抽象依赖 ResKitRuntimeSettings；具体 AA 热更策略由热更拓展包注册。
- ToolsHub 的 AB、AA、ResKit 审计工具依赖 ResKit 数据结构。

## 扩展点

- 新增第三方后端：继承 `ResLoader`，实现真实同步/异步加载和卸载，注册 `ResKit.RegisterCustomLoader("YooAsset", request => new YooAssetResLoader())`。Addressables 也按同一方式以 `"Addressables"` key 接入。
- 新增 AB 卸载策略：扩展 `AssetBundleUnloadMode` 和 AssetBundleManager 的释放逻辑。
- 新增运行时配置：扩展 `ResKitRuntimeSettings`，同步 ToolsHub 配置和测试。
- 新增审计信息：从 `ResMgr` 和 loader owner 侧扩展快照，不要让业务层承担引用统计。

## 测试入口

- `ResKit_Playable.unity`：Resources/AB/AA 加载样例。
- `AAHotUpdatePublishToolTests`：AA 发布路径和校验。
- `AddressablesCanLoadHybridClrDllBytesAndMetadata`：远端 AA 加载链路。
- `QuickStartCatalogPolicyTests`：ResKit 文档和 Custom loader 示例防回归。
