# Samples / 源码文档

## 模块职责

`Samples` 目录承担两种角色：

- 对使用者：提供最小接线示例
- 对维护者：提供回归和行为验证入口

它们不是独立产品，而是用来展示和验证框架各模块的最小可运行链路。

## 源码文件

与样例体系直接相关的主要代码包括：

- `Samples/README.md`
- `Samples/KitSamples/README.md`
- `Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs`
- `Samples/ArchitectureDemo/*`
- 各 Kit 样例脚本

## 总体结构

```text
Samples
├─ ArchitectureDemo
├─ KitSamples
│  ├─ Scenes
│  ├─ Example_*
│  └─ Editor
└─ README / 索引文档
```

## 目录结构

### `ArchitectureDemo`

用于演示：

- `Architecture`
- `Model`
- `Service`
- `View`
- `UI`

之间的协作链路。

### `KitSamples`

用于分别验证各 Kit 的最小接线方式。

典型包括：

- `UIKit_Playable`
- `ResKit_Playable`
- `SettingsKit_Playable`
- 其他 Kit 对应场景

## 关联类型与工具

- `ExamplePlayableSceneBuilder`
  负责样例资源和场景生成
- 各 `Example_*` 组件
  负责最小闭环验证

## 与运行时模块的关系

- 样例直接依赖 Runtime Kits
- 样例资源依赖 `Resources`
- 部分资源链路依赖 `Generated` 产物
- 样例入口和资源补齐依赖 ToolsHub 的样例构建逻辑

## 关键构建入口

样例构建通常通过：

- `Quick Start`
- 样例构建相关 ToolModule

来生成或修复：

- 场景
- Prefab
- 示例配置
- 示例资源

## 关键场景

- `UIKit_Playable.unity`
- `ResKit_Playable.unity`
- `SettingsKit_Playable.unity`
- `ArchitectureDemo` 相关场景

## 运行时调用链

1. 用户从 `Quick Start` 或样例入口触发样例构建
2. ToolsHub 写入或修复样例资源
3. 用户打开 Playable 场景
4. 场景中的样例入口脚本初始化对应 Kit
5. 通过可见 UI、日志或运行结果验证模块行为

## 设计约束

- 样例路径和场景名带有文档与测试约定
- 修改样例路径、入口脚本和名称时，要同步更新 Quick Start、README 和测试
- 样例代码应优先体现“最小闭环”，而不是堆功能

## 常见误用

- 把样例当成正式产品逻辑继续膨胀
- 改样例路径后不更新文档和测试
- 样例依赖关系修改后不重跑样例构建

## 测试与验证

- `QuickStartCatalogPolicyTests`
- `OnboardingSurfacePolicyTests`
- 手动运行对应 Playable 场景
