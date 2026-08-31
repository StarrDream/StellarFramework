# Kit 导出验证矩阵

本文件记录框架原始工程的导出验证基线。业务项目只需使用导出的 `.unitypackage` 与其同名依赖说明，无需导入本目录。

## 当前可选目标

当前目录包含 45 个分发 Profile：2 个单文件目标、2 个 Runtime 支持包、1 个 ToolsHub 包、1 个生成支持包、12 个 Foundation Kit、3 个 Extension Kit、9 个 Adapter Profile，以及 15 个可选样例包。

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
| HotUpdate.Core | ResKit.Core、HttpKit、热更策略抽象 | Addressables、HybridCLR、代码热更实现 |
| HotUpdate.Addressables | HotUpdate.Core、ResKit.Addressables | HybridCLR |
| HotUpdate.HybridCLR | HotUpdate.Addressables、HybridCLR 运行时与导出工具 | 无 |

完整 Profile、依赖闭包与 UPM 要求以 [KitDistributionCatalog.json](KitDistributionCatalog.json) 为准。

样例验证规则：核心 Kit Profile 不包含 `Assets/StellarFramework/Samples`；样例则按 Kit 拆成独立 Profile 和独立 asmdef，导出时才随对应 Kit 闭包一起生成。

## 已执行验证

- Unity 编译：无非预期 Console error。
- 分发边界测试：覆盖单文件导出、Adapter 排除、ToolsHub 程序集识别、依赖闭包与 Catalog 架构元数据。
- TimeKit：EditMode 与 PlayMode 测试通过；单 Kit 安装包已实际导出并检查外层 Bootstrap、内层 payload 与 LogKit 依赖闭包。
- 完整 EditMode：177 项完成，176 通过、0 失败、1 项明确标记为 Player/IL2CPP 环境专用而跳过；HybridCLR AA 全链路用例仍需 Player/IL2CPP 环境。
- 已实际导出并核对依赖说明：AudioKit.Core / ResKitAdapter、SettingsKit.Core / UnityAdapters / AudioKitAdapter、ConfigKit.Core / NewtonsoftJson。
- HotUpdate.HybridCLR 的完整启动路径已单独验证通过。

## 后续空白工程检查

每次发布前，在独立空白 Unity 工程中按以下顺序抽检：

1. 仅导入 `ToolsHub.Core`，确认 `Kit 安装状态` 可打开且没有 Kit 专属工具。
2. 导入某个 Core 包，确认编译通过且安装状态页只显示其依赖闭包。
3. 再导入其 Adapter，确认仅新增对应能力和工具入口。
4. 对 Addressables、HybridCLR 这类外部插件层，确认未安装插件时入口隐藏，安装后才显示。

> 本机曾尝试对 `ToolsHub.Core` 执行空白工程导入烟测，但 Unity LicensingClient 的 IPC 通道在启动阶段超时（返回码 199），因此该项未计为通过；需在许可服务可用的 Unity 环境重跑。

> 生产放行还必须在目标平台 IL2CPP Player 上执行 `HybridClrAaRunnerCanEnterHotUpdate` 等价的真实远端发布烟测：下载 catalog、bundle、Manifest、DLL 与 AOT metadata，完成 SHA256 校验并进入热更入口。该步骤不能由编辑器测试或离线构建替代。
