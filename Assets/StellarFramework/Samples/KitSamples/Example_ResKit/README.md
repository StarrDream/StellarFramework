# Example ResKit / 资源样例

本目录用于验收 ResKit 的四类加载路径：`Resources`、`AssetBundle`、`Addressables` 和自定义 `RawTextLoader`。

## 1. 示例资源

- `Resources/ResKitTest/TestCube_Res.prefab`：Resources 示例。
- `Art/AssetBundle/TestCapsule_AB.prefab`：AssetBundle 示例源资源。
- `Addressables/TestSphere_AA.prefab`：Addressables 示例源资源。
- `RawTextLoader.cs`：自定义文本加载器示例。

## 2. 路径约定

- Resources：`ResKitTest/TestCube_Res`
- AssetBundle：`Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab`
- Addressables：`Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab`
- RawText：`StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt`

AA 示例故意使用完整 `Assets/...` address，和 AB 的业务路径保持一致。

## 3. 本地验收步骤

1. 打开 `Assets/StellarFramework/Samples/KitSamples/Scenes/ResKit_Playable.unity`。
2. Resources：直接点击 `加载 Resources 资源`。
3. RawText：确认 `Assets/StreamingAssets/StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt` 存在，再点击 `加载 RawText`。
4. AB：通过 ToolHub 的 AssetBundle 模块构建 AB，运行时先点击 `初始化 AB 管理器`，再点击 `加载 AssetBundle 资源`。
5. AA：通过 ToolHub 的 Addressables 模块执行 `应用 Address/Labels`，然后到 Addressables 官方 Groups 窗口执行构建或选择 Play Mode Script，再点击 `加载 Addressables 资源`。
6. 点击 `销毁实例并回收加载器`，确认没有异常日志和明显资源泄漏。

## 4. 模拟加载说明

- AA 编辑器模拟加载使用 Addressables 官方 Play Mode Script：`Use Asset Database`、`Simulate Groups` 或 `Use Existing Build`。
- AB 如果要做编辑器模拟加载，建议单独注册一个 AssetDatabase 自定义 loader；正式 AB 模式仍以 ToolHub 构建产物和 `AssetBundleManager` 为准。
- 不要让 ToolHub 替代 AA/YooAsset 的官方构建窗口。ToolHub 对 AA 只做检查、address/labels 和配置辅助。

## 5. 远端 AA 热更验收

1. 在 Addressables Groups 开启 `Build Remote Catalog`。
2. 配置 Profile 的 `RemoteBuildPath` 和 `RemoteLoadPath`。
3. ToolHub 点击 `检查 Settings/Profile`，确认 group、schema、address、labels 都通过。
4. 在 Addressables 官方窗口执行完整构建。
5. 上传 remote catalog、hash 和 bundle。
6. 修改示例 AA prefab 后，使用官方 Content Update 流程生成更新产物。
7. 再次上传新产物，运行客户端确认 `CheckCatalogUpdatesAsync` 能发现并下载更新。

## 6. 常见错误排查

- `UNITY_ADDRESSABLES` 未启用：确认已安装 Addressables 包并等待 Unity 重新编译。
- Addressables 加载失败：确认 address 是完整 `Assets/...prefab`，不是短名 `TestSphere_AA`。
- Play Mode 加载不到：确认 Play Mode Script 和构建产物一致，生产验收建议使用 `Use Existing Build`。
- AB 加载失败：确认已生成 `StreamingAssets/AssetBundles/[Platform]` 产物。
- RawText 失败：确认文件位于 `Assets/StreamingAssets` 下。
- SHA256 不匹配：只影响 HybridCLR AA 代码热更，重新计算 `dll.bytes` 的 SHA256 并更新 settings。
