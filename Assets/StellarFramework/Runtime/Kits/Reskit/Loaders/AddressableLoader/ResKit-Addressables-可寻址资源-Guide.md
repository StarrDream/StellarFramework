# ResKit Addressables / 可寻址资源 Guide

Addressables 是 ResKit 推荐的生产热更资源后端。ResKit 负责统一加载入口和运行时热更流程，Addressables 的构建、清理、Content Update 继续使用 Unity 官方 Addressables 窗口。

## 1. 边界约定

- AB 没有官方构建面板，StellarFramework 的 ToolHub 可以提供 AB 构建。
- AA、YooAsset 等插件已有官方/插件构建流程，ToolHub 不再替代它们的构建器。
- ToolHub 的 Addressables 模块只做：Settings/Profile 检查、address 同步为 `Assets/...`、labels 维护、Runtime Settings 定位、HybridCLR dll.bytes 处理、打开官方 Addressables 窗口。
- 业务代码使用同一份路径：`Assets/...`。这样 AB 和 AA 可以共享资源 key。

## 2. 本地 AA 加载闭环

1. 安装 `com.unity.addressables`，等待 asmdef 自动启用 `UNITY_ADDRESSABLES`。
2. 打开 `Window -> Asset Management -> Addressables -> Groups`。
3. 将资源加入 Addressables Group。
4. 打开 `StellarFramework -> Tools Hub -> [框架核心] -> 资源配置 (Addressables)`。
5. 点击 `创建/定位 Runtime Settings`。
6. 选择资源目录或 Project 中选中资源，点击 `使用配置 Labels` 和 `应用 Address/Labels`。
7. 回到 Addressables Groups，选择合适的 Play Mode Script：
   - 编辑器快速验证：`Use Asset Database`
   - 模拟构建加载：`Simulate Groups`
   - 生产验收：`Use Existing Build`
8. 在 Addressables 官方窗口执行 `Build -> New Build -> Default Build Script`。
9. 运行场景，通过 `AddressableLoader` 异步加载：

```csharp
IResLoader loader = ResKit.Allocate<AddressableLoader>();
GameObject prefab = await loader.LoadAsync<GameObject>(
    "Assets/Game/Prefabs/Hero.prefab",
    destroyCancellationToken);
```

同步 `Load<T>` 在 AA 下会 fail-fast 返回 `null`，避免远端资源阻塞或死锁。

## 3. 远端 AA 热更闭环

1. 在 Addressables Settings 开启 `Build Remote Catalog`。
2. 配置 Profile：
   - `RemoteBuildPath`：本机输出目录。
   - `RemoteLoadPath`：客户端可访问的 CDN/文件服务器 URL。
3. ToolHub 执行 `检查 Settings/Profile`，确认 Remote Path、Group Schema、address、labels 都通过。
4. ToolHub 执行 `应用 Address/Labels`。
5. 在 Addressables 官方 Groups 窗口执行完整构建。
6. 上传 remote catalog、hash 和 bundle 文件。
7. 启动时执行：

```csharp
ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();
AddressableHotUpdateManager manager = AddressableHotUpdateManager.Instance;

UpdateCheckResult check = await manager.CheckCatalogUpdatesAsync(
    settings.BuildAddressablesDefaultUpdateKeys(),
    settings.AddressablesUpdateCatalogsOnCheck,
    token);

if (check.IsSuccess && check.HasUpdate)
{
    AddressableDownloadResult download = await manager.DownloadDependenciesAsync(
        settings.BuildAddressablesDefaultUpdateKeys(),
        progress => Debug.Log($"AA download: {progress.Percent:P0}"),
        token);
}
```

## 4. Content Update

1. 保留上一版正式发布的 `addressables_content_state.bin`。
2. 修改需要热更的 Addressables 资源。
3. 使用 Addressables 官方 `Build -> Update a Previous Build` 流程。
4. 上传新的 catalog/hash/bundle。
5. 客户端通过 `CheckCatalogUpdatesAsync` 和 `DownloadDependenciesAsync` 更新。

ToolHub 不持有 Content State，也不调用 Content Update 构建，只负责让配置、address 和 labels 更容易检查。

## 5. 真机与压力验收

- Android/iOS 真机使用 `Use Existing Build` 验证，不只看 Editor Play Mode。
- 断网、弱网、取消下载、CDN 404、catalog hash 未更新都要跑一遍。
- 重复 50 到 200 次加载/释放同一 prefab，确认 handle 正确释放、无持续增长。
- 切后台再回来后继续下载/加载，确认不会卡死。
- 资源热更后确认旧客户端能下载新 catalog 并加载新资源。

## 6. 常见错误排查

- `UNITY_ADDRESSABLES` 未启用：确认已安装 Addressables 包并让 Unity 重新编译。
- 加载返回 `null`：确认 address 是完整 `Assets/...prefab`，并且资源已加入 Addressables。
- Editor 能加载、真机不能加载：真机必须使用已构建并可访问的 catalog/bundle。
- `Use Existing Build` 加载失败：先在 Addressables 官方窗口重新 Build。
- Catalog 无更新：确认 `.hash` 文件已上传，`RemoteLoadPath` 指向正确版本。
- 下载大小为 0：检查 labels/keys 是否和 entry 上的 label 一致。
- 同步接口失败：AA 生产模式只承诺异步加载，请改用 `LoadAsync<T>`。
