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

## 新项目正式接入：从导包到 CDN

如果目标项目需要直接复用 StellarFramework 当前已经验证过的完整热更能力，推荐从 Tools Hub 导出并导入：

```text
StellarFramework-Profile-HotUpdate-Full.unitypackage
```

该 Recommended Profile 由以下能力闭包组成：

```text
ResKit.Core
ResKit.YooAsset
ResKit.Tools
HybridCLRKit
HybridCLRKit.Tools
```

Bootstrap 会安装当前 Catalog 声明的第三方依赖。当前正式锁定的关键依赖包括：

```text
UniTask   -> e5acc106ee196bc5a32fb14cdf2987b0f96d11e0
YooAsset  -> 2.3.19
HybridCLR -> 4feac30cb2e105992986c737f7f54992b8300e1a
```

业务项目不要再额外复制 StellarFramework 仓库里的 Verification、Tests 或 Android Harness；它们属于框架维护门禁，不属于运行时热更包。

### 1. 首次建立热更工程结构

目标项目至少准备：

```text
Assets/GameHotUpdate/Source/HotUpdate.asmdef
Assets/GameHotUpdate/Source/HotUpdateMain.cs
Assets/Resources/HotUpdateSettings.asset
```

推荐入口保持简单：

```csharp
namespace HotUpdate
{
    public static class HotUpdateMain
    {
        public static void Main()
        {
            // 在这里进入项目自己的热更启动逻辑。
        }
    }
}
```

`HotUpdateSettings.asset` 默认建议：

```text
ResourceLoaderKey      = YooAsset
HotUpdateManifestKey   = Assets/GameHotUpdate/Manifest/HotUpdateManifest.json
HotUpdateAssemblyKey   = Assets/GameHotUpdate/Code/HotUpdate.dll.bytes
HotUpdateEntryClass    = HotUpdate.HotUpdateMain
HotUpdateEntryMethod   = Main
```

### 2. 每个目标平台先生成 HybridCLR 产物

热更发布不能跨平台共用 AOT metadata。Windows、Android、iOS 等目标必须分别在对应 BuildTarget 下执行 HybridCLR 官方生成流程，再执行 StellarFramework 的 HybridCLR 导出工具。

导出后应得到：

```text
Assets/GameHotUpdate/Code/HotUpdate.dll.bytes
Assets/GameHotUpdate/Metadata/*.dll.bytes
Assets/GameHotUpdate/Manifest/HotUpdateManifest.json
```

必须检查 Manifest 的 `buildTarget` 与当前发布平台一致，并确认 Manifest 中的 DLL SHA256 与实际 `HotUpdate.dll.bytes` 一致。

### 3. 把热更资产交给 YooAsset

以下内容必须进入**同一个 YooAsset ResourcePackage / 同一个 package version**：

```text
HotUpdateManifest.json
HotUpdate.dll.bytes
AOT metadata *.dll.bytes
以及本次需要热更新的普通资源
```

不要把 Manifest 单独放 Web API，把 DLL 放 CDN，又把 metadata 放 StreamingAssets。当前框架故意要求三者走同一个 ResKit/YooAsset 内容版本，以避免跨版本组合。

### 4. YooAsset 构建完成后，服务器到底放什么

服务器不需要运行 Unity，也不需要专门写一个“热更后端”。本质上只需要一个支持静态文件下载的 HTTP/HTTPS 服务，例如：

- Nginx；
- 阿里云 OSS / 腾讯云 COS / AWS S3；
- 对象存储 + CDN；
- 其他能正确支持 HTTP Range 的静态 CDN。

YooAsset 构建完成后，**把该平台该 package version 的远端输出目录整体上传，保持文件名与目录内容不变**。不要人工只挑 `.bundle` 文件上传，因为版本文件、Manifest 与 Bundle 都属于同一发布单元。

推荐服务器按“渠道 / 平台 / 基础 App 版本”隔离，例如：

```text
https://cdn.example.com/mygame/prod/android/1.0.0/DefaultPackage/
https://cdn.example.com/mygame/prod/windows/1.0.0/DefaultPackage/
```

`MainHostServer` 必须指向**YooAsset 远端文件所在目录本身**。当前 `YooAssetContentUpdater` 的 RemoteServices 只是执行：

```text
MainHostServer + "/" + fileName
```

因此不要把 URL 指到站点首页，也不要再额外多拼一层不存在的目录。

### 5. CDN / Web Server 必须满足的条件

生产环境建议：

1. 使用 HTTPS；
2. 支持普通 GET；
3. 支持 `Range: bytes=...`，续传时返回正确的 `206 Partial Content`；
4. 不要让网关把 Bundle 请求改写成 HTML 错误页；
5. Bundle 文件使用不可变文件策略，已经发布的旧 Bundle 不要原地覆盖；
6. 版本入口 / Manifest 可以使用较短缓存或显式刷新策略；
7. 大 Bundle 建议 CDN 保留 `Accept-Ranges: bytes`；
8. 配置 `FallbackHostServer` 时，备用 CDN 必须拥有同一版本的完整文件集。

正式上线前至少用一次真实断网/中断验证确认第二次请求存在正数 Range offset。框架自己的 Release Gate 已验证 YooAsset 2.3.x 的缓存与断点恢复链，但业务 CDN 的反向代理/CDN 配置仍需要项目自己验证。

### 6. 游戏启动时如何检查更新

推荐在“进入业务主流程之前”执行：

```csharp
var contentOptions = new YooAssetContentUpdateOptions
{
    PackageName = "DefaultPackage",
    MainHostServer =
        "https://cdn.example.com/mygame/prod/android/1.0.0/DefaultPackage",
    FallbackHostServer =
        "https://cdn-backup.example.com/mygame/prod/android/1.0.0/DefaultPackage",
    BuildinPackageRoot = null,
    CachePackageRoot = null,
    RetryPolicy = new YooAssetContentUpdateRetryPolicy(2, 500)
};

YooAssetContentUpdateResult content =
    await YooAssetContentUpdater.UpdateHostPackageAsync(contentOptions);

if (!content.Success)
{
    Debug.LogError(
        $"Content update failed: {content.ErrorCode} / {content.FailureStage} / {content.Error}");
    return;
}

HybridCLRUpdateResult code = await HybridCLRKit.RunAsync();
if (!code.Success)
{
    Debug.LogError("Code update failed: " + code.Error);
    return;
}
```

建议实际项目再包一层项目自己的 `GameUpdateService`，负责：

- 更新 UI；
- 下载大小确认；
- Wi-Fi / 蜂窝策略；
- 失败重试与退出；
- 渠道 / 环境 / CDN 地址选择；
- 埋点与版本日志。

不要把这些业务决策塞回 HybridCLRKit。

### 7. 一次正常的“热更发布”应该怎么做

假设线上基础 App 为 `1.0.0`：

```text
修改热更代码/资源
  -> 切到目标 BuildTarget
  -> 生成 HotUpdate.dll
  -> 使用该基础 App 对应的 AOT metadata
  -> 重新导出 Manifest + DLL + metadata
  -> 构建新的 YooAsset package version
  -> 本地/预发布环境运行 Release Gate
  -> 先上传所有新 Bundle / Manifest
  -> 最后发布新的远端 package version 入口
  -> 客户端下次启动 RequestPackageVersion
  -> 下载差异文件
  -> ResKit 安装 YooAsset Loader
  -> HybridCLRKit 校验 SHA / metadata
  -> Assembly.Load
  -> 进入 HotUpdateMain.Main
```

发布顺序最关键的一点是：**先把版本所需文件全部上传完整，再让客户端看到新版本入口**。否则客户端可能先拿到新版本号，却在 CDN 上找不到对应 Bundle。

### 8. AOT metadata 与基础 App 版本的关系

AOT metadata 不是可以随意跨基础包替换的普通配置。它必须与用户设备上实际安装的基础 Player/AOT 程序保持兼容。

推荐按基础 App 版本隔离 CDN 根目录：

```text
android/1.0.0/...
android/1.1.0/...
```

当 `1.1.0` 更换了 Unity/HybridCLR、AOT assembly、裁剪配置、原生插件或大量基础程序集后，应重新生成该基础版本自己的 metadata 和热更内容，不要让 `1.0.0` 客户端读取 `1.1.0` 的 metadata。

简单理解：

```text
基础 APK/EXE = 这一代热更环境的“底座”
HotUpdate.dll + YooAsset 内容 = 可以在这个底座上迭代的远端层
```

如果修改内容已经超出当前底座能力，例如新增/替换原生插件、修改 Unity Player 本身、需要新的平台权限或必须改变基础 AOT 构建，则应该发布新的 App，而不是强行远端热更。

### 9. 回滚策略

服务器至少保留最近若干个已经验证过的 package version，不要发布后立即删旧 Bundle。

发生线上问题时推荐：

1. 停止继续发布新版本；
2. 将远端 package version 入口恢复到上一已知正常版本；
3. 保留旧 Manifest / Bundle，确保客户端可以重新解析；
4. 如果错误版本已经被客户端下载，依赖 YooAsset package version / Manifest 重新切回旧版本，不通过手工删除客户端目录修复；
5. 如果问题来自基础 Player / AOT 不兼容，则回滚热更不够，应回滚或重新发布 App。

### 10. 推荐生产目录与环境隔离

建议至少区分：

```text
dev
staging
prod
```

例如：

```text
cdn.example.com/mygame/dev/android/1.0.0/DefaultPackage/
cdn.example.com/mygame/staging/android/1.0.0/DefaultPackage/
cdn.example.com/mygame/prod/android/1.0.0/DefaultPackage/
```

不要让开发服覆盖正式服目录。正式服发布建议由 CI/CD 上传已通过 Gate 的同一份构建产物，而不是开发者在服务器上重新生成一次内容。

### 11. 新项目最小验收清单

第一次接入完成后至少确认：

- Hot Update Full Bootstrap 导入后编译 0 Error；
- UniTask / YooAsset / HybridCLR 版本与当前框架 Catalog 一致；
- 目标平台 HybridCLR Generate 成功；
- Manifest `buildTarget` 正确；
- DLL SHA256 校验通过；
- YooAsset 可以从真实 CDN 请求版本与 Manifest；
- 第一次启动可以下载内容；
- 第二次启动命中缓存；
- 模拟下载中断后能够出现 HTTP Range 续传；
- `LoadMetadataForAOTAssembly` 成功；
- `HotUpdate.dll` 实际加载；
- `HotUpdateMain.Main` 实际执行；
- Release 非 Development Player 至少完整跑通一次。

只有 Editor 里能运行不能视为正式接入完成。

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

Android Release IL2CPP 使用另一条互补 Gate：`Tools/AndroidVerification/Invoke-StellarAndroidReleaseVerification.ps1 -HotUpdate`。Editor Prepare 要求 Active Build Target 为 Android，临时设置 IL2CPP + x86_64 后运行 HybridCLR `Generate/All`，重新生成 Android HotUpdate DLL 与 stripped AOT assemblies，再导出 Android Manifest / SHA 并构建 YooAsset verification package；Player 设置在 `finally` 中恢复。验证 Player 通过 Android Intent extras 连接 adb reverse 的 loopback CDN，不修改 `HotUpdateSettings`。

Android Player 的机器结果必须同时证明 YooAsset 更新成功、ResKit 读取 Manifest/DLL、Manifest target 为 Android、DLL SHA256 匹配、HybridCLR 每个 AOT metadata 加载成功、HotUpdate Assembly 已加载、Manifest 配置的 `HotUpdate.HotUpdateMain.Main` 已执行并输出入口 marker。冷启动须下载内容并写入 persistent cache；force-stop/restart 后须复用该缓存且报告 0 个重新下载文件。该 Gate 不替代 Editor 的 Range 中断恢复验证，两者覆盖不同证据。

## 相关文档

- [HybridCLRKit 源码文档](HybridCLRKit-代码热更新-源码文档-Guide.md)
- [ResKit 统一资源说明](../Reskit/ResKit-统一资源-说明文档-Guide.md)
