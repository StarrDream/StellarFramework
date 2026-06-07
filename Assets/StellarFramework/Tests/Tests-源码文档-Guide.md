# Tests / 源码文档

## 模块职责

`Tests` 目录主要存放 EditMode 测试，用来保护框架入口、文档结构、编辑器工具和关键运行时表面。

它的重点不是“业务逻辑回归”，而是“框架约束不被破坏”。

## 源码文件

当前主要覆盖：

- `Tests/EditMode/FrameworkValidation/*`
- `Tests/EditMode/UIKit/*`
- 以及被这些测试读取的 README、Quick Start、ToolsHub 文档和部分源码

## 总体结构

```text
Tests
├─ EditMode
│  ├─ FrameworkValidation
│  └─ UIKit
└─ 通过路径读取 README / 文档 / 源码
```

## 目录结构

### `Tests/EditMode/FrameworkValidation`

这部分主要保护：

- README / 文档入口
- Quick Start / Onboarding
- Addressables / HotUpdate 发布流程
- ToolsHub 文档和入口策略

### `Tests/EditMode/UIKit`

这部分主要保护：

- UIKit 公开表面
- UIKit 运行时快照输出
- 栈相关行为约束

## 代表性测试

- `QuickStartCatalogPolicyTests`
- `OnboardingSurfacePolicyTests`
- `AAHotUpdatePublishToolTests`
- `HotUpdateManifestTests`
- `UIKitRuntimeSnapshotTests`
- `UIKitStackSurfacePolicyTests`

## 测试策略

### 文档类测试

目标：

- 防止 README 和文档入口失效
- 防止旧文档和旧入口回归
- 防止 Quick Start 链接断裂

### ToolsHub 测试

目标：

- 防止编辑器入口和关键工具路径失效
- 防止发布和初始化流程约束被破坏

### Runtime 表面测试

目标：

- 保证关键接口名称、快照输出和最低限度行为稳定

## 与文档的关系

文档和测试是联动的：

- 改 README 或快速开始，要同步看文档测试
- 改 ToolsHub 文档入口，要同步看 onboarding 和 catalog 策略测试
- 改样例路径，要同步看 Quick Start 策略测试

## 设计约束

- 修改文档入口时要同步更新对应测试
- 修改 AA / HotUpdate 流程时优先补测试再改逻辑
- 样例和 Quick Start 的固定路径是测试基线的一部分

## 推荐回归顺序

1. 文档和入口改动
   跑 `QuickStartCatalogPolicyTests`
2. Onboarding 改动
   跑 `OnboardingSurfacePolicyTests`
3. AA / HotUpdate 改动
   跑 `AAHotUpdatePublishToolTests` 及相关测试
4. UIKit 改动
   跑 `UIKit` 相关 EditMode 测试

## 常见误用

- 改文档但不改测试
- 改 Quick Start 路径却忘了验证入口策略
- 改发布流程却只手测、不补自动化约束
