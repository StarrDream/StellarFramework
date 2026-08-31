# GridKit 样例

`Example_GridKit` 是 GridKit 的可运行样例，场景为 `Scenes/GridKit_Playable.unity`。

样例使用 `(-6,-4)` 起点、`12 x 8` 的负坐标网格，面板直接展示：

- `GridCoord`、`GridRect`、`GridSize` 的半开区间与 Row-Major index；
- `DenseGrid<int>` 的连续存储与点击选格；
- 4 邻居查询（调用者提供 `Span`，越界自动过滤）；
- 不可变 L 形 `GridFootprint` 的 0/90/180/270 度旋转与反射；
- `GridOccupancy` 的整数 OccupantId、两遍原子占用、冲突坐标、错误释放与清空。

场景不引用 Addressables、HybridCLR、ResKit、TimeKit 或其他 Kit。导出 `samples.gridkit` 时会同时带上 `GridKit.Core`，不额外安装 UPM 包。

运行方式：

1. 在框架开发工程打开 `Scenes/GridKit_Playable.unity`；
2. 点击任意格观察负坐标与 index；
3. 点击 `Occupy A` 后在相同 Anchor 点击 `Occupy B`，应得到 `Occupied`，且不会产生半占用；
4. 切换旋转/反射后重复操作，观察 Footprint 世界坐标与越界检查。
