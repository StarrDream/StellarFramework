# HybridCLRKit / 代码热更新源码文档

## 模块职责

`HybridCLRKit` 是一个 **startup-only code-update adapter**。它只负责 HybridCLR 代码热更新，不拥有内容更新系统。

核心约束：

```text
HybridCLRKit
  depends on ResKit.Core
  depends on HybridCLR.Runtime
  does NOT depend on Addressables
  does NOT depend on YooAsset
  does NOT depend on HttpKit
```

YooAsset / Addressables 通过 ResKit Loader key 被间接使用，因此 HybridCLRKit 不需要任何第三方资源 SDK 类型。
HybridCLR Runtime 是例外：`HybridCLRKit` 本身就是 HybridCLR 代码热更新适配器，因此它对 `HybridCLR.Runtime` 使用显式 asmdef 依赖和强类型 API 调用。

不要把 `RuntimeApi.LoadMetadataForAOTAssembly` 改回纯字符串反射。Editor 下反射可以工作，但 Release IL2CPP Linker 可能把 `HybridCLR.Runtime` 判定为不可达并裁剪，导致 Player 中 `Type.GetType("HybridCLR.RuntimeApi, HybridCLR.Runtime")` 返回 `null`。当前实现通过编译期依赖保证 Runtime API 进入 Player，并能在 HybridCLR 升级后直接暴露 API 不兼容问题。

## 源码目录

```text
Runtime/Kits/HybridCLRKit/
  HotUpdateContracts.cs
  HotUpdateManifest.cs
  HotUpdateRuntimePolicy.cs
  HotUpdateSettings.cs
  Runtime/
    HybridCLRHotUpdateAdapter.cs
    HybridCLRHotUpdateInstaller.cs

Editor/StellarToolsHub/Modules/HybridCLRKit/
  HybridCLRHotUpdateAssetExporter.cs
```

## `HybridCLRKit`

对外门面位于 `HotUpdateContracts.cs`。

```csharp
HybridCLRUpdateResult result = await HybridCLRKit.RunAsync(
    settings,
    progress,
    cancellationToken);
```

Facade 只负责：

- 解析 Settings。
- 做 Settings 校验。
- 调用 `IHybridCLRCodeUpdateStrategy`。

真正运行实现由 `HybridCLRCodeHotUpdateStrategy` 注册。

如果运行时没有 HybridCLR Adapter，默认 `UnavailableHybridCLRCodeUpdateStrategy` 返回明确失败结果，不制造成功状态。

## `IHybridCLRCodeUpdateStrategy`

这是 Facade 与 HybridCLR 具体实现之间的边界。

```csharp
public interface IHybridCLRCodeUpdateStrategy
{
    UniTask<HybridCLRUpdateResult> RunAsync(...);
}
```

这样 `HybridCLRKit` Facade 可以存在于项目中，而真正的 HybridCLR 包和实现保持可选。

## `HotUpdateSettings`

Settings 是 **启动和导出配置**，不是远端版本描述。

关键字段：

- `resourceLoaderKey`
- `hotUpdateManifestKey`
- `hotUpdateAssemblyKey`
- `hotUpdateEntryClass`
- `hotUpdateEntryMethod`
- `aotMetadataKeys`

注意：运行时 DLL SHA256 不再存储在 Settings 中。SHA256 只存在于导出的 Manifest，避免“双份事实来源”。

Settings 也不再包含：

- Addressables catalog 开关。
- Addressables label / update keys。
- Manifest HTTP URL。
- StreamingAssets fallback。
- Resources fallback。
- HTTP timeout。

这些都不是代码热更运行器的职责。

## `HotUpdateManifest`

Manifest 是运行时事实来源，包含：

- 主热更程序集 key。
- 主热更程序集 SHA256。
- 入口类。
- 入口方法。
- AOT metadata key 列表。

Manifest 本身也是普通 ResKit `TextAsset`。运行时不会再创建 `IHotUpdateManifestSource`、HTTP source、StreamingAssets source 或 source chain。

## `HybridCLRRunner`

主流程：

```text
Validate Settings
  -> ResKit.CreateCustomScope(ResourceLoaderKey)
  -> Load Manifest TextAsset
  -> Parse + Validate Manifest
  -> parallel load DLL + all metadata TextAssets
  -> verify DLL SHA256
  -> LoadMetadataForAOTAssembly
  -> Assembly.Load
  -> resolve entry type/method
  -> invoke entry
```

### 为什么 Manifest 也走 ResKit

旧实现中 Manifest 可以独立通过 HTTP 或 `StreamingAssets/aa` 获取，而 DLL / metadata 又由 Addressables 读取。这会形成两个版本源：

```text
Manifest version A
Addressables catalog version B
```

新实现要求它们由同一 ResKit 内容后端提供，从架构上消除这一类跨版本组合。

### Scope

Runner 使用：

```csharp
using ResScope resources = ResKit.CreateCustomScope(loaderKey, "HybridCLRRunner");
```

因此本次启动期间加载的 Manifest / DLL / metadata 全部在同一个 Owner 生命周期中。退出 Runner 后会统一释放。

### 并行 metadata 加载

metadata TextAsset 获取阶段使用 `UniTask.WhenAll` 并行读取；HybridCLR metadata 应用阶段仍保持顺序执行。

原因：

- 资源 IO 可以并行。
- `RuntimeApi.LoadMetadataForAOTAssembly` 属于运行时注册动作，顺序执行更容易诊断。

## SHA256

正式运行：

```text
manifest.hotUpdateAssemblySha256 required
actual dll bytes -> SHA256
expected != actual -> fail startup
```

Editor / Development 下允许 Manifest 暂时缺 SHA，用于开发期链路自检；Release 严格模式必须具备 SHA。

## `HybridCLRRuntimePolicy`

当前严格模式定义：

```csharp
!Application.isEditor && !Debug.isDebugBuild
```

即非 Editor 且非 Development Build 的 Player 视为生产运行时。

## `HybridCLRHook`

Hook 封装两步：

1. `LoadMetadataForAOTAssembliesAsync`
2. `LoadAndStartHotUpdateAssembly`

它不加载资源，只接受已经准备好的 byte[]。

这保证 HybridCLR API 和资源系统之间仍有边界：

```text
ResKit -> byte[] -> HybridCLRHook
```

## Exporter

`HybridCLRHotUpdateAssetExporter` 负责 Editor 产物转换。

输出：

```text
Assets/GameHotUpdate/Code/*.dll.bytes
Assets/GameHotUpdate/Metadata/*.dll.bytes
Assets/GameHotUpdate/Manifest/HotUpdateManifest.json
```

不再生成：

```text
Assets/StreamingAssets/aa/HotUpdateManifest.json
```

Exporter 也不调用 Addressables Build，不知道 YooAsset Package，不做远端上传。

## Addressables 边界

`StellarFramework.ToolsHub.Addressables.Editor` 不再引用：

- `StellarFramework.HybridCLRKit`
- `StellarFramework.ToolsHub.HybridCLRKit.Editor`

Addressables Tools Hub 只负责：

- Settings 创建/读取。
- 本地路径配置。
- 禁用 Remote Catalog。
- Player Content 构建。
- 本地配置检查。

这保证 AA 可以单独导出使用，而不会拖入 HybridCLR。

## YooAsset 边界

HybridCLRKit 不引用 YooAsset assembly。项目启动层完成：

```text
YooAssetContentUpdater.UpdateHostPackageAsync
HybridCLRKit.RunAsync
```

`YooAssetContentUpdater` 位于独立的 `ResKit.YooAsset` Adapter；HybridCLRKit 只通过 ResKit Loader key 消费已经准备好的 Manifest / DLL / metadata。

## 错误处理

不得吞异常。

- 配置错误 -> `HybridCLRUpdateResult.Success=false`。
- 资源读取失败 -> 明确 key。
- SHA 不匹配 -> 输出 expected / actual。
- metadata 失败 -> 输出对应 metadata key / HybridCLR error。
- Entry 失败 -> 输出类 / 方法 / exception。
- 外部取消 -> `OperationCanceledException` 原样传播。

## GC / 性能

该流程只在启动期运行，不是逐帧路径。重点不是极限零 GC，而是：

- metadata 资源读取并行化，减少串行启动等待。
- `ResScope` 统一释放句柄。
- 不创建第二套内容缓存/引用计数。
- 不在 HybridCLRKit 内复制 YooAsset/Addressables 下载状态机。

## 发布冻结条件

HybridCLRKit 可冻结前应满足：

1. Runtime assembly 不引用 Addressables/YooAsset/HttpKit。
2. Addressables Editor assembly 不引用 HybridCLRKit。
3. Manifest 只通过 ResKit 读取。
4. Exporter 只写 `Assets/GameHotUpdate`。
5. Release SHA 校验有效。
6. 目标平台 IL2CPP Player 完成真实 metadata + Assembly.Load + Entry 验证。
7. IL2CPP Player 中 `HybridCLR.Runtime` 通过强类型程序集依赖保留，不依赖反射字符串碰运气。

## 相关文档

- [HybridCLRKit 说明文档](HybridCLRKit-代码热更新-说明文档-Guide.md)
- [ResKit 源码文档](../Reskit/ResKit-统一资源-源码文档-Guide.md)
