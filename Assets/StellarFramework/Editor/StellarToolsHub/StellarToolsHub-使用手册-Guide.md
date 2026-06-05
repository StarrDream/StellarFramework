# StellarToolsHub / 使用手册

ToolsHub 是 StellarFramework 的统一编辑器工具入口。它把文档、Quick Start、样例构建、资源构建、热更新发布、UIKit 生成、配置中心、设置中心、音频中心和诊断工具集中在一个窗口里。

## 打开入口

```text
StellarFramework -> Tools Hub
```

窗口左侧是工具分组和工具列表，右侧是当前工具。工具通过 `[StellarTool]` 自动注册，所以新增工具后通常会自动出现在对应分组里。

## 左侧分组

左侧分组固定顺序为：

1. `Start Here`
2. `资源管理`
3. `框架核心`
4. `热更新`
5. `样例支持`
6. `生产力`
7. `常用工具`

如果有未命中上述清单的新分组，会追加在末尾。

其中 `资源管理` 组内固定顺序为：

1. `资源打包 (AssetBundle)`
2. `AA 配置与发布`
3. `ResKit 资源审计`

## 新人第一次使用路线

1. 进入 `Start Here -> Quick Start`。
2. 首屏会先显示 `欢迎使用 StellarFramework` 欢迎页，点击主按钮 `进入 30 分钟上手`。
3. 在第二层按顺序执行 `构建样例`、`打开 UIKit_Playable`、`打开 ResKit_Playable`、`阅读快速开始`。
4. 需要重新查看首页时，使用顶部轻量按钮 `返回欢迎页`。
5. 进入 `文档中心 (Docs)`，按 README、快速开始、Kit 说明文档、源码文档继续阅读。
6. 需要热更新时再进入 `热更新 -> AA 配置与发布`。基础框架和热更扩展不是同一上手门槛。

## 文档中心 (Docs)

用途：

- 扫描 `Assets/StellarFramework` 下的 Markdown 文档。
- 按文档用途分组显示：快速开始和 README、Kit 说明文档、Kit 源码文档、架构/Runtime 源码文档、ToolsHub 文档、Samples/Tests/Generated/Resources 文档。
- 在 ToolsHub 内预览 Markdown。
- 用系统默认应用打开原始 Markdown。

注意事项：

- 新增文档后点击 `刷新文档列表`。
- 文档中心只负责阅读，不负责生成或修改文档。
- Kit 文档按“双轨”组织：说明文档讲怎么用，源码文档讲怎么读和怎么改。

## Quick Start

用途：

- 新人第一入口。
- 构建样例场景和公共资源。
- 打开用户安装包中实际可运行的核心场景和说明文档。
- 检查常见缺失项。

界面结构：

- 首屏是 `欢迎使用 StellarFramework` 欢迎门户，主行动只有 `进入 30 分钟上手`。
- 第二层保留现有 `30 分钟上手`、`官方推荐路线`、`环境检查`、`常用入口`。
- 第二层顶部提供轻量按钮 `返回欢迎页`，用于回到欢迎首页。

重点按钮：

- `进入 30 分钟上手`：进入推荐上手路径，先跑通样例、资源主链路和推荐文档。
- `返回欢迎页`：从第二层回到欢迎门户，不会改变现有步骤内容。
- `构建样例`：生成 KitSamples 运行所需资源、UIRoot、示例面板和 AB 示例资源。
- `FrameworkValidation` 已迁入框架外验证区，用户安装包默认不会包含它。
- 打开 `UIKit_Playable.unity`、`ResKit_Playable.unity`：分别验证 UI 和资源门面。

## AA 配置与发布

用途：

- 管理 `本地内置 AA` 和 `远端热更 AA` 两条工作流。
- 写入 Addressables Profile、RemoteBuildPath、RemoteLoadPath 和 Remote Catalog 设置。
- 构建 catalog/hash/bundle。
- 导出 `HotUpdate.dll.bytes`、AOT metadata 和 `HotUpdateManifest.json`。
- 校验发布目录是否包含同批次 Manifest、catalog、hash 和 bundle。

建议：

- 新人先跑通基础框架，再进入这组工具。
- 这组工具属于可选热更扩展路径，不是基础框架的默认起步路线。

本地内置 AA 适合资源随包：

- 点击 `一键本地内置构建`。
- 输出进入 `StreamingAssets/aa`。
- 资源变更通常需要重新打 Player。

远端热更 AA 适合旧 Player 远端拉新资源：

- 设置远端目录或 URL。
- 点击 `一键远端热更发布`。
- 工具会写入远端 AA 配置、导出热更 DLL 与 Manifest、构建 Addressables、复制到远端发布目录、写入 Player 运行时读取的 Manifest 地址。

注意事项：

- 资源版本匹配、catalog/hash、bundle 下载和缓存由 Addressables 官方机制负责。
- `HotUpdateManifest`、DLL SHA256 校验和 HybridCLR 入口加载由框架负责。
- 远端模式第一次接入后，需要打一次带远端 Manifest 地址的新 Player。

## HybridCLR DLL 导出

用途：

- 从 HybridCLR 生成目录复制热更 DLL。
- 把 `.dll` 改名为 `.dll.bytes`，作为 Addressables 可加载资源。
- 复制 AOT metadata。
- 生成 `HotUpdateManifest.json`。

输出：

- `Assets/GameHotUpdate/Code/HotUpdate.dll.bytes`
- `Assets/GameHotUpdate/Metadata/*.dll.bytes`
- `Assets/GameHotUpdate/Manifest/HotUpdateManifest.json`

注意事项：

- 不要手写 Manifest。
- DLL、metadata、catalog、hash、bundle 必须来自同一批发布。

## 资源打包 (AssetBundle)

用途：

- 配置 AB 打包规则。
- 自动设置 `assetBundleName`。
- 构建 AssetBundle。
- 生成 `Generated/AssetMap/AssetMap.cs`。
- 清理冗余 bundle。

运行时注意：

- AB 模式启动前调用 `await AssetBundleManager.Instance.InitAsync()`。
- 业务层建议传完整 `Assets/...` 路径。
- 如果使用严格卸载策略，先销毁场景实例，再释放 loader。

## ResKit 资源审计

用途：

- 查看当前 ResKit loader、owner、资源路径和引用计数。
- 排查资源泄漏、重复持有、释放顺序错误。

建议：

- 先运行相关业务场景，再打开审计工具。
- 发现引用不归零时，优先检查 `Unload(path)` 和 `ResKit.Recycle(loader)` 是否成对调用。

## UIKit 工具

用途：

- 管理 UI 工作区。
- 创建或修复 UIRoot。
- 扫描 `UIAutoBind`。
- 生成 UI 绑定代码。
- 修复 UIKit 样例资源。

注意事项：

- 面板打开失败时先检查 `UIKitSettings`、UIRoot、Prefab 路径和加载后端。
- 热更 UI 优先使用异步打开接口。

## ConfigKit 配置中心

用途：

- 查看普通配置和网络配置。
- 编辑环境地址。
- 验证 StreamingAssets、PersistentDataPath 和远端配置路径。

注意事项：

- 网络配置应和 HttpKit 的实际请求入口一起验证。
- 修改配置后确认缓存是否需要清理或重新加载。

## SettingsKit 设置中心

用途：

- 查看设置页和设置项。
- 验证默认提供器。
- 检查存储、应用、保存、回滚流程。

注意事项：

- 设置值变更通常需要 `TrySetValue -> ApplyPending -> Save`。
- 自定义业务设置页实现 `ISettingsPageProvider`。

## AudioKit 音频中心

用途：

- 检查 AudioMixer。
- 引导创建符合框架约定的 Mixer。
- 验证 BGM、SFX、音量分组和静音策略。

注意事项：

- AudioKit 可接 ResKit、Addressables 或自定义 `IAudioLoader`。
- Mixer 参数名要和 AudioKit 配置一致。

## EventKit 链路追踪

用途：

- 查看活跃事件监听。
- 排查重复注册、未反注册、广播链路不清晰。

注意事项：

- 监听建议绑定生命周期，例如 `UnRegisterWhenGameObjectDestroyed`。
- 链路追踪是调试工具，不要把它当业务逻辑依赖。

## 样例构建

用途：

- 生成 KitSamples 场景和公共资源。
- 修复样例 Prefab、配置、音频、AB 示例资源。
- 为 FrameworkValidation 场景准备依赖。

注意事项：

- 样例构建用于学习和验收，不等同于产品发布流水线。
- 修改样例资源后建议重新构建样例。

## 开发者快捷工具

用途：

- 管理个人常用场景。
- 打开最近开发入口。
- 控制调试倍率。
- 提供常用本机开发快捷入口。

注意事项：

- 这个工具偏个人工作流，不要把本机路径当团队默认配置。

## 常见排错

- 文档中心没有新文档：点击 `刷新文档列表`。
- AA 远端没有更新：检查远端 `.hash` 是否更新，RemoteLoadPath 是否指向正确版本。
- Player 仍读 StreamingAssets：确认当前选择远端热更 AA，并且已重新打过带远端 Manifest 地址的 Player。
- DLL SHA256 mismatch：重新执行同一页签的一键流程，确保 Manifest 和 DLL 同批次。
- UI 生成字段为空：检查节点是否挂 `UIAutoBind`。
- AB 加载失败：先执行 `资源打包 (AssetBundle)`，确认 AssetMap 已生成。
