# Resources / 说明与源码文档

Resources 目录保存框架默认资源、示例配置和 Resources 后端测试资源。它既是使用者会接触的资源入口，也是 Runtime 默认加载路径的一部分。

## 源码位置

- `Resources/StellarFramework`：框架默认设置资产。
- `Resources/UIRoot` 或相关 UI 默认资源：UIKit 初始化需要的默认根节点。
- `Resources/ResKitTest`：Resources 后端样例资源。
- `Resources/Config`：示例配置。
- `Resources/Audio`：AudioKit 示例音频或 Mixer 相关资源。

## 核心类型

- `ResourceLoader`：通过 Unity Resources API 加载资源。
- `ResKitRuntimeSettings`：从 Resources 默认路径读取 ResKit 运行时配置。
- `UIKitSettings`：UIRoot、面板路径和 UI 加载策略配置。
- `AudioKit` 默认资源：音频样例和 Mixer 配置的资源侧依赖。

## 关键方法

- `Resources.Load` / `Resources.LoadAsync`：Resources 后端实际加载入口。
- `ResKitRuntimeSettings.LoadOrCreateDefault`：读取或创建默认运行时配置。
- `UIKit.InitAsync`：按配置加载或创建 UIRoot。
- `ResKit.Recycle(loader)`：释放 Resources 后端 loader 持有关系。

## 使用说明

- 轻量本地资源可以放入 Resources，但生产资源热更优先使用 Addressables。
- 默认设置资产由 Runtime 通过固定路径加载，例如 `ResKitRuntimeSettings.LoadOrCreateDefault()`。
- Quick Start 和样例构建会修复一部分缺失的示例资源。
- 资源路径不要随意改名，Samples、Tests 和说明文档可能依赖固定路径。

## 源码关系

- `ResKit` 的 `ResourceLoader` 通过 `Resources.Load` 和 `Resources.LoadAsync` 读取资源。
- `ResKitRuntimeSettings` 会从 Resources 默认路径加载资源设置。
- `UIKitSettings`、AudioKit 或样例配置可能通过 Resources 提供默认资产。
- ConfigKit 示例会演示 StreamingAssets、PersistentDataPath 和 Resources 附近的配置边界。

## 数据流

1. Runtime 或样例请求固定 Resources 路径。
2. Unity 从包体内 Resources 索引加载资源。
3. ResKit 或对应 Kit 持有资源引用。
4. 调用 `Unload`、`ResKit.Recycle(loader)` 或 `Resources.UnloadUnusedAssets` 后释放。

## 依赖关系

- 依赖 Unity `Resources` 机制。
- 资源命名和路径依赖 ResKit、UIKit、SettingsKit、Samples。
- 不依赖 Addressables 远端 catalog，也不参与 AA 版本校验。

## 扩展点

- 新增默认设置：放到稳定路径，并在对应 Kit 源码文档写清加载入口。
- 新增样例资源：同步更新 Samples 文档和 Quick Start。
- 新增 Resources 后端测试：同步更新 ResKit Resources 子文档。
- 不建议把大体积生产资源长期放 Resources，因为它们会随 Player 包体进入构建。

## 测试入口

- `ResKit_Playable.unity`：Resources 后端样例。
- `FrameworkValidation_Playable.unity`：集中验证默认资源是否存在。
- `QuickStartCatalogPolicyTests`：检查关键文档和样例入口，不直接扫描所有资源。
