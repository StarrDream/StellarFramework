# GridKit 网格系统说明

GridKit 是一个只负责整数二维网格基础能力的 Foundation Kit。它可以单独导出到业务项目，不依赖 Architecture、LogKit、EventKit、PoolKit、SingletonKit、TimeKit、SaveKit、ResKit、Addressables、HybridCLR、UniTask 或其他 UPM 包。

## 适用范围

- 棋盘、建筑占位、战斗格、地图索引和离散空间数据；
- 需要负坐标、稳定坐标↔数组索引、连续内存和可预测遍历顺序的运行时逻辑；
- 由上层业务决定寻路、地图分块、Tilemap、序列化和渲染接线。

GridKit 不包含 Hex/3D/Chunk/Sparse/Tilemap/Pathfinding/Placement/Save/Event，也不创建全局对象、Manager、Update 驱动器或线程锁。

## 最小使用

```csharp
var bounds = new GridRect(new GridCoord(-6, -4), new GridSize(12, 8));
var cells = new DenseGrid<int>(bounds);
cells[new GridCoord(-6, -4)] = 42;
int index = cells.GetIndex(new GridCoord(-6, -4)); // 0
GridCoord same = cells.GetCoord(index);
```

`GridRect` 使用 Min inclusive / Max exclusive。坐标约定为 +X 向右、+Y 向上；`DenseGrid` 使用 row-major，index = localY × Width + localX，Y 从小到大、每行 X 从小到大。

## 八个核心部件

| 部件 | 用途 |
| --- | --- |
| `GridCoord` | 绝对整数坐标，支持负数 |
| `GridOffset` | 相对位移，与绝对坐标分离 |
| `GridSize` | 非负宽高，`long Area` |
| `GridRect` | 半开区间 Bounds、交集、包含、平移和无 GC 枚举 |
| `GridMath` / `GridDistance` | FloorDiv/FloorMod、溢出安全偏移、Manhattan/Chebyshev |
| `DenseGrid<T>` | 固定 Bounds 的连续 `T[]`，含 Span/ref 访问 |
| `GridFootprint` / `GridTransform` | 不可变形状、canonical 顺序、旋转与反射 |
| `GridOccupancy` | 整数 OccupantId、原子占用/释放、冲突结果 |

邻居 API 为 `GridNeighbors.WriteNeighbors4/8`，由调用方传入 `Span<GridCoord>`；顺序固定为 4 邻居 N/E/S/W，8 邻居 N/NE/E/SE/S/SW/W/NW，越界和 Int32 溢出会被跳过。

## Footprint 变换

构造 `GridFootprint` 时会复制、按 Y 再 X 排序并拒绝空集合与重复 offset。Anchor 不必包含 `(0,0)`。变换顺序固定为 `ReflectX` → `ReflectY` → Rotation；逻辑 XY 顺时针旋转为：

- 0° `(x,y)`；90° `(y,-x)`；180° `(-x,-y)`；270° `(-y,x)`。

使用 `TryWriteCells(anchor, transform, callerBuffer, out written)` 将形状写入调用方 buffer；容量不足抛 `ArgumentException`，坐标溢出返回 `false`。

## Occupancy 原子性

`GridOccupantId(0)` 表示空，正数才是合法 owner，负数会在构造时拒绝。`CanOccupy` 和 `TryOccupy` 先完整检查每个变换后的格子，再一次性提交，因此边界失败、冲突或溢出都不会留下半个占位。失败结果包含 `GridOccupancyError`、冲突坐标和已有 owner。

`TryRelease` 同样先检查所有格子都由指定 owner 持有；错 owner 或部分不匹配时不修改任何格子。容器默认非线程安全，调用方应在单线程或外部同步下使用。

## 样例与导出

打开 `Samples/KitSamples/Scenes/GridKit_Playable.unity`，可点击负坐标网格、查看 row-major index、旋转/反射 L 形 Footprint，并观察 A/B 原子占用冲突。样例只引用 `StellarFramework.GridKit.Core`。

在框架开发工程的 `StellarFramework -> Framework Source -> Kit Package Exporter` 选择 `GridKit` 导出 `StellarFramework-GridKit.unitypackage`；选择 `Sample.GridKit` 可导出样例包。GridKit 没有必需 UPM，安装器不会拉入 Addressables 或 HybridCLR。

## 生产检查

1. 为 Bounds 选择业务明确的坐标原点和尺寸；数组面积必须不超过 `Int32.MaxValue`。
2. 业务层不要把 index 当作跨版本存档 ID；需要持久化时保存坐标或业务 ID。
3. 需要多线程时在外层同步，不能把 `GridOccupancy` 当作锁。
4. 运行框架 EditMode 测试与 `GridKitBenchmark_1MStorageGeometryAndOccupancy`；基准只记录趋势，不以固定毫秒数作为放行条件。

## 与其他 Kit 的组合

- **SaveKit**：保存 Bounds、Cell 数据或业务 DTO；GridKit 不定义 Section、Serializer 或文件格式。
- **TimeKit**：TimeKit 负责“何时”，GridKit 负责“在哪个格”；两者可以由业务同时组合，但互不依赖。
- **PathKit**：PathKit 可以依赖 GridKit 的 `IReadOnlyGrid<T>`，路径搜索、Cost、Heuristic 和 Corner Cut 不进入 GridKit。
- **WorldKit**：World/Region/Chunk/Streaming 由 WorldKit 管理；每个固定 Chunk 内可使用一个或多个 DenseGrid。
- **PlacementKit**：GridKit 只回答格子是否被占；Snap、Terrain、道路和建筑规则由 PlacementKit 组合 `GridFootprint` 与 `GridOccupancy`。
- **SimulationKit / SpatialKit**：SimulationKit 可通过 Span 批处理，SpatialKit 负责动态实体索引，两者均不需要反向修改 GridKit Core。

多个 Layer 使用组合而不是 Layer Manager，例如 `DenseGrid<GroundCell>`、`GridOccupancy buildings` 和 `GridOccupancy crops` 由上层 Model 分别持有。

## 常见错误与 FAQ

**为什么坐标不是从 `(0,0)` 开始？** GridCoord 是绝对逻辑坐标，负原点是合法用例；只有 DenseGrid 的 local index 从 0 开始。

**`Contains` 为什么不包含 Max？** GridRect 永久采用 `[Min, MaxExclusive)`，相邻 `[0,3)` 与 `[3,6)` 不重叠，避免边界重复。

**如何移动一个占用物？** V1 不提供 `TryMove`。先用 `CanOccupy(..., allowedExistingOccupant)` 预览，再由业务按自己的事务策略释放旧 Footprint、占用新 Footprint。

**能否把 GameObject 放进 Occupancy？** 不能。Occupancy 只保存正整数 ID；业务自行维护 ID 到对象的映射。

**能否直接写 Occupancy 的 Span？** 不能，只有只读 Span；写入必须走 `TryOccupy` / `TryRelease`，这样才能保证失败零修改。

## AOT、IL2CPP 与线程

Core 只使用标准 C# struct/class/array/Span，不依赖反射、动态代码或编辑器 API，适合 Unity Player、IL2CPP 和 AOT 构建。`DenseGrid<T>` 在实际使用到的 T 上由业务程序集实例化；不需要额外 link.xml。只读并发可以由调用方保证无写入，任何并发写入都必须由外层同步，GridKit 不内置锁。

## 版本边界与 Roadmap

V1 只承诺二维正交整数空间、稳定 row-major DenseGrid、Footprint 几何和事实型 Occupancy。后续可能独立提供 `GridKit.UnityAdapter`、`GridKit.TilemapAdapter`、`GridKit.CollectionsAdapter` 或在真实需求出现后再评估 SparseGrid；不承诺把 Chunk、寻路、放置规则或存档并入 Core。
