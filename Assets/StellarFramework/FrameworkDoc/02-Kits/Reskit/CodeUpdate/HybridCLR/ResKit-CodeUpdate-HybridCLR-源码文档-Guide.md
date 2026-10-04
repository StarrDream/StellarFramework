# ResKit.CodeUpdate.HybridCLR / 源码说明

## 模块职责

`ResKit.CodeUpdate.HybridCLR` 是 ResKit 的 HybridCLR 代码更新 Provider。它只负责读取和加载代码载荷，不拥有内容更新系统，也不是 ResKit.Core 的硬依赖。框架默认通过 YooAsset 准备内容，再把 YooAsset Scope 交给该 Provider。

核心约束：

```text
ResKit.CodeUpdate.HybridCLR
  depends on ResKit.Core
  depends on HybridCLR.Runtime
  does NOT depend on Addressables
  does NOT depend on YooAsset
  does NOT depend on HttpKit
```

YooAsset / Addressables 通过应用传入的 ResKit Scope 被间接使用，因此该 Provider 不需要引用任何第三方资源 SDK 类型。
HybridCLR Runtime 是例外：该扩展是 HybridCLR 代码运行时适配器，因此它对 `HybridCLR.Runtime` 使用显式 asmdef 依赖和强类型 API 调用。

不要把 `RuntimeApi.LoadMetadataForAOTAssembly` 改回纯字符串反射。Editor 下反射可以工作，但 Release IL2CPP Linker 可能把 `HybridCLR.Runtime` 判定为不可达并裁剪，导致 Player 中 `Type.GetType("HybridCLR.RuntimeApi, HybridCLR.Runtime")` 返回 `null`。当前实现通过编译期依赖保证 Runtime API 进入 Player，并能在 HybridCLR 升级后直接暴露 API 不兼容问题。

## 源码目录

```text
Runtime/Kits/Reskit/CodeUpdate/HybridCLR/
  HotUpdateContracts.cs
  HotUpdateManifest.cs
  HotUpdateRuntimePolicy.cs
  HotUpdateSettings.cs
  Runtime/
    HybridCLRHotUpdateAdapter.cs
    HybridCLRHotUpdateInstaller.cs

Editor/StellarToolsHub/Modules/ResKit/CodeUpdate/HybridCLR/
  HybridCLRHotUpdateAssetExporter.cs
```

## ResKit Provider API

公共边界由 ResKit.Core 定义，HybridCLR 扩展在加载时注册自己的实现。项目从 ResKit 取得 Provider，并显式传入自己创建的资源 Scope：

```csharp
using ResScope codeAssets = ResKit.CreateCustomScope(YooAssetResKitInstaller.LoaderKey, "CodeUpdate");
IResCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult> provider =
    ResKit.GetCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult>(HybridCLRResCodeUpdateProvider.ProviderId);
HybridCLRUpdateResult result = await provider.RunAsync(settings, codeAssets, progress, cancellationToken);
```

`HybridCLRHotUpdateInstaller` 在 Player 启动前和 Editor 域加载时注册 Provider。项目可不安装该扩展，也可另行注册实现 `IResCodeUpdateProvider<TOptions, TResult>` 的代码运行时。

运行时公开入口只有 ResKit Provider。Provider 要求应用传入 Scope，由应用选择 Loader 并持有 Scope 生命周期；代码运行实现 `HybridCLRCodeUpdateRuntime` 是扩展内部细节。

## `HotUpdateSettings`

Settings 是 **启动和导出配置**，不是远端版本描述。

关键字段：

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

## Provider 的内部执行流程

主流程：

```text
Project creates a ResKit ResScope
  -> HybridCLR Provider validates Settings
  -> HybridCLRCodeUpdateRuntime reads through the supplied Scope
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

Provider 接收应用创建的 Scope：

```csharp
using ResScope resources = ResKit.CreateCustomScope(loaderKey, "CodeUpdate");
IResCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult> provider =
    ResKit.GetCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult>(HybridCLRResCodeUpdateProvider.ProviderId);
HybridCLRUpdateResult result = await provider.RunAsync(settings, resources);
```

本次启动期间加载的 Manifest / DLL / metadata 全部由该 Scope 管理。Provider 不会自行选择或创建 Loader；应用在调用结束后释放 Scope。

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

## 内部 HybridCLR 桥接

`HybridCLRHook` 是 Provider 内部实现，不是项目侧 API。它封装两步：

1. `LoadMetadataForAOTAssembliesAsync`
2. `LoadAndStartHotUpdateAssembly`

它不加载资源，只接受已经准备好的 byte[]。

项目侧应只调用 ResKit 的 `IResCodeUpdateProvider`；Provider 内部把从 ResKit Scope 读取到的字节交给 HybridCLR：

```text
ResKit Scope -> HybridCLR Provider -> byte[] -> HybridCLR Runtime API
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

- `StellarFramework.ResKit.CodeUpdate.HybridCLR`
- `StellarFramework.ToolsHub.ResKit.CodeUpdate.HybridCLR.Editor`

Addressables Tools Hub 只负责：

- Settings 创建/读取。
- 本地路径配置。
- 禁用 Remote Catalog。
- Player Content 构建。
- 本地配置检查。

这保证 AA 可以单独导出使用，而不会拖入 HybridCLR。

## YooAsset 边界

HybridCLR Provider 不引用 YooAsset assembly。项目启动层完成：

```text
ResKit.GetContentUpdateProvider(...).UpdateAsync(...)
ResKit.GetCodeUpdateProvider(...).RunAsync(settings, codeAssets)
```

`YooAssetResContentUpdateProvider` 位于独立的 `ResKit.YooAsset` Adapter；HybridCLR Provider 只通过应用传入的 ResKit Scope 消费已经准备好的 Manifest / DLL / metadata。应用可换用其他内容更新和 Loader Provider，前提是这些 Provider 提供同一份可匹配的代码制品。

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
- 不在代码更新 Provider 内复制 YooAsset/Addressables 下载状态机。

## 发布冻结条件

ResKit.CodeUpdate.HybridCLR 可发布前应满足：

1. Runtime assembly 不引用 Addressables/YooAsset/HttpKit。
2. Addressables Editor assembly 不引用该扩展。
3. Manifest 只通过 ResKit 读取。
4. Exporter 只写 `Assets/GameHotUpdate`。
5. Release SHA 校验有效。
6. 目标平台 IL2CPP Player 完成真实 metadata + Assembly.Load + Entry 验证。
7. IL2CPP Player 中 `HybridCLR.Runtime` 通过强类型程序集依赖保留，不依赖反射字符串碰运气。

## 相关文档

- [ResKit.CodeUpdate.HybridCLR 说明](ResKit-CodeUpdate-HybridCLR-说明文档-Guide.md)
- [ResKit 源码文档](../Reskit/ResKit-统一资源-源码文档-Guide.md)
