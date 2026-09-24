# HotUpdate Publisher 使用文档

HotUpdate Publisher 是 Unity Editor 内的构建与发布编排工具。它根据 Git、程序集边界和 Unity 资产信息判断变更风险，复用现有 HybridCLR/YooAsset 构建与验证能力，再通过可替换的发布目标安全发布不可变文件和 PackageVersion 指针。

Publisher 不负责客户端下载、缓存、资源生命周期或 DLL 加载。客户端继续使用：

```text
YooAssetContentUpdater.UpdateHostPackageAsync(...)
    ↓
ResKit.YooAsset
    ↓
HybridCLRKit.RunAsync(...)
```

## 当前实现状态

当前仓库已有变更分类、BaseRelease 仓库、HybridCLR/YooAsset Build Adapter、产物校验、Release Gate、LocalFolder/S3-Compatible 发布目标、远端 GET/Range 校验、Dry Run、Release History 与 Rollback 服务。ToolsHub 提供 Overview、Changes、Build、Server、History、Advanced 分区。

ToolsHub 的构建和发布主按钮会在生产 YooAsset Collector、目标平台 BaseRelease、完整阶段配置和发布目标尚未连接时保持禁用。当前仓库的 YooAsset Collector 是验证用途；不能用它冒充业务生产包，也不能把未配置流程报告成发布成功。接入新业务项目时，需要先完成本指南中的项目配置和发布流水线装配。

## Base App 与 HotUpdate 边界

Base App 包含启动和稳定运行所需的框架、平台 Adapter、原生 SDK Wrapper、网络/存储后端、Bootstrap、Updater、ResKit、YooAsset Adapter、HybridCLR Loader 及底层 Flow Runtime。Base 程序集不得引用具体 HotUpdate 类型。

HotUpdate 适合承载 Gameplay、Mission、Stage、业务 Flow、NPC 行为、UI 业务逻辑、活动和数值规则。HotUpdate 可以依赖 Base App 暴露的稳定 API；Base App 不得反向依赖 HotUpdate。

如果变化需要修改 Player 构建契约、Native Plugin、Packages、ProjectSettings、基础程序集或内置启动资源，应按 Base App Release 处理，不得作为普通 Hot Patch 上传。

## 推荐目录

```text
Assets/_Project/
├─ Base/          # Bootstrap、Platform、Infrastructure、Adapters
├─ HotUpdate/     # HybridCLR HotUpdate 源码与 asmdef
├─ Content/       # YooAsset 远端 Scene、Prefab、Config 与美术内容
└─ BaseContent/   # Bootstrap/Fallback Scene、Update/Error UI、Fallback Font
```

旧项目不要求一次性迁移目录。Publisher 结合 asmdef、资产类型、PluginImporter、Build Settings 和项目配置分类；归属不清楚的资产会升为 Yellow 或阻止发布，而不会仅因目录名熟悉就默认判 Green。

HotUpdate MonoBehaviour Prefab/Scene 必须由 YooAsset 远端包管理，并在 HotUpdate 程序集加载后才加载。Build Settings 推荐只保留 Bootstrap 和 Fallback 场景。

## 程序集约定

- HotUpdate C# 源码使用独立 HotUpdate asmdef。
- HotUpdate asmdef 可以引用 Base App 稳定程序集。
- 禁止 Base App asmdef 引用 HotUpdate asmdef。
- 修改 asmdef、引用、平台约束或编译定义至少属于 Yellow，需要 Full Gate。
- Base App 源码或程序集依赖变化属于 Red，需随 Base App 重新构建交付。

## 变更分类

| 分类 | 典型内容 | 普通 Hot Patch |
| --- | --- | --- |
| GREEN | 已识别的 HotUpdate 源码、远端 Prefab、配置和一般 YooAsset 内容 | 运行 Fast Gate 后可以继续 |
| YELLOW | Shader、asmdef/引用、未知资产归属或需目标平台验证的变化 | 必须运行 Full Gate |
| RED | Base App、内置启动内容、ProjectSettings、UPM、原生插件或依赖方向违规 | 阻止普通 Hot Patch，转 Base App Release |

任意 Red 都会阻止普通 Hot Patch。Git/Unity 元数据读取失败时 Preflight 必须报错，不能把错误吞掉后继续分类。

## 首次配置

1. 创建并验证目标平台 BaseRelease，保存 Unity/HybridCLR/YooAsset 版本、Scripting Backend、AOT metadata 文件及 SHA256。
2. 配置 YooAsset 业务 Collector 和 HotUpdate DLL/AOT metadata 收集规则。不要使用 `StellarHotUpdateVerification` 作为生产 Collector。
3. 在 ToolsHub 的 Server 区分别设置 Development、Staging、Production 的 MainHostServer、FallbackHostServer、RemoteRoot、PublishTarget 和 Credential Profile Name。使用 LocalFolder 时，为每个环境选择已挂载目录的 Local Folder Root；该路径与非秘密 profile 元数据一起存放在项目级 EditorPrefs。
4. MainHostServer 必须直接指向 YooAsset Package 文件目录；不要重复追加 Package 名或 RemoteRoot。
5. LocalFolder 的根目录通过 Server 区的文件夹选择器配置；Profile 的 RemoteRoot 会追加到该根目录下。S3-Compatible 凭证由环境变量 Provider 读取，不写入 Assets、EditorPrefs 或 Git。`CredentialProfileName=ProductionCdn` 对应变量 `STELLAR_HOTUPDATE_PRODUCTIONCDN`；变量内容是 JSON，必须含 `accessKeyId`、`secretAccessKey`，`sessionToken` 可选。
6. 对每个环境执行只读的目标连通性和文件 GET/Range 验证。Production 主/回退 Host 必须使用 HTTPS。

推荐远端布局：

```text
/game/<environment>/<platform>/<base-app-version>/<package>/
```

Development、Staging、Production 必须使用独立目录或独立 Bucket 前缀，避免测试环境覆盖生产 PackageVersion 指针。

## 日常 Hot Patch

1. 确认变更只涉及 HotUpdate 程序集或 YooAsset 远端内容。
2. 在 Changes 刷新 Git 与 Unity 分类，检查 Red/Yellow 原因和程序集边界。
3. 选择匹配平台和 BaseRelease，填写 Package、Release Notes 与目标环境。
4. 执行 Dry Run；检查将上传与可复用的文件、字节数、SHA256、Gate 和环境。
5. Dry Run 通过后，完整流水线依次执行 Compile、HybridCLR Export、YooAsset Build、Artifact Validation、Release Gate、PrepareUpload、UploadFiles、VerifyRemote、PublishVersion 和 Finalize。
6. 只有机器证据全部通过，History 才会保存 ACTIVE Release Record。

Release Gate 必须验证真实的 ResKit/Manifest/DLL/Entry 链路。YELLOW、重大 Hot Patch 和 Base App Release 需要 Full Gate；Full Runner 缺失或失败都必须停止发布。

## Base App Release

Base App Release 会更新目标平台的 BaseRelease：生成平台匹配的 HybridCLR/AOT metadata，校验其 Hash，并记录 Player 构建契约。随后 HotUpdate 包必须绑定到该 BaseRelease。不得从未知的 `HybridCLRData` 临时拷贝 metadata 作为正式发布依据。

Base App Release 需要 Full Gate 和目标平台验证。Android 验证必须使用 Android Target 重新生成 HybridCLR/AOT metadata、Manifest 和 YooAsset 包，不得使用 Windows metadata 冒充。

## Dry Run

Dry Run 运行构建与 Gate 直到 PrepareUpload，并查询远端 Exists/GetInfo 生成 New/Reuse 计划。它不会调用 Upload、远端发布校验、PublishVersion 或 Rollback，也不会更改服务器。

若同一路径已存在但长度或 SHA256 不同，Dry Run 必须失败，不得覆盖不可变对象。

## Rollback

Rollback 从 Release History 读取历史文件路径、长度、SHA256 和 Manifest 文件名，先验证全部远端对象仍存在且 Hash 一致，再只 compare-and-swap 更新历史 PackageVersion 指针，最后执行历史 Manifest/Bundle GET 与 Range 校验并记录事件。Rollback 不会重新上传历史 DLL 或 Bundle。

如果指针已经切换但远端运行校验失败，结果会明确报告 `PointerChanged=true`，History 将旧活动版本标记为 RolledBack，将目标版本标记为 RollbackUnverified，并记录失败事件。此时不能把结果当作成功回滚。

## Production 安全要求

- Git 工作区必须干净；staged、unstaged 或 untracked 改动都会阻止 Production 发布。
- Git branch、完整 commit 和 dirty 状态写入发布来源记录。
- UPM Git 依赖必须 pin 到 tag 或 commit。
- 不可变文件路径已存在但内容不同必须失败。
- 上传全部不可变文件并通过 SHA256、Manifest/Bundle GET 和 Range 校验后，才允许更新 PackageVersion。
- 凭证只通过外部 Provider 注入，不能写到 Assets、EditorPrefs 或仓库。
- 任何缺失 Gate、构建、远端或历史证据都不得显示为 PASS。

## 版本号

默认 PackageVersion 为 UTC `YYYY.MM.DD.NNN`，例如 `2026.09.24.001`；同一天从已有有效版本递增。ReleaseId 是独立的发布审计标识，不能与 YooAsset PackageVersion 混用。
