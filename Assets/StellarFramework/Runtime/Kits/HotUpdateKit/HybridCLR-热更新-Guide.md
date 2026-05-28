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

创建或打开 `Resources/ResKitRuntimeSettings.asset`，配置：

- `HotUpdateAssemblyKey`：热更程序集 TextAsset address，例如 `Assets/Game/HotUpdate/HotUpdate.dll.bytes`。
- `HotUpdateAssemblySha256`：热更 dll.bytes 的 SHA256；留空表示不校验。
- `AotMetadataKeys`：AOT metadata TextAsset address 列表。
- `HotUpdateEntryClass`：热更入口完整类名，例如 `HotUpdate.HotUpdateMain`。
- `HotUpdateEntryMethod`：入口静态方法名，例如 `Main`。
- `AddressablesDefaultHotUpdateLabels`：热更资源 label，默认 `hotupdate`。

所有 dll.bytes 和 metadata.bytes 推荐使用完整 `Assets/...` address，并打上同一组热更 label。

## 3. 启动期 AA 闭环

1. 把 `HotUpdate.dll.bytes` 和 AOT metadata `.dll.bytes` 放入项目。
2. 在 Addressables 官方 Groups 窗口把这些文件加入 Group，address 设置为完整 `Assets/...` 路径，并添加热更 label。
3. 在 Addressables 官方 Groups 窗口执行完整构建，或使用官方 Content Update 流程。
4. 上传 remote catalog、hash 和 bundle。
5. 游戏最早入口调用：

```csharp
ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();
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

入口类名和方法名必须与 `ResKitRuntimeSettings` 一致，方法必须是 `public static`。

## 5. 产物处理

StellarFramework 不在 ToolHub 中处理 AA 或第三方资源插件的构建配置。推荐在项目流水线中完成：

- 从 HybridCLR 输出目录复制 `.dll`。
- 重命名为 `.dll.bytes`。
- 计算 SHA256。
- 将 `.dll.bytes` 和 AOT metadata 放入项目热更资源目录。
- 在 Addressables 官方 Groups 中配置 address/labels，或由项目自己的自动化脚本完成。
- 将热更 dll 的 key 和 SHA256 写回 `ResKitRuntimeSettings`。

真实 dll 生成、裁剪和 AOT metadata 以 HybridCLR 官方流程为准。

## 6. 常见错误排查

- `HYBRIDCLR_ENABLE is not enabled`：未开启宏，Runner 会直接失败退出。
- `HotUpdateAssemblyKey is empty`：检查 `ResKitRuntimeSettings` 是否在 Resources 下并配置正确。
- SHA256 不匹配：确认上传的 dll.bytes 与配置中的 hash 是同一个文件。
- AOT metadata 缺失：确认 `AotMetadataKeys` 都是 Addressables 可加载的 TextAsset。
- metadata 类型不匹配：重新生成 AOT metadata，并确认 HybridCLR Settings 与主工程一致。
- 找不到入口类/方法：入口必须包含完整命名空间，方法必须是 `public static`。
- Catalog 无更新：确认远端 `.hash` 文件已上传，`RemoteLoadPath` 指向正确版本。
