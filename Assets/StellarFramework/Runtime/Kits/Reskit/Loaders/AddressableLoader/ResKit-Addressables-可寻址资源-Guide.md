# ResKit Addressables / 可寻址资源 Guide

Addressables 是 ResKit 推荐的生产热更资源后端。ResKit 负责统一加载入口、运行时 Catalog 检查、依赖下载和释放；ToolsHub 的 `AA 配置与发布` 负责常用本地内置 AA 与远端热更 AA 工作流；Addressables 的 Groups、Analyze、Play Mode Script、Content Update 和 catalog/hash/bundle 机制仍以 Unity 官方流程为准。

## 1. 边界约定

- AB 没有官方统一业务构建面板，StellarFramework 的 ToolHub 可以提供 AB 构建。
- AA 的底层资源组织使用 Unity Addressables 官方 Groups、Profiles、Analyze、Build、Content Update。
- ToolsHub 提供 `AA 配置与发布`，用于写入 Profile、切换 Remote Catalog、导出 Manifest、Build Addressables、复制发布目录和校验产物。
- YooAsset 或其他第三方资源插件使用插件自己的构建界面和流水线。
- StellarFramework 不接管第三方资源插件的构建面板，只通过 `ResKit.RegisterCustomLoader` 接入第三方 loader。
- 业务代码建议统一使用完整 `Assets/...` address。这样 AB 和 AA 可以共享资源 key。

## 2. 本地 AA 加载闭环

1. 安装 `com.unity.addressables`，等待 asmdef 自动启用 `UNITY_ADDRESSABLES`。
2. 打开 `Window -> Asset Management -> Addressables -> Groups`。
3. 将资源加入 Addressables Group。
4. 在 Addressables Groups 中把 entry address 设置为完整资产路径，例如：
   `Assets/Game/Prefabs/Hero.prefab`。
5. 按项目约定添加 label，例如 `hotupdate`。
6. 选择合适的 Play Mode Script：
   - 编辑器快速验证：`Use Asset Database`
   - 模拟构建加载：`Simulate Groups`
   - 生产验收：`Use Existing Build`
7. 如只是验证 Addressables 原生构建，可在官方窗口执行 `Build -> New Build -> Default Build Script`；如要走框架本地内置闭环，使用 ToolsHub 的 `AA 配置与发布 -> 本地内置 AA -> 一键本地内置构建`。
8. 运行场景，通过 `AddressableLoader` 异步加载：

```csharp
IResLoader loader = ResKit.Allocate<AddressableLoader>();
GameObject prefab = await loader.LoadAsync<GameObject>(
    "Assets/Game/Prefabs/Hero.prefab",
    destroyCancellationToken);
```

同步 `Load<T>` 在 AA 下会 fail-fast 返回 `null`，避免远端资源阻塞或死锁。

## 3. 远端 AA 热更闭环

1. 打开 `StellarFramework -> Tools Hub -> 热更新 -> AA 配置与发布`，选择 `远端热更 AA`。
2. 配置远端发布目录和远端加载路径/URL。D 盘模拟可以留空 URL，让工具从发布目录推导 `file:///` 地址。
3. 工具会写入 Addressables Profile：
   - `RemoteBuildPath`：本机输出目录。
   - `RemoteLoadPath`：客户端可访问的 CDN/文件服务器 URL。
4. 在官方 Groups 中确认热更资源使用完整 `Assets/...` address，并设置对应 label。
5. 点击 `一键远端热更发布`，工具会开启 `Build Remote Catalog`、构建 Addressables、发布 catalog/hash/bundle 和 `HotUpdateManifest.json`。
6. 启动时执行：

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

ResKit 不持有 Content State，也不调用 Content Update 构建。

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
