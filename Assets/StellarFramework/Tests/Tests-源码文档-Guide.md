# Tests / 源码文档

## 模块职责

Tests 是维护者自动验证层，证明单 Kit Contract、性能趋势、框架 Policy 和真实 Runtime 边界。它不承载用户教学，也不承载业务 Demo。

最高层职责定义见 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md)。

## 源码

测试源码位于 EditMode、PlayMode 两套程序集下；FrameworkValidation 的浅层分类只改变物理路径，不改变测试类的 namespace 或程序集身份。

## Directory Taxonomy（当前）

~~~text
Assets/StellarFramework/Tests
├─ EditMode
│  ├─ FrameworkValidation
│  │  ├─ Kits/TimeKit
│  │  ├─ Kits/SaveKit
│  │  ├─ Kits/GridKit
│  │  ├─ Kits/SpatialKit
│  │  ├─ Performance/SaveKit
│  │  ├─ Performance/GridKit
│  │  ├─ Performance/SpatialKit
│  │  ├─ Policies/Architecture
│  │  ├─ Policies/Documentation
│  │  ├─ Policies/Packaging
│  │  ├─ Policies/Samples
│  │  ├─ Policies/ToolsHub
│  │  ├─ Policies/RuntimeDelivery
│  │  ├─ Policies/Verification
│  │  ├─ Addressables
│  │  └─ 根目录 Legacy / Cross-cutting
│  └─ UIKit
└─ PlayMode
~~~

P0 Behavior 与 Performance 以及职责清晰的 Policy 已做浅层迁移；Addressables 有独立程序集；UIKit 和不明确的跨框架测试暂留原位置。

## asmdef

FrameworkValidation 继续使用：

~~~text
Assets/StellarFramework/Tests/EditMode/FrameworkValidation/StellarFramework.FrameworkValidation.Tests.asmdef
~~~

采用目录方案 B：asmdef 物理位置和 name、references、includePlatforms 保持不变，新增子目录不会改变程序集身份，也不扩大 references。Addressables 子目录继续使用 StellarFramework.FrameworkValidation.Addressables.Tests.asmdef。PlayMode 程序集和 UIKit 程序集不在本轮迁移。

不要为了目录整齐上移 asmdef、拆分程序集或制造 assembly overlap/circular reference。

## Namespace 与 Category

本轮保持历史 namespace（如 StellarFramework.Tests.FrameworkValidation、StellarFramework.Tests.HotUpdate、StellarFramework.Tests.ResKit），目录迁移不触发批量 namespace Diff。

新测试建议使用：

~~~csharp
[Category("Kit")]
[Category("Performance")]
[Category("Policy")]
[Category("Integration")]
[Category("Release")]
~~~

本轮不对旧测试机械补 Category。命名应表达 Contract，推荐 Method_Condition_Result 或项目现有的可读命名，禁止 Test1、BasicTest、TempTest。

## Behavior Tests

Behavior 测试验证公开 API、边界、失败结果、失败原子性和 Regression。纯 C# 优先 EditMode。TimeKitTests、SaveKitCoreTests、GridKitTests、SpatialKitTests 的代码和 namespace 未改变，只改变了物理目录。

重大修复必须保留回归测试，并在测试名或注释中说明曾经错误的行为。例如 GridKit 的 cross-owner takeover impossible，以及 SpatialKit 的负坐标 floor 和查询范围保护。

## Benchmark

Benchmark 文件沿用 <KitName>BenchmarkTests.cs，放在 Performance/<KitName>。必须输出规模、操作数、Elapsed、环境和校验和；GC.GetTotalMemory(false) 只能作为 coarse heap / GC trend。不要写机器相关的硬编码毫秒门槛，也不要把 Benchmark 当作语义正确性的替代。

## Policy

Policies 按 Architecture、Documentation、Packaging、Samples、ToolsHub、RuntimeDelivery、Verification 做浅层分组。结构化检查优先：

- asmdef 和 Catalog JSON 解析；
- 文件和链接存在；
- 程序集/类型反射；
- Exporter、Bootstrap、ToolsHub 的最小源码边界。

FrameworkValidation 根目录剩余的 DeveloperQuickToolsLogicTests、FrameworkValidationReportTests、HotUpdateManifestTests、HybridCLRHotUpdateAssetExporterTests、ResKitAssetBundleManagerTests 仍是 Legacy / Cross-cutting；职责清晰前不要猜测迁移。

## PlayMode

PlayMode 仅用于真实 Unity Runtime：生命周期、Object.Destroy、Coroutine、Scene、Resources、UIKit、异步和 Runtime initialization。现有 PlayMode asmdef 不动；运行前按 PlayMode/README 的样例构建前置条件准备资源。

## Expected Logs

故意触发 Error/Warning 的测试必须 LogAssert.Expect 或等价声明，并提供清晰测试名/注释。测试 Runner 中未被声明的 Console error 属于真实失败，不可全局屏蔽。

## Asset paths

测试读取的 README、Catalog、Sample、Runtime 源码路径属于验证输入；它们不是测试程序集的运行时依赖。物理路径只在 Exporter、asmdef 边界、Sample distribution 或文档链接确有意义时才写入 Policy。

## .meta、文件移动与 GUID

文件迁移使用 git mv 或等价操作，并同时保留 .cs 与 .cs.meta。目录 .meta 必须是有效 folderAsset；不要删除旧 meta 再新建同名脚本，不要在 Unity 中打开并保存无关 Scene YAML。

## CI / Test Runner

Local Fast Gate 运行编译和定向 EditMode；Framework Gate 运行全量 EditMode、必要 PlayMode、Policy 和性能；Release Gate 在独立空白工程和目标 Player/IL2CPP 环境运行。测试发现数量在目录或 asmdef 变化前后都要核对，不能静默丢失。

## Common mistakes

- 把 Sample 当作 Bug 回归或把 Integration 做成完整游戏。
- 移动文件时丢失 .meta、改变 asmdef identity 或批量改 namespace。
- 只看代码推断 Release PASS，未记录真实导出/导入/Player 证据。
- 未声明预期日志，或用全局关闭 Error 掩盖问题。
- Benchmark 未运行却把计划数值写入 Validation Matrix。
- 把 StellarFrameworkVerification 注册成普通 Catalog Profile。
