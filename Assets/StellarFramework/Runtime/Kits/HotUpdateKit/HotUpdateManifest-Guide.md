# HotUpdateManifest 教学

`HotUpdateManifest.json` 是 HybridCLR 热更 DLL 的运行时清单。它记录本次要加载的 DLL key、SHA256、入口类、入口方法和 AOT metadata 列表。

不要手写 Manifest。它应该由 ToolHub 在导出 `.dll.bytes` 时自动生成，SHA256 来自真实的 `HotUpdate.dll.bytes` 文件。

## 先选 AA 工作流

ToolHub 路径：

```text
StellarFramework Tools -> 热更新 -> AA 配置与发布
```

默认只保留两套配置：

```text
本地内置 AA
远端热更 AA
```

怎么选：

- `本地内置 AA`：AA 替代 AB，内容随 Player 放在 `StreamingAssets`，不做远端下载。
- `远端热更 AA`：旧 Player 不重打包，从 D 盘、HTTP、CDN 等远端位置读取 Manifest、catalog、hash、bundle。

详细步骤：

- `AA-LocalBuiltIn-Guide.md`
- `AA-RemoteHotUpdate-Guide.md`

## Manifest 文件会生成到哪里

工具会生成两份：

```text
Assets/GameHotUpdate/Manifest/HotUpdateManifest.json
Assets/StreamingAssets/aa/HotUpdateManifest.json
```

第一份用于工程内查看和调试。第二份会跟随 AA 输出一起进入本地内置目录，或者被发布到远端目录。

## Manifest 内容结构

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

## Manifest 主 DLL 选择规则

导出器会优先读取 `Resources/HotUpdateSettings.asset` 里的 `HotUpdateAssemblyKey`，并在本次导出的热更 DLL 列表中优先选择与该 key 匹配的 `.dll.bytes` 写入 `hotUpdateAssemblyKey` 和 `hotUpdateAssemblySha256`。

如果当前配置为空，或本次导出列表里没有匹配项，才会退回到第一条热更 DLL 记录。

这意味着：

- 推荐把 `HotUpdateSettings.HotUpdateAssemblyKey` 配成最终运行时真正要加载的那个 `Assets/.../*.dll.bytes` address。
- 如果同时导出多个热更 DLL，不要默认认为 Manifest 一定会取列表第一项。

## 运行时加载顺序

`HybridCLRAAHotUpdateRunner` 会按下面顺序找 Manifest：

1. `HotUpdateSettings.HotUpdateManifestPathOrUrl`，如果配置了远端地址。
2. `Application.streamingAssetsPath/aa/HotUpdateManifest.json`，如果允许 StreamingAssets fallback。
   旧的 `aa/<BuildTarget>/HotUpdateManifest.json` 会作为兼容路径再尝试一次。
3. `HotUpdateSettings` 中的旧 DLL 字段，如果允许 Resources fallback。

本地内置 AA 通常走第 2 条。远端热更 AA 应该走第 1 条，并默认关闭 fallback，避免远端失败时误以为热更成功。

## 日志判断

本地内置：

```text
Manifest=StreamingAssets:<Player>/StreamingAssets/aa/HotUpdateManifest.json
```

D 盘模拟远端：

```text
Manifest=File:file:///D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json
```

HTTP/CDN：

```text
Manifest=Http:https://example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json
```

## 发布检查清单

每次发布 AA 内容时，Manifest、catalog、hash、bundle 必须是同一批。

本地内置 AA 目录里应该包含：

```text
HotUpdateManifest.json
Windows/settings.json
Windows/catalog.json
Windows/StandaloneWindows64/*.bundle
```

远端热更 AA 目录里应该包含：

```text
HotUpdateManifest.json
catalog_*.json
catalog_*.hash
*.bundle
```

如果只替换其中一部分，常见结果就是：

```text
Hot update dll SHA256 mismatch
```

出现这个错误时，重新执行对应页签的一键流程，不要手动只复制单个文件。

## 启动示例

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

public sealed class StartupHotUpdate : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
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
        Debug.Log($"Assembly={result.LoadedAssemblyFullName}");
    }
}
```
