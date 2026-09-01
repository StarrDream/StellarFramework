# GridKit 源码文档

> 状态：GridKit V1 / Core Semantics Frozen

## Design Goals 与 Foundation 边界

源码优先保证数学正确性、确定性、固定内存布局和小而稳定的 API。GridKit 只描述二维、正交、整数、纯逻辑网格；任何“这个格子为什么能放建筑”“Chunk 何时加载”“NPC 如何寻路”的业务语义都必须留在后续 Kit 或业务项目。

Core 没有隐藏初始化、Shutdown、Update、Driver、Singleton、Manager 或 Global Registry。构造 `new DenseGrid<T>(bounds)` 即可使用。

## 目录与程序集

`Assets/StellarFramework/Runtime/Kits/GridKit` 是唯一 Runtime 根目录，`StellarFramework.GridKit.Core.asmdef` 没有程序集引用并设置 `noEngineReferences=true`。源码按 `Geometry`、`Storage`、`Footprints`、`Occupancy` 分组；目录划分只服务框架开发工程，导出器会将该目录作为一个 Kit 闭包导出。

Core 源码不能出现 `using UnityEngine`、Unity 生命周期、LogKit/EventKit/PoolKit/SingletonKit/TimeKit/SaveKit/ResKit、UniTask、Newtonsoft、Unity.Collections、Burst、Jobs、Addressables 或 HybridCLR 引用。

## 几何不变量

- `GridCoord` 和 `GridOffset` 的每个分量都是 `int`；加减使用 `long` 中间值并在不能表示时抛 `OverflowException`。
- `GridSize.Width/Height >= 0`，面积以 `long` 计算；`GridRect` 可以是空矩形。
- `GridRect` 的上界不直接用 `int` 相加，而通过 `long MaxExclusiveX/Y` 保留 `int.MaxValue + 1` 的边界表达。
- `Contains` 遵循 `[Min, MaxExclusive)`；交集和重叠对空矩形保持稳定语义。
- `GridRect.Enumerator` 使用结构体和 long 游标，遍历顺序为 Y 升序、每行 X 升序，不在 foreach 中创建托管枚举器。

`GridMath.FloorDiv/FloorMod` 为正除数提供欧几里得语义：`a = FloorDiv(a,b) * b + FloorMod(a,b)` 且余数在 `[0,b)`。`TryOffset` 将坐标与 offset 的加法限制在 Int32，`OffsetChecked` 在失败时抛异常。

## 稳定排序协议

以下顺序是公开协议，不能在不改版本的情况下调整：`GridRect` 枚举为 Y ascending、每行 X ascending；DenseGrid 为 row-major；4 邻居为 N/E/S/W；8 邻居为 N/NE/E/SE/S/SW/W/NW；Footprint canonical 顺序为 Y ascending、X ascending。Dictionary/hash 遍历不属于确定性协议。

## DenseGrid 实现

构造时固定 `_bounds` 和一段 row-major `T[]`。面积大于 `Int32.MaxValue` 直接拒绝；零宽或零高允许并分配空数组。`TryGetIndex` 先做半开区间检查，再以 long 计算 local 坐标和 index，保证负原点与极端上界不回绕。

`AsSpan`、`AsReadOnlySpan`、`GetRef`、`GetRefReadOnly` 和 `GetRefByIndex` 暴露连续存储的明确访问路径；`CopyFrom/CopyTo` 要求长度严格等于 `Count`。容器不支持 Resize、事件、Dirty 标记或隐式线程安全。

## Footprint 实现

`GridFootprint` 构造时枚举输入到私有数组，canonical 排序后复制为只读视图，并计算 RelativeBounds。数组永远不从公开 API 写回；`GetOffset` 和 `AsReadOnlySpan` 只读。`GridTransform.TryApply` 先做反射，再按旋转公式计算，并以 long 检查结果是否能回到 int。

占用层不使用 stackalloc 保存不受信任大小的 Footprint；它逐项执行 `TryApply` + `GridMath.TryOffset`，先验证全量坐标，再提交写入。这使得大形状仍有确定的内存上界（仅已有连续网格），并保持失败零变更。

## Occupancy 两遍算法

1. 检查 owner 是否为正数；非法输入返回 `InvalidOccupant`。
2. 按 Footprint canonical 顺序计算所有世界格；任一变换溢出或不在 Bounds 返回 `OutOfBounds`。
3. `TryOccupy` 要求每一格都是 Empty；`CanOccupy(..., allowedExistingOccupant)` 仅在 Preview 路径允许指定的 self owner，其他已有 owner 返回 `Occupied`，并附 `ConflictCoord` / `ExistingOccupant`。
4. 全部通过后第二遍通过 `DenseGrid.GetRef` 写入 owner。

释放采用相同的两遍结构：第一遍确认所有格子存在且 owner 完全匹配，第二遍清零；不匹配返回 `NotOwned` 且不修改。算法默认非线程安全，外层若需要并发必须提供同步。

## Ownership 状态机与 API 分离

GridKit V1 的合法 Occupancy 状态机只有：

```text
Empty
  │
  │ TryOccupy(A)
  ▼
 A
  │
  │ TryRelease(A)
  ▼
Empty
```

`TryOccupy` 的验证路径是 Empty-only：

```text
validate occupant

for each canonical target:
    transform
    bounds check
    if cell != Empty:
        return Occupied

// 到这里为止没有修改
for each canonical target:
    cell = occupant

return Success
```

普通 `TryOccupy` 不接受 `allowedExistingOccupant`，因此不会把已有 owner 当作成功，也不会覆盖任何 owner。V1 RC 已删除 write-side ignore-owner overload；`TryOccupy` 不能承担 Move、Replace、Refresh、幂等重试或 Ownership Transfer。

Preview 使用单独的只读路径：

```text
for each canonical target:
    transform
    bounds check
    if Empty:
        continue
    if Existing == allowedExisting:
        continue
    return Occupied

return Success
// never commit
```

因此 `CanOccupy(..., allowedExistingOccupant)` 只能忽略 Preview 请求中指定的 self owner，遇到其他 owner 仍失败，且任何结果都不改变 Occupancy。不存在普通 `OwnerA → OwnerB` mutation；未来 Relocate 或显式 Transfer 必须由 PlacementKit 另行定义。

## 公开类型关系

```text
GridCoord / GridOffset / GridSize / GridRect / GridMath / GridDistance
                         ↓
                IReadOnlyGrid<T> / IGrid<T>
                         ↓
                    DenseGrid<T>

GridOffset → GridTransform → GridFootprint
GridFootprint + GridTransform + GridRect → GridOccupancy
```

通用算法接受 `IReadOnlyGrid<T>` / `IGrid<T>`；性能敏感的批处理可明确依赖 `DenseGrid<T>.AsSpan()` 或 ref API，避免接口虚调用。

## 测试与基准

`Tests/EditMode/FrameworkValidation/Kits/GridKit/GridKitTests.cs` 覆盖负坐标、边界溢出、FloorDiv/FloorMod、半开矩形、枚举顺序、DenseGrid Span/ref、邻居顺序、Footprint canonical/变换、Occupancy 冲突与原子失败。

`GridKitBenchmarkTests` 在 Unity Editor 中执行：1000×1000 DenseGrid（填充、线性 Span 读写、坐标索引读写）、1M Rect/坐标↔index 往返，以及 100k `CanOccupy`、`TryOccupy`/`TryRelease`。输出 Unity 版本、各段 elapsed milliseconds、校验和和 `GC.GetTotalMemory` 变化；没有机器相关的固定性能门槛。

## 复杂度与分配特征

| 操作 | 复杂度 | Warm-up 后目标 |
| --- | --- | --- |
| `Contains` / Coord↔Index / Dense Get/Set | O(1) | 0 managed allocation |
| Neighbor4/8 | O(1) | 0 managed allocation |
| Rect foreach | O(visited cells) | 结构体枚举，无 GC |
| Footprint transform/write | O(footprint cells) | caller-owned buffer |
| `CanOccupy` / `TryOccupy` / `TryRelease` | O(footprint cells) | 不创建临时 Cell 数组 |
| Idle | O(1) | 无 CPU、无 GC |

百万格 DenseGrid 的额外开销只有 `T[]`：例如 `byte` 约 1 MB、`int` 或 OccupantId 约 4 MB（不含业务对象）。大型 Cell 推荐使用紧凑 struct；Core 不增加 `unmanaged` 约束。

## AOT / IL2CPP 与 ToolsHub 边界

Core 不使用动态代码、反射或 UnityEditor API，`allowUnsafeCode=false`，可直接进入 Player 和 IL2CPP。泛型实例由业务编译器生成，不需要为 GridKit 添加 link.xml。ToolsHub 若未来增加 `gridkit.tools`，只能调用这些公开 API，不得反射 `_cells`、暴露可写 Occupancy 原始数组或把工具反向编入 Runtime。

## Core Invariants

1. GridKit.Core 只描述 2D Orthogonal Integer Grid。
2. Core 0 Kit / 0 UPM dependency，且无 `UnityEngine` 引用。
3. 坐标 +X 向右、+Y 向上；负坐标合法。
4. Rect 永久为 Min inclusive / Max exclusive，面积使用 long。
5. DenseGrid Bounds 创建后不可变，存储永久 row-major `T[]`。
6. Rect、Neighbor、Footprint 的公开顺序固定且可测试。
7. Footprint immutable、非空、无重复，变换顺序为 ReflectX → ReflectY → Rotation。
8. Occupancy 只保存正整数 owner；TryOccupy/TryRelease 成功全写、失败零修改。
9. Core 不拥有生命周期、全局状态、业务引用、事件或自动扩容。
10. Ownership 只能通过显式合法 mutation 改变：TryOccupy 仅 `Empty → Owner`，TryRelease 仅 `Owner → Empty`；Preview 永不 Commit，不存在隐式 `OwnerA → OwnerB`。

## Failure Matrix

| API | 情况 | 行为 |
| --- | --- | --- |
| `GridSize` ctor | negative width/height | `ArgumentOutOfRangeException` |
| `GridRect` ctor/Translate | 坐标范围溢出 | `ArgumentOutOfRangeException` 或 `OverflowException` |
| `FloorDiv/FloorMod` | divisor <= 0 | `ArgumentOutOfRangeException` |
| Dense indexer / `GetIndex` / `GetCoord` | 越界 | `ArgumentOutOfRangeException` |
| Dense `TryGet` / `TrySet` / `TryGetIndex` / `TryGetCoord` | 越界 | `false` |
| `GridFootprint` ctor | empty / duplicate | `ArgumentException` |
| `TryWriteCells` | buffer too small | `ArgumentException` |
| `TryWriteCells` | Int32 coordinate overflow | `false`, `written=0` |
| `CanOccupy` / `TryOccupy` | out of bounds | `OutOfBounds`，不修改 |
| `TryOccupy` | conflict | `Occupied` + conflict，零修改 |
| `TryRelease` | wrong owner | `NotOwned`，零修改 |

Ownership 的关键边界也固定为：`CanOccupy` 遇 Empty 返回 Success 且不修改；带 `allowedExistingOccupant=A` 时允许已有 A 但不允许 B；`TryOccupy(A)` 遇已有 A 或 B 都返回 `Occupied` 且保持原 owner；`TryRelease(A)` 只有遇到 A 才清零，遇到 B 返回 `NotOwned`。

## Test Matrix

| 区域 | 覆盖 |
| --- | --- |
| Coord/Offset | 负数、相等/hash、加减、溢出、TryOffset |
| Size/Rect | 0/正面积、半开边界、空矩形、交集、平移、极值、顺序 |
| GridMath/Distance | -1000..1000 × 多个正除数、欧几里得恒等式、long 距离 |
| DenseGrid | 负原点、1x1/empty、双向 Round Trip、Span/ref、Fill/Clear/Copy、越界 |
| Neighbor | 4/8 顺序、边角过滤、Int32 极值、容量 |
| Footprint | immutable、canonical、duplicate/empty、旋转反射、写入缓冲、溢出 |
| Occupancy | 单/多格、冲突、越界、错误 owner、Clear、旋转反射、原子零修改、same-owner 重复失败、cross-owner takeover 防护、Preview self-overlap/只读/他人冲突 |

## Benchmark Matrix

Editor Benchmark 使用 1000×1000（1,000,000）DenseGrid 执行 Fill、Span 线性读写和坐标读写；执行 1M Rect foreach 与 Coord↔Index round-trip；执行 100,000 次 `CanOccupy` 及 `TryOccupy`/`TryRelease`。报告 elapsed、分配变化、Unity 版本和校验和，不设置固定毫秒门槛；目标平台性能由发布项目单独测量。

## 边界与演进

Pathfinding、Sparse/Chunk、Hex、3D、Tilemap、Placement、Save 和 Event 都必须在独立 Kit 中设计，不得为了“方便”写进 Core。若未来增加 `GridKit.Tools`，它应作为独立 ToolsHub 子模块和独立导出 Profile，不能反向引用 Runtime Core 以外的 Kit。当前 V1 只交付零依赖 Core 与可选样例。
