# Kit 导出验证矩阵

本文件记录框架原始工程的导出验证基线。业务项目只需使用导出的 `.unitypackage` 与其同名依赖说明，无需导入本目录。

## 当前可选目标

当前目录包含 52 个分发 Profile：2 个单文件目标、2 个 Runtime 支持包、2 个 ToolsHub 包、1 个生成支持包、14 个 Foundation Kit、3 个 Extension Kit、10 个 Adapter Profile，以及 18 个可选样例包。

Catalog schema v2 以 `tier` / `category` 描述架构职责；它不改变 `kind` 的分发语义，也不会让同层 Kit 自动安装。完整规则见 [KitArchitectureGuide.md](KitArchitectureGuide.md)。

关键组合如下：

| 目标 | 导入后包含 | 明确不包含 |
| --- | --- | --- |
| AudioKit.Core | PoolKit、SingletonKit、ToolsHub.Core | ResKit、Addressables、HybridCLR |
| AudioKit.ResKitAdapter | AudioKit.Core、ResKit.Core | Addressables、HybridCLR |
| ConfigKit.Core | 文本来源、路径和覆盖规则 | Newtonsoft Json、Addressables、HybridCLR |
| ConfigKit.NewtonsoftJson | ConfigKit.Core、ToolsHub.Core、JSON 配置工具 | Addressables、HybridCLR |
| SettingsKit.Core | SingletonKit、设置定义与存储 | AudioKit、LogKit、Addressables、HybridCLR |
| SettingsKit.UnityAdapters | SettingsKit.Core、Unity 图形/语言/输入适配器 | AudioKit、Addressables、HybridCLR |
| SettingsKit.AudioKitAdapter | SettingsKit.Core、AudioKit.Core | ResKit、Addressables、HybridCLR |
| TimeKit | LogKit、游戏世界 Tick 与定时调度 | ActionKit、UniTask、Addressables、HybridCLR |
| SaveKit.Core | LogKit、存档容器、Section、事务、Migration 与 FileSystem Storage | Newtonsoft、TimeKit、Addressables、HybridCLR |
| SaveKit.NewtonsoftJson | SaveKit.Core、Newtonsoft JSON Serializer | TimeKit、Addressables、HybridCLR |
| SaveKit.Tools | ToolsHub 存档中心、Verify、Raw/Hex、Migration Type Chain、Dry Run | 不增加运行时领域依赖 |
| GridKit | 负坐标几何、DenseGrid、Footprint、整数 Occupancy | Addressables、HybridCLR、所有其他 Kit 与 UPM |
| HotUpdate.Core | ResKit.Core、HttpKit、热更策略抽象 | Addressables、HybridCLR、代码热更实现 |
| HotUpdate.Addressables | HotUpdate.Core、ResKit.Addressables | HybridCLR |
| HotUpdate.HybridCLR | HotUpdate.Addressables、HybridCLR 运行时与导出工具 | 无 |

样例 Profile 也遵守同一依赖边界：

| 目标 | 导出内容 | 依赖与排除 |
| --- | --- | --- |
| Sample.TimeKit | TimeKit Sample 脚本、Common 场景说明、`TimeKit_Playable.unity` | 依赖 `TimeKit`；排除 SaveKit、ResKit、Addressables、HybridCLR |
| Sample.SaveKit | SaveKit Sample DTO/Section、Common 场景说明、`SaveKit_Playable.unity` | 依赖 `SaveKit.Core` + UniTask；排除 TimeKit、ResKit、Addressables、HybridCLR、Newtonsoft |
| Sample.GridKit | GridKit 示例脚本、Common 场景说明、`GridKit_Playable.unity` | 依赖 `GridKit`；无 UPM；排除 Addressables、HybridCLR、其他 Kit |

完整 Profile、依赖闭包与 UPM 要求以 [KitDistributionCatalog.json](KitDistributionCatalog.json) 为准。

样例验证规则：核心 Kit Profile 不包含 `Assets/StellarFramework/Samples`；样例则按 Kit 拆成独立 Profile 和独立 asmdef，导出时才随对应 Kit 闭包一起生成。

## 已执行验证

- Unity 编译：无非预期 Console error。
- 分发边界测试：覆盖单文件导出、Adapter 排除、ToolsHub 程序集识别、依赖闭包与 Catalog 架构元数据。
- TimeKit：EditMode 与 PlayMode 测试通过；单 Kit 安装包已实际导出并检查外层 Bootstrap、内层 payload 与 LogKit 依赖闭包。
- 完整 EditMode：228 项完成，227 通过、0 失败、1 项明确标记为 Player/IL2CPP 环境专用而跳过；HybridCLR AA 全链路用例仍需 Player/IL2CPP 环境。
- 已实际导出并核对依赖说明：AudioKit.Core / ResKitAdapter、SettingsKit.Core / UnityAdapters / AudioKitAdapter、ConfigKit.Core / NewtonsoftJson。
- HotUpdate.HybridCLR 的完整启动路径已单独验证通过。
- SaveKit.Core：EditMode 覆盖 Slot/Section 安全、Container、Checksum、事务、Backup、Migration、Missing/Unknown、Restore DAG、跨 DTO 类型链和未来版本提前失败；Newtonsoft Adapter 已完成 Round Trip 验证。
- SaveKit：已完成 100000 CropSaveRecord End-to-End Save/Load 基准；ToolsHub 已验证 Raw/Hex 有界预览、Migration Type Chain 和只读 Dry Run 入口。
- SaveKit 示例：独立 asmdef 仅依赖 SaveKit.Core 与 UniTask，不包含框架业务样例或热更插件。
- TimeKit 示例：模板和开发场景均由 `ExamplePlayableSceneBuilder.BuildAllSamples()` 生成，场景只包含相机、方向光、样例脚本和统一说明面板。
- SaveKit 示例：覆盖两个 Section 的 `RestoreAfter` 顺序、Save/Load/Delete、Revision/Diagnostics 和真实 V1→V2 DTO Migration；不解析私有磁盘格式。
- GridKit：17 项 EditMode 行为测试与 1 项 1M/100k 基准通过；覆盖 same-owner 重复失败、cross-owner takeover 防护、Preview self-overlap/只读/他人冲突；Core asmdef 无引用、无 UnityEngine，`GridKit_Playable` 场景验证通过且无 missing script。
- GridKit V1 RC ownership regression：write-side `allowedExistingOccupant` overload 已删除；`TryOccupy` 仅执行 Empty → Owner，`CanOccupy` Preview 永不修改，`TryRelease` 保持 Owner → Empty 原子语义。

## 后续空白工程检查

每次发布前，在独立空白 Unity 工程中按以下顺序抽检：

1. 仅导入 `ToolsHub.Core`，确认 `Kit 安装状态` 可打开且没有 Kit 专属工具。
2. 导入某个 Core 包，确认编译通过且安装状态页只显示其依赖闭包。
3. 再导入其 Adapter，确认仅新增对应能力和工具入口。
4. 对 Addressables、HybridCLR 这类外部插件层，确认未安装插件时入口隐藏，安装后才显示。

> 本机曾尝试对 `ToolsHub.Core` 执行空白工程导入烟测，但 Unity LicensingClient 的 IPC 通道在启动阶段超时（返回码 199），因此该项未计为通过；需在许可服务可用的 Unity 环境重跑。

> 生产放行还必须在目标平台 IL2CPP Player 上执行 `HybridClrAaRunnerCanEnterHotUpdate` 等价的真实远端发布烟测：下载 catalog、bundle、Manifest、DLL 与 AOT metadata，完成 SHA256 校验并进入热更入口。该步骤不能由编辑器测试或离线构建替代。
