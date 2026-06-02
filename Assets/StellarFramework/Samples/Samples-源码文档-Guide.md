# Samples / 源码文档

Samples 是新人验收和维护者回归的重要入口。它们不是独立小游戏，而是把 Runtime Kit 的最小可用链路做成可运行场景。

## 源码位置

- `Samples/README.md`：样例总览。
- `Samples/ArchitectureDemo`：MSV 架构示例。
- `Samples/KitSamples/README.md`：Kit 样例说明。
- `Samples/KitSamples/Samples_Index.md`：样例索引。
- `Samples/KitSamples/Scenes`：可运行场景。
- `Samples/KitSamples/Scripts`：样例脚本。
- `Editor/StellarToolsHub/Modules/SampleBuilderHubModule.cs`：样例构建工具入口。

## 核心类型

- `SampleBuilderHubModule`：ToolsHub 中负责生成和修复样例的工具模块。
- `FrameworkValidation` 相关样例脚本：集中验证 Runtime Kit 主链路。
- `UIKit` 样例脚本：演示 UI 初始化、打开、关闭和页面栈。
- `ResKit` 样例脚本：演示 Resources、AB、AA 后端加载。
- `SettingsKit` 样例脚本：演示设置页、应用、保存和回滚。

## 关键方法

- 样例构建工具的构建按钮逻辑：创建或修复 KitSamples 资源。
- 各 Playable 场景入口脚本的 `Start` / `Awake`：初始化对应 Kit。
- UI 样例按钮回调：触发面板打开、栈导航和关闭。
- ResKit 样例加载方法：分配 loader、加载资源、实例化和释放。

## 核心内容

- `FrameworkValidation_Playable.unity`：集中验证场景，优先用于新人第一次跑通。
- `UIKit_Playable.unity`：UIKit 面板、页面栈、UIRoot 和加载策略验证。
- `ResKit_Playable.unity`：Resources、AB、AA 等加载后端验证。
- `SettingsKit_Playable.unity`：设置页、保存、应用和回滚验证。
- `ArchitectureDemo`：MSV 模型、服务、视图交互演示。

## 数据流

1. 用户在 ToolsHub `Quick Start` 或 `样例构建` 中点击构建。
2. 样例构建器创建或修复样例资源、Prefab、场景和配置。
3. 新人先运行 FrameworkValidation 场景。
4. 再按具体 Kit 打开单独 Playable 场景。
5. 修改 Runtime Kit 后，可以回到对应样例场景做行为回归。

## 依赖关系

- 依赖 Runtime Kits。
- 依赖 `Resources` 中的默认配置和示例资源。
- 部分资源链路依赖 Generated 目录，例如 AB 的 AssetMap。
- 构建入口依赖 ToolsHub 的样例构建模块。

## 扩展点

- 新增 Kit 样例：在 `Samples/KitSamples/Scripts` 添加脚本，在 `Scenes` 添加 Playable 场景。
- 新增样例资源：放入明确的 Kit 子目录，并在样例构建工具中补生成逻辑。
- 新增集中验证项：优先补到 `FrameworkValidation_Playable.unity`。
- 新增文档：更新 `Samples_Index.md` 和对应 Kit 说明文档。

## 测试入口

- `QuickStartHubModule` 依赖样例场景路径固定存在。
- `QuickStartCatalogPolicyTests` 会检查关键样例场景和样例索引存在。
- 修改样例构建逻辑后，至少重新执行 ToolsHub `构建样例` 并打开 FrameworkValidation 场景。
