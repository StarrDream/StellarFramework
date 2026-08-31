# SaveKit / 源码、职责与不变量

本文件面向框架维护者、Serializer / Storage Adapter 开发者和未来修改 Core 的 Agent。

目标是说明真实实现为什么这样组织，以及哪些语义不能被“顺手优化”破坏。

## 1. Design Goals

SaveKit 的核心目标是可演进的本地持久化。

每个业务 Domain 以稳定 Section ID 独立演进。

Container Metadata 由 Core 控制，业务 DTO 不决定文件目录。

Save 使用 tmp、Verify 和 Commit，Load 使用 Prepare / Apply 两阶段。

未知 Section 可以 Preserve，旧版本 Section 可以通过 Typed Migration 升级。

Core 保持 Unity 依赖最小化，并允许 Adapter 自己选择 Serializer 和 Storage。

## 2. Non-goals

Core 不提供自动保存 MonoBehaviour。

Core 不反射扫描场景，不保存 GameObject 图，不管理资源句柄。

Core 不提供 Cloud Save、Steam Cloud、SQLite、压缩或加密。

Core 不定义玩家、世界、物品、作物或任务的领域模型。

ToolsHub 也不编辑玩家字段，不替换外部原文件。

这些能力只能由明确的 Adapter 或业务层提供。

## 3. Foundation Boundary

SaveKit 属于 Foundation / data。

它可以与 TimeKit、ConfigKit、ResKit 组合，但不引用这些程序集。

时间只以 Tick 或 UTC 数值进入 DTO。

资源只以稳定 Business ID 进入 DTO。

Load 后由业务重建调度器和资源句柄。

Core 不应增加 TimeKit API、UI API 或场景扫描入口。

## 4. Assembly Dependencies

`StellarFramework.SaveKit.Core.asmdef` 只引用：

```text
StellarFramework.LogKit
UniTask
```

Newtonsoft Adapter 是独立程序集，引用 Core、UniTask 和 `com.unity.nuget.newtonsoft-json`。

ToolsHub Adapter 是 Editor-only 程序集，引用 ToolsHub.Editor、Core 和 UniTask。

Sample 程序集是 `autoReferenced: false`，只引用 Core 和 UniTask。

Core 禁止反向引用 Adapter、ToolsHub、Samples、ResKit、Addressables 或 HybridCLR。

## 5. Runtime Type Map

```text
SaveKit
└─ SaveCoordinator
   ├─ SaveSectionRegistry
   ├─ SaveMigrationRegistry
   ├─ Serializer dictionary
   ├─ ISaveStorage
   ├─ SaveContainerReader / Writer
   ├─ XxHash64Checksum
   └─ SaveOperationDiagnostics
```

`SaveKit` 是静态 Facade，负责初始化和公开入口。

`SaveCoordinator` 负责 Gate、Save、Load、Dry Run 和恢复策略。

`SaveSectionRegistry` 负责 Section 注册、ID 唯一性和 Restore DAG。

`SaveMigrationRegistry` 负责版本链和类型链。

Serializer dictionary 按 Descriptor 的 SerializerId 查找，不按当前默认值强读旧数据。

Storage 只负责文件或远端介质，不理解 DTO。

Container Reader / Writer 只负责稳定二进制布局、边界和校验。

Diagnostics 是观察模型，不参与容器布局。

## 6. SaveKit Facade

`Initialize` 每次创建全新的 Options、Storage、Section Registry 和 Migration Registry。

默认 Serializer 是 `unity-json`，同时注册 `raw-bytes`。

`UseSerializer` 在初始化时加入额外 Serializer。

`RegisterSerializer` 只允许新增 ID，不允许静默覆盖已有实现。

`Register` 通过 Section Registry 检查 ID、版本、Serializer 和 Restore DAG。

`RegisterMigration` 把 Migration 放进目标 Section 的版本图。

`SaveAsync`、`LoadAsync` 和 `DeleteAsync` 都经过 Coordinator 的 Operation Gate。

`TryBuildMigrationChain` 给 ToolsHub 和诊断页读取已验证的链。

`RunMigrationDryRunAsync` 只执行 Prepare 管线，不进入 Restore 或 Save。

## 7. Registry

Section ID 在 Registry 中以 Ordinal 字符串作为键。

重复 Section 注册失败，不会替换旧实例。

注册时立即进行 RestoreAfter 循环检测。

Restore DAG 拓扑排序使用稳定的 Section ID 作为 tie-breaker。

因此同样的 Registry 顺序和 ID 集合总能得到相同结果。

未知 RestoreAfter ID 被忽略，不会生成不存在的边。

Migration 同一个 Section 的 FromVersion 只能注册一次。

Migration 的 ToVersion 必须大于 FromVersion。

## 8. Section Adapter

`ISaveSection` 是 Coordinator 使用的非泛型边界。

`ISaveSection<TData>` 为业务提供强类型 Capture、Validate、Restore。

`SaveSection<TData>` 通过 `typeof(TData)` 固定 DataType。

自定义 `ISaveSection` 实现也必须返回非空 DataType；Registry 会在注册时拒绝缺少类型信息的 Section。

显式接口实现负责 object 到 TData 的边界检查。

Restore 不接收 Serializer 的原始字节，只接收 Prepare 完成的 DTO。

Section 不应把运行时对象引用泄漏到 DTO。

## 9. Container Format

当前 `ContainerVersion = 1`。

Magic 是 ASCII `STSV`。

Endian Marker 是 `0x01020304`，使用 BinaryWriter / BinaryReader 的小端布局。

Container 依次保存 Metadata、CustomMetadata、Section Count、Descriptor Directory 和 Payload。

当前 Writer 在内存中写完目录和 Payload；没有 Seek 回填版本。

Reader 只接受当前 ContainerVersion，未知版本返回 UnsupportedContainerVersion。

同一个 ContainerVersion 的字节布局不能静默改变。

不兼容布局必须增加 ContainerVersion，并保留旧 Reader。

## 10. Binary Layout

字段顺序如下：

| 顺序 | 字段 | 类型 | 说明 |
| --- | --- | --- | --- |
| 1 | Magic | 4 bytes | `STSV` |
| 2 | ContainerVersion | Int32 | 当前为 1 |
| 3 | EndianMarker | Int32 | 0x01020304 |
| 4 | Revision | Int64 | 从 1 开始 |
| 5 | CreatedUtc | Int64 | DateTime ticks |
| 6 | UpdatedUtc | Int64 | DateTime ticks |
| 7 | SlotId | length + UTF8 | 逻辑 ID |
| 8 | ApplicationVersion | length + UTF8 | 诊断信息 |
| 9 | CustomMetadataCount | Int32 | 受 Options 限制 |
| 10 | CustomMetadata | key/value | UTF8 |
| 11 | SectionCount | Int32 | 受 Options 限制 |
| 12 | SectionId | length + UTF8 | 稳定 ID |
| 13 | SchemaVersion | Int32 | Section 版本 |
| 14 | SerializerId | length + UTF8 | Serializer 注册键 |
| 15 | PayloadLength | Int64 | Payload 字节数 |
| 16 | Checksum | UInt64 | xxHash64 |
| 17 | Flags | Byte | 当前保留 |
| 18 | Payload | bytes | 按目录顺序排列 |

Descriptor 的 PayloadOffset 是 Reader 在读取目录后记录的绝对流位置。

Offset 不是写入字段，因此不会改变 V1 字节布局。

## 11. Bounds Validation

Reader 先检查文件长度、Magic、ContainerVersion 和 EndianMarker。

Revision 必须大于零。

时间戳必须落在 DateTime ticks 的有效范围内。

字符串长度按 UTF8 字节数限制，而不是字符数。

CustomMetadata Count 和 Section Count 受 Options 限制。

PayloadLength 不允许负数。

Payload 总和不能超过 MaxPayloadBytes。

每个 Payload 必须完整读到声明长度。

末尾不允许存在未声明的尾部数据。

重复 Section ID、空 SerializerId 和非法 SchemaVersion 都会拒绝。

metadata-only Reader 会 Seek 跳过 Payload，仍检查目录长度和文件截断。

完整 Reader 会读取 Payload 并验证每个 Checksum。

## 12. Checksum

`XxHash64Checksum` 对每个 Section Payload 计算 UInt64。

Checksum 的目标是完整性和损坏检测。

Checksum 不是加密签名，也不是 Anti-Cheat。

外部 Save 仍必须视为不可信输入。

未来若增加签名，应作为独立能力和版本化字段设计。

## 13. Serializer Registry

Serializer 以字符串 ID 注册。

Writer 写入 Section 当前的 SerializerId。

Load 使用 Descriptor 中 Stored SerializerId。

因此切换默认 Serializer 不会改变旧 Section 的读取策略。

缺少 Stored Serializer 时返回 SerializerMissing。

Serializer 不能通过外部 Payload 自由指定任意 CLR 类型。

DataType 来自当前注册 Section，旧 DataType 由 Migration Chain 的 FromType 决定。

## 14. Serializer Capability

`ISaveSerializer` 保持最小的 SerializeAsync / DeserializeAsync 合同。

实现可选 `ISaveSerializerCapabilities`：

```csharp
public interface ISaveSerializerCapabilities
{
    SaveSerializerCapabilities Capabilities { get; }
}
```

能力标记：

| 标记 | 语义 |
| --- | --- |
| `Streaming` | 实现可按流处理，不要求完整字符串 |
| `BackgroundExecution` | CPU 工作不依赖 Unity 主线程 |
| `ThreadSafe` | 实例和共享状态可安全跨线程使用 |

只有同时声明 BackgroundExecution 和 ThreadSafe 才会调度到 Worker。

未知或只声明一个标记的 Serializer 留在调用线程。

## 15. Built-in Serializer

UnityJson 的能力是 None。

RawBytes 的能力是 BackgroundExecution 和 ThreadSafe；当前 API 仍返回完整 byte[]，不等于零拷贝。

Newtonsoft Adapter 的能力是 BackgroundExecution 和 ThreadSafe。

UnityJson 使用 JsonUtility，必须留在 Unity-safe 路径。

RawBytes 不访问 Unity API，可以在支持线程的平台后台执行。

Newtonsoft Serializer 每次操作创建 JsonSerializer，不共享可变 JsonReader 状态。

Editor 和 WebGL 在 Coordinator 中禁用 ThreadPool，安全退化到调用线程。

不能仅因为方法名是 Async 就假设 Serializer CPU 已经后台执行。

## 16. Storage Contract

`ISaveStorage` 提供 Current、Backup、Temporary 三种文件语义。

Storage 不应把未经 Verify 的数据标记为 current。

`WriteTemporaryAsync` 只写临时目标。

`CommitAsync` 才有权提升 tmp 为 current。

`RestoreBackupAsync` 恢复已验证 Backup，并尽量保留 Backup。

`ListSlotsAsync` 是索引操作，默认走 metadata-only Reader。

Storage 的异常由 Coordinator 映射为 StorageError 或恢复失败。

## 17. FileSystem Storage

根目录默认是 `Application.persistentDataPath/Saves`。

SlotId 在进入 Path.Combine 前已完成字符白名单验证。

后缀固定为 `.sav`、`.bak`、`.tmp`。

写 tmp 使用 WriteThrough、FlushAsync 和 Flush(true)。

存在 current 时优先使用 File.Replace。

平台不支持或 Replace 失败时使用复制 Backup、删除 current、提升 tmp 的回退路径。

回退路径无法保证所有平台的严格 atomic replace，这是 Storage 的已知边界。

## 18. Transaction State Machine

```text
Idle
↓
Capture
↓
Serialize
↓
Checksum
↓
WritingTemp
↓
VerifyTemp
↓
CommitStarted
↓
BackupPrevious / PromoteTemp
↓
Committed
```

Commit 前失败会清理可清理的 tmp，current 保持不变。

Save 开始前完整验证旧 current，坏 current 不会被旋转成 Backup。

关键 Commit 使用 CancellationToken.None，避免半替换。

成功 Commit 后才返回 Revision 和 Success。

## 19. Recovery State Machine

```text
Load current
├─ Valid → Current
└─ Invalid
   ↓
   Load backup
   ├─ Valid → RecoveredFromBackup
   └─ Invalid → Failure
```

current 和 backup 都经过完整 Container Reader。

从 Backup 恢复后，`UsedBackup` 和 Diagnostics.BackupUsed 为 true。

AutoRecoverBackup 开启时把已验证 Backup 复制回 current。

恢复复制不会把损坏 current 旋转覆盖已知良好的 Backup。

## 20. Save Pipeline

Save 的主线程边界是 Capture。

每个 Section Capture 后可立即 Validate。

Serializer 根据能力决定直接执行或 Worker 执行。

当前 Writer 使用每个 Payload byte[] 和一个 Container MemoryStream。

所有 Section 完成后写入目录和 Payload。

写入 Storage tmp 后重新打开 tmp，完整 Verify。

只有 Verify 成功才进入 Commit。

## 21. Load Pipeline

Load 首先完整读取并校验 current。

current 失败时尝试完整校验 backup。

随后为每个已知 Section 进入 Prepare。

Prepare 包含 Stored Serializer、类型选择、Deserialize、Migration 和 Validate。

全部 Prepare 成功后才建立 Restore DAG 并进入 Apply。

Unknown Preserve 只缓存已验证 raw entry，不进入业务管线。

## 22. Prepare / Apply

Prepare 是无副作用的数据阶段。

Prepare 失败不能调用任何 Restore。

Apply 按 RestoreAfter DAG 顺序调用 Restore。

Apply 开始前进行最后一次 Cancellation 检查。

第一个 Restore 开始后，普通 Cancellation 不再打断剩余 DAG。

业务 Restore 不应依赖另一个尚未完成的 Section。

## 23. Migration Registry

Registry 的键是 Section ID。

每个列表按 FromVersion 查找下一步。

同一 FromVersion 重复注册直接失败。

链构建只允许从低版本递增到目标版本。

Step 的 ToVersion 不能超过请求目标。

缺少中间步骤返回 MigrationMissing。

类型缺失、不连续或最终类型错误返回 MigrationTypeMismatch。

## 24. Typed Migration Type Resolution

`SaveMigration<TFrom, TTo>` 暴露：

```csharp
Type FromType => typeof(TFrom);
Type ToType   => typeof(TTo);
```

`ISaveMigration` 还提供 SectionId、版本和 object Migrate 合同。

注册参数中的 Section ID 是权威目标；若 Migration 自声明 SectionId，则必须一致。

跨 DTO Load 的真实伪代码：

```text
if stored == current:
    deserialize current type

else if stored < current:
    path = BuildPath(stored, current, section.DataType)
    oldType = path[0].FromType
    value = serializer.Deserialize(oldType)

    for migration in path:
        assert value type matches migration.FromType
        value = migration.Migrate(value)
        assert result type matches migration.ToType

    assert final type == section.DataType

else:
    fail UnsupportedSectionVersion

validate current DTO
```

版本相同绝不执行 Migration。

未来版本在 Deserialize 之前失败，避免用错误 DTO 解释 Payload。

## 25. Restore DAG

Node 是 Section，Edge 是 RestoreAfter。

Registry 注册时做循环检测。

Load 时再生成 deterministic 拓扑顺序。

同入度节点按 ID 排序，不依赖 Dictionary 枚举偶然顺序。

Prepare 不使用 DAG 顺序；所有 Section 必须先准备完成。

## 26. Missing / Unknown

Missing 是当前 Section 没有对应 Descriptor。

Missing Policy 可以是 Fail、UseDefault 或 Ignore。

UseDefault 仍必须 Validate。

Unknown 是 Descriptor 存在但当前 Registry 没有 Section。

Preserve 只保存 raw Descriptor 和 Payload。

Unknown 不进入 Deserialize、Migration、Validate、Restore。

Save 时过滤已重新注册的 ID，避免 Preserve 产生重复 Descriptor。

## 27. Threading

| 阶段 | 线程语义 |
| --- | --- |
| Capture | Unity 主线程 |
| UnityJson Serialize | Unity-safe 调用线程 |
| RawBytes Serialize | 支持平台可后台 |
| Newtonsoft Serialize | 支持平台可后台 |
| Checksum | 当前在 Coordinator 调用线程 |
| File IO | Storage async 合同 |
| Deserialize | Serializer capability 决定 |
| Migration | 当前在 Prepare continuation 执行 |
| Restore | Unity 主线程边界 |

`UNITY_EDITOR` 和 `UNITY_WEBGL` 禁用 ThreadPool 调度。

这是安全退化，不是所有平台都提供线程池的假设。

如果未来启用流式 Worker，必须重新验证 Stream 生命周期和线程归属。

## 28. Cancellation

Save 在 Gate、读取旧 current、Capture、Serialize、写 tmp 和 Verify 阶段响应取消。

Commit 使用不可取消令牌，保证已开始的提交可以完成。

Load 在读取、Prepare、Deserialize、Migration 和 Validate 阶段响应取消。

Apply 前最后一次检查取消。

Apply 开始后不普通取消，避免业务模型半恢复。

Storage 实现应继续传递 CancellationToken。

## 29. Operation Gate

Gate 是互斥 Busy，不是 Queue。

Save、Load、Delete 和 Dry Run 共用同一个 Gate。

新操作在已有操作完成前返回 Busy。

Gate 不保护业务自己直接修改 Section Registry 的并发行为。

因此注册和初始化应安排在操作空闲时。

## 30. Diagnostics

Diagnostics 不改变 SaveResult 的成功语义。

Operation 记录 Save、Load、Delete 或 MigrationDryRun。

阶段计时包含 Capture、Serialize、Deserialize、Migration、Validation、Checksum、IO、Commit、Restore 和 Total。

Section 计时记录 Payload 字节、版本、SerializerId 和类型链起点/终点。

LastExceptionType 仅作为 Development 诊断，不能被业务当作稳定错误协议。

Clone 会深复制 Section 列表，防止调用方修改最近一次结果。

## 31. Performance

CPU 可以分解为 Capture、Serialize、Checksum、IO 和 Migration。

Checksum 对 Payload 字节数是 O(bytes)。

GC 主要来自 DTO、数组、字符串、Serializer 缓冲和 Container MemoryStream。

Peak Memory 当前包含 Payload byte[]、Container MemoryStream 和 Storage copy buffer。

Main Thread Stall 主要来自 Capture、UnityJson 和 Restore。

RawBytes/Custom Binary 可降低格式膨胀，但仍要测量业务 Capture 和 Restore。

## 32. Memory Model

当前 V1 不是 Stream-first。

Writer 先物化每个完整 Section Payload。

随后这些数组在 Container MemoryStream 中组合。

FileSystem Storage 再把 Container 流复制到 tmp 文件。

Reader 的完整 TryRead 也会物化全部 Payload。

metadata-only TryReadMetadata 只用于 Slot 列表和结构检查。

Raw/Hex ToolsHub 预览通过 bounded file read 限制文本 64 KiB、Hex 每页 256 bytes。

V1.1 P1 可改为 Temp Stream Direct Write、Directory Seek 回填和 BoundedReadStream。

在完成并测试前，禁止文档宣称全文件流式或零拷贝。

## 33. ToolsHub Read Model

ToolsHub 依赖 Core 的公开读模型，不反射扫描业务工程。

Slot 列表使用 Storage 的 metadata-only 索引。

Inspector 使用完整 Reader 验证 Container 和 Section Checksum。

Raw/Hex 只读 Descriptor 指定的 bounded 范围。

Migration 页面使用 `TryBuildMigrationChain` 展示类型链。

Dry Run 调用 Coordinator 的 Prepare 管线，不调用 Restore、Save 或源文件写入。

Registry 为空时仍可打开、Verify、Raw、Hex 和查看 Stored Schema。

## 34. Security

外部 Save 是不可信输入。

SlotId 和 SectionId 字符白名单防止路径穿越和异常键。

Magic、Version、Endian、Count、Offset、Length 和 Overflow 都要验证。

MaxFile 由 MaxPayloadBytes 加容器余量约束。

MaxStringBytes、MaxSectionCount、MaxCustomMetadataCount 限制资源消耗。

SerializerId 必须命中已注册白名单。

Newtonsoft TypeNameHandling 固定关闭。

Checksum 只提供 Integrity / Corruption Detection。

不能把这些检查写成 Anti-Cheat。

## 35. AOT / IL2CPP

Core 不依赖运行时代码生成。

Typed DTO 可由业务程序集静态引用，适合 AOT 保留。

Newtonsoft Adapter 的 DTO 必须按项目 AOT 策略保留。

反射不是 Core 注册 Section 的必需机制。

Core 的容器和 Registry 不使用场景扫描。

Player/IL2CPP 仍应运行真实导出包和链接配置验证。

## 36. Invariants

1. ContainerVersion 字节布局不可静默变化。
2. current 只能由经过 Verify 的 tmp 提升。
3. Save 发现坏 current 时不覆盖它。
4. Backup 是上一份可恢复 current，不是无限历史。
5. Prepare 任一 Section 失败时不得 Restore。
6. Stored SerializerId 优先于当前默认 Serializer。
7. StoredVersion < CurrentVersion 时必须按 FirstMigration.FromType 反序列化。
8. Migration 版本和 CLR 类型链必须连续。
9. Unknown Preserve 不得进入业务 Deserialize/Migration/Restore。
10. Apply 开始后不普通取消，避免半恢复。
11. Restore DAG 必须无环且 deterministic。
12. Core 不得增加领域、UI、资源或热更依赖。

## 37. Failure Matrix

| 阶段 | 失败 | Current | Backup | Result |
| --- | --- | --- | --- | --- |
| Capture | Exception | unchanged | unchanged | failure |
| Serialize | failure | unchanged | unchanged | failure |
| Temp Write | failure | unchanged | unchanged | failure |
| Verify Temp | failure | unchanged | unchanged | failure |
| Commit | failure | recoverable | preserve | StorageError |
| Load Current | corrupted | unchanged | try backup | recovered/failure |
| Migration Build | missing/type mismatch | unchanged | unchanged | MigrationMissing/TypeMismatch |
| Migration Run | Exception | unchanged | unchanged | MigrationFailed |
| Prepare Validate | invalid DTO | unchanged | unchanged | ValidationFailed |
| Restore | business exception | may be partially applied | unchanged | RestoreFailed |

Restore 失败不能声称业务模型自动回滚。

## 38. Test Matrix

EditMode 覆盖：

- Slot/Section ID 白名单和长度。
- Container Round Trip、Magic、截断、负长度、尾部数据。
- Checksum、Serializer 缺失和 Newtonsoft `$type` 安全。
- Save Revision、tmp 写入、Commit 失败和坏 current 保护。
- Backup Recovery 和不覆盖已知良好 Backup。
- Missing Default、Unknown Preserve 和 Restore DAG。
- Prepare 全部完成前不 Restore。
- 跨 DTO 1 -> 2、1 -> 2 -> 3、链类型不连续、最终类型错误。
- Stored Serializer、相同版本类型和未来版本提前失败。
- 缺 Migration、Migration 抛异常和迁移后 Validate 失败。
- 生命周期钩子、100000 Crop End-to-End Benchmark。

PlayMode 覆盖真实 persistentDataPath FileSystem Save/Load、Backup Recovery、取消和删除。

## 39. Extension Points

稳定扩展点：

- `ISaveSection` / `SaveSection<TData>`。
- `ISaveSerializer`。
- `ISaveSerializerCapabilities`。
- `ISaveStorage`。
- `ISaveMigration` / `SaveMigration<TFrom, TTo>`。
- `ISaveLifecycleHooks`。
- `XxHash64Checksum` 及未来独立 Checksum 策略。
- ToolsHub 只读 Inspector Provider 的未来接口。

未来可独立增加 Compression、Cloud Storage 和 Partition。

扩展不得把领域模型倒灌进 Core。

## 40. Forbidden Dependencies

SaveKit.Core 禁止新增：

```text
World
Item
Crop
TimeKit API
UI API
Scene Scan
SaveableMonoBehaviour
[SaveField]
ResKit Handle
Addressables Handle
HybridCLR API
```

如果业务需要这些内容，应保存稳定 ID 或数值，再由业务恢复。

## 41. Limitations / Roadmap

V1 当前仍使用 MemoryStream + byte[] 的完整物化模型。

V1 不保证所有平台严格 atomic replace。

V1 不包含加密、签名、云冲突、压缩、增量和无限历史。

V1 ToolsHub 不提供 JSON Tree、Save Compare 或编辑后写回。

V1.1 P1 方向是 Temp Stream Direct Write、BoundedReadStream、流式 Checksum 和目录回填。

P2 可研究压缩、Cloud Storage、分页 Save Compare 和 Create Modified Copy。

任何 Roadmap 项在代码和测试完成前都不能写成现有能力。

## 42. 修改守则

先更新类型合同，再更新 Coordinator，再更新回归测试。

任何 Container 布局改动必须增加格式测试和版本说明。

任何线程调度改动必须测试 Editor、Player 和 WebGL 退化。

任何 Serializer 改动必须测试 Stored SerializerId 和跨 DTO Migration。

任何 Restore 改动必须验证 Prepare / Apply 不变量。

任何 ToolsHub 改动必须保持外部文件只读和预览上限。

文档必须描述代码已实现的行为，不得用未来设计代替现状。

## 43. Exception Mapping

Container Reader 将截断、Magic 错误、非法长度和尾部数据映射为 ContainerCorrupted 或 InvalidManifest。

Checksum 不匹配映射为 ChecksumMismatch。

未知 ContainerVersion 映射为 UnsupportedContainerVersion。

已注册 Section 缺少 Serializer 映射为 SerializerMissing。

未来 Section SchemaVersion 在反序列化前映射为 UnsupportedSectionVersion。

缺少 Migration Step 映射为 MigrationMissing。

类型链不连续或最终类型错误映射为 MigrationTypeMismatch。

Migration 代码抛异常映射为 MigrationFailed。

当前 DTO Validate 失败映射为 ValidationFailed。

业务 Restore 抛异常映射为 RestoreFailed。

Storage 打开、写入或 Commit 异常映射为 StorageError。

Exception Message 供诊断使用，ErrorCode 才是业务分支依据。

## 44. Metadata-only 与 Full Reader

`TryReadMetadata` 仍读取整个 Header、Manifest 和所有 Descriptor。

它按 PayloadLength Seek，不分配每个 Payload byte[]。

因此 ListSlots 可以快速显示 SlotId、Revision、时间戳和文件大小。

metadata-only 模式不重新计算 Payload Checksum。

需要完整健康结论时必须调用 `TryRead`。

Save 覆盖前使用 Full Reader，避免坏 current 被旋转成 Backup。

Load current 和 backup 使用 Full Reader，确保进入 Prepare 的 Payload 已完整验证。

ToolsHub Inspector 使用 Full Reader；Raw/Hex 预览再按 Offset 做有界读取。

## 45. Dry Run Contract

`DryRunMigrationAsync` 接收已经由 Reader 构造的 SaveSnapshot。

它进入同一套 PrepareSnapshotAsync 管线。

它会执行 Stored Serializer 选择、旧类型反序列化、Migration 和当前 DTO Validate。

它不会调用生命周期 BeforeRestore 或 AfterRestore。

它不会调用任何 Section Restore。

它不会调用 Storage WriteTemporary、Commit 或 RestoreBackup。

它不会更新 preserved unknown 缓存。

成功只代表该快照在当前 Registry 下可以完成 Prepare。

Dry Run 不代表业务 Restore 一定成功，也不代表源文件可以被信任为防篡改数据。

## 46. API 兼容性

`ISaveSerializer` 保持最小的两个异步方法，以便旧的自定义 Serializer 可以继续编译。

后台调度能力通过可选 `ISaveSerializerCapabilities` 增加，不强迫所有旧 Serializer 声明线程语义。

`ISaveMigration` 的 V1 合同包含 SectionId、FromVersion、ToVersion、FromType、ToType 和 Migrate。

`SaveMigration<TFrom, TTo>` 自动提供 FromType 与 ToType，减少手写错误。

直接实现 ISaveMigration 的类型必须显式补齐类型信息。

注册 API 仍接受 SectionId 参数，兼容迁移对象不声明 SectionId 的旧代码。

如果迁移对象声明有效 SectionId，Registry 会检查它与注册参数一致。

类型信息是 V1 Semantics 的一部分，不能通过 InvalidCastException 延迟验证。

## 47. Benchmark Interpretation

End-to-End Benchmark 使用 InMemorySaveStorage，因此 IO 数字反映内存 Storage，不是磁盘延迟。

它使用 CropBinarySerializer 和 100000 条 CropSaveRecord，避免把 JSON 结果误认为所有 Serializer 的上限。

Save Diagnostics 的 Capture、Serialize、Checksum、IO、Commit 便于比较阶段趋势。

Load Diagnostics 的 Deserialize、Migration、Validation、Restore 便于定位主线程或 CPU 阶段。

GC delta 使用 `GC.GetTotalMemory(false)`，不是完整的分配分析器 Peak 值。

Benchmark 不设置跨机器固定阈值，也不替代目标平台 Profiler。

## 48. Release Checklist

确认 ContainerVersion 未发生未记录的字节布局变化。

确认跨 DTO Migration 有旧 Payload 夹具和失败路径测试。

确认坏 current 不会覆盖已知良好 Backup。

确认 Prepare 失败时 RestoreCount 全部为零。

确认 Editor、Player 和 WebGL 的 Serializer 线程退化策略与文档一致。

确认 ToolsHub 外部打开仍是只读，预览有字节上限。

确认 Catalog 的 sourcePaths、requiredProfileIds 和 requiredUpm 与 asmdef 一致。

确认完整 EditMode、PlayMode、End-to-End Benchmark 和导出烟测均通过。

确认未把 Addressables、Payload、ProjectSettings 等业务或环境改动混入 SaveKit 提交。
