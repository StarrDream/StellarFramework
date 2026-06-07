# Tests / 说明文档

`Tests` 目录主要存放框架的 EditMode 测试。

它们的作用不是演示功能，而是保护这些关键内容：

- README 和文档入口
- Quick Start 和 Onboarding
- Tools Hub 工作流
- Addressables / HotUpdate 关键链路
- 部分 Runtime 公开表面

主要测试区：

- `Tests/EditMode/FrameworkValidation`
- `Tests/EditMode/UIKit`

使用建议：

- 改动文档入口时，优先跑文档和 onboarding 相关测试
- 改动 AA / HotUpdate 工具时，优先跑发布和 manifest 相关测试
- 改动 UIKit 时，优先跑 UIKit 相关 EditMode 测试

相关文档：

- [Tests 源码文档](Tests-源码文档-Guide.md)
