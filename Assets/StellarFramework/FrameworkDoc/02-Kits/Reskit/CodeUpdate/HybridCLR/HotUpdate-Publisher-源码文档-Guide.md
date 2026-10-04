# 热更发布器（HotUpdate Publisher） 源码文档

## 组装边界

Publisher 位于 `Assets/StellarFramework/Editor/StellarToolsHub/Modules/HotUpdatePublisher/`，由 Editor-only asmdef 承载。Runtime Kits 不依赖 Publisher。模块通过小接口隔离 Unity 构建入口、Git、远端对象存储、凭证 Provider 和 HTTP 验证边界。

```text
ToolsHub UI
   ↓
HotUpdatePublishPipeline / HotUpdatePublishDryRun
   ↓
Stage Handlers
   ├─ HybridCLR / YooAsset Build Adapters
   ├─ Artifact Validator / Release Gate
   ├─ IHotUpdatePublishTarget
   ├─ HotUpdateRemoteValidator
   └─ Release History Repository
```

ToolsHub 已接通发布工作流。`HotUpdatePublisherHubModule.DrawMainActions` 根据 Build 与 Publish 就绪检查分别启用 `Build`、`Dry Run`、`Build & Publish`；按钮进入对应的运行方法，再由 `CreateWorkflow` 调用 `HotUpdatePublisherWorkflowAssembly.Create(...)` 组装标准阶段。缺少平台构建 Adapter、有效 BaseRelease、Collector、发布目标、凭证或 Gate 配置时，相应操作保持禁用并显示具体阻断原因；运行期间也会禁用重复操作。LocalFolder 根路径作为非秘密环境 Profile 字段保存在项目级 EditorPrefs，并传给 `LocalFolderPublishTarget(profile)`。不要在 UI 的 `OnGUI` 中重写发布逻辑。

当前端到端证据覆盖 Development + LocalFolder：`2026.09.30.011` 在本地发布目录完成不可变文件上传、HTTP 回读、Android Gate、消费者冷启动/缓存重启和回滚。发布目录与 CDN 均为本机测试资源；真实 Production S3/CDN endpoint、凭证、TLS 域名和生产权限尚未验证，部署项目仍需配置并执行 Production Dry Run。

## 核心数据

### `HotUpdatePublishContext`

保存一次发布事务中的平台、环境、Base App/Package/Release 版本、Git 来源、变更分类、构建目录、YooAsset 清单、Gate 证据、上传文件及发布状态。阶段按顺序补全 Context；后续阶段依赖前置阶段设置的结构化结果，不通过日志文本猜测成功。

### `HotUpdatePublishResult` 与 `HotUpdatePublishStepResult`

每个 Stage 返回 Success/ErrorCode/Error/Warnings。Pipeline 捕获异常时保留原始 Exception 和失败 Stage。缺少 Handler、空结果、失败返回、异常和取消都会停止流水线。

### `HotUpdateReleaseRecord`

记录 ReleaseId 与 PackageVersion、兼容 BaseRelease、平台/环境、Git branch/commit/dirty、变更分类、DLL SHA、Bundle 数、文件路径/长度/SHA、YooAsset Manifest 文件名、Gate 摘要、ServerRoot 和状态。历史数据需要包含远端证据，Rollback 不从当前工作区重建历史内容。

## Pipeline 阶段

`HotUpdatePublishPipeline` 按固定顺序运行：

```text
Preflight → ClassifyChanges → CompileHotUpdate → ExportHybridCLRAssets
→ BuildYooAsset → ValidateArtifacts → RunReleaseGate → PrepareUpload
→ UploadFiles → VerifyRemote → PublishVersion → Finalize
```

任意阶段失败、抛异常、取消或缺失都会停止。`PublishVersion` 仅在不可变文件上传和远端验证都完成后才能执行；`Finalize` 只有在 Gate、远端校验和版本发布通过后才持久化 Active Release Record。

`HotUpdatePublisherWorkflowAssembly.Create(...)` 提供 Editor 侧的标准组装入口，生成上述 12 阶段全流程和只读 8 阶段 Dry Run。调用方显式注入 `IHotUpdateGitSnapshotProvider`、变更分类器、构建 Adapter、产物验证器、Gate Runner、发布目标、远端预验证器和 History Repository；组装器不会自行搜索项目设置、Collector 或凭据。ToolsHub 负责从已校验的项目配置创建这些依赖并执行工作流。

## Build 与 Validation Adapter

- `HybridCLRHotUpdateBuildAdapter` 复用 `HybridCLRHotUpdateAssetExporter`，按 `HotUpdateSettings.AotMetadataKeys` 从选定的 BaseRelease 筛选目标平台 AOT metadata；缺少任一配置项时构建失败。
- `YooAssetHotUpdateBuildAdapter` 调用 YooAsset Editor Build，校验输出目录、PackageVersion、Bundle 和全部 Manifest 文件。上传清单会排除 `OutputCache` 与 `OutputCache.manifest`；它们仅供下一次 Editor 增量构建使用，不是客户端运行资源。
- `HotUpdateArtifactValidator` 检查 Manifest、DLL SHA256、入口类型/方法、Manifest 所选 AOT metadata、目标平台和 PackageVersion，并逐项对照所选 BaseRelease 的 SHA256。
- Adapter 只在 Editor 发布程序集，HotUpdate Runtime 继续由既有 Bootstrap/YooAsset/ResKit/HybridCLR 链路消费。

## Git Provenance

`GitHotUpdateSnapshotProvider` 直接运行固定 Git 子命令读取 branch、commit 与 `status --porcelain=v1 -z --untracked-files=all`。它不经过 Shell，不记录文件内容。Detached HEAD 记录为 `(detached)`。

`HotUpdateGitPreflightStageHandler` 将来源写入 Context。Development/Staging 允许 Dirty 并给出 Warning；Production Dirty、未知环境、Git 命令失败或缺失仓库元数据都必须失败。ToolsHub 的显式刷新会显示完整 Commit 和 Dirty 状态。

## 版本策略

`IHotUpdateVersionPolicy` 与 PackageVersion 的存储/生成位置隔离。`DailyHotUpdateVersionPolicy` 要求 UTC 时间，使用 `YYYY.MM.DD.NNN`，按同日有效历史最大 sequence 加一，并在 999 后失败。它不产生或修改 ReleaseId。

## Publish Target 契约

`IHotUpdatePublishTarget` 抽象 Upload、Exists、GetInfo、Verify、PublishVersion 和 Rollback。核心不依赖 S3/Nginx/YooAsset SDK。

- `LocalFolderPublishTarget` 把文件写入配置挂载目录，通过临时 sibling + no-overwrite 移动上传；相同路径/Hash 可幂等复用，不同内容不能覆盖。PackageVersion 用互斥锁和原子替换实施 expected-current compare-and-swap。
- `S3CompatiblePublishTarget` 复用签名对象存储接口；不可变对象采用 create-only，并读取远端实际字节确认 SHA。PackageVersion 使用 create-only 或 ETag compare-and-swap。凭证由外部 Provider 提供。
- 远端实现的错误必须向上传播并带目标路径/操作上下文，不允许空 catch 或以“上传成功”推测验证成功。

## Remote Verification

`HotUpdateRemoteValidator` 使用可替换的 `IHotUpdateRemoteHttpClient`。发布前检查 expected-current 版本指针、JSON Manifest、一个大于 262,144 字节的 Bundle、Content-Length、实际读取字节和 `Range: bytes=262144-` 的 206/Content-Range。配置了回退 Host 时对主/回退 Host 执行相同检查。

Rollback 校验从 Release Record 查找历史 PackageVersion、JSON Manifest 和 Range Bundle；每个历史不可变文件还必须与记录长度/SHA256 完全一致。HTTP 响应体使用固定 buffer 流式读取，大对象不整体缓存在内存中；Manifest 捕获有大小上限。

## Dry Run

`HotUpdatePublishDryRun` 复用 Preflight 到 PrepareUpload 的阶段，再通过 Exists/GetInfo 形成 New/Reuse 文件计划。它不调用 Upload、远端发布验证、PublishVersion 或 Rollback。实现上必须保证 Dry Run Handler 集合中不存在这些写操作阶段。

## History 与 Rollback

`HotUpdateReleaseHistoryRepository` 在 `BuildArtifacts/HotUpdate/ReleaseHistory` 保存每个 Release JSON 和独立事件 JSON。写文件先写临时文件再原子替换；ReleaseId 和所有远端相对路径通过安全校验。

Release 创建时间以 ISO-8601 UTC 字符串持久化，因为 Unity `JsonUtility` 不会序列化 `DateTime` 字段。兼容旧记录时，Repository 优先使用不可变 `Activated` 事件的文件时间恢复顺序；只有没有对应事件时才使用 Release JSON 文件时间。

Rollback 的执行顺序：

```text
加载并校验当前/历史 Release
→ 读取每个远端对象并比对记录 SHA256/长度
→ 只 CAS 更新历史 PackageVersion 指针
→ 历史 Manifest/Bundle GET 与 Range 验证
→ 更新状态并记录 Rollback Event
```

不重新上传旧 DLL/Bundle。如果指针写成功但远端运行验证失败，服务返回 `PointerChanged=true`，历史目标为 `RollbackUnverified`，旧活动版本为 `RolledBack`，并写入 `RollbackVerificationFailed` 事件。

## 测试分层

- Git 分类、Boundary、DevelopmentConvention 和 Git preflight：纯 Editor/EditMode 测试。
- Build/Artifact/Release Gate/Pipeline：注入假 Adapter，断言失败停在准确阶段。
- LocalFolder/S3-Compatible：临时目录或假对象存储检查幂等、不可变、CAS、路径安全和凭证诊断。
- Remote Validator：假 HTTP transport 验证状态码、长度、Range 和 fallback；另有本机 loopback GET/Range 验证 BCL transport。
- History/Rollback：临时历史目录和假目标验证 SHA 校验、无历史文件重传、指针切换及失败事件。
- Clean Consumer：通过推荐 Profile 的精确依赖闭包验证 Publisher/验证工具没有进入 Hot Update Full。源码测试之外，正式版本还应导出并在干净消费者项目导入。

任何未完成的平台验证、远端配置或测试都必须保留为待验证状态，不能降低 Gate 或改写断言来补造 PASS。
