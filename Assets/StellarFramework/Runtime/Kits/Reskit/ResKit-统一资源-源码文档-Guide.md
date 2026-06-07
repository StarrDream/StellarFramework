# ResKit / 统一资源源码文档

## 模块职责

`ResKit` 是框架的统一资源门面，负责把不同资源后端抽象成同一套资源加载与释放接口。

它解决的核心问题有：

- 对业务层隐藏 `Resources / AssetBundle / Addressables / 自定义 Loader` 的差异
- 用 `ResLoader` 统一本地持有关系
- 用 `ResMgr` 统一全局共享缓存和引用计数
- 用 `ResKitRuntimeSettings` 统一默认后端和运行时配置

## 源码文件

- `Runtime/Kits/Reskit/ResKit.cs`
- `Runtime/Kits/Reskit/Core/IResLoader.cs`
- `Runtime/Kits/Reskit/Loaders/ResLoader.cs`
- `Runtime/Kits/Reskit/Core/ResMgr.cs`
- `Runtime/Kits/Reskit/Data/ResData.cs`
- `Runtime/Kits/Reskit/Data/ResKitRuntimeSettings.cs`
- `Runtime/Kits/Reskit/Loaders/ResourceLoader/ResourceLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleManager.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableLoader.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableHotUpdateManager.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressablesResKitInstaller.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressablesHotUpdateInstaller.cs`

## 总体结构

```text
ResKit
├─ ResLoadBackend
├─ ResLoaderRequest
├─ _backendFactories
├─ _customFactories
└─ 默认后端解析

IResLoader
└─ ResLoader
   ├─ 本 loader 持有记录
   ├─ 本 loader 异步等待表
   ├─ LoaderId / OwnerName
   └─ 具体后端实现

ResMgr
├─ _sharedCache
├─ _loadingTasks
└─ 引用计数与共享异步加载

具体后端
├─ ResourceLoader
├─ AssetBundleLoader
└─ AddressableLoader
```

## 运行时调用链

### Loader 分配

1. 业务调用 `ResKit.Allocate(...)`
2. `ResolveRequest(...)` 解析 `Default` 后端
3. 若是 `Custom`，走 `_customFactories`
4. 若注册了后端工厂，走 `_backendFactories`
5. 否则落到 `AllocateBuiltin(...)`
6. 若 loader 是 `ResLoader`，写入 `OwnerName`

### 同步加载

1. `IResLoader.Load<T>(path)`
2. `ResLoader.Load<T>(...)`
3. 先查 `_loadedRecord`
4. 再查 `ResMgr.GetCache(...)`
5. 若缓存不存在，调用具体后端 `LoadRealSync(...)`
6. 返回的 `ResData` 进入 `ResMgr.AddSync(...)`
7. 记录本 loader 已持有路径

### 异步加载

1. `IResLoader.LoadAsync<T>(path)`
2. `ResLoader.LoadAsync<T>(...)`
3. 先查 `_loadedRecord`
4. 再查 `_loadingRecord`
5. 若本 loader 没有在等，则转到 `ResMgr.LoadSharedAsync(...)`
6. `ResMgr` 负责不同 loader 之间的共享异步加载
7. 完成后回写 `_loadedRecord`

### 释放

1. 业务调用 `Unload(path)`、`ReleaseAll()` 或 `ResKit.Recycle(loader)`
2. `ResLoader` 移除本地持有关系
3. `ResMgr.RemoveRef(...)` 递减共享引用
4. 引用归零时调用 `RealUnload(...)`
5. 由具体后端 `UnloadReal(...)` 执行真实释放

## 类型详解

## `ResLoadBackend`

### 作用

定义资源后端类型。

### 枚举值

- `Default`
  使用默认后端解析逻辑。
- `Resources`
  使用 Unity Resources。
- `AssetBundle`
  使用 AssetBundle 管线。
- `Custom`
  使用 `CustomKey` 路由到自定义工厂。

## `ResLoaderRequest`

### 作用

描述一次 loader 分配请求。

### 字段

- `Backend`
  本次请求期望使用的后端。
- `OwnerName`
  用于给 loader 标记可读业务拥有者。
- `CustomKey`
  当 `Backend == Custom` 时，决定路由到哪个自定义工厂。

### 工厂方法

#### `Default(string ownerName = null)`

创建默认后端请求。

#### `For(ResLoadBackend backend, string ownerName = null)`

创建指定内置后端请求。

#### `Custom(string customKey, string ownerName = null)`

创建自定义后端请求。

## `ResLoaderFactory`

### 作用

定义 loader 工厂委托：

`ResLoaderRequest -> IResLoader`

## `ResKit`

### 作用

对外统一入口。

### 核心字段

- `_backendFactories : Dictionary<ResLoadBackend, ResLoaderFactory>`
  存放注册过的后端工厂。
- `_customFactories : Dictionary<string, ResLoaderFactory>`
  存放注册过的自定义 key 工厂。
- `_configuredDefaultBackend`
  手动配置的默认后端。
- `_configuredDefaultCustomKey`
  手动配置的默认自定义 key。
- `_configuredRuntimeSettings`
  手动注入的运行时设置对象。

### 关键方法

#### `Allocate<T>() where T : ResLoader, new()`

旧式 typed loader 入口。

本质：

- 直接从 `PoolKit` 分配 `T`
- 跳过后端解析

用途：

- 向后兼容旧写法
- 适合明确知道具体 loader 类型的场景

#### `Configure(ResLoadBackend defaultBackend, ResKitRuntimeSettings runtimeSettings, string defaultCustomLoaderKey)`

设置默认后端解析策略。

影响范围：

- `ResLoaderRequest.Default(...)`
- 未显式指定 `Backend` 的默认资源链路

#### `RegisterLoaderFactory(...)`

注册内置后端工厂。

约束：

- 不允许注册 `Default`
- 不允许注册 `Custom`
- `factory == null` 时表示移除

#### `RegisterCustomLoader(...)`

注册自定义后端工厂。

约束：

- `customKey` 不能为空
- `factory == null` 时表示移除

#### `UnregisterCustomLoader(...)`

删除自定义 key 注册。

#### `Allocate(ResLoaderRequest request)`

生产推荐入口。

执行顺序：

1. `ResolveRequest(request)`
2. 若后端为 `Custom`，走 `AllocateCustom(...)`
3. 若后端工厂存在，直接调用
4. 否则走 `AllocateBuiltin(...)`
5. 若结果是 `ResLoader`，写入 `OwnerName`

#### `Allocate(ResLoadBackend backend, string ownerName = null)`

对 `ResLoaderRequest.For(...)` 的简化包装。

#### `Recycle<T>(T loader)` / `Recycle(IResLoader loader)`

统一回收入口。

区别：

- typed 版本直接进 `PoolKit`
- interface 版本调用 `RecycleToPool()`

### 私有方法

#### `ResolveRequest(...)`

默认后端解析主入口。

优先级：

1. 如果请求不是 `Default`，直接返回
2. 如果调用过 `Configure(...)` 指定默认后端，优先使用
3. 否则从 `ResKitRuntimeSettings` 中读取
4. 最终兜底 `Resources`

#### `BuildResolvedRequest(...)`

把最终解析出的默认后端与 ownerName 重新构造成请求对象。

#### `AllocateBuiltin(...)`

当前内置支持：

- `Resources -> ResourceLoader`
- `AssetBundle -> AssetBundleLoader`

#### `AllocateCustom(...)`

按 `CustomKey` 从 `_customFactories` 中取工厂。

常见失败分支：

- `CustomKey` 为空
- 未注册对应 factory

#### `NormalizeCustomKey(...)`

统一做：

- `null / whitespace -> string.Empty`
- `Trim()`

## `IResLoader`

### 作用

资源加载器统一接口。

### 方法

- `Load<T>(string path)`
- `LoadAsync<T>(string path, CancellationToken cancellationToken = default)`
- `PreloadAsync(IList<string> paths, Action<float> onProgress = null, CancellationToken cancellationToken = default)`
- `Unload(string path)`
- `ReleaseAll()`
- `RecycleToPool()`

## `ResLoader`

### 作用

所有具体 loader 的基类。

### 核心字段

- `_loadedRecord : HashSet<string>`
  当前 loader 已持有的路径集合。
- `_loadingRecord : Dictionary<string, UniTaskCompletionSource<ResData>>`
  当前 loader 内部对同一路径的等待任务表。
- `_loaderVersion : int`
  用于防止旧任务在释放后回写到新生命周期。
- `_loaderId : string`
  loader 实例唯一 ID，用于资源审计。

### 抽象成员

- `LoaderName`
- `LoadRealSync(string path)`
- `LoadRealAsync(string path, CancellationToken cancellationToken)`
- `UnloadReal(ResData data)`

### 虚方法

#### `LoadRealAsyncTyped<T>(...)`

默认调用 `LoadRealAsync(...)`，允许子类按泛型类型优化异步加载逻辑。

### 构造函数

- 调用 `GenerateNewLoaderId()`

### 关键方法

#### `GenerateNewLoaderId()`

生成形如：

`{LoaderName}_{Guid8}`

的唯一标识。

#### `SetOwnerName(string ownerName)`

把可读业务名拼入 `_loaderId`，便于调试和审计。

#### `Load<T>(string path)`

同步加载入口。

执行步骤：

1. 路径为空直接返回 `null`
2. 若 `_loadedRecord` 中已有路径，则回查共享缓存
3. 若共享缓存中有对象，直接返回
4. 若 `ResMgr` 正在异步加载该路径，直接报错并拒绝同步加载
5. 调用 `LoadRealSync(...)`
6. 成功后补充 `ResData.Path / LoaderName / UnloadAction`
7. 交给 `ResMgr.AddSync(...)`
8. 记录 `_loadedRecord`

#### `LoadAsync<T>(string path, CancellationToken cancellationToken = default)`

异步加载入口。

执行步骤：

1. 路径为空返回 `null`
2. 若 `_loadedRecord` 中已有路径，则回查共享缓存
3. 若 `_loadingRecord` 中已有等待任务，直接 await 同一个任务
4. 记录当前 `_loaderVersion` 和 `_loaderId`
5. 创建本 loader 级别的 `loadingSource`
6. 调用 `ResMgr.LoadSharedAsync(...)`
7. 在共享加载成功后检查 `_loaderVersion` 是否变化
8. 若 loader 生命周期已变，则撤回这次引用并丢弃结果
9. 否则写入 `_loadedRecord`，返回资源对象

#### `PreloadAsync(...)`

批量预加载入口。

特点：

- 分批执行
- 支持进度回调
- 默认每批 5 个

#### `Unload(string path)`

释放当前 loader 对某个路径的持有关系。

特点：

- 只减少本 loader 的持有
- 最终是否真实释放由 `ResMgr` 决定

#### `ReleaseAll()`

释放当前 loader 持有的全部路径。

副作用：

- 遍历 `_loadedRecord`
- 调用 `ResMgr.RemoveRef(...)`
- 清空 `_loadedRecord`
- 清空 `_loadingRecord`
- `_loaderVersion++`

#### `OnAllocated()`

对象池分配回调。

职责：

- 清空持有记录
- 清空等待表
- 版本号自增
- 重新生成 loaderId

#### `OnRecycled()`

对象池回收回调，默认调用 `ReleaseAll()`

#### `RecycleToPool()`

默认只输出错误。

设计目的：

- 强迫具体 loader 显式实现自己的强类型回收
- 防止 `IResLoader` 接口层失去真实类型信息后错误回收

## `ResMgr`

### 作用

全局共享缓存、引用计数和共享异步加载中心。

### 内部类型 `OngoingLoadEntry`

字段：

- `SharedCts`
  共享取消源
- `CompletionSource`
  共享加载结果
- `WaiterCount`
  当前等待者数量
- `IsCompleted`
  是否已完成

### 核心字段

- `_sharedCache : Dictionary<string, ResData>`
  全局资源缓存，key 为 `LoaderName:path`
- `_loadingTasks : Dictionary<string, OngoingLoadEntry>`
  全局共享异步加载表
- `_pendingResourcesUnloadCount`
  `Resources.UnloadUnusedAssets()` 的延迟触发计数
- `RESOURCES_UNLOAD_THRESHOLD`
  触发阈值

### 关键方法

#### `GetCacheKey(path, loaderName)`

生成共享缓存 key：

`{loaderName}:{path}`

#### `LoadSharedAsync(...)`

全局共享异步加载主入口。

执行步骤：

1. 先查 `_sharedCache`
2. 若资源已存在，则直接 `AddRefInternal(...)`
3. 若资源正在加载，则只增加 `WaiterCount`
4. 若没有进行中任务，则创建 `OngoingLoadEntry`
5. 启动 `RunSharedLoadAsync(...)`
6. 所有等待者共享同一个 `CompletionSource`
7. 取消时只减少等待计数
8. 当最后一个等待者取消时，才取消共享任务

#### `LoadInternalAsync(...)`

包装具体 `loadFunc`，成功后把数据写入 `_sharedCache`。

#### `AddSync(...)`

把同步加载结果放进共享缓存并增加引用计数。

#### `GetCache(...)`

查共享缓存，若发现缓存中的 `Asset == null`，会主动清理坏缓存。

#### `IsLoadingAsync(...)`

判断某路径是否正在共享异步加载。

#### `AddRef(...)`

按路径增加共享引用。

#### `RemoveRef(...)`

按路径减少共享引用。

关键行为：

- `RefCount--`
- 开发期移除 owner
- 对负引用计数做断言
- 引用归零时从 `_sharedCache` 删除并 `RealUnload(...)`

#### `GarbageCollect()`

强制：

- `GC.Collect()`
- `Resources.UnloadUnusedAssets()`

#### `TriggerResourcesUnload()`

延迟触发 `Resources.UnloadUnusedAssets()` 的节流入口。

#### `RealUnload(...)`

真实释放资源：

- 优先执行 `ResData.UnloadAction`
- 没有卸载委托时才走 `Destroy / DestroyImmediate`

#### `RunSharedLoadAsync(...)`

共享异步加载任务主循环。

职责：

- 调用 `LoadInternalAsync(...)`
- 写回 `CompletionSource`
- 收尾清理 `_loadingTasks`
- 若没有任何等待者且资源 `RefCount == 0`，立即释放资源

#### `TakeSnapshot()`

输出开发期资源持有快照。

重点输出：

- 当前缓存资源数量
- 每个资源的 `Path`
- `LoaderName`
- `RefCount`
- `Owners`

## `ResData`

### 作用

描述单个缓存资源实体。

### 字段

- `Path`
  原始资源路径。
- `Asset`
  实际资源对象。
- `RefCount`
  当前共享引用计数。
- `LoaderName`
  资源来源 loader 名称。
- `Data`
  后端附加数据，比如 Addressables 的 Handle。
- `UnloadAction`
  真实卸载委托。

### 开发期字段

- `_owners`
  持有该资源的 loaderId 集合。
- `Owners`
  公开只读访问入口。

### 方法

- `AddOwner(...)`
- `RemoveOwner(...)`

## `AssetBundleUnloadMode`

### 作用

控制 AssetBundle 释放策略。

### 枚举值

- `PreserveLoadedAssets`
- `DestroyLoadedAssets`

## `ResKitRuntimeSettingsValidationReport`

### 作用

承载运行时配置校验结果。

### 字段

- `Errors`
- `Warnings`
- `IsValid`

### 方法

- `AddError(...)`
- `AddWarning(...)`

## `ResKitRuntimeSettings`

### 作用

保存运行时资源配置。

### 核心字段

- `defaultLoadBackend`
- `defaultUILoadBackend`
- `defaultCustomLoaderKey`
- `defaultUICustomLoaderKey`
- `resourcesRootPath`
- `assetBundleRootPath`
- `uiRootPath`
- `uiPanelPathFormat`
- `assetBundleUnloadMode`

### 公开属性

- `DefaultLoadBackend`
- `DefaultUILoadBackend`
- `DefaultCustomLoaderKey`
- `DefaultUICustomLoaderKey`
- `ResourcesRootPath`
- `AssetBundleRootPath`
- `UIRootPath`
- `UIPanelPathFormat`
- `AssetBundleUnloadMode`

### 关键方法

#### `LoadOrCreateDefault(...)`

尝试从 `Resources` 读取运行时配置；找不到时返回运行时默认对象。

#### `Validate(...)`

校验：

- `Custom` 后端是否配了 `CustomLoaderKey`
- `UIRootPath`
- `UIPanelPathFormat`

#### `ToObjectKeyList(...)`

把字符串 key 列表转成 `List<object>`。

#### `ToDistinctStringList(...)`

去空、去重、保序。

## `ResourceLoader`

### 作用

基于 Unity `Resources` 的具体 loader。

### `LoaderName`

固定为 `"Resources"`

### 关键方法

- `LoadRealSync(...)`
  调用 `Resources.Load`
- `LoadRealAsync(...)`
  调用 `Resources.LoadAsync`
- `UnloadReal(...)`
  非 GameObject/Component 走 `Resources.UnloadAsset`，否则走延迟卸载计数
- `RecycleToPool()`
  显式回收到 `PoolKit`

## `AssetBundleLoader`

### 作用

基于 `AssetBundleManager` 的具体 loader。

### `LoaderName`

固定为 `"AssetBundle"`

### 关键方法

- `LoadRealSync(...)`
  调用 `AssetBundleManager.Instance.LoadAssetSync(...)`
- `LoadRealAsync(...)`
  调用 `AssetBundleManager.Instance.LoadAssetAsync(...)`
- `UnloadReal(...)`
  调用 `AssetBundleManager.Instance.UnloadAsset(data.Path)`
- `RecycleToPool()`
  显式回收到 `PoolKit`

## `AssetBundleManager`

### 作用

管理 AssetBundle manifest、依赖、bundle 缓存和 bundle 引用计数。

### 状态枚举

#### `AssetBundleManagerState`

- `Uninitialized`
- `Initializing`
- `Initialized`
- `Failed`

#### `AssetBundleLoadState`

- `Unloaded`
- `Loading`
- `Loaded`
- `Failed`

### 内部类型 `BundleRecord`

字段：

- `BundleName`
- `Bundle`
- `RefCount`
- `State`
- `LoadingSource`
- `LastError`
- `Dependencies`

### 核心字段

- `_bundleRecords`
- `_dependenciesCache`
- `_manifest`
- `_assetPathToBundleMap`
- `_initCompletionSource`
- `_state`
- `_lastError`
- `_basePath`
- `_unloadMode`

### 关键方法

- `Configure(ResKitRuntimeSettings settings)`
- `Configure(AssetBundleUnloadMode unloadMode)`
- `InitAsync(...)`
- `LoadAssetSync(...)`
- `LoadAssetAsync(...)`
- `UnloadAsset(...)`
- `TakeSnapshot()`

### 关键私有流程

- `EnsureAssetMap()`
- `EnsureInitializedForSync()`
- `InitSync()`
- `LoadManifestSync() / LoadManifestAsync()`
- `LoadBundleRecursiveSync() / LoadBundleRecursiveAsync()`
- `UnloadBundleRecursive()`
- `LoadGlobalShadersSync() / LoadGlobalShadersAsync()`

## `AddressableLoader`

### 作用

Addressables 具体 loader，通过 `CustomKey=Addressables` 接入。

### `LoaderName`

固定为 `"Addressables"`

### 关键方法

- `LoadRealSync(...)`
  当前显式禁用同步加载，只记录错误。
- `LoadRealAsync(...)`
  转发到 `LoadRealAsyncTyped<Object>(...)`
- `LoadRealAsyncTyped<T>(...)`
  调用 `Addressables.LoadAssetAsync<T>(path)`
- `UnloadReal(...)`
  释放 `AsyncOperationHandle` 或直接 `Addressables.Release(asset)`
- `RecycleToPool()`
  显式回收到 `PoolKit`

### 设计约束

- 生产 Addressables 路径只支持异步加载
- 推荐地址格式使用完整 `Assets/...` 路径

## `AddressableHotUpdateManager`

### 作用

管理 Addressables catalog 检查、更新、依赖下载和缓存清理。

### 状态枚举

#### `AddressableHotUpdateStatus`

- `None`
- `Success`
- `AddressablesUnavailable`
- `InitializationFailed`
- `InvalidKeys`
- `CatalogCheckFailed`
- `CatalogUpdateFailed`
- `DownloadSizeFailed`
- `DownloadFailed`
- `CacheClearFailed`
- `Cancelled`
- `Exception`

### 结果结构

- `AddressableOperationResult`
- `AddressableDownloadProgress`
- `AddressableDownloadResult`
- `UpdateCheckResult`

### 核心字段

- `_lastReport`
- `_isInitialized`
- `_isInitializing`
- `_initializeSource`
- `_lastError`

### 关键方法

- `InitializeAsync(...)`
- `CheckCatalogUpdatesAsync(...)`
- `DownloadDependenciesAsync(...)`
- `ClearDependencyCacheAsync(...)`
- `CheckUpdateAsync(...)`
- `DownloadUpdateAsync(...)`

### 关键私有方法

- `FailOperation(...)`
- `BeginReport(...)`
- `Report(...)`
- `ReportError(...)`
- `NormalizeKeys(...)`
- `ToKeyStrings(...)`

## `AddressablesResKitInstaller`

### 作用

在运行时把 Addressables loader 注册到 `ResKit` 的 `CustomKey=Addressables`。

## `AddressablesHotUpdateInstaller`

### 作用

把 Addressables 资源热更新策略注册到 `HotUpdateKit`。

## 设计约束

- `ResMgr` 才是共享缓存和引用计数中心
- `ResLoader` 只负责当前 loader 的持有关系
- `AddressableLoader` 禁止同步加载
- `AssetBundleManager` 初始化和依赖管理必须先于 AB 资源加载
- 自定义 loader 必须显式实现 `RecycleToPool()`

## 常见误用

- 忘记回收 loader
- Addressables 路径在异步加载中又发同步请求
- 只销毁实例对象，不释放 loader 或共享引用
- AssetBundle 未初始化就直接加载
- 自定义 loader 注册不完整

## 测试建议

- 默认后端解析
- loader 注册与路由
- 全局共享异步加载去重
- 引用计数归零释放
- AssetBundle manifest 和依赖加载
- Addressables catalog 检查与下载
- 审计快照输出
