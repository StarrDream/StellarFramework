# StellarFramework Kit 分发矩阵

本目录只服务于框架原始工程。这个 GitHub 仓库用于维护框架源码、导出器、样例与测试，不承载任何业务项目代码。业务项目只导入由 `StellarFramework/Export` 导出的 `.unitypackage`，不需要导入本目录、测试或发布工具。

## 导出规则

- 每个可导出 Profile 都声明自己的源路径、依赖 Profile、UPM 依赖和明确排除的能力。
- Runtime Kit Profile 额外声明 `tier` 和 `category`，用于 Foundation / Extension / Adapter 的架构约束；不会改变实际依赖闭包。Export 的用户导航使用独立的“基础功能 / 完整功能 / 扩展功能”交付视角，不直接暴露 tier。
- 导出时会自动计算依赖闭包；开发者只选择目标 Kit，不必手动猜测依赖顺序。
- 每个 Kit 包均采用 Bootstrap + Payload 两段式导入：先导入无第三方依赖的安装器，再安装该包依赖闭包中缺失的 UPM 包，最后导入 Kit 源码 Payload。没有 UPM 依赖的 Kit 会直接进入 Payload 导入阶段。
- 当依赖闭包包含 `Runtime.Core` 时，安装完成后会自动整理为 `Runtime/StellarArchitecture.cs` 和 `Runtime/StellarExtensions.cs` 两个文件。框架原始工程继续保持 `Core`、`Extensions` 的职责拆分；Kit 本身、Editor 工具、资源和 asmdef 不会被错误地合并。
- 可选能力必须作为独立 Adapter/Profile 交付，绝不因为导入基础 Kit 而被隐式带入。
- 每个导出包旁会生成同名 `*-Dependencies.md`，这是对最终包内容的可读回执。

## Recommended Profile

Recommended Profile 是“常见项目目标的推荐组合”，不是新的 Kit。它只引用已有原子 Profile，并继续使用同一套依赖闭包解析。

| 推荐组合 | 入口 Profile | 适合场景 | 不强制包含 |
| --- | --- | --- | --- |
| Localization Complete | `localizationkit.tools + localizationkit.tmp.tools` | 完整本地化：UGUI/TMP Scanner/Binding、Workspace、Translation Matrix、JSON/CSV、Validator、ToolsHub | SettingsKit、UIKit、ResKit、热更 |
| ResKit Complete | `reskit.tools` | 完整 ResKit Core + AssetsMap + 资源审计/生成工具 | AssetBundle、Addressables、YooAsset、HybridCLR |
| UIKit Complete | `uikit.reskit + uikit.tools + uikit.adaptation.tools + reskit.tools` | 完整 UIKit Runtime/Tooling + ResKit + 多尺寸 UI Adaptation | Addressables、YooAsset、HybridCLR |
| Hot Update Full | `reskit.yooasset + reskit.tools + hybridclrkit.tools` | 需要资源内容更新 + C# 代码热更的项目 | Addressables |

如果项目已经拥有自己的 UI、Settings、资源系统，只缺某一个能力，不要机械选择完整组合。直接从左侧的 `01 基础功能` 或 `03 扩展功能` 中选择对应原子 Profile 即可。

## 单文件

| 目标 | 导出入口 | 结果 | 不带入 |
| --- | --- | --- | --- |
| Architecture | `独立文件 -> 导出 Architecture.cs` | `StellarArchitecture.cs` | 所有 Kit、Addressables、HybridCLR、代码热更 |
| Extensions | `独立文件 -> 导出 Extensions.cs` | `StellarExtensions.cs` | 所有 Kit、Addressables、HybridCLR、代码热更 |

这两个文件只适合希望直接拷贝源码的用户；其中原有 LogKit 调用会转换为 `UnityEngine.Debug`。

## 基础与独立 Kit

| 目标包 | 自动包含 | 外部 UPM | 不带入 |
| --- | --- | --- | --- |
| ToolsHub.Core | 通用编辑器工具和已导入 Kit 检测 | 无 | Kit、AA、HybridCLR、代码热更 |
| SaveKit.Tools | SaveKit 存档中心：Slots、Inspector、Raw/Hex、Migration Type Chain、Dry Run、Profiler、Diagnostics | UniTask（随 Core） | 不增加 Newtonsoft、TimeKit、AA、HybridCLR |
| LogKit | LogKit | 无 | AA、HybridCLR、代码热更 |
| EventKit | EventKit Runtime | 无 | ToolsHub、HybridCLR、代码热更 |
| ConfigKit.Core | 文本配置读取、持久化覆盖和自定义来源接口 | UniTask | Newtonsoft Json、AA、HybridCLR、代码热更 |
| ConfigKit.NewtonsoftJson | ConfigKit.Core + Newtonsoft JSON Runtime Adapter | UniTask、Newtonsoft Json | ToolsHub、AA、HybridCLR、代码热更 |
| ConfigKit.Tools | ConfigKit.NewtonsoftJson + ToolsHub.Core + JSON 配置面板 | UniTask、Newtonsoft Json | Player Runtime |
| SettingsKit.Core | SingletonKit + 设置定义、存储、Provider | 无 | AudioKit、AA、HybridCLR、代码热更 |
| SettingsKit.UnityAdapters | SettingsKit.Core + Unity 图形、简易语言/输入适配器 | 无 | AudioKit、AA、HybridCLR、代码热更 |
| SettingsKit.AudioKitAdapter | SettingsKit.Core + AudioKit.Core + 音频设置适配器 | UniTask | ResKit、AA、HybridCLR、代码热更 |
| FSMKit | FSMKit | 无 | HybridCLR、代码热更 |
| PoolKit | PoolKit | 无 | HybridCLR、代码热更 |
| SingletonKit | Runtime + 构建期 SingletonGenerator | 无 | ToolsHub、HybridCLR、代码热更 |
| HttpKit | LogKit + HttpKit | UniTask、Newtonsoft Json | AA、HybridCLR、代码热更 |
| TimeKit | LogKit + 世界 Tick、日历换算与高性能定时调度 | 无 | ActionKit、UniTask、AA、HybridCLR、代码热更 |
| SaveKit.Core | LogKit + Section 化存档、版本迁移、事务写入、备份恢复与可扩展 Serializer / Storage | UniTask | Newtonsoft、TimeKit、Addressables、HybridCLR、代码热更 |
| SaveKit.NewtonsoftJson | SaveKit.Core 的可选 JSON Serializer Adapter | UniTask、Newtonsoft Json | TimeKit、Addressables、HybridCLR、代码热更 |
| ActionKit | LogKit、PoolKit | UniTask | ToolsHub、AA、HybridCLR、代码热更 |
| ActionKit.Tools | ActionKit + ToolsHub.Core + Action 诊断工具 | UniTask | Player Runtime |
| BindableKit | EventKit、LogKit | 无 | AA、HybridCLR、代码热更 |
| LocalizationKit.Core | Locale/Key、Table/Catalog、Fallback、Lookup、语言切换事件、命名参数格式化 | 无 | UnityEngine、UGUI、SettingsKit、UIKit、资源系统 |
| LocalizationKit.UnityUGUIAdapter | LocalizationKit.Core + UGUI authoring/binding | UGUI | SettingsKit、UIKit、ResKit、热更 |
| LocalizationKit.Editor | Core + UGUI 的 Validator API | UGUI | Player Runtime、ToolsHub |
| LocalizationKit.Tools | LocalizationKit.Editor + ToolsHub.Core，本地化校验与字体维护入口 | UGUI | Player Runtime、SettingsKit |
| LocalizationKit.TMPAdapter | LocalizationKit.Core + LocalizedTMPTextView | TextMeshPro | UGUI Runtime、SettingsKit、UIKit、ResKit、热更 |
| LocalizationKit.TMP.Editor | TMP Adapter + Localization Editor authoring/registry + TMP Scanner | TextMeshPro、UGUI（authoring） | Player Runtime |
| LocalizationKit.TMP.Tools | TMP Editor + ToolsHub Core + TMP Scan & Bind | TextMeshPro、UGUI（authoring） | Player Runtime |
| AudioKit.Core | PoolKit、SingletonKit | UniTask | ToolsHub、ResKit、AA、HybridCLR、代码热更 |
| AudioKit.Tools | AudioKit.Core + ToolsHub.Core + AudioKit 专属面板 | UniTask | Player Runtime |
| AudioKit.ResKitAdapter | AudioKit.Core + ResKit.Core + ResKit 音频加载器 | UniTask | Addressables、HybridCLR、代码热更 |
| FlowKit.Core | Graph/Compiler/immutable Plan、Runner、Timer、Signal、State、Blackboard、Polling、Operation 与 Parallel/Race/Join | 无 | UnityEngine、UniTask、Addressables、HybridCLR、UI、资源和业务对象 |
| FlowKit.UnityIntegration | FlowHost、稳定 FlowBinding、JSON Graph 入口 | 无 | UniTask、Addressables、HybridCLR、ResKit、ToolsHub |
| FlowKit.ToolsHub | ToolsHub.Core + FlowKit 可视化编辑、项目校验、运行时诊断 | 无 | Editor-only；不进入玩家 Runtime，不提供独立 FlowKit 顶层菜单 |

## 资源与 UI 组合

| 目标包 | 自动包含 | 外部 UPM | 明确不包含 |
| --- | --- | --- | --- |
| ResKit.Core | LogKit、PoolKit | UniTask | SingletonKit、Generated.AssetMap、ToolsHub、AssetBundle、Addressables、HybridCLR、代码热更 |
| ResKit.Tools | ResKit.Core + Generated.AssetMap + ToolsHub.Core + AssetsMap/资源审计工具 | UniTask | Player Runtime |
| ResKit.AssetBundle | ResKit.Core + SingletonKit + Generated.AssetMap + AB Loader | UniTask | ToolsHub、Addressables、HybridCLR、代码热更 |
| ResKit.AssetBundle.Tools | ResKit.AssetBundle + ToolsHub.Core + AB 构建工具 | UniTask | Player Runtime |
| ResKit.Addressables | ResKit.Core + Addressables Loader | UniTask、Addressables | HybridCLR、代码热更 |
| UIKit.Core | Runtime.Core、SingletonKit | UniTask、UGUI | PoolKit、Newtonsoft Json、ToolsHub、ResKit、AA、HybridCLR、代码热更 |
| UIKit.Tools | UIKit.Core + ToolsHub.Core + CodeGen/Inspector/UIKit Hub | UniTask、UGUI | Player Runtime |
| UIKit.ResKitAdapter | UIKit.Core + ResKit.Core + ResKit UI Adapter | UniTask、UGUI | Addressables、HybridCLR、代码热更 |
| UIKit.Adaptation | UIKit.Core + Safe Area / Aspect Breakpoint / CanvasScaler runtime controller + Layout Variant | UGUI | ResKit、ToolsHub、Addressables、HybridCLR |
| UIKit.Adaptation.Tools | UIKit.Adaptation + ToolsHub.Core + 多尺寸 Preview / SafeArea / Anchor 风险 Validator + Variant Capture | UGUI | Player Runtime |

`UIKit.Core` 的默认加载策略是 Resources；只有导入 `UIKit.ResKitAdapter` 后才会注入 ResKit 加载策略。

`AudioKit.Core` 同样默认使用 Resources，也支持直接传入任意 `IAudioLoader`；安装 `AudioKit.ResKitAdapter` 后，才可通过 `AudioKitResKit.Init<TLoader>(mixer)` 接入 ResKit。

`SettingsKit.Core` 不依赖 AudioKit 或 LogKit；音频、图形、语言和输入均通过可选适配器或项目自定义实现接入。

`ConfigKit.Core` 只交付文本读取与路径规则，可替换 `IConfigTextSource` 接入自己的资源系统；`NormalConfig`、`NetConfig` 和可视化 JSON 编辑器属于 `ConfigKit.NewtonsoftJson`。

`SaveKit.Core` 只交付存档容器、Section、事务、Migration 和 Storage/Serializer 抽象；它不保存 Unity Object，不依赖 TimeKit，也不自动保存业务对象。`SaveKit.NewtonsoftJson` 仅在需要 JSON 时导入，ToolsHub 存档诊断属于独立的 `SaveKit.Tools` Profile。

## 可选样例包

核心 Kit 包不携带 `Samples`。需要示例时，在 `StellarFramework -> Export -> 样例包` 中选择一个或多个样例；导出器会把示例代码、对应可运行场景、必需的预制体/资源和它所需的 Kit 闭包一起写入同一个 `.unitypackage`。

| 样例 | 自动包含的能力 | 不会带入 |
| --- | --- | --- |
| ActionKit、BindableKit、EventKit、FSMKit、HttpKit、LogKit、PoolKit、SingletonKit | 对应的单 Kit 及各自必需依赖 | AA、HybridCLR、代码热更 |
| AudioKit | AudioKit.Core + ResKit.Core，用自定义 ResKit Loader 演示音频加载 | AA、HybridCLR、代码热更 |
| ConfigKit | ConfigKit.NewtonsoftJson | AA、HybridCLR、代码热更 |
| ResKit | ResKit.AssetBundle，附 Resources、AB 和可选 AA 场景资源 | HybridCLR、代码热更 |
| SettingsKit | SettingsKit.UnityAdapters + SettingsKit.AudioKitAdapter + Resources 音频样例资源 | ResKit、AA、HybridCLR、代码热更 |
| UIKit | UIKit.Core + Resources UIRoot/面板预制体 | ResKit、AA、HybridCLR、代码热更 |
| Architecture | ActionKit + BindableKit + UIKit.Core 的完整架构演示 | ResKit、AA、HybridCLR、代码热更 |
| FlowKit | FlowKit.UnityIntegration + FlowKit.Core 的 JSON Graph/Delay/Complete 场景 | UniTask、Addressables、HybridCLR、代码热更 |

样例运行时代码按目录各自拥有独立 asmdef；不再存在一个引用全部 Kit 的样例运行时程序集。原始框架工程保留的“构建全部样例”编辑器只用于维护和生成场景，不会随单 Kit 或单样例包导出。

## 资源后端与代码热更新可选层

| 目标包 | 自动包含 | 外部 UPM | 明确不包含 |
| --- | --- | --- | --- |
| ResKit.Addressables | ResKit.Core + Addressables Loader | UniTask、Addressables | HybridCLR、YooAsset、catalog/download 热更新编排 |
| ResKit.YooAsset | ResKit.Core + YooAsset Loader | UniTask、YooAsset | Addressables、HybridCLR、Package 初始化/版本/下载编排 |
| HybridCLRKit | ResKit.Core + HybridCLR Runtime | UniTask、HybridCLR | ToolsHub、Addressables、YooAsset、HttpKit、内容版本/下载流程 |
| HybridCLRKit.Tools | HybridCLRKit + ToolsHub.Core + DLL/AOT/Manifest 导出工具 | UniTask、HybridCLR | Player Runtime |

未导入 `HybridCLRKit` 的项目不会因为 ToolsHub、ResKit、Addressables 或 YooAsset Adapter 被要求安装 HybridCLR。HybridCLRKit 只通过 ResKit Loader key 读取 Manifest / DLL / metadata，因此不依赖具体资源 SDK。HybridCLR 工具会检查 `HybridCLR.Editor` 程序集，插件不在时不会显示。

## Tools Hub 自动识别

`ToolsHub.Core` 不直接引用任何 Kit。每个 Kit 专属编辑器模块位于独立 asmdef，并通过 `StellarTool.RequiredAssemblyNames` 声明可用条件。

- 运行时扫描当前已加载程序集。
- 依赖满足才注册并显示该模块。
- 缺少 Kit、Addressables 或 HybridCLR 时，对应入口不会显示，而不是显示后报错。
- 新 Kit 按相同规则新增一个 `Modules/<Kit>/StellarFramework.ToolsHub.<Kit>.Editor.asmdef` 即可接入。
- `Kit 安装状态` 页还会直接列出当前已加载的核心 Kit 和 Adapter，便于开发者确认“当前项目实际导入了什么”。

## 原始工程维护约定

原始框架工程只在顶层 `StellarFramework` 菜单保留两个入口：`Tools Hub` 与 `Export`。其中 `StellarFramework/Export` 是源码工程专用导出窗口，可多选 Kit、使用 Recommended Profile、预览并去重依赖闭包、导出为一个 `.unitypackage` 和同名依赖说明。Kit 专属维护、诊断、代码生成与验证能力优先进入 ToolsHub，不再各自创建 `StellarFramework/...` 子菜单。窗口与组合导出器位于 `Modules/Packaging`，该目录已被所有消费者分发路径排除，因此业务项目不会携带它。

窗口会将 Runtime Kit Profile 分为 Foundation Kits、Extension Kits 与 Adapter Profiles，并按 category 继续分组；Runtime Core、ToolsHub 等基础支持项保持单独显示。每张卡仍明确显示“独立”或“自动带依赖”，依赖闭包算法不因分组发生变化。Addressables、HybridCLR 与代码热更均只在明确选择相关 Adapter/Profile 后进入导出包。

新增或拆分 Kit 时，同步更新：

1. `KitDistributionCatalog.json` Profile 与依赖闭包；
2. Kit 专属 Tools Hub 子程序集（如有编辑器工具）；
3. `StandaloneSourceExportPolicyTests` 的边界测试；
4. 本文档的分发矩阵。
5. [KitArchitectureGuide.md](KitArchitectureGuide.md) 的架构规则与分类登记。

具体的导出与测试基线见 [KitExportValidationMatrix.md](KitExportValidationMatrix.md)，分层规则见 [KitArchitectureGuide.md](KitArchitectureGuide.md)。
