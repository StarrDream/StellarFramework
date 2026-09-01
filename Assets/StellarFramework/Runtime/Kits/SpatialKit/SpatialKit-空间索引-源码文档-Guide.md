# SpatialKit 源码文档

> 状态：SpatialKit V1 Release Candidate；Core Semantics Frozen：NO（待真实项目与导出闭环继续审查）。

## 1. 目录和程序集

`Assets/StellarFramework/Runtime/Kits/SpatialKit` 是 SpatialKit 唯一 Runtime 根目录：

```text
Geometry/   SpatialPoint、SpatialRect
Identity/   SpatialId
Index/      SpatialIndex2D、Mutation/Query Result、MutationError
Internal/   SpatialBucketCoord、SpatialEntrySlot
```

`StellarFramework.SpatialKit.Core.asmdef` 的 `references` 为空、`noEngineReferences=true`、`allowUnsafeCode=false`。源码不引用 UnityEngine、Architecture、GridKit、任何 Kit、UPM、反射、动态代码或 Unity 生命周期。

## 2. 核心不变量

1. `SpatialId.Value == 0` 无效；正数由调用方分配；负数构造直接抛异常。
2. `SpatialPoint` 与 `SpatialRect` 只接受 finite 浮点值；`default(SpatialPoint)` 是合法原点。
3. 每个活动 Entry 恰好存在于一个 `SpatialBucketCoord` 的侵入式双向链表中。
4. `_idToSlot` 是 ID 到槽位的唯一查找表；Count 等于活动 Entry 数量。
5. 写操作的所有可预见失败都在逻辑修改前返回；更新越界保留旧位置/旧桶。
6. Rect/Circle 查询每个桶只扫描一次，再进行精确几何过滤，因此不会返回重复 ID。
7. 查询使用调用方 Span；空间不足只截断写入，不截断 `MatchCount` 扫描。
8. 类型不承诺线程安全、顺序或业务对象生命周期。

## 3. 存储算法

`SpatialIndex2D` 使用两个 `Dictionary` 和一个槽位数组：

- `Dictionary<SpatialId,int>`：O(1) 期望的 Contains、位置读取和 ID 定位。
- `Dictionary<SpatialBucketCoord,int>`：桶头索引；桶为空时删除字典项。
- `SpatialEntrySlot[]`：保存 ID、坐标、桶坐标、`Previous/Next` 链接和 Free List 链接。
- `_nextSlot` 记录高水位；删除的槽位进入 `_freeHead`，容量不足时数组按倍增扩容并做溢出保护。

插入先校验 ID、桶坐标和重复，再取得槽位、加入 ID 表并挂入桶头。移除摘除链表、删除 ID、回收槽位并递减 Count。移动先计算新桶；同桶只写 Position，跨桶执行一次摘链和挂链。`Clear` 清空两个字典并将高水位归零，保留已分配数组/字典容量。

## 4. 桶坐标与查询

桶坐标由 `Math.Floor((double)coordinate / bucketSize)` 计算，先检查 finite 结果是否位于 Int32。负坐标不能使用 C# 整数截断代替 floor。Rect/Circle/Nearest 在进入嵌套桶循环前验证四个边界；超出 Int32 BucketCoord 直接抛 `ArgumentOutOfRangeException`，避免 `int.MaxValue++` 回绕或巨型循环挂死。

Rect 查询扫描矩形涉及的桶并调用 `SpatialRect.Contains` 做 `[Min, Max)` 精确过滤。Circle 和 Nearest 扫描中心±半径的桶，用 double 距离平方做闭边界判断。Nearest 保留最小距离；距离完全相等时比较 `SpatialId.Value`，避免依赖链表插入顺序。

## 5. 复杂度与容量

期望的 `TryInsert/TryRemove/TryUpdatePosition/Contains/TryGetPosition` 为 O(1)。查询复杂度为相关桶数量加候选数量（O(B+C)）；桶过大或点高度聚集时会退化，应按数据密度选择 BucketSize。查询不分配临时 List/HashSet/数组；Core 热路径没有 LINQ、yield、delegate、callback 或对象查找。Dictionary/数组增长仍可能分配，生产批量注册时建议提供合理 `initialCapacity`。

## 6. 验证 Contract

- `SpatialKitTests` 覆盖 ID/几何构造、数学 floor、写操作原子性、Rect/Circle 边界、Span 截断和只读、Nearest tie/exclude、Clear、槽位复用以及极端查询范围保护。
- `SpatialKitBenchmarkTests` 记录 100,000 条 Insert/Lookup/同桶移动/跨桶移动、局部 Rect/Circle/Nearest、Remove/Clear；另有 1,000,000 条存储压力、抽样查找、部分移动和 Clear。基准使用复用缓冲区和校验和，`GC.GetTotalMemory(false)` 只作为粗略堆趋势，不是严格零分配证明。
- Core 为纯 C# 数据结构，不要求 PlayMode 或 Integration Scene；Playable Sample 只验证 Unity 场景挂载和公开操作。

## 7. 导出边界

Core Profile 的 source path 只有 `Assets/StellarFramework/Runtime/Kits/SpatialKit`，导出闭包不应包含其他 Kit、样例、Tests、Verification 或 UPM。`samples.spatialkit` 额外包含 Example_SpatialKit、Common 和生成的 `SpatialKit_Playable.unity`，不包含 Verification、Addressables、HybridCLR 或热更产物。

## 8. 后续候选

在真实项目出现稳定需求后，再评估 SpatialKit.UnityAdapter（Transform/MonoBehaviour 同步）、3D/体积专用 Kit、Jobs/Burst Adapter 或过滤索引。任何候选都不能把对象引用、线程调度、业务分类或 Unity 生命周期倒灌进 Core。
