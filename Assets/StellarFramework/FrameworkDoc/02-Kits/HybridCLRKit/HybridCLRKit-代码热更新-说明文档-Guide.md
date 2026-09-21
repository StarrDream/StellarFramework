# HybridCLRKit / 代码热更新说明文档

## 模块定位

`HybridCLRKit` 只负责 **HybridCLR 代码热更新启动链**。它不负责资源版本管理、catalog 更新、下载器、缓存淘汰或 CDN 发布。

StellarFramework 当前明确分工：

- `ResKit`：业务侧统一资源 Load / Release / Scope 生命周期。
- `YooAsset`：推荐的生产内容热更新方案，负责 Package 初始化、版本、Manifest、下载和缓存。
- `Addressables`：ResKit 的可选加载后端与本地内容构建入口，不承担 StellarFramework 的正式热更新编排。
- `HybridCLRKit`：读取已经准备好的 Manifest / DLL / AOT metadata，校验后进入热更代码。

因此不存在“大一统 HotUpdateKit”。内容热更与代码热更是两个独立职责。

## 推荐启动顺序

生产项目推荐：

```text
启动项目
  -> YooAssetContentUpdater.UpdateHostPackageAsync(...)
  -> HybridCLRKit.RunAsync(...)
  -> 进入热更程序集
```

`HybridCLRKit.RunAsync` 开始执行时，内容后端必须已经可用。HybridCLRKit 不会替你初始化 YooAsset 或 Addressables。

`YooAssetContentUpdater` 属于 `ResKit.YooAsset` Adapter，不属于 HybridCLRKit。它只是把 YooAsset 官方 HostPlayMode 内容更新流程收成一次调用；因此代码热更与内容热更仍然是两个独立职责。

## 运行时资源

默认约定：

```text
Assets/GameHotUpdate/Manifest/HotUpdateManifest.json
Assets/GameHotUpdate/Code/HotUpdate.dll.bytes
Assets/GameHotUpdate/Metadata/*.dll.bytes
```

这三类资产应进入 **同一个内容版本**。如果使用 YooAsset，它们应由同一个 ResourcePackage 管理；如果仅做本地验证，也可以让其他 ResKit 后端提供这些地址。

Manifest 不再额外复制到 `StreamingAssets/aa`，也不再由 HybridCLRKit 单独通过 HTTP 下载。这样可以避免 Manifest、DLL 与 metadata 出现跨版本组合。

## HotUpdateSettings

默认资源：

```text
Assets/Resources/HotUpdateSettings.asset
```

主要配置：

- `ResourceLoaderKey`：HybridCLRKit 读取代码热更资产时使用的 ResKit 后端，默认 `YooAsset`。
- `HotUpdateManifestKey`：Manifest 的 ResKit 地址。
- `HotUpdateAssemblyKey`：导出器选择主热更程序集时使用的默认地址。
- `HotUpdateEntryClass` / `HotUpdateEntryMethod`：导出 Manifest 时使用的默认入口。
- `AotMetadataKeys`：导出/Authoring 默认 metadata 列表。

运行时真正的 DLL SHA256、入口和 metadata 列表以 `HotUpdateManifest.json` 为事实来源。

## HotUpdateManifest.json

典型内容：

```json
{
  "version": 1,
  "buildTarget": "StandaloneWindows64",
  "hotUpdateAssemblyKey": "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
  "hotUpdateAssemblySha256": "...64位SHA256...",
  "hotUpdateEntryClass": "HotUpdate.HotUpdateMain",
  "hotUpdateEntryMethod": "Main",
  "aotMetadataKeys": [
    "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes"
  ]
}
```

正式 Release 下 SHA256 不能为空。Manifest 与 DLL 不一致时启动会失败，不会假装成功继续运行。

## 最小运行代码

使用 YooAsset 时，项目启动层可以保持为两步：

```csharp
YooAssetContentUpdateResult content =
    await YooAssetContentUpdater.UpdateHostPackageAsync(
        new YooAssetContentUpdateOptions
        {
            PackageName = "DefaultPackage",
            MainHostServer = "https://cdn.example.com/game/Windows"
        });

if (!content.Success)
{
    Debug.LogError(content.Error);
    return;
}

HybridCLRUpdateResult result = await HybridCLRKit.RunAsync();
if (!result.Success)
{
    Debug.LogError(result.Error);
    return;
}
```

如果项目不用 YooAsset，可把 `ResourceLoaderKey` 改为已经注册到 ResKit 的其他 Loader key。HybridCLRKit 不需要知道具体后端类型。

## Tools Hub 导出

Tools Hub 的 `HybridCLR DLL 导出` 负责：

1. 从 HybridCLR 生成目录收集热更 DLL。
2. 复制成 `.dll.bytes`。
3. 收集 AOT metadata。
4. 计算主热更 DLL SHA256。
5. 生成 `Assets/GameHotUpdate/Manifest/HotUpdateManifest.json`。

导出器不会：

- 构建 Addressables catalog。
- 初始化 YooAsset Package。
- 上传 CDN。
- 下载内容。
- 把 Manifest 复制到 `StreamingAssets/aa`。

这些职责由内容管线自行完成。

## Addressables 与 YooAsset

### YooAsset

推荐用于需要：

- 正式内容热更新。
- 版本管理。
- 下载器。
- 缓存管理。
- 多 Package。

HybridCLRKit 只在 YooAsset 内容准备完成后，通过 ResKit 读取 Manifest / DLL / metadata。

### Addressables

StellarFramework 中的 Addressables Adapter 只提供资源 Load / Release。Tools Hub 只保留本地 Settings / Group 配置和 Player Content 构建。

如果项目自己决定使用 Addressables 官方远端能力，那属于项目层选择；框架不会再把它包装成 HotUpdateKit 或与 HybridCLR 绑定。

## 生命周期与失败语义

`HybridCLRRunner` 使用一个 `ResScope` 包住本次启动所需的 Manifest、DLL 与 metadata。启动链结束后 Scope 自动释放底层资源句柄。

失败包括：

- ResKit 后端未注册。
- Manifest 读不到或 JSON 无效。
- Manifest 正式校验失败。
- DLL / metadata 资源缺失。
- DLL SHA256 不一致。
- HybridCLR metadata 加载失败，包括 `LoadMetadataForAOTAssembly` 返回非 `OK` 错误码。
- 热更程序集加载失败。
- 入口类或入口方法不存在。
- 入口执行抛异常。

取消操作会继续以 `OperationCanceledException` 向上传播，不会被伪装成普通资源缺失。

## 发布前检查

至少验证：

1. 目标平台已完成 HybridCLR Generate / AOT metadata 生成。
2. Tools Hub 已重新导出 DLL / metadata / Manifest。
3. Manifest SHA 与实际 DLL 一致。
4. 内容后端中 Manifest、DLL、metadata 属于同一个版本。
5. Release Player 能成功加载 AOT metadata 并进入热更入口。
6. 真实下载中断后，第二次启动能观察到正数 HTTP `Range` offset，而不是从 0 重新下载。
7. 断网、缺文件、损坏 DLL、错误 metadata 都能得到明确失败结果。

当前仓库提供 Verification 专用链路：构建内容会复制到 `Temp/StellarHotUpdateVerification/RemoteCDN` 模拟远端 CDN，客户端缓存独立放在 `ClientCache`。验证会故意在大 Bundle 下载中途断开 TCP，再重新创建 Package，只有第二次请求确实从已有字节 `Range` 续传、ResKit 能读取同版本 Manifest/DLL、SHA 正确且热更入口执行成功，才会记录 PASS。该验证不依赖 `StreamingAssets`。

## 相关文档

- [HybridCLRKit 源码文档](HybridCLRKit-代码热更新-源码文档-Guide.md)
- [ResKit 统一资源说明](../Reskit/ResKit-统一资源-说明文档-Guide.md)
