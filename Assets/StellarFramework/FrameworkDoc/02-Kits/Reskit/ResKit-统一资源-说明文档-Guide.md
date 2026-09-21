# ResKit / 统一资源说明文档

## 模块定位

`ResKit` 是框架的统一资源入口。目标不是要求每个业务开发者先理解引用计数、缓存和资源后端，而是提供三层使用体验：

1. **新手**：5 分钟内能正确加载和释放资源。
2. **熟手**：能明确控制 Scope、引用、预加载和释放时机。
3. **高级项目**：业务代码基本不变，只替换 `Addressables / YooAsset / AssetBundle / Resources` 后端。

普通业务代码优先从 `ResScope` 开始，不需要直接接触 `ResMgr`。

---

## 5 分钟快速开始

### 1. 创建 Scope

```csharp
using StellarFramework.Res;

ResScope resources = ResKit.CreateScope("Inventory");
```

默认后端由 `ResKitRuntimeSettings` 决定；未配置时回退到 `Resources`。

### 2. 加载资源

```csharp
GameObject prefab =
    await resources.LoadAsync<GameObject>("Characters/Hero");
```

### 3. 用完释放

```csharp
resources.Dispose();
```

`Dispose()` 会自动：

- 取消该 Scope 尚未完成的异步等待
- 释放该 Scope 持有的全部资源引用
- 回收底层 Loader

因此最推荐的写法是：

```csharp
using ResScope resources = ResKit.CreateScope("Battle");

GameObject prefab =
    await resources.LoadAsync<GameObject>("Characters/Hero");
```

如果 Scope 生命周期跨多个 Unity 回调，就把它保存为字段并在 `OnDestroy` / 业务 Dispose 中释放。

### 4. 不手写资源字符串：AssetsMap

ResKit 提供自动维护的业务资源常量表：

```csharp
using StellarFramework.Generated;

string manifestKey = AssetsMap.GameHotUpdate.Manifest.HotUpdateManifest;
string settingsKey = AssetsMap.Resources.HotUpdateSettings;
```

生成文件：

```text
Assets/StellarFramework/Generated/AssetMap/AssetsMap.cs
```

规则：

- 值始终是标准 Unity `Assets/...` 路径。
- 文件夹生成嵌套静态类，文件名生成 `const string`。
- C# 非法字符、关键字、同名文件会自动做稳定消歧。
- `Editor / Tests / FrameworkDoc / AddressableAssetsData / StreamingAssets` 等工程基础设施不会进入业务 AssetsMap。
- 只有内容发生变化时才重写文件，不会每次 Import 都制造无意义编译。
- `AssetPostprocessor` 会在资源导入、删除、移动后自动维护；也可以手动执行 `StellarFramework/ResKit/Regenerate AssetsMap`。

注意：已有的单数 `AssetMap` 是 **AssetBundle path -> bundle name** 内部映射；新的复数 `AssetsMap` 是业务层资源 key。两者职责不同，不应合并。

## 模块结构

运行时主要由四层组成：

- `ResKit`
  对外门面，负责创建 Scope、分配 Loader、注册后端
- `ResScope`
  普通业务推荐入口，负责一组资源的生命周期与自动释放
- `ResLoader`
  loader 基类，负责本地持有记录、异步等待、与 `ResMgr` 协作
- `ResMgr`
  全局共享缓存和引用计数中心

具体后端目前包括：

- `ResourceLoader`
- `AssetBundleLoader`
- `AddressableLoader`
- `YooAssetLoader`

## 后端模式

### Resources

适合：

- 默认配置
- 默认 UI 资源
- 小体量、固定资源

特点：

- 不需要额外构建
- 直接依赖 Unity `Resources`
- 不适合生产热更新

### AssetBundle

适合：

- 已经明确采用 AB 管线的项目
- 需要保留 `AssetMap`、依赖和本地 AB 工作流

特点：

- 依赖 `AssetBundleManager`
- 依赖 ToolsHub 的 `资源打包 (AssetBundle)` 和生成的 `AssetMap`

### Addressables

适合：

- 生产资源管理
- 本地内置 AA
- 远端热更 AA

特点：

- 通过 `Custom loader` 接入
- 资源 key 推荐使用完整 `Assets/...` 路径
- 生产模式只建议异步加载

### YooAsset

适合：

- 需要完整资源版本、下载、缓存和热更新体系的项目
- 多 ResourcePackage 项目
- 需要把资源系统从 Addressables 切换到 YooAsset 的项目

特点：

- 独立 Adapter：`StellarFramework.ResKit.YooAsset`
- 当前工程锁定 YooAsset `2.3.19`
- ResKit Adapter 只负责 Asset 加载/释放
- `YooAssetContentUpdater` 可选地把官方 HostPlayMode 启动流程收成一条简单调用
- `ResKit Core` 本身仍不接管 YooAsset Package / Manifest / Downloader 状态机
- 多 Package 会进入不同缓存命名空间，不会因为地址相同串资源

### Custom

适合：

- `YooAsset`
- 项目自有资源系统
- 第三方资源插件

特点：

- 只要求实现 `ResLoader`
- 不改变业务层的 `IResLoader` 用法

## 三层使用方式

### 第一层：普通业务推荐

```csharp
using ResScope resources = ResKit.CreateScope("Battle");
GameObject prefab = await resources.LoadAsync<GameObject>("Characters/Hero");
```

这一层不需要理解：

- RefCount
- OwnerId
- Loader Pool
- ResMgr
- 后端句柄

### 第二层：熟手精确控制

需要提前释放某个资源、批量预加载或长期持有 Loader 时，可以直接使用 `IResLoader`：

```csharp
IResLoader loader = ResKit.Allocate(
    ResLoaderRequest.Custom("Addressables", "BattlePreload"));

await loader.PreloadAsync(paths, progress => { });
GameObject prefab = await loader.LoadAsync<GameObject>(heroPath);

loader.Unload(heroPath);
loader.ReleaseAll();
ResKit.Recycle(loader);
```

### 第三层：替换后端

业务侧不改加载写法，只在启动层切默认后端：

```csharp
ResKit.Configure(
    ResLoadBackend.Custom,
    defaultCustomLoaderKey: "YooAsset");
```

或显式创建指定后端 Scope：

```csharp
using ResScope resources =
    ResKit.CreateCustomScope("YooAsset", "Inventory");
```

## 路径规则

### Resources

- 兼容传统 Resources 相对路径：不带 `Resources/`，不带扩展名。
- 同时支持 AssetsMap 的标准全路径，例如 `Assets/Resources/HotUpdateSettings.asset`；`ResourceLoader` 会自动归一化为 `HotUpdateSettings`。

示例：

```csharp
TextAsset txt = await loader.LoadAsync<TextAsset>("Configs/GameSetting");
```

因此使用 AssetsMap 时不需要为 Resources 单独维护第二套字符串地址。

### AssetBundle / Addressables / YooAsset

- 推荐统一使用完整 `Assets/...` 路径
- YooAsset 如果启用了 Addressable 规则，也可以使用项目定义的 Location；团队应统一一种稳定地址规则

示例：

```csharp
GameObject prefab = await loader.LoadAsync<GameObject>(
    "Assets/Game/Prefabs/Hero.prefab",
    token);
```

这样做的好处是：

- `AB` 和 `AA` 可以共用同一套业务资源 key
- 工具链更容易定位真实资产来源

## Addressables 说明

`Addressables` 在 `ResKit` 里不是单独枚举，而是作为 `Custom loader` 接入。

典型注册形式：

```csharp
ResKit.RegisterCustomLoader("Addressables", request => new AddressableLoader());
```

StellarFramework 对 Addressables 的职责约束：

- 作为 `ResKit` 的 Load / Release 后端。
- Tools Hub 提供本地 Settings / Group 路径检查和 Player Content 构建。
- 不包装 Addressables catalog 更新、远端下载或代码热更发布流程。
- 需要正式内容热更新时，框架推荐使用 YooAsset；如果项目自行使用 Addressables 官方远端能力，那属于项目层策略。

## YooAsset 说明

当前 Adapter 针对 YooAsset `2.3.x`。

推荐在项目启动层直接使用轻量 helper：

```csharp
var options = new YooAssetContentUpdateOptions
{
    PackageName = "DefaultPackage",
    MainHostServer = "https://cdn.example.com/game/Windows",
    FallbackHostServer = "https://cdn-backup.example.com/game/Windows",
    CachePackageRoot = null, // null = 使用 YooAsset 默认缓存目录
    BuildinPackageRoot = null, // 纯远端包不创建 BuildinFileSystem
    RetryPolicy = new YooAssetContentUpdateRetryPolicy(
        maxRetryCount: 2,
        delayMilliseconds: 500)
};

YooAssetContentUpdateResult update =
    await YooAssetContentUpdater.UpdateHostPackageAsync(options);

if (!update.Success)
{
    Debug.LogError($"{update.ErrorCode} @ {update.FailureStage}: {update.Error}");
    return;
}
```

成功后 helper 会默认自动注册 YooAsset 后端到 ResKit，因此业务代码可以直接：

```csharp
using ResScope resources =
    ResKit.CreateCustomScope(
        YooAssetResKitInstaller.LoaderKey,
        "Battle");

GameObject hero =
    await resources.LoadAsync<GameObject>(
        "Assets/Game/Prefabs/Hero.prefab");
```

如果以后从 YooAsset 切回 Addressables，业务层的 `ResScope.LoadAsync<T>()` 写法不需要改变。

`YooAssetContentUpdater` 只是 `StellarFramework.ResKit.YooAsset` Adapter 中的启动辅助器，内部严格按官方流程执行：

```text
Initialize Package
-> RequestPackageVersion
-> UpdatePackageManifest
-> CreateResourceDownloader
-> Download
-> YooAssetResKitInstaller.Install
```

断点续传使用 YooAsset `DefaultCacheFileSystem` 自己的临时文件 + HTTP `Range` 实现；不是框架自己重写下载器。下载中断后临时文件保留，下一次 Package 生命周期会从已有字节继续。

`YooAssetContentUpdater` 的失败结果不要求业务解析字符串：

- `ErrorCode`：稳定错误分类，例如 `VersionRequestFailed / ManifestUpdateFailed / DownloadFailed`。
- `FailureStage`：失败发生在哪个阶段。
- `RetryCount`：本次控制面实际重试次数。
- `RetryPolicy`：可插拔策略接口。默认只重试版本请求和 Manifest 请求。
- `Progress.Attempt`：版本 / Manifest 重试时可直接给 UI 显示当前尝试次数。

资源文件下载重试仍由 YooAsset `ResourceDownloaderOperation` 自己处理；框架不会在外面再包一层重复下载状态机。Package 初始化失败也不会被盲目重试，因为失败后的 Package 状态需要显式恢复。

如果项目需要内置首包，显式设置 `BuildinPackageRoot`；如果是纯远端内容包，保持 `null`，避免无意义访问 `StreamingAssets/yoo/.../BuildinCatalog.bytes`。

> `ResKit Core` 仍然不包含 YooAsset SDK 类型，也不复制 YooAsset 的版本/Manifest/Downloader 状态机。
> 高级项目仍可完全绕过 `YooAssetContentUpdater`，直接使用 YooAsset 官方 API，最后只调用 `YooAssetResKitInstaller.Install(...)`。

## AssetBundle 说明

`AssetBundle` 后端依赖：

- `AssetBundleManager`
- `Generated/AssetMap/AssetMap.cs`
- ToolsHub 的 `资源打包 (AssetBundle)`

使用前通常需要：

```csharp
await AssetBundleManager.Instance.InitAsync();
```

若使用严格卸载模式：

- 先销毁场景实例
- 再释放 loader 和 bundle 引用

否则可能出现资源对象被提前销毁。

## Resources 说明

`Resources` 后端适合框架默认资源和轻量固定资源。

不适合：

- 大体量内容
- 高频版本更新内容
- 需要正式热更新管理的资源

## 自定义 Loader / 第三方 Adapter

### 注册

```csharp
ResKit.RegisterLoader("MyBackend", request =>
{
    // 返回项目自己的 ResLoader
});
```

### 分配

```csharp
using ResScope scope =
    ResKit.CreateCustomScope("MyBackend", "Startup");
```

要求：

- 继承 `ResLoader`
- 实现同步 / 异步真实加载
- 实现自己的 `RecycleToPool()`

## 生命周期与释放规则

### 普通业务

优先使用 `ResScope`：

```csharp
private ResScope _resources;

private void Awake()
{
    _resources = ResKit.CreateScope(nameof(MyView));
}

private void OnDestroy()
{
    _resources?.Dispose();
    _resources = null;
}
```

`Dispose` 已经包含 `ReleaseAll + Recycle`，不要再次手工回收同一个 Loader。

### 精确管理

业务层需要遵守：

- 加载后由当前 loader 持有引用
- `Unload(path)` 只释放当前 loader 对单个路径的持有关系
- `ReleaseAll()` 释放当前 loader 全部持有关系
- `ResKit.Recycle(loader)` 是标准收口动作

不要只销毁场景对象而不回收 loader，也不要只回收 loader 而不处理场景中还在使用的实例对象。

### 取消语义

- 调用方取消 `LoadAsync` 会抛出 `OperationCanceledException`
- 取消不会再被伪装成 `null`
- Scope Dispose 会取消该 Scope 的未完成等待
- 多个 Owner 等待同一资源时，一个 Owner 取消等待不会取消其他 Owner 的有效请求
- 两个 Scope 同时请求相同 `LoaderName + Path` 时只发生一次底层物理加载
- 最后一个等待 Owner 被取消或销毁后，共享物理加载会收到取消，不继续执行无人消费的后台 IO

## ToolsHub 关联

- `资源打包 (AssetBundle)`
  负责 AB 规则、构建和 `AssetMap` 生成
- `ResKit 资源审计`
  查看 loader、共享缓存、资源引用和持有者
- `Addressables`
  负责 Addressables 本地 Settings / Group 检查和 Player Content 构建；不负责框架级内容热更新

## 常见问题

- Addressables 同步加载返回空
  生产模式的 Addressables 只支持异步加载。
- YooAsset Loader 提示 Package 未初始化
  先完成 YooAsset `ResourcePackage.InitializeAsync(...)`，再使用 ResKit。
- YooAsset 多 Package 使用同地址
  Adapter 的缓存身份包含 PackageName，不会跨 Package 共享同一条 ResMgr 缓存。
- AssetBundle 加载失败
  先确认 `AssetBundleManager.Instance.InitAsync()` 已执行，且 `AssetMap` 已生成。
- 自定义 loader 分配失败
  检查 `CustomKey` 是否已注册。
- 资源释放后对象丢失
  先销毁场景实例，再释放 loader 和底层资源引用。

## 相关文档

- [ResKit 源码文档](ResKit-统一资源-源码文档-Guide.md)
