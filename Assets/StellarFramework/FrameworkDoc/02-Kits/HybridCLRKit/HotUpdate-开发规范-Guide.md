# HotUpdate 开发规范

本文约束可通过 YooAsset 热更新的代码和内容边界。目标是让 Base App 与 Hot Update 可以独立演进，并让发布前检查能够基于工作区事实阻止不安全的普通热补丁。

## 运行时依赖方向

运行时保持现有调用链：

```text
YooAsset → YooAssetContentUpdater → ResKit.YooAsset → HybridCLRKit → HotUpdate Assembly
```

HybridCLRKit 不直接依赖 YooAsset。Publisher 是 Editor-only 工具，不进入 Player，也不实现第二套 DLL 加载器、资源下载器或资源更新器。

HotUpdate 可以调用 Base App 暴露的稳定 API。Base App 程序集不得引用 HotUpdate 程序集，也不得在 Base App 类型中声明 HotUpdate 类型字段、参数或返回值。Base App 在安装时必须能独立编译和启动。

## 新项目目录

新项目建议按以下职责放置文件：

| 目录 | 内容 | 发布边界 |
| --- | --- | --- |
| `Assets/_Project/Base` | 启动、平台适配、稳定服务接口和必须随应用安装的代码 | Base App |
| `Assets/_Project/HotUpdate` | 通过 HybridCLR 加载的 C# 代码及对应 asmdef | 远端 HotUpdate 程序集 |
| `Assets/_Project/Content` | 需要由 YooAsset 远端包更新的场景、Prefab、配置和美术资源 | YooAsset 远端内容 |
| `Assets/_Project/BaseContent` | Bootstrap/Fallback Scene、Update/Error UI、Fallback Font、Logo、Loading 和平台启动必需资源 | Base App |

目录名是新项目的约定，不要求旧项目立即移动或重命名资产。旧项目继续沿用现有路径；Publisher 会结合程序集、资产类型和 Unity 导入信息分类。旧项目暂时无法确定归属的资产会被标记为 Yellow，并要求 Full Gate，直到项目显式整理或分类。

Base App 还包括 StellarFramework、平台与 PICO Adapter、Android/iOS Bridge、Native SDK Wrapper、Network Transport、Storage Backend、Crash Handler、Bootstrap、Updater、ResKit、YooAsset Adapter、HybridCLR Loader 和底层 Flow Runtime。高频变化的 Gameplay、Mission、Stage、Flow Business、NPC Behaviour、UI Business Logic、教程、活动、任务状态机和数值规则适合放入 HotUpdate。

## HotUpdate 代码与程序集

HotUpdate C# 源码必须由独立 asmdef 编译，并通过 HybridCLR 的生成和导出流程提供程序集。HotUpdate 程序集可以依赖 Base App 稳定接口及允许的 Unity/第三方程序集；不要为了绕开边界把 Base App 源码复制进 HotUpdate。

修改 HotUpdate asmdef、引用、平台约束或编译定义属于 Yellow：必须运行 Full Gate。Base App 源码、asmdef 或运行时程序集引用发生变化属于 Red：普通 Hot Patch 不允许发布，必须随新 Base App 一起交付。

## 远端内容与 MonoBehaviour

HotUpdate 程序集中的 MonoBehaviour 只能序列化到 `Assets/_Project/Content` 及项目明确配置的 YooAsset 远端内容中。不要把这类组件放进 `BaseContent`、`Resources`、`StreamingAssets` 或 Build Settings 中随应用安装的场景。

启动顺序必须是先通过现有热更启动链完成 YooAsset 更新并加载 HotUpdate 程序集，再加载含 HotUpdate MonoBehaviour 的远端 Prefab 或 Scene。Unity 在程序集未加载时无法正确解析这些组件类型。Publisher 检查 Prefab/Scene 以及其嵌套 Prefab 的序列化脚本引用；无法通过已识别的远端根目录解释的 HotUpdate 组件归为 Red。

Prefab/Scene 的归属取决于它由哪个 YooAsset 包管理，而不是仅看文件扩展名。切换目录不会自动让资产进入或退出某个 YooAsset 包，项目的收集规则仍需覆盖该内容。

## 内容、配置和着色器

- 游戏逻辑应放入 HotUpdate 程序集；不要把仅因修改方便而需要重编的基础服务放进 HotUpdate。
- 需要远端调整的数值、文案和普通 ScriptableObject 配置放入远端内容；影响 Player 构建、启动方式或依赖解析的配置属于 Base App。
- 纯数值和运营配置优先用 JSON、Binary Config、编译后的 CSV 或 YooAsset ScriptableObject。HP 100 调整为 120 这类数值变化不应重新生成 HotUpdate DLL。
- Build Settings 推荐只保留 Bootstrap 和 Fallback；业务场景通过 YooAsset 加载。若资源明天修改后不希望重新发布 APK，就优先放入 YooAsset。
- `ProjectSettings`、`Packages/manifest.json`、`Packages/packages-lock.json`、平台插件和原生插件的变化属于 Red，因为它们改变 Player 构建契约。
- 远端 Shader、Shader Variant 和 Compute Shader 的变化属于 Yellow，必须通过 Full Gate 检查目标平台、变体和构建结果。远端 Shader 仍可能受 Player 已包含的变体和渲染管线限制。
- Render Feature、URP Renderer Feature 和复杂 Scene serialization 变化至少属于 Yellow；Graphics API、Player Graphics Setting、Native Graphics Plugin 或平台/基础程序集变化可能要求 Base App Release。
- 涉及 AOT、反射发射、原生互操作等敏感 API 的 HotUpdate 源码属于 Yellow。Publisher 对已知 API 标记做静态提示，不声称穷尽全部 AOT 风险；Full Gate 和目标平台验证仍是最终依据。

## Preflight 分类和 Gate

Publisher 把 Git staged、unstaged 和 untracked 状态与项目相对路径、asmdef 成员关系及引用图、Unity AssetDatabase 类型、PluginImporter、Build Settings 场景、ProjectSettings 和 Packages 变化合并分类。删除和重命名的源路径也参与检查。asmdef 图检查 Base App → HotUpdate 反向引用；Editor-only asmdef 不代表 Player 依赖。

| 等级 | 含义 | 发布前要求 |
| --- | --- | --- |
| **GREEN** | 已识别的 HotUpdate 代码或远端内容，且未发现 Base App 边界变化 | 有可发布内容时运行 Fast Gate |
| **YELLOW** | 需要目标平台构建、完整验证，或当前资产归属尚不明确 | 必须运行 Full Gate；结果仍需满足该 Gate 的发布条件 |
| **RED** | 修改了 Base App/内置内容/构建契约，或破坏程序集依赖方向/MonoBehaviour 内容边界 | 阻止普通 Hot Patch；随 Base App 版本发布并重新验证 |

任何 Red 变更都会使整个工作区不能作为普通 Hot Patch 发布。混合 Green/Yellow 变更至少运行 Full Gate。未知资产不会因为位于熟悉的目录就自动判为 Green。Git、Unity 导入、资产类型或依赖图读取失败时，Preflight 必须报告错误，不能把失败折算成安全等级。

## 旧项目逐步采用

旧项目无需为启用 Publisher 进行一次性目录迁移。先让现有程序集边界和 YooAsset 收集规则可被工具识别；再按功能增量采用上述目录约定。对于 Yellow 的未知资产，使用项目自己的明确配置或迁移该资产来消除歧义，并用真实 Gate 结果确认分类。不要通过放宽安全等级、跳过 Gate 或屏蔽异常来获得发布许可。

## 推荐远端目录布局

新项目可按环境、目标平台、兼容的 Base App 版本和 YooAsset Package 隔离远端文件：

```text
/game/
  prod/
    android/
        1.0.0/
        DefaultPackage/
          YooAsset package manifests and version pointer
          *.bundle
  staging/
    android/
      1.0.0/
        DefaultPackage/
```

例如：

```text
https://cdn.example.com/game/prod/android/1.0.0/DefaultPackage
https://cdn.example.com/game/staging/android/1.0.0/DefaultPackage
```

YooAsset 的 `MainHostServer` 应配置为对应 Package 目录，不能再追加一遍 Package 名或 `RemoteRoot`。Development、Staging、Production 必须使用独立目录或独立 Bucket 前缀，避免测试发布覆盖生产的可变 PackageVersion 指针。旧项目可以继续使用既有布局，但 Profile 的 RemoteRoot、MainHostServer 和实际挂载目录必须映射到同一个 Package 内容根目录。

## 发布来源与 PackageVersion

发布记录应保留 Git branch、完整 commit 和工作区 Dirty 状态。Production 发布要求干净工作区；Development 和 Staging 可在 Dirty 工作区继续，但记录必须明确标记未提交状态。无法读取 Git 元数据或使用未识别的环境 ID 时，Preflight 应失败。

默认 YooAsset `PackageVersion` 采用 UTC `YYYY.MM.DD.NNN`，例如 `2026.09.24.001`；同一天按已有有效版本递增。Publisher 的 `ReleaseId` 是独立审计标识，不能拿来替代 PackageVersion。新版本发布前仍必须完成不可变文件上传、远端字节校验、Manifest/Bundle GET 与 Range 验证，最后才更新 PackageVersion 指针。

## 性能与验证边界

分类只在用户显式运行 Preflight 时执行，不进入运行时 Update 路径。Git 状态、asmdef 图和资产元数据在一次 Preflight 中读取；Prefab/Scene 脚本引用检查使用 Unity 资产依赖关系，不需要每帧扫描。

静态分类是发布前的风险筛查，不代替编译、EditMode/PlayMode 测试、FrameworkValidation、HotUpdate Release Gate 或目标平台验证。绿色分类也必须通过对应的自动化 Gate 后，才能形成可发布证据。
