# SaveKit / 源码与不变量

## 职责边界

SaveKit.Core 负责 Slot、Section Registry、Snapshot、Container、Serializer/Storage 抽象、Checksum、事务、Backup、Migration、Validation、Restore DAG、结果模型和诊断。它不依赖 TimeKit、EventKit、ResKit、Addressables、HybridCLR 或 Newtonsoft。

业务通过 ISaveSection<TData> 提供纯 DTO 的 Capture、Validate 和 Restore。SaveKit 不反射扫描业务对象，也不保存 Unity Object 图。

## ContainerVersion 1

容器使用固定 Magic STSV、显式 ContainerVersion、Little Endian 标记、Metadata、Section Directory 和连续 Payload。Descriptor 保存：

    SectionId
    SchemaVersion
    SerializerId
    PayloadOffset
    PayloadLength
    Checksum
    Flags

Container 元数据由 Core 自己读写，因此没有 Newtonsoft 时仍可列出 Slot、检查 Header 和 Section Descriptor。

## Pipeline

Save：在主线程 Capture 成 DTO，按 Section 选择 Serializer 写入临时容器，为每个 Payload 计算 xxHash64，重新读取并验证 .tmp，最后交给 Storage Commit。

Load：读取 current 或健康 Backup，完成全部 Checksum、Deserialize、Migration、Validate 和 RestoreAfter 排序后，才执行任何 Restore。某个 Section 在 Prepare 阶段失败时，不会提前修改其他业务 Model。

## Transaction / Recovery

FileSystem Storage 使用 .tmp、.sav 和 .bak。平台支持时优先使用 File.Replace，否则使用“current 复制到 backup、删除 current、移动 tmp”的可恢复策略；不承诺所有平台具备严格文件系统原子替换。

关键不变量：

1. current 只在 tmp 完整写入并 Verify 后替换。
2. failed save 不破坏上次成功 current。
3. backup 表示最近可恢复的 previous current。
4. Revision 只在 successful commit 后提升。
5. Load 全部 Prepare 成功前不得 Restore。
6. Unknown Preserve 不 Deserialize 未知 Payload。
7. Core Container 不依赖外部 Serializer。
8. SaveKit Core 不依赖 Extension。
9. Idle 无 Update。
10. 外部 Save 一律视为不可信输入。

## Migration

Migration Registry 为每个 Section 保存连续的 FromVersion -> ToVersion 链。缺少一步、版本倒退、目标版本过高或 Migration 抛异常都会返回明确 ErrorCode，不会回退到当前 DTO 强读。

## Serializer / Storage

Core 默认提供 Unity Json 和 Raw Bytes 两个轻量实现，但业务可以注册自定义 Serializer 和 Storage。Serializer 通过 Stream 接口读写并接收 CancellationToken；Storage 集中处理路径、文件名、Commit 和平台差异。

Newtonsoft Adapter 位于独立程序集，使用 TypeNameHandling.None 和有限 MaxDepth。它不是 Core 的编译依赖。

## Threading

Capture 与 Restore 必须在主线程。Serialize、Checksum、Read、Write、Verify 可由 Storage 或自定义实现决定是否异步；公开 Async 不代表所有平台都支持后台线程。Load 在 Apply 开始前响应取消，进入 Restore DAG 后会完成整个 Apply，避免半恢复；Save 的 Commit 开始后同样会完成可恢复提交。

## Diagnostics

最近一次操作记录 Capture、Serialize、Checksum、IO、Commit、Total 时长、Section 数量、Raw/Final 字节、Backup、Migration 计数和错误；`Sections` 列表还记录每个 Section 的 Capture、Serialize、Payload、Migration 和 Validation。运行时不以固定毫秒阈值作为失败条件，Benchmark 只记录趋势。

EditMode Benchmark 以 100000 个定长 struct record 记录 Capture、Serialize、Write、Read、Deserialize、Migration、Checksum 和内存变化，供不同机器比较结构性回归。

## Tests

EditMode 覆盖 ID 安全、Container Round Trip、Checksum、Registry、Restore DAG、Missing/Unknown、Migration、事务失败和 Newtonsoft Adapter。PlayMode 覆盖真实 persistentDataPath 异步保存、加载、取消和生命周期。故障注入使用 Memory Storage，不触碰玩家文件。

V1 不包含 Cloud、网络存档、数据库、自动保存调度、TimeKit Adapter、World/Inventory Adapter、压缩、加密、Journal、Patch Save、反射自动存档或 GameObject 自动恢复。
