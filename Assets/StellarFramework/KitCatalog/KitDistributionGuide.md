# StellarFramework Kit 分发矩阵

本目录只服务于框架原始工程。业务项目只导入由 `StellarFramework/Framework Source/Kit Package Exporter` 导出的 `.unitypackage`，不需要导入本目录、测试或发布工具。

## 导出规则

- 每个可导出 Profile 都声明自己的源路径、依赖 Profile、UPM 依赖和明确排除的能力。
- 导出时会自动计算依赖闭包；开发者只选择目标 Kit，不必手动猜测依赖顺序。
- 可选能力必须作为独立 Adapter/Profile 交付，绝不因为导入基础 Kit 而被隐式带入。
- 每个导出包旁会生成同名 `*-Dependencies.md`，这是对最终包内容的可读回执。

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
| LogKit | LogKit | 无 | AA、HybridCLR、代码热更 |
| EventKit | ToolsHub.Core + EventKit 专属追踪工具 | 无 | HybridCLR、代码热更 |
| ConfigKit.Core | 文本配置读取、持久化覆盖和自定义来源接口 | UniTask | Newtonsoft Json、AA、HybridCLR、代码热更 |
| ConfigKit.NewtonsoftJson | ConfigKit.Core、ToolsHub.Core + JSON 配置面板 | UniTask、Newtonsoft Json | AA、HybridCLR、代码热更 |
| SettingsKit.Core | SingletonKit + 设置定义、存储、Provider | 无 | AudioKit、AA、HybridCLR、代码热更 |
| SettingsKit.UnityAdapters | SettingsKit.Core + Unity 图形、简易语言/输入适配器 | 无 | AudioKit、AA、HybridCLR、代码热更 |
| SettingsKit.AudioKitAdapter | SettingsKit.Core + AudioKit.Core + 音频设置适配器 | UniTask | ResKit、AA、HybridCLR、代码热更 |
| FSMKit | FSMKit | 无 | HybridCLR、代码热更 |
| PoolKit | PoolKit | 无 | HybridCLR、代码热更 |
| SingletonKit | ToolsHub.Core + 单例注册表工具 | 无 | HybridCLR、代码热更 |
| HttpKit | LogKit + HttpKit | UniTask、Newtonsoft Json | AA、HybridCLR、代码热更 |
| ActionKit | LogKit、PoolKit、ToolsHub.Core + Action 编辑器 | UniTask | AA、HybridCLR、代码热更 |
| BindableKit | EventKit、LogKit | 无 | AA、HybridCLR、代码热更 |
| AudioKit.Core | PoolKit、SingletonKit、ToolsHub.Core + AudioKit 专属面板 | UniTask | ResKit、AA、HybridCLR、代码热更 |
| AudioKit.ResKitAdapter | AudioKit.Core + ResKit.Core + ResKit 音频加载器 | UniTask | Addressables、HybridCLR、代码热更 |

## 资源与 UI 组合

| 目标包 | 自动包含 | 外部 UPM | 明确不包含 |
| --- | --- | --- | --- |
| ResKit.Core | LogKit、PoolKit、SingletonKit、AssetMap、ToolsHub.Core | UniTask | AssetBundle、Addressables、HybridCLR、代码热更 |
| ResKit.AssetBundle | ResKit.Core + AB Loader + AB 编辑器工具 | UniTask | Addressables、HybridCLR、代码热更 |
| ResKit.Addressables | ResKit.Core + Addressables Loader | UniTask、Addressables | HybridCLR、代码热更 |
| UIKit.Core | Runtime.Core、LogKit、PoolKit、SingletonKit、ToolsHub.Core、UIKit 工具 | UniTask、UGUI、Newtonsoft Json | ResKit、AA、HybridCLR、代码热更 |
| UIKit.ResKitAdapter | UIKit.Core + ResKit.Core + ResKit UI Adapter | UniTask、UGUI、Newtonsoft Json | Addressables、HybridCLR、代码热更 |

`UIKit.Core` 的默认加载策略是 Resources；只有导入 `UIKit.ResKitAdapter` 后才会注入 ResKit 加载策略。

`AudioKit.Core` 同样默认使用 Resources，也支持直接传入任意 `IAudioLoader`；安装 `AudioKit.ResKitAdapter` 后，才可通过 `AudioKitResKit.Init<TLoader>(mixer)` 接入 ResKit。

`SettingsKit.Core` 不依赖 AudioKit 或 LogKit；音频、图形、语言和输入均通过可选适配器或项目自定义实现接入。

`ConfigKit.Core` 只交付文本读取与路径规则，可替换 `IConfigTextSource` 接入自己的资源系统；`NormalConfig`、`NetConfig` 和可视化 JSON 编辑器属于 `ConfigKit.NewtonsoftJson`。

## 可选样例包

核心 Kit 包不携带 `Samples`。需要示例时，在 `Kit Package Exporter -> 样例包` 中选择一个或多个样例；导出器会把示例代码、对应可运行场景、必需的预制体/资源和它所需的 Kit 闭包一起写入同一个 `.unitypackage`。

| 样例 | 自动包含的能力 | 不会带入 |
| --- | --- | --- |
| ActionKit、BindableKit、EventKit、FSMKit、HttpKit、LogKit、PoolKit、SingletonKit | 对应的单 Kit 及各自必需依赖 | AA、HybridCLR、代码热更 |
| AudioKit | AudioKit.Core + ResKit.Core，用自定义 ResKit Loader 演示音频加载 | AA、HybridCLR、代码热更 |
| ConfigKit | ConfigKit.NewtonsoftJson | AA、HybridCLR、代码热更 |
| ResKit | ResKit.AssetBundle，附 Resources、AB 和可选 AA 场景资源 | HybridCLR、代码热更 |
| SettingsKit | SettingsKit.UnityAdapters + SettingsKit.AudioKitAdapter + Resources 音频样例资源 | ResKit、AA、HybridCLR、代码热更 |
| UIKit | UIKit.Core + Resources UIRoot/面板预制体 | ResKit、AA、HybridCLR、代码热更 |
| Architecture | ActionKit + BindableKit + UIKit.Core 的完整架构演示 | ResKit、AA、HybridCLR、代码热更 |
| HotUpdate.HybridCLR | HotUpdate.HybridCLR 的完整可运行示例 | 无；仅在明确选择时才带入 AA 与 HybridCLR |

样例运行时代码按目录各自拥有独立 asmdef；不再存在一个引用全部 Kit 的样例运行时程序集。原始框架工程保留的“构建全部样例”编辑器只用于维护和生成场景，不会随单 Kit 或单样例包导出。

## 热更新可选层

| 目标包 | 自动包含 | 外部 UPM | 明确不包含 |
| --- | --- | --- | --- |
| HotUpdate.Core | ResKit.Core、HttpKit、基础热更抽象 | UniTask、Newtonsoft Json | Addressables、HybridCLR 运行时、代码热更 |
| HotUpdate.Addressables | HotUpdate.Core + ResKit.Addressables + AA 发布工具 | UniTask、Addressables、Newtonsoft Json | HybridCLR |
| HotUpdate.HybridCLR | HotUpdate.Addressables + HybridCLR DLL 导出工具 | UniTask、Addressables、HybridCLR、Newtonsoft Json | 无 |

未导入 HotUpdate 相关包的项目不会因为 ToolsHub 或 ResKit/UI Kit 被要求安装 HybridCLR。`HotUpdate.Core` 只提供策略接口和明确的不可用结果；HybridCLR Hook、启动器与自动注册器都位于 `HotUpdate.HybridCLR`。HybridCLR 工具也会检查 `HybridCLR.Editor` 程序集，插件不在时不会显示。

## Tools Hub 自动识别

`ToolsHub.Core` 不直接引用任何 Kit。每个 Kit 专属编辑器模块位于独立 asmdef，并通过 `StellarTool.RequiredAssemblyNames` 声明可用条件。

- 运行时扫描当前已加载程序集。
- 依赖满足才注册并显示该模块。
- 缺少 Kit、Addressables 或 HybridCLR 时，对应入口不会显示，而不是显示后报错。
- 新 Kit 按相同规则新增一个 `Modules/<Kit>/StellarFramework.ToolsHub.<Kit>.Editor.asmdef` 即可接入。
- `Kit 安装状态` 页还会直接列出当前已加载的核心 Kit 和 Adapter，便于开发者确认“当前项目实际导入了什么”。

## 原始工程维护约定

原始框架工程还提供独立的 `StellarFramework/Framework Source/Kit Package Exporter` 窗口。它不属于 ToolsHub：可多选 Kit、预览并去重依赖闭包、导出为一个 `.unitypackage` 和同名依赖说明。窗口与组合导出器位于 `Modules/Packaging`，该目录已被所有消费者分发路径排除，因此业务项目不会携带它。

窗口会将 Profile 分为“独立 Kit”和“有依赖 / 适配器”：前者不自动带入其他 StellarFramework Kit，后者会直接列出并自动合并依赖。Addressables、HybridCLR 与代码热更均只在明确选择相关 Adapter/Profile 后进入导出包。

新增或拆分 Kit 时，同步更新：

1. `KitDistributionCatalog.json` Profile 与依赖闭包；
2. Kit 专属 Tools Hub 子程序集（如有编辑器工具）；
3. `StandaloneSourceExportPolicyTests` 的边界测试；
4. 本文档的分发矩阵。

具体的导出与测试基线见 [KitExportValidationMatrix.md](KitExportValidationMatrix.md)。
