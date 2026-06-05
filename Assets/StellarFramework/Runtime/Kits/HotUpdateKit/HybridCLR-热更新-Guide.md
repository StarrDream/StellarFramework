# HybridCLR / 代码热更新 Guide

HotUpdateKit 负责启动期热更新编排：资源更新走策略，代码热更默认使用 HybridCLR。当前只保证启动期装载，不承诺运行中替换已经加载的程序集。

## 1. 环境准备

1. 安装并配置 HybridCLR。
2. 在 Player Settings 的 Scripting Define Symbols 中添加 `HYBRIDCLR_ENABLE`。
3. 在 HybridCLR Settings 中配置热更程序集，例如 `HotUpdate.dll`。
4. 生成热更 dll 和 AOT metadata dll。
5. 将产物改名为 `.dll.bytes`，作为 Addressables `TextAsset` 资源。

未启用 `HYBRIDCLR_ENABLE` 时，框架仍可编译，运行时会返回明确的不可用结果。

## 2. Runtime Settings

创建或打开 `Resources/HotUpdateSettings.asset`，配置：

- `HotUpdateAssemblyKey`：热更程序集 TextAsset address，例如 `Assets/Game/HotUpdate/HotUpdate.dll.bytes`。
- `HotUpdateAssemblySha256`：热更 dll.bytes 的 SHA256；留空表示不校验。
- `AotMetadataKeys`：AOT metadata TextAsset address 列表。
- `HotUpdateEntryClass`：热更入口完整类名，例如 `HotUpdate.HotUpdateMain`。
- `HotUpdateEntryMethod`：入口静态方法名，例如 `Main`。
- `AddressablesDefaultHotUpdateLabels`：热更资源 label，默认 `hotupdate`。

所有 dll.bytes 和 metadata.bytes 推荐使用完整 `Assets/...` address，并打上同一组热更 label。

## 3. 启动期 AA 闭环

1. 使用 ToolsHub 的 `HybridCLR DLL 导出` 把 `HotUpdate.dll` 和 AOT metadata 复制成 `.dll.bytes`，并生成 `HotUpdateManifest.json`。
2. 在 Addressables 官方 Groups 窗口把这些文件加入 Group，address 设置为完整 `Assets/...` 路径，并添加热更 label。
3. 使用 ToolsHub 的 `AA 配置与发布` 执行 `一键本地内置构建` 或 `一键远端热更发布`；需要官方 Content Update 时仍走 Addressables 官方流程。
4. 远端模式上传或发布 remote catalog、hash、bundle 和同批次 Manifest。
5. 游戏最早入口调用：

```csharp
HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
HybridCLRAAHotUpdateResult result = await HybridCLRAAHotUpdateRunner.RunAsync(
    settings,
    progress => Debug.Log($"Hot update: {progress:P0}"),
    destroyCancellationToken);

if (!result.Success)
{
    Debug.LogError(result.Error);
    return;
}
```

## 8. HotUpdateManifest.json code update flow

Code hot update must not depend on a SHA value baked into the Player. `HotUpdateManifest.json`
is the runtime source of truth for the hot-update DLL key, SHA256, AOT metadata keys, and entry
method.

The ToolHub item `HybridCLR DLL 导出` copies HybridCLR generated DLLs into `.dll.bytes` assets
and writes two manifest copies:

- `Assets/GameHotUpdate/Manifest/HotUpdateManifest.json`
- `Assets/StreamingAssets/aa/HotUpdateManifest.json`

The manifest shape is:

```json
{
  "version": 1,
  "buildTarget": "StandaloneWindows64",
  "hotUpdateAssemblyKey": "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
  "hotUpdateAssemblySha256": "82bb7e922f887a9460e44c5c2a282239ac5d9722dca1f0a7bf2e0536b18cb77c",
  "hotUpdateEntryClass": "HotUpdate.HotUpdateMain",
  "hotUpdateEntryMethod": "Main",
  "aotMetadataKeys": [
    "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes",
    "Assets/GameHotUpdate/Metadata/System.dll.bytes",
    "Assets/GameHotUpdate/Metadata/System.Core.dll.bytes",
    "Assets/GameHotUpdate/Metadata/UnityEngine.CoreModule.dll.bytes"
  ]
}
```

The exporter prefers the DLL whose destination asset path matches `HotUpdateSettings.HotUpdateAssemblyKey`.
If no configured key matches the exported DLL list, it falls back to the first exported hot-update DLL.
When multiple hot-update DLLs exist, set `HotUpdateAssemblyKey` explicitly instead of relying on list order.

Runtime manifest source order:

1. `HotUpdateSettings.HotUpdateManifestPathOrUrl`, if set.
2. `Application.streamingAssetsPath/aa/HotUpdateManifest.json`, if StreamingAssets fallback is enabled.
   The old `aa/<BuildTarget>/HotUpdateManifest.json` path is still tried as a compatibility fallback.
3. The old `HotUpdateSettings` DLL fields, if Resources fallback is enabled.

For local AA mode, rebuild Addressables and replace the Player's `StreamingAssets/aa`
folder. That folder should contain `HotUpdateManifest.json`, Addressables `settings.json`,
the local catalog, and bundles under the Addressables platform folder such as
`Windows/StandaloneWindows64`.

For no-webserver remote testing, copy the AA output and manifest to a folder such as
`D:/HotUpdate/StandaloneWindows64`, then set:

```text
HotUpdateManifestPathOrUrl = file:///D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json
```

ToolsHub provides `热更新 / AA 配置与发布` for this workflow. The default remote workflow publishes
to `D:/HotUpdate/<BuildTarget>`, writes the matching manifest URL into
`HotUpdateSettings.asset`, and validates the manifest/catalog/hash/bundle files. Developers can
edit local or remote workflow configs in the tool. The config asset lives under
`Editor/StellarToolsHub/Configs`, while the currently selected item is kept per developer.

For custom Addressables publishing, enable `Apply AA Profile` in the tool and optionally let it set
`Remote.BuildPath`, `Remote.LoadPath`, and `Build Remote Catalog` before running Addressables build.
This keeps official Addressables profiles as the source of AA layout decisions while still giving a
single button for export, build, publish, and manifest configuration.

For production HTTP/HTTPS, set the same field to an HTTP URL. The HTTP manifest source uses
the framework `HttpKit` internally:

```text
HotUpdateManifestPathOrUrl = https://example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json
```

FTP or authenticated private delivery should implement `IHotUpdateManifestSource` and register a
custom code hot-update strategy or runner wrapper. Keep protocol-specific credentials, retries,
and platform behavior outside the default HTTP/file sources.

Minimal startup call:

```csharp
HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
HybridCLRAAHotUpdateResult result = await HotUpdateKit.RunStartupHotUpdateAsync(
    settings,
    progress => Debug.Log($"Hot update: {progress:P0}"),
    destroyCancellationToken);

if (!result.Success)
{
    Debug.LogError(result.Error);
    return;
}

Debug.Log($"Manifest={result.ManifestSource}");
```

When a manifest is available, the runner verifies `HotUpdate.dll.bytes` against the manifest SHA.
Changing the DLL only requires publishing the new `.dll.bytes`, its Addressables catalog/bundle
content, and the matching `HotUpdateManifest.json`.

Runner 顺序：

1. 初始化 Addressables。
2. 检查并更新 Catalog。
3. 下载 dll.bytes 和 AOT metadata 依赖。
4. 加载 TextAsset 字节流。
5. 校验热更 dll SHA256。
6. 调用 `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly`。
7. `Assembly.Load` 热更 dll。
8. 调用配置中的静态入口。

## 4. 热更入口规范

```csharp
namespace HotUpdate
{
    public static class HotUpdateMain
    {
        public static void Main()
        {
            UnityEngine.Debug.Log("Entered HotUpdate assembly.");
        }
    }
}
```

入口类名和方法名必须与 `HotUpdateSettings` 一致，方法必须是 `public static`。

## 5. 产物处理

当前推荐把“导出 DLL、生成 Manifest、构建和发布 AA”交给 ToolsHub 的热更新工具完成；底层 Addressables 资源分组、Analyze、Content Update 和 HybridCLR 裁剪流程仍按官方工具执行。手动或 CI 流水线需要完成同样的产物闭环：

- 从 HybridCLR 输出目录复制 `.dll`。
- 重命名为 `.dll.bytes`。
- 计算 SHA256。
- 将 `.dll.bytes` 和 AOT metadata 放入项目热更资源目录。
- 在 Addressables 官方 Groups 中配置 address/labels，或由项目自己的自动化脚本完成。
- 生成同批次 `HotUpdateManifest.json`，并确保 Manifest、catalog、hash、bundle 一起发布。

真实 dll 生成、裁剪和 AOT metadata 以 HybridCLR 官方流程为准。

## 6. 常见错误排查

- `HYBRIDCLR_ENABLE is not enabled`：未开启宏，Runner 会直接失败退出。
- `HotUpdateAssemblyKey is empty`：检查 `HotUpdateSettings` 是否在 Resources 下并配置正确。
- SHA256 不匹配：确认上传的 dll.bytes 与配置中的 hash 是同一个文件。
- AOT metadata 缺失：确认 `AotMetadataKeys` 都是 Addressables 可加载的 TextAsset。
- metadata 类型不匹配：重新生成 AOT metadata，并确认 HybridCLR Settings 与主工程一致。
- 找不到入口类/方法：入口必须包含完整命名空间，方法必须是 `public static`。
- Catalog 无更新：确认远端 `.hash` 文件已上传，`RemoteLoadPath` 指向正确版本。

## 7. 可复制模板

### 7.1 资源热更检查与下载

适合：启动时先检查 catalog 和资源包更新，再决定是否进入大厅。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

public sealed class StartupResourceUpdate : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        HotUpdateOperationResult init = await HotUpdateKit.InitializeAsync(destroyCancellationToken);
        if (!init.Success)
        {
            Debug.LogError($"热更初始化失败: {init.Error}");
            return;
        }

        HotUpdateCheckResult check = await HotUpdateKit.CheckResourceUpdatesAsync(
            keys: new object[] { "hotupdate" },
            updateCatalogs: true,
            cancellationToken: destroyCancellationToken);

        if (!check.Success)
        {
            Debug.LogError($"热更检查失败: {check.Error}");
            return;
        }

        if (!check.HasUpdate)
        {
            Debug.Log("当前没有资源更新");
            return;
        }

        HotUpdateDownloadResult download = await HotUpdateKit.DownloadResourceUpdatesAsync(
            check.Keys,
            progress => Debug.Log($"下载进度: {progress.Percent:P0}"),
            destroyCancellationToken);

        if (!download.Success)
        {
            Debug.LogError($"热更下载失败: {download.Error}");
            return;
        }

        Debug.Log("资源热更完成，可以进入游戏");
    }
}
```

### 7.2 启动期代码热更

适合：使用 HybridCLR 加载 `dll.bytes` 和 AOT metadata。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

public sealed class StartupCodeUpdate : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
        HybridCLRAAHotUpdateResult result = await HotUpdateKit.RunCodeHotUpdateAsync(
            settings,
            progress => Debug.Log($"代码热更进度: {progress:P0}"),
            destroyCancellationToken);

        if (!result.Success)
        {
            Debug.LogError($"代码热更失败: {result.Error}");
            return;
        }

        Debug.Log("代码热更入口执行完成");
    }
}
```

### 7.3 一次跑完整启动链路

适合：把代码热更放到启动入口统一执行。注意 `RunStartupHotUpdateAsync(...)` 当前只封装代码热更，资源热更仍需显式调用 `InitializeAsync`、`CheckResourceUpdatesAsync` 和 `DownloadResourceUpdatesAsync`。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

public sealed class StartupHotUpdateEntry : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
        HybridCLRAAHotUpdateResult result = await HotUpdateKit.RunStartupHotUpdateAsync(
            settings,
            progress => Debug.Log($"启动热更进度: {progress:P0}"),
            destroyCancellationToken);

        if (!result.Success)
        {
            Debug.LogError($"启动热更失败: {result.Error}");
            return;
        }

        Debug.Log("启动热更完成，继续进入游戏逻辑");
    }
}
```

### 7.4 热更入口程序集模板

```csharp
namespace HotUpdate
{
    public static class HotUpdateMain
    {
        public static void Main()
        {
            UnityEngine.Debug.Log("进入热更程序集入口");
        }
    }
}
```
