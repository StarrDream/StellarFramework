# SpatialKit 空间索引说明

SpatialKit 是 `foundation / world` 层的连续二维点索引。它回答一个很窄的问题：在一组动态登记的二维点中，哪些 `SpatialId` 落在矩形、圆形或最近邻半径内。

## 适用范围

- 连续二维点（`float X/Y`），支持负数、小数和有限的极端值。
- 外部业务分配 `SpatialId`；`0` 是无效 ID，SpatialKit 不生成 ID，也不保存业务对象。
- 动态 `Insert / Remove / UpdatePosition`。
- 半开矩形 `[Min, MaxExclusive)`、闭圆 `distance <= radius` 和有限半径最近邻。

SpatialKit 不等同于 GridKit：GridKit 负责整数网格、格子几何和 Occupancy；SpatialKit 负责连续点的空间候选筛选。它不包含 3D、Octree/Voxel、AABB 体积、Transform 跟踪、MonoBehaviour、Update、寻路、模拟、放置规则、Layer/Tag、Save/Event/Pool 或 ToolsHub。

## 最小用法

```csharp
var index = new SpatialIndex2D(bucketSize: 8f, initialCapacity: 256);
SpatialId enemy = new SpatialId(1001);
index.TryInsert(enemy, new SpatialPoint(-3.5f, 2.25f));

SpatialId[] buffer = new SpatialId[64];
SpatialQueryResult result = index.QueryCircle(
    new SpatialPoint(0f, 0f), 10f, buffer);

if (index.TryFindNearest(new SpatialPoint(0f, 0f), 20f, out SpatialId nearest))
{
    // 使用 nearest 交给业务层读取对象或执行行为。
}
```

查询不会返回 `IEnumerable`，也不会自动扩容。`WrittenCount` 是实际写入调用方 Span 的数量，`MatchCount` 是完整匹配数量；缓冲区不足时继续扫描并令 `IsTruncated == true`。使用 `Span<SpatialId>.Empty` 可以只统计匹配数。

## API 与失败语义

| API | 成功 | 失败/异常 |
| --- | --- | --- |
| `SpatialId(int)` | `>0` 可用；`0` 为无效 | 负数 `ArgumentOutOfRangeException` |
| `SpatialPoint(float,float)` | 所有坐标 finite | NaN/±Infinity `ArgumentOutOfRangeException` |
| `SpatialRect(...)` | finite 且 Max≥Min | NaN/Infinity 或 Max<Min 抛异常；零宽/零高是 Empty |
| `SpatialIndex2D(bucketSize, initialCapacity)` | bucketSize finite 且 >0，capacity≥0 | 不合法参数 `ArgumentOutOfRangeException` |
| `TryInsert` | `Success` | 无效 ID、重复 ID、桶坐标超出 Int32 分别返回 `InvalidId`、`DuplicateId`、`PositionOutOfRange` |
| `TryRemove` | `Success` | 无效 ID 或不存在返回 `InvalidId` / `NotFound` |
| `TryUpdatePosition` | `Success` | 无效、缺失或新桶超范围返回 `InvalidId` / `NotFound` / `PositionOutOfRange` |
| `QueryCircle` / `TryFindNearest` | 有效半径（含 0） | 负数、NaN、Infinity 或查询桶范围超出 Int32 抛 `ArgumentOutOfRangeException` |

所有写操作先完成验证；失败不会改变 Count、旧坐标、旧桶链和可观察查询结果。更新跨桶时先验证新桶，再摘链/挂链。同桶更新只改坐标。

## 边界和精度

- 桶坐标是 `floor(position / BucketSize)`，例如 BucketSize=10 时 `-0.1 → -1`、`-10 → -1`、`-10.1 → -2`。
- Rect 使用 `[Min, MaxExclusive)`，最大边界不匹配；空 Rect 直接返回 `0/0/false`。
- Circle 使用 double 计算 `dx/dy/distanceSquared`，边界 `distanceSquared <= radiusSquared`；半径 0 只匹配完全相同坐标。
- 最近邻只支持有限 `maxRadius`，同距离选择数值更小的 `SpatialId.Value`；无 KNN、无无限半径 Global Nearest。
- 查询顺序不属于 Contract，业务必须按 ID 或自己的稳定键排序。

## 依赖与导出

`StellarFramework.SpatialKit.Core.asmdef` 没有程序集引用并设置 `noEngineReferences=true`。SpatialKit Core 不需要 Architecture、GridKit、ResKit、Addressables、HybridCLR、UniTask 或其他 UPM。

在框架原始工程中打开 `StellarFramework -> Framework Source -> Kit Package Exporter`，选择 `SpatialKit` 可导出 `StellarFramework-SpatialKit.unitypackage`；选择 `Sample.SpatialKit` 可导出样例、Common 和 `SpatialKit_Playable.unity`。样例包依赖 SpatialKit，但不携带 Addressables、HybridCLR 或代码热更。

## 运行样例与验收

打开 `Assets/StellarFramework/Samples/KitSamples/Scenes/SpatialKit_Playable.unity`。面板会显示 BucketSize、Count、Selected ID/Position、查询写入/匹配数、Truncated 和最近邻 ID，可执行 Reset、Insert、Move Selected、Remove Selected、Query Rect、Query Circle、Nearest 及排除 Selected 的最近邻。

样例中的圆形可视区域与 `QueryCircle` 使用同一 Center / Radius；只有欧氏距离 `<= Radius` 的点会匹配并高亮。黄色圆线外接方框的角点不会因为落在方框内而匹配。

Behavior 测试位于 `Tests/EditMode/FrameworkValidation/Kits/SpatialKit/SpatialKitTests.cs`，覆盖构造、负坐标 floor、失败原子性、查询边界/截断、半径校验、最近邻 tie-break、Clear 和极端查询范围。性能趋势位于 `Tests/EditMode/FrameworkValidation/Performance/SpatialKit/SpatialKitBenchmarkTests.cs`，记录 100k 动态操作和 1M 存储压力，不设置固定毫秒门槛。

## V1 非目标与后续

V1 不承诺 3D、体积实体、层/标签过滤、Transform 自动同步、线程安全、Jobs/Burst/Native、KNN、排序查询、持久化或 Unity 组件适配。若真实项目需要这些能力，应在独立 Adapter/Extension 中组合公开 API，并保持 Core 的纯 C# 边界。
