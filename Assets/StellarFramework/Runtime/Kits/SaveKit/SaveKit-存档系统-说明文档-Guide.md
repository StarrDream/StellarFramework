# SaveKit / 存档系统使用指南

## 1. SaveKit 是什么

SaveKit 是 StellarFramework 的 Foundation / data Kit。

它把一个存档拆成多个稳定 ID 的业务 Section。

Core 负责容器格式、版本、校验、事务、备份、迁移和恢复顺序。

业务代码负责 Capture、Validate、Restore 和 DTO。

SaveKit 不知道玩家、地图、作物、背包或任务的含义。

这条边界让存档格式可以长期演进。

## 2. 适用范围

适合保存：

- 玩家进度、货币、等级和解锁状态。
- 世界状态、任务状态和设置覆盖。
- 由 ID、数值、时间戳组成的纯数据。
- 需要版本迁移、损坏检测和 Backup 恢复的本地存档。

不适合直接保存：

- GameObject、Transform、Component 或场景引用。
- MonoBehaviour、ScriptableObject 实例图。
- Delegate、TimerHandle、协程、线程或 Native 容器句柄。
- 需要云端冲突解决、加密签名或无限历史的系统。

这些内容应由业务层转换成可持久化 DTO，或由后续 Adapter 处理。

## 3. 在 StellarFramework 中的位置

SaveKit.Core 是 Foundation / data，依赖 LogKit 和 UniTask。

它不依赖 TimeKit、WorldKit、InventoryKit、ResKit、Addressables、HybridCLR 或 Newtonsoft。

可选 Profile：

| Profile | 作用 | 是否必须 |
| --- | --- | --- |
| `savekit.core` | 容器、Section、Storage、Serializer、Migration | 必须 |
| `savekit.newtonsoft-json` | Newtonsoft JSON Adapter | 需要 JSON 灵活性时 |
| `savekit.tools` | ToolsHub 存档中心 | 仅开发环境 |
| `samples.savekit` | 可运行示例 | 可选 |

导出器会根据 Catalog 计算依赖闭包。

导入说明会列出随包的 Kit 和需要通过 UPM 安装的外部包。

## 4. 导入 Profile

在框架开发工程中打开 `Kit Package Exporter`。

选择 `SaveKit.Core` 时会包含 LogKit 和 Core 源码。

选择 `SaveKit.NewtonsoftJson` 时会自动带上 Core Profile。

选择 `SaveKit.Tools` 时会带上 Core 和 ToolsHub.Core。

选择样例时会带上样例所需的 Core 闭包。

Core 导出不包含 Adapter、Samples、业务项目和热更插件。

导入业务工程后等待 Unity 完成脚本编译，再注册 Section。

## 5. 30 秒快速开始

下面示例使用当前真实 API。

```csharp
using StellarFramework;
using Cysharp.Threading.Tasks;

[System.Serializable]
public sealed class PlayerSaveData
{
    public long Money;
    public int Level;
}

public sealed class PlayerSaveSection : SaveSection<PlayerSaveData>
{
    public override SaveSectionId Id => SaveSectionId.From("player");
    public long Money;
    public int Level;

    public override PlayerSaveData Capture(SaveCaptureContext context)
    {
        return new PlayerSaveData { Money = Money, Level = Level };
    }

    public override void Restore(PlayerSaveData data, SaveRestoreContext context)
    {
        Money = data == null ? 0 : data.Money;
        Level = data == null ? 0 : data.Level;
    }
}

public async UniTask SavePlayerAsync()
{
    if (!SaveKit.IsInitialized) SaveKit.Initialize();
    SaveKit.Register(new PlayerSaveSection());
    SaveResult result = await SaveKit.SaveAsync("slot-01");
    if (!result.IsSuccess) UnityEngine.Debug.LogError(result.ErrorMessage);
}

public async UniTask LoadPlayerAsync()
{
    SaveResult result = await SaveKit.LoadAsync("slot-01");
    if (!result.IsSuccess) UnityEngine.Debug.LogError(result.ErrorMessage);
}
```

正式项目应在启动阶段初始化和注册一次，不要每次按钮点击都注册 Section。

## 6. Initialize 生命周期

`SaveKit.Initialize()` 创建新的 Coordinator、Registry 和默认 FileSystem Storage。

默认目录是 `Application.persistentDataPath/Saves`。

默认注册 `UnityJsonSaveSerializer` 和 `RawBytesSaveSerializer`。

第一次业务启动时调用 Initialize。

可使用 Builder 选择 Storage、默认 Serializer 和生命周期钩子。

```csharp
SaveKit.Initialize(builder => builder
    .UseStorage(new FileSystemSaveStorage())
    .SetApplicationVersion("1.4.0")
    .Configure(options =>
    {
        options.MissingSectionPolicy = MissingSectionPolicy.UseDefault;
        options.UnknownSectionPolicy = UnknownSectionPolicy.Preserve;
    }));
```

重复 Initialize 会替换当前静态状态，原 Registry 不会自动合并。

因此重复初始化只用于切换配置、测试隔离或编辑器临时检查。

运行中的 Save/Load 不应同时重新 Initialize。

运行中可以注册新 Section，但应在 Operation Gate 空闲且下一次操作前完成。

V1 没有公开 Shutdown；需要隔离测试时重新 Initialize 新 Storage。

框架内部测试可使用内部 ResetForTests，不应在业务工程依赖该 API。

## 7. SaveSlot

Slot ID 是逻辑 ID，不是文件路径。

允许字母、数字、下划线、短横线和点，最大长度 64。

`../slot`、路径分隔符和空白 ID 会被拒绝。

Storage 决定物理目录和文件名。

FileSystem Storage 使用：

| 文件 | 含义 |
| --- | --- |
| `slot.sav` | 当前有效存档 |
| `slot.bak` | 上一个可恢复存档 |
| `slot.tmp` | 当前写入事务的临时文件 |

`SaveSlotInfo.Metadata` 包含 SlotId、Revision、时间戳和自定义元数据。

Revision 从 1 开始，每次成功 Commit 后递增。

CreatedUtc 只在首个成功保存时建立。

UpdatedUtc 在每次成功保存时更新。

`GetSlotsAsync` 是元数据索引操作，不会把每个 Payload 交给业务反序列化。

FileSystem Storage 使用 metadata-only Reader 检查结构，Payload Checksum 在 Inspector 或 Load 时完整验证。

## 8. SaveSection<TData>

一个 Section 表示一个业务 Domain 的持久化边界。

常用成员：

| 成员 | 作用 |
| --- | --- |
| `Id` | 稳定的 Section ID |
| `SchemaVersion` | 该 Domain 的 DTO 版本 |
| `SerializerId` | 当前 Section 使用的 Serializer |
| `MissingPolicy` | 当前版本缺少该 Section 时的策略 |
| `RestoreAfter` | Restore DAG 的依赖 |
| `Capture` | 主线程采集纯 DTO |
| `Validate` | 写入前和 Load Prepare 阶段校验 |
| `Restore` | Apply 阶段把 DTO 应用到运行时模型 |

正确模型是每个业务 Domain 一个 Section。

错误模型是每个 GameObject 挂一个 Save Component。

Domain Section 可以批量保存、统一迁移和统一校验。

100000 个作物应是一个 Crop Section 内的数组或紧凑数据块。

100000 个 Section 会放大目录、字符串、GC 和 Restore 调度开销。

## 9. Capture

Capture 在 Unity 主线程执行。

Capture 只读取运行时 Model，不应修改运行时状态。

建议返回稳定的 class DTO、struct 数组或业务专用缓冲区。

不要在 Capture 中访问存档文件、发送网络请求或调用 Restore。

不要把 GameObject 引用放进 DTO。

## 10. Validate

当 `ValidateAfterCapture` 开启时，Save 会在序列化前校验每个 Section。

Load Prepare 会在 Deserialize 和 Migration 之后再次校验当前 DTO。

Validate 失败会阻止 Commit，也会阻止任何 Restore。

```csharp
public override SaveValidationResult Validate(PlayerSaveData data, SaveValidationContext context)
{
    if (data == null) return SaveValidationResult.Invalid("NullData", "玩家数据为空");
    if (data.Money < 0) return SaveValidationResult.Invalid("Money", "货币不能为负数");
    if (data.Level < 1) return SaveValidationResult.Invalid("Level", "等级必须大于零");
    return SaveValidationResult.Valid();
}
```

## 11. Restore

Restore 只在所有已知 Section Prepare 成功后开始。

Prepare 失败时不会调用任何 Section 的 Restore。

Restore 是 Apply 阶段，业务应保持确定性并避免抛出异常。

Restore 中不要发网络请求、加载未知资源或依赖尚未恢复的 Domain。

需要资源时保存 Asset Business ID，Restore 后由业务协调 ResKit 加载。

## 12. RestoreAfter

`RestoreAfter` 表示当前 Section 依赖哪些 Section 已经 Restore。

内部把 Section 视为节点，把 `RestoreAfter` 视为有向边。

Coordinator 使用拓扑排序生成 Restore 顺序。

相同入度节点按 Section ID 的稳定顺序处理，保证 deterministic。

未知依赖 ID 不会制造错误边；当前已注册节点之间的循环会失败。

```csharp
public override IReadOnlyList<SaveSectionId> RestoreAfter =>
    new[] { SaveSectionId.From("world") };
```

## 13. DTO 规范

持久化 DTO 应表达业务事实，而不是运行时对象图。

坏示例：

```csharp
public GameObject CropObject;
public Transform Transform;
public CropView View;
public ScriptableObject Config;
```

好示例：

```csharp
public struct CropSaveRecord
{
    public int CropTypeId;
    public int CellX;
    public int CellY;
    public long PlantTick;
    public byte Stage;
}
```

核心原则是 `Runtime Object != Persistent Data`。

DTO 中保存稳定 ID、数值、枚举、时间戳和有限长度字符串。

不要保存 Delegate、TimerHandle、NativeArray、Task 或闭包。

## 14. Serializer 选择

| Serializer | 优点 | 缺点 | 推荐场景 |
| --- | --- | --- | --- |
| UnityJson | Core 自带、轻量、无需额外包 | 能力和性能有限 | 小中型普通 DTO |
| Newtonsoft | 灵活、可读、类型支持广 | 体积和 CPU 较高 | 中型业务、调试友好 |
| RawBytes | 直接、可控 | 业务自己维护格式 | 已有二进制格式 |
| Custom Binary | 紧凑、高性能、可流式 | 维护成本高 | 10 万以上 Records |

UnityJson 是 Core 的开箱即用方案，不是 10 万或 100 万 Record Section 的默认高性能方案。

## 15. UnityJson

UnityJson Serializer 的 ID 是 `unity-json`。

它基于 Unity JsonUtility，适合公开字段组成的简单 DTO。

它对复杂字典、多态图和特殊构造函数的支持有限。

它声明 `SaveSerializerCapabilities.None`，不会被 Coordinator 放到后台线程。

这样可避免 Unity API 路径和 Unity-safe 约束被误用。

## 16. Newtonsoft

导入 `SaveKit.NewtonsoftJson` 后可使用：

```csharp
using StellarFramework.SaveKitAdapters.NewtonsoftJson;

SaveKit.Initialize(builder => builder
    .UseSerializer(new NewtonsoftJsonSaveSerializer())
    .SetDefaultSerializer("newtonsoft-json"));
```

Adapter 固定 `TypeNameHandling.None`、`MetadataPropertyHandling.Ignore` 和 `MaxDepth = 64`。

外部 JSON 不会通过 `$type` 动态实例化任意 CLR 类型。

Newtonsoft Adapter 声明可后台执行且线程安全。

Player 构建在支持线程的平台上会使用 Worker；Editor 和 WebGL 安全退化到调用线程。

## 17. 自定义 Serializer

实现 `ISaveSerializer`：

```csharp
public sealed class MySerializer : ISaveSerializer
{
    public string Id => "my-format";
    public UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken token) { ... }
    public UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken token) { ... }
}
```

通过 `UseSerializer` 或初始化后 `SaveKit.RegisterSerializer` 注册。

自定义 Serializer 默认被视为 Unity-safe，不会自动后台调度。

需要后台执行时额外实现 `ISaveSerializerCapabilities`。

同时声明 `BackgroundExecution` 和 `ThreadSafe`，才会进入 Worker。

只有真的不访问 Unity API、不共享可变状态时才可声明这些能力。

## 18. Storage

`ISaveStorage` 抽象了路径、读取、临时写入、Commit、Backup 恢复和删除。

业务可以实现内存、沙盒、加密文件或云端 Storage Adapter。

Storage 必须遵守：

- WriteTemporary 失败不能替换 current。
- Commit 前取消不能破坏 current。
- OpenRead 返回可读流并尊重取消。
- RestoreBackup 不应覆盖唯一的已验证 Backup。

## 19. FileSystem Storage

默认 `FileSystemSaveStorage` 使用 `Application.persistentDataPath/Saves`。

文件名由经过验证的 SlotId 和固定后缀拼接。

SlotId 不允许路径分隔符，因此不会从 Storage 根目录逃逸。

WriteTemporary 使用 `FileOptions.WriteThrough`、Flush 和取消令牌。

当前平台支持时使用 `File.Replace`，否则使用安全的回退 Commit。

Backup 恢复会把已验证 Backup 复制到 current，并保留 Backup 供再次恢复。

## 20. Save 流程

```text
SaveAsync
↓
Operation Gate
↓
读取并完整验证旧 current
↓
BeforeCapture
↓
Capture（主线程）
↓
AfterCapture
↓
Serialize（按能力决定线程）
↓
Checksum
↓
Write .tmp
↓
重新读取并 Verify .tmp
↓
Commit（关键阶段使用不可取消令牌）
↓
current -> backup，tmp -> current
↓
Revision++ / Result
```

如果旧 current 存在但校验失败，Save 会拒绝覆盖，避免把坏档旋转成 Backup。

## 21. Load 流程

```text
LoadAsync
↓
Operation Gate
↓
读取 current
↓
完整验证 current
├─ 失败 → 读取 backup
↓
读取 descriptors
↓
Prepare Every Section
    ├─ checksum
    ├─ stored serializer
    ├─ migration chain
    ├─ deserialize stored type
    ├─ migrate
    └─ validate current DTO
↓
全部成功
↓
RestoreAfter DAG
↓
BeforeRestore
↓
Restore（主线程）
↓
AfterRestore
↓
Result
```

所有 Section 都 Prepare 成功后才会进入 Apply。

## 22. Backup / Recovery

假设 current 是 Revision 10，保存 Revision 11：

```text
tmp = Revision 11
验证 tmp
current Revision 10 -> backup
tmp Revision 11 -> current
```

下一次成功保存时，Backup 只保留上一份 current。

它不是无限历史版本系统。

Load current 损坏时会尝试 Backup。

Backup 也损坏时返回明确失败，不会调用 Restore。

成功从 Backup 加载且 `AutoRecoverBackup` 开启时，会把已验证 Backup 恢复为 current。

SaveKit 保证未验证的新 Save 不替换 current。

SaveKit 保证临时写入失败不破坏 current。

SaveKit 保证 Commit 前取消不破坏 current。

SaveKit 不保证所有平台都有严格 atomic replace。

SaveKit 不保证硬盘物理损坏、突然断电和 OS flush 永远不丢数据。

业务 Restore 失败也不会自动回滚已经应用的业务字段。

## 23. Migration

Section SchemaVersion 独立于 ApplicationVersion。

Game Version 是发布版本；Player Schema、Crop Schema 和 NPC Schema 是数据版本。

迁移依据是每个 Section 的 SchemaVersion。

实现 `SaveMigration<TFrom, TTo>`：

```csharp
public sealed class PlayerV1ToV2 : SaveMigration<PlayerDataV1, PlayerDataV2>
{
    public override int FromVersion => 1;
    public override int ToVersion => 2;

    public override PlayerDataV2 Migrate(PlayerDataV1 data, SaveMigrationContext context)
    {
        return new PlayerDataV2 { Money = data.Money, Level = 1 };
    }
}
```

注册时提供目标 Section ID：

```csharp
SaveKit.RegisterMigration(SaveSectionId.From("player"), new PlayerV1ToV2());
```

## 24. 不同 DTO 类型 Migration

旧 Payload 的反序列化类型由 StoredVersion 和 Migration Chain 决定。

它不是直接使用当前 Section.DataType。

完整流程是：

```text
Read Descriptor
↓
Checksum
↓
比较 Stored / Current SchemaVersion
↓
Stored == Current → Deserialize CurrentType
↓
Stored < Current → Build Migration Chain
↓
Deserialize FirstMigration.FromType
↓
Apply Chain
↓
验证每一步 FromType / ToType
↓
最终类型 == Current Section.DataType
↓
Validate Current DTO
```

例如 V1 是 `PlayerDataV1`，V2 是 `PlayerDataV2`：

```text
V1 Payload
↓
Deserialize(PlayerDataV1)
↓
Migration<PlayerDataV1, PlayerDataV2>
↓
Validate(PlayerDataV2)
↓
Restore(PlayerDataV2)
```

不能先按 V2 读取 V1 Payload 再声称完成迁移。

链中必须满足 `Step1.ToVersion == Step2.FromVersion`。

类型必须满足 `Step1.ToType == Step2.FromType`。

最后一步必须满足 `LastMigration.ToType == section.DataType`。

缺步、类型不连续、迁移抛异常都会在 Restore 前失败。

Stored SerializerId 也必须用于旧 Payload。

因此旧 JSON 可以用旧 Serializer + 旧 DTO 类型读取后再迁移。

## 25. Missing Section

Missing 表示当前注册的 Section 不在旧存档中。

例如旧档没有 weather，新版本新增 weather Section。

`Fail` 适合必需数据。

`UseDefault` 会调用 `CreateDefault`，然后 Validate，再进入 Restore。

`Ignore` 不会把该 Section 放进 prepared 数据。

Section 自身的 `MissingPolicy` 优先于全局默认值。

## 26. Unknown Preserve

Unknown 表示存档中有 Section，但当前工程没有注册对应 Section。

例如旧档有 DLC `dlc_fishing`，当前 DLC 尚未加载。

`Preserve` 会保留原始 Descriptor 和 Payload。

Unknown 不进入 Deserialize、Migration、Validate 或 Restore。

下次 Save 时仍会把它带回新容器，避免数据因 DLC 暂未加载而丢失。

`Ignore` 会放弃未知数据，`Fail` 会直接拒绝 Load。

Unknown Preserve 不是反序列化兼容性保证，Payload 仍必须通过容器完整性检查。

## 27. Cancellation

Save 在 Capture、Serialize、写 tmp 和 Verify 阶段响应取消。

进入关键 Commit 后使用不可取消令牌完成恢复安全的 Commit。

这避免取消发生在 current 已被替换、Backup 尚未建立的中间状态。

Load 在读取、Prepare、Deserialize、Migration 和 Validate 阶段响应取消。

Restore Apply 开始前会最后检查一次取消。

第一个 Restore 开始后不会普通取消，以避免只恢复一半 DAG。

取消返回 `SaveErrorCode.Cancelled`。

## 28. 并发

SaveKit 使用 Operation Gate，而不是排队系统。

同时执行 Save + Save 时，后一个返回 Busy。

同时执行 Save + Load 时，后一个返回 Busy。

同时执行 Load + Delete 时，后一个返回 Busy。

Busy 不代表失败的 current 被修改。

业务应在 UI 层决定重试、排队或禁用按钮。

不要在 Restore 中再次调用 SaveKit 操作。

## 29. Diagnostics

`SaveResult.Diagnostics` 和 `SaveKit.GetDiagnostics()` 提供最近一次操作信息。

包含 Operation、Slot、Revision、Result、阶段耗时、Raw/Final 字节数、BackupUsed 和 MigrationCount。

每个 Section 还记录 Stored/Current Schema、SerializerId、Stored/Current Type、Payload 字节和各阶段耗时。

诊断用于定位问题，不是稳定存档格式的一部分。

## 30. ToolsHub

导入 `SaveKit.Tools` 后，ToolsHub 出现“SaveKit 存档中心”。

Slots 页面提供本地 Slot 扫描、健康状态、检查、加载、Backup 恢复和删除。

外部存档默认只读。

`Open External Save` 不会修改原文件。

`Import Copy` 只复制经过完整校验的文件到一个新 Slot，不覆盖已有 Slot。

Inspector 展示 Container、Revision、时间戳、Section Descriptor、Payload 长度和 Checksum。

Raw Preview 最多显示 64 KiB 文本。

Hex Preview 每页最多显示 256 bytes，带 Offset、Hex 和 ASCII。

Migration 页面展示 Stored/Current Version 和每一步 CLR Type Chain。

`Run Migration Dry Run` 实际执行读取、Checksum、旧类型 Deserialize、Migration 和当前 DTO Validate。

Dry Run 不执行 Restore、不 Save、不修改源文件。

Registry 不可用时显示 Current Schema / Migration Graph Unavailable，但仍可 Verify、Raw、Hex 和查看 Stored Schema。

Diagnostics 页面显示 ErrorCode、Slot、Section、版本、SerializerId、Message 和 Development Exception Type。

默认不会在编辑器文本框中展开超大 Payload，也不提供编辑 Money、修改 JSON 或覆盖原 Save 的能力。

## 31. 玩家坏档排查流程

1. 打开 Tools Hub。
2. 进入 SaveKit 存档中心。
3. 使用 Open External Save。
4. 先执行 Verify / Inspector 检查。
5. 查看 Container Health。
6. 查看每个 Section Checksum。
7. 查看 Stored Schema 和 SerializerId。
8. 查看 Migration Path 和 Type Chain。
9. 必要时执行 Migration Dry Run。
10. 未确认前不要执行 Restore 或 Import Copy。

若 current 损坏而 Backup 健康，Load 会标记 `SuccessWithBackupRecovery`。

若 current 和 Backup 都损坏，应保留原文件并交给离线修复流程。

## 32. 性能指南

性能要分 CPU、GC、Peak Memory 和 Main Thread Stall 观察。

Capture 成本主要来自业务 Model 的读取和 DTO 组装。

Serialize 成本来自 Serializer 的格式编码。

Checksum 是 O(bytes) 的顺序扫描。

IO 成本由 Storage、磁盘和平台沙盒决定。

Migration 是 Load 的一次性升级成本。

建议使用 struct、数组、紧凑整数和可复用缓冲区。

避免每条 Record 创建 object、string、GUID 或 LINQ 临时集合。

## 33. 当前 Peak Memory 模型

当前 V1 ContainerWriter 仍是 MemoryStream 模型。

每个 Section Serialize 先形成完整 `byte[]` Payload。

Unknown Preserve 也暂存完整 raw Payload。

随后所有 Section Payload 会一起写入 Container MemoryStream。

写入 FileSystem Storage 时还会复制一次到 `.tmp` 文件流。

因此大存档峰值可能同时包含 Section byte[]、Container MemoryStream 和 Storage copy buffer。

V1 没有宣称 Stream-first 或 100 MB+ 零拷贝。

Stream-first Direct Write、BoundedReadStream 和目录回填列为 V1.1 P1。

大型存档应优先使用 Custom Binary，并按 Domain 分区控制峰值。

## 34. 10 万 / 100 万 Record

100000 Crop 不等于 100000 Section。

建议一个 Crop Section 内保存 `CropSaveRecord[]`。

真正的风险通常来自 object overhead、string、JSON 膨胀、多份 buffer 和 GC。

当前仓库包含 100000 CropSaveRecord 的 End-to-End Benchmark。

该基准实际调用 Initialize、Register、SaveAsync、LoadAsync。

它记录 Capture、Serialize、Checksum、IO、Commit、Deserialize、Migration、Validation、Restore、文件大小和 GC delta。

基准用于趋势比较，不设所有机器通用的毫秒阈值。

## 35. TimeKit 配合

SaveKit Core 不依赖 TimeKit。

业务可以保存 PlantTick、FinishTick、NextRefreshTick 等数值。

不要保存 TimerHandle、Delegate、Scheduler Heap 或协程状态。

Load Restore 后由业务根据时间戳重建 TimeKit Scheduler。

## 36. ConfigKit / ResKit 配合

存档保存 ConfigId、Asset Business Id 或稳定资源键。

Restore 阶段由业务调用 ConfigKit 或 ResKit 解析这些 ID。

SaveKit 不自动扫描资源、不持有 Addressables Handle。

因此 SaveKit.Core 可以在不导入 ResKit、Addressables 的项目中独立使用。

## 37. 常见错误

### 一个全局 GameSaveData

会让所有 Domain 的迁移、校验和加载耦合在一起。

改为多个有稳定 ID 的 Section。

### 每对象一个 Section

会放大目录、字符串、GC 和 Restore 调度。

改为一个 Domain Section 加紧凑数组。

### 保存 GameObject

GameObject 不是稳定跨版本的数据格式。

保存业务 ID 和数值，Restore 时重建运行时对象。

### 保存 TimerHandle

Handle 属于运行时调度器，读档后可能已经失效。

保存时间戳，重新创建 Timer。

### Restore 中发网络请求

会使 Apply 阶段不可确定并拉长半恢复窗口。

提前准备数据，Restore 只应用已经验证的 DTO。

### 用 ApplicationVersion 迁移

游戏发布版本不等于每个 Domain 的 SchemaVersion。

迁移依据是 Section Descriptor 的 SchemaVersion。

### 旧 DTO 用当前类型直接反序列化

跨 DTO Migration 必须先按 FirstMigration.FromType 读取旧 Payload。

### 认为 SaveAsync 等于全后台

Capture 和 Restore 仍是 Unity-safe 主线程边界。

只有声明能力且平台允许的 Serializer 才会后台执行。

### 认为 Checksum 是防作弊

Checksum 只能说明完整性和损坏检测，不能阻止有意篡改。

### 直接覆盖 `.sav`

必须通过 SaveKit 的 tmp、Verify 和 Commit 流程。

### 外部 Save 自动 Restore

先 Verify 和 Dry Run，确认后再由业务决定是否 Load。

### ToolsHub 渲染 100k 节点

使用 Section 汇总和分页 Raw/Hex，而不是一次性生成百万字符。

## 38. FAQ

### 能自动保存 MonoBehaviour 吗？

不能。业务必须在 Section Capture 中把运行时状态转换成 DTO。

### 可以保存 ScriptableObject 吗？

不建议直接保存实例。保存稳定 ID 和需要的字段，再由业务重新解析。

### 每个对象都要 ISaveSection 吗？

不需要。每个业务 Domain 一个 Section 通常更稳定高效。

### 能和 TimeKit 一起用吗？

可以。保存 Tick 或时间戳，Load 后重建调度器。

### JSON 适合 10 万作物吗？

通常不适合作为默认高性能格式。使用 Custom Binary 或 RawBytes，并测量内存峰值。

### 坏档怎么恢复？

先 Verify current，再验证 Backup。Load 会在 current 失败时尝试 Backup。

### ToolsHub 能修改玩家 Save 吗？

V1 默认只读。Import Copy 只创建副本，不覆盖已有 Slot。

### DTO 改了旧档怎么办？

提高 Section SchemaVersion，注册连续的 Typed Migration，并覆盖旧档测试。

### 为什么旧档不能直接按当前 DTO 反序列化？

字段变化、数值范围和默认值可能在读取阶段就丢失语义。必须先按旧类型解释，再逐步迁移。

## 39. V1 不做什么

SaveKit V1 不包含：

- 加密、签名、反作弊或密钥管理。
- 云存档、Steam Cloud 或冲突合并。
- 压缩、增量包、Journal 或无限历史。
- SQLite、World Chunk DB 或自动保存。
- GameObject 反射保存、SaveField 标记和场景扫描。
- ToolsHub 编辑玩家字段、JSON Tree 和 Save Compare。
- 全文件 Stream-first Direct Write。

需要这些能力时实现明确的 Adapter，不要把领域依赖加入 SaveKit.Core。

## 40. Roadmap

V1 当前冻结 Core Semantics：

- 跨 DTO Typed Migration。
- Container、Checksum、事务和 Backup Recovery。
- Prepare / Apply 两阶段 Restore。
- Unknown Preserve 和 Missing Policy。
- Serializer Capability 的安全退化。
- ToolsHub Verify、Raw、Hex、Type Chain 和 Dry Run。

V1.1 P1 候选：

- Temp Stream Direct Write。
- BoundedReadStream 和流式 Checksum。
- Container Directory Seek 回填。
- 压缩和 Cloud Storage Adapter 的独立设计。

这些功能尚未作为 V1 Core 行为承诺。
