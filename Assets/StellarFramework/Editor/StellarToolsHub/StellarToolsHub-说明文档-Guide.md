# StellarToolsHub / 说明文档

`Tools Hub` 是框架的统一编辑器入口。

主要用途：

- Quick Start
- 文档中心
- 资源构建
- 热更新配置与发布
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
- `样例支持`
- `生产力`
- `常用工具`

## 新手路线

1. 进入 `Start Here -> Quick Start`
2. 执行样例构建
3. 运行 `UIKit_Playable.unity`
4. 运行 `ResKit_Playable.unity`
5. 再按需进入 `AA 配置与发布`

## 常用模块

- `Quick Start`
- `文档中心`
- `资源打包 (AssetBundle)`
- `AA 配置与发布`
- `ResKit 资源审计`
- `UIKit 工具`
- `SettingsKit 设置中心`
- `ConfigKit 配置中心`
- `HybridCLR DLL 导出`

`AA 配置与发布` 提供 `一键本地内置构建` 与 `一键远端热更发布`；启用 HybridCLR 后可在 `HybridCLR DLL 导出` 中导出热更 DLL。

## 使用建议

- 日常入口优先用 `Quick Start`
- 框架文档统一从 `文档中心` 查看
- Addressables / HybridCLR 发布统一走 `AA 配置与发布`
- 欢迎使用 StellarFramework：可从 Start Here 的欢迎页进入 30 分钟上手，并在任意模块中返回欢迎页。

## 相关文档

- [ToolsHub 源码文档](StellarToolsHub-源码文档-Guide.md)
