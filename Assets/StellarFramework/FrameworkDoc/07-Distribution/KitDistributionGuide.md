# StellarFramework Kit 分发矩阵

本目录只服务于框架原始工程。这个 GitHub 仓库用于维护框架源码、导出器、样例与测试，不承载任何业务项目代码。业务项目只导入由 `StellarFramework/Export` 导出的 `.unitypackage`，不需要导入本目录、测试或发布工具。

## 导出规则

- 每个可导出 Profile 都声明自己的源路径、依赖 Profile、UPM 依赖和明确排除的能力。
- Runtime Profile 通过 `documentationPaths` 显式声明随独立包交付的正式 Guide；文档不再依赖开发者回到母仓库自行寻找。
- Runtime Kit Profile 额外声明 `tier` 和 `category`，用于 Foundation / Extension / Adapter 的架构约束；不会改变实际依赖闭包。Export 的用户导航使用独立的“基础功能 / 完整功能 / 扩展功能”交付视角，不直接暴露 tier。
- 每个原子 Profile 还声明 `maturity=stable / rc / experimental`。`availability` 只表示能否安装/导出，`maturity` 才表示生产成熟度。
- 导出时会自动计算依赖闭包；开发者只选择目标 Kit，不必手动猜测依赖顺序。
- 每个 Kit 包均采用 Bootstrap + Payload 两段式导入：先导入无第三方依赖的安装器，再安装该包依赖闭包中缺失的 UPM 包，最后导入 Kit 源码 Payload。没有 UPM 依赖的 Kit 会直接进入 Payload 导入阶段。
- 当依赖闭包包含 `Runtime.Core` 时，安装完成后会自动整理为 `Runtime/StellarArchitecture.cs` 和 `Runtime/StellarExtensions.cs` 两个文件。框架原始工程继续保持 `Core`、`Extensions` 的职责拆分；Kit 本身、Editor 工具、资源和 asmdef 不会被错误地合并。
- 可选能力必须作为独立 Adapter/Profile 交付，绝不因为导入基础 Kit 而被隐式带入。
- 每个导出包旁会生成同名 `*-Dependencies.md`，这是对最终包内容的可读回执。

### UPM 依赖固定

Package Publisher 自动安装的 Git UPM 依赖必须固定到语义版本 Tag 或 commit；未指定 revision 或使用 `main` 等移动分支的 Git URL 会被拒绝。Unity Registry 依赖继续使用 `packageId@version`，不按 Git URL 规则判断。UniTask 当前固定到已验证的 commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`，对应版本为 **2.5.11**。Bootstrap 单包安装器与母工程 manifest 使用相同 pin，避免消费者项目从不同入口导入时解析漂移。

Recommended Profile 不单独维护成熟度；Package Publisher 会解析完整依赖闭包，并取其中最低成熟度作为组合结果。只要闭包里存在 RC 或 Experimental，完整组合就不能显示为 Stable。

## Recommended Profile

Recommended Profile 是“常见项目目标的推荐组合”，不是新的 Kit。它只引用已有原子 Profile，并继续使用同一套依赖闭包解析。

| 推荐组合 | 入口 Profile | 适合场景 | 不强制包含 |
| --- | --- | --- | --- |
| Localization Complete | `localizationkit.tools + localizationkit.tmp.tools` | 完整本地化：UGUI/TMP Scanner/Binding、Workspace、Translation Matrix、JSON/CSV、Validator、ToolsHub | SettingsKit、UIKit、ResKit、热更 |
| ResKit Complete | `reskit.tools` | 完整 ResKit Core + AssetsMap + 资源审计/生成工具 | AssetBundle、Addressables、YooAsset、HybridCLR |
| UIAdaptationKit Complete | `uiadaptation.tools` | 独立 UGUI 多机型适配 + SafeArea/Cutout/Fallback + Preview/Validator | UIKit、ResKit、SingletonKit |
| UIKit Complete | `uikit.reskit + uikit.tools + uiadaptation.tools + reskit.tools` | 完整 UIKit Runtime/Tooling + ResKit + 独立 UIAdaptationKit | Addressables、YooAsset、HybridCLR |
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
| RuntimeTools.Core | WeightedRandom、Transform/随机点、FrameRate、Follow/Rotator/Billboard/Shake、Physics Cast/Overlap/Ground/Bounds/Relay、Renderer PropertyBlock、CoroutineRunner | 无 | UGUI、URP、PoolKit、TimeKit、ResKit、SingletonKit、ToolsHub |
| RuntimeTools.Tools | RuntimeTools.Core + ToolsHub.Core + 快速挂载、Transform Snapshot、Bounds/FPS Diagnostics、PropertyBlock/Selection Validation | 无 | Player Runtime |
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
| UIAdaptationKit.Core | SafeArea、System Cutouts、PreciseCutout、Automatic Fallback、Breakpoint、Layout Variant | UGUI | UIKit、ResKit、SingletonKit、ToolsHub、热更 |
| UIAdaptationKit.Tools | UIAdaptationKit.Core + ToolsHub.Core + 一键独立 UIRoot + Preview/Validator | UGUI | Player Runtime、UIKit |
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

旧 `UIKit.Adaptation` / `UIKit.Adaptation.Tools` 导出入口作为兼容别名保留，但源码与依赖均指向独立 UIAdaptationKit，不再要求 UIKit.Core。新项目统一使用 `uiadaptation.core / uiadaptation.tools`。

`UIKit.Core` 的默认加载策略是 Resources；只有导入 `UIKit.ResKitAdapter` 后才会注入 ResKit 加载策略。

`AudioKit.Core` 同样默认使用 Resources，也支持直接传入任意 `IAudioLoader`；安装 `AudioKit.ResKitAdapter` 后，才可通过 `AudioKitResKit.Init<TLoader>(mixer)` 接入 ResKit。

`SettingsKit.Core` 不依赖 AudioKit 或 LogKit；音频、图形、语言和输入均通过可选适配器或项目自定义实现接入。

`ConfigKit.Core` 只交付文本读取与路径规则，可替换 `IConfigTextSource` 接入自己的资源系统；`NormalConfig`、`NetConfig` 和可视化 JSON 编辑器属于 `ConfigKit.NewtonsoftJson`。

`SaveKit.Core` 只交付存档容器、Section、事务、Migration 和 Storage/Serializer 抽象；它不保存 Unity Object，不依赖 TimeKit，也不自动保存业务对象。`SaveKit.NewtonsoftJson` 仅在需要 JSON 时导入，ToolsHub 存档诊断属于独立的 `SaveKit.Tools` Profile。

## Sample / Demo 策略

Catalog 当前不提供 `sample` Profile，Export 也不再提供“样例包”页。用户入门统一从：

`Assets/StellarFramework/Samples/ArchitectureDemo`

进入；单个 Kit 的最小用法、代码片段、依赖边界和排错统一由随包导出的 `FrameworkDoc` Guide 承担。

只有当某个跨 Kit 行为确实无法通过 Guide、自动测试或现有 ArchitectureDemo 表达时，才重新评估新增 Demo。Demo 是教学入口，不承担 Kit 分发或 Release Gate 职责。

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

窗口面向用户按 `01 基础功能 / 02 完整功能 / 03 扩展功能` 组织交付；架构层的 Foundation / Extension / Adapter 继续作为卡片元数据和依赖约束显示。每张卡同时显示成熟度与“独立 / 自动带依赖”，依赖闭包算法不因 UI 分组发生变化。Addressables、YooAsset、HybridCLR 与代码热更均只在明确选择相关 Adapter/Profile 后进入导出包。

新增或拆分 Kit 时，同步更新：

1. `KitDistributionCatalog.json` Profile 与依赖闭包；
2. Kit 专属 Tools Hub 子程序集（如有编辑器工具）；
3. `StandaloneSourceExportPolicyTests` 的边界测试；
4. 本文档的分发矩阵。
5. [KitArchitectureGuide.md](../01-Architecture/KitArchitectureGuide.md) 的架构规则与分类登记。

当前验证摘要见 [ValidationCurrentStatus.md](../08-Validation/ValidationCurrentStatus.md)，历史 Evidence Ledger 见 [KitExportValidationMatrix.md](../08-Validation/KitExportValidationMatrix.md)，分层规则见 [KitArchitectureGuide.md](../01-Architecture/KitArchitectureGuide.md)，BuildArtifacts 规则见 [BuildArtifactsGuide.md](BuildArtifactsGuide.md)。
