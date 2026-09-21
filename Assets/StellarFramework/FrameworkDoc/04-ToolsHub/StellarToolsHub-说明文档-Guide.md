# StellarToolsHub / 说明文档

`Tools Hub` 是框架的统一编辑器入口。

主要用途：

- Quick Start
- 文档中心
- 资源构建
- HybridCLR 代码热更新产物导出
- UIKit / SettingsKit / ConfigKit 等工具入口
- 开发辅助和诊断工具

## 打开入口

```text
StellarFramework -> Tools Hub
```

## 主要分组

左侧分组固定顺序如下：

- `Start Here`
- `资源管理`
- `框架核心`
- `热更新`
- `生产力`
- `常用工具`

## 新手路线

1. 进入 `Start Here -> Quick Start`
2. 打开并运行唯一 `ArchitectureDemo`
3. 阅读快速开始与对应 Kit Guide
4. 再按需进入 `Addressables`、`HybridCLR DLL 导出` 或其他资源工具

## 常用模块

- `Quick Start`
- `文档中心`
- `资源打包 (AssetBundle)`
- `Addressables`
- `ResKit 资源审计`
- `UIKit 工具`
- `SettingsKit 设置中心`
- `ConfigKit 配置中心`
- `HybridCLR DLL 导出`

`Addressables` 只负责本地 Settings / Group 配置检查与 Player Content 构建；正式内容热更新由项目的 YooAsset 启动层负责。启用 HybridCLR 后，可在 `HybridCLR DLL 导出` 中生成热更 DLL、AOT metadata 与 Manifest。

## 使用建议

- 日常入口优先用 `Quick Start`
- 框架文档统一从 `文档中心` 查看
- Addressables 与 HybridCLR 保持独立：AA 走 `Addressables` 模块，代码热更产物走 `HybridCLR DLL 导出`
- 欢迎使用 StellarFramework：可从 Start Here 的欢迎页进入 30 分钟上手，并在任意模块中返回欢迎页。

## 相关文档

- [ToolsHub 源码文档](StellarToolsHub-源码文档-Guide.md)

