# BuildArtifacts 产物管理规则

`BuildArtifacts/StellarFramework` 是本地导出目录，不是源码事实来源。正式分发定义永远以 `KitDistributionCatalog.json` 和 Package Publisher 为准。

## 当前正式产物

当前仍在 Catalog 中存在的 `output` 文件，才算“当前正式产物”。例如：

```text
StellarFramework-UIAdaptationKit-Core.unitypackage
StellarFramework-Profile-UIAdaptationKit-Complete.unitypackage
StellarFramework-UIKit-Core.unitypackage
StellarFramework-Profile-UIKit-Complete.unitypackage
```

对应 `*-Dependencies.md` 必须与包同名，并由 Publisher 同次导出生成。

## Legacy / Validation 产物

以下命名只代表历史或维护者验证，不应交给业务项目：

```text
StellarFramework-Sample-*
*-With-Sample.*
Validation-*
StellarFramework-Validation-*
旧 HotUpdate-Core / HotUpdate-Addressables / HotUpdate-HybridCLR 拆分包
```

这些文件可以保留用于历史排查，但不得作为当前 Catalog 能力的入口。

## 维护规则

1. 开发者找包时先看 ToolsHub / Catalog，不直接根据 `BuildArtifacts/Kits` 文件名猜。
2. 新 Profile 导出覆盖同名当前产物，不制造时间戳副本。
3. 删除 Catalog Profile 后，其旧包必须被标记为 Legacy 或在下一次发布清理。
4. Validation 包只能由维护者使用，不进入 README 的用户下载入口。
5. Release 前执行 Artifact Audit：Catalog 当前 outputs 必须存在，Legacy/Validation 文件不得被误列为当前正式产物。

## ToolsHub 清理入口

框架原始工程的 `StellarFramework -> Export` 顶部提供 `清理旧产物`：

- 先扫描并预览候选；
- 二次确认后才删除；
- 当前 Catalog / Recommended Profile 的 `output` 及对应 `*-Dependencies.md` 始终加入保护集；
- 只匹配明确的 `Validation-*`、`StellarFramework-Validation-*`、`StellarFramework-Sample-*`、`*-With-Sample*` 与旧 HotUpdate 拆分包命名。

因此这个入口不是“清空 BuildArtifacts”，也不会删除当前正式包。

## 为什么暂不强制改目录

当前已有自动化、测试工程和本地工作流引用 `BuildArtifacts/StellarFramework/Kits`。为了避免一次“整理目录”破坏既有 clean-project 验证，本阶段先建立明确规则和自动审计，再在未来需要时迁移到 `Release / Validation / Legacy` 物理子目录。
