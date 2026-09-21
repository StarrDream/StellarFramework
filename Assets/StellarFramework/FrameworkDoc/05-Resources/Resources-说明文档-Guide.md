# Resources / 说明文档

`Resources` 目录保存框架默认运行资源、示例资源和部分默认配置资产。

主要内容：

- `ResKitRuntimeSettings`
- `UIKitSettings`
- `UIRoot`
- 示例音频、示例配置、Resources 后端测试资源

主要用途：

- 作为 `ResKit` 的 `Resources` 后端运行时资源入口
- 作为 `UIKit` 默认 `UIRoot` 和默认配置的读取位置
- 作为样例和 Quick Start 的默认资源基础

使用约束：

- 这里的资源路径带有明确约定，不建议随意改名或迁移
- 示例和 Quick Start 依赖这些固定路径
- 大体量、生产期可热更资源不建议长期放在 `Resources`

相关文档：

- [Resources 源码文档](Resources-源码文档-Guide.md)
- [ResKit 统一资源说明文档](../Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [UIKit 界面系统说明文档](../Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)
