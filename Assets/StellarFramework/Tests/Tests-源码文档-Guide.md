# Tests / 源码文档

Tests 目录主要放 EditMode 测试，用来保护框架入口、文档策略、ToolsHub 工作流和关键 Runtime 链路。

## 源码位置

- `Tests/EditMode/FrameworkValidation`：框架策略、文档、Quick Start、AA、HotUpdate 等验证测试。
- `Tests/EditMode`：其他编辑器测试入口。
- `Runtime/Kits/*`：部分 Kit 的行为通过样例或工具测试间接覆盖。

## 核心类型

- `QuickStartCatalogPolicyTests`：文档、README、Quick Start 和双轨文档策略测试。
- `OnboardingSurfacePolicyTests`：新人入口和 ToolsHub onboarding 策略测试。
- `AAHotUpdatePublishToolTests`：AA 配置、路径、安全校验和发布逻辑测试。
- `HotUpdateManifestTests`：Manifest 解析、BOM 和字段校验测试。
- Addressables/HotUpdate 相关测试：验证 DLL bytes、metadata 和远端 AA 加载链路。

## 关键方法

- `QuickStartReferencedPathsExistOnDisk`：检查样例和文档入口存在。
- `DocumentationDoesNotContainOutdatedAAWorkflowGuidance`：禁止旧 AA 口径回归。
- `DocumentationHubGroupsDocumentsByAudienceAndPurpose`：检查文档中心分组。
- `SourceGuideCoversMainSourceReadingRoutes`：检查源码文档覆盖关键类型。

## 核心测试类型

- 文档策略测试：检查 README 链接、双轨文档、旧文案防回归、源码文档关键类型覆盖。
- 新人入口测试：检查 Quick Start 是否仍指向固定样例和文档。
- ToolsHub 测试：检查核心工具入口、AA 配置和发布逻辑。
- HotUpdate 测试：检查 `HotUpdateManifest` 解析、BOM 处理、SHA 字段等。
- ResKit/Addressables 测试：检查远端 AA、catalog、dll bytes、metadata 加载链路。

## 数据流

1. Unity Test Runner 加载 EditMode 测试程序集。
2. 测试通过 `Application.dataPath` 定位 `Assets/StellarFramework`。
3. 文档测试扫描 Markdown 和 README 链接。
4. ToolsHub 测试读取 Editor 源码、配置资产或临时目录。
5. 运行链路测试通过 Addressables、ResKit、HotUpdateKit 验证真实加载结果。

## 依赖关系

- 依赖 NUnit 和 Unity Test Framework。
- 文档测试依赖 Markdown 文件路径稳定。
- AA/HotUpdate 测试依赖 Addressables 和对应测试资源。
- PlayMode 热更测试可能依赖构建产物，本次文档整理不跑远端热更运行测试。

## 扩展点

- 新增文档入口时，补 README 链接测试或文档策略测试。
- 新增 ToolsHub 模块时，补入口存在性或关键按钮文案测试。
- 新增 Kit 时，补说明文档、源码文档和样例路径测试。
- 新增热更流程时，先补 Manifest/路径/校验测试，再接运行链路测试。

## 测试入口

- 修改文档：跑 `QuickStartCatalogPolicyTests`。
- 修改 Quick Start 或样例入口：跑 `OnboardingSurfacePolicyTests`。
- 修改 AA 发布工具：跑 `AAHotUpdatePublishToolTests` 和必要的 Addressables 加载测试。
- 修改 Runtime Kit：优先跑对应 Kit 的 EditMode 测试和样例场景。
