# PathKit 源码文档

## Foundation 边界

`StellarFramework.PathKit.Core` 的 asmdef 不引用任何程序集并设置 `noEngineReferences = true`。它只包含通用 Graph API、A*、Dijkstra 和内部可复用 workspace；GridKit 适配器位于 `Adapters/GridKit` 的独立 asmdef 中。Core 不引用 UnityEngine、GridKit、LogKit、EventKit、UPM、Addressables 或 HybridCLR。

## 不变量

1. `PathNodeId` 的零值无效，正值有效，PathKit 不分配业务 ID。
2. `PathNeighbor.Cost` 是正 `long`；不可通行边由 Graph 省略。
3. `IPathGraph` 暴露稳定顺序的 outgoing neighbors，支持有向图。
4. `PathSearchStatus.None = 0` 只表示 default/unexecuted；`Success` 为非零，`FindPath` 永不返回 `None`。
5. Dijkstra 永不调用 heuristic；A* heuristic 必须非负且为 admissible 才能保证最优。
6. A* 对更小 G 的 Closed record 执行 reopen。
7. 堆平局为 A* 的 F/H/NodeId、Dijkstra 的 G/NodeId。
8. 路径方向固定为 Start -> Goal，成功结果包含两端。
9. buffer 不足不写任何 partial path；NoPath 不提供 fallback。
10. `MaxExpandedNodes` 只统计实际枚举邻居的节点；Goal pop 不计数。
11. 成本乘加不允许溢出 wrap，必须返回 `CostOverflow` 或由 Graph 契约错误抛出。
12. Pathfinder 复用 workspace，不保存 Graph，不监听外部状态，不保证线程安全或可重入。
13. Grid、移动、世界事件和存档属于适配层或业务层。

## 算法与生命周期

`PathSearchRunner` 先验证 request、Start/Goal 存在性和 Start==Goal，再清理 workspace。workspace 用 `Dictionary<PathNodeId,int>` 定位 record，record 保存 Node/G/H/F/Parent/State/HeapIndex，open heap 只保存 record index。弹出 Goal 时先重建 parent 链并计算完整节点数，只有 buffer 足够才从末尾倒序写入，因此输出是原子性的。

每条边先校验节点和正成本，再做 checked 的 `G + edge`。A* 对新节点读取并校验 heuristic，然后做 checked 的 `G + H`。同一个节点只有在发现更小 G 时更新 parent；Open record 做 decrease-key，Closed record 恢复为 Open 并重新入堆。相同 G 不重写 parent，结果依赖稳定邻居顺序与堆 tie-break。

## 复杂度与分配

设发现节点数为 V、检查的 outgoing edge 数为 E，二叉堆实现的期望复杂度为 `O((V + E) log V)`，字典查找期望 `O(1)`，重建为路径长度 O(P)。容量预热后，搜索热路径不创建 List、LINQ、邻居数组或临时输出；扩容仍可能分配，不能宣传绝对 zero-GC。Graph 自己应避免在 `GetNeighborCount/GetNeighbor` 中产生临时集合。

## Grid 适配器

`GridPathGraph` 使用 GridRect 的 Min/Size 计算 local row-major index，并将 index 加一映射为 PathNodeId。FourWay 邻居顺序为 N/E/S/W；EightWay 为 N/NE/E/SE/S/SW/W/NW。NoCornerCut 要求两个 side cell 在 bounds 内且 walkable，AllowCornerCut 只要求目标 cell 与 policy 的有向边可通过。

FourWay heuristic 为 `(abs(dx) + abs(dy)) * MinimumOrthogonalCost`。EightWay heuristic 使用 `diag=min(dx,dy)`、`straight=max-diag`、`effectiveDiag=min(diagMin, 2*orthMin)`，并对每次乘加做 checked 计算。policy 声明的两个最小成本和每条实际边成本都必须为正，且实际成本不能低于对应下界。

## AOT、动态状态与保存

实现只使用普通 C# struct、interface、array、Dictionary 和二叉堆，不使用 dynamic、反射分派、运行时代码生成、Jobs 或 Burst，适合 IL2CPP/AOT。Grid 状态由 policy 在每次搜索中读取；同一次同步搜索期间调用方必须保证稳定。不要保存 workspace、open heap 或 parent 链；持久化移动状态应由业务保存当前位置、目标和 movement state，加载后重新寻路。

Core standalone Sample 的节点二维坐标只用于绘制，并不参与 traversal cost；由于任意加权 Graph 无法从绘图坐标证明 lower bound，示例 `EstimateCost` 使用 `H = 0`。GridKit Adapter Sample 才展示基于真实 minimum traversal cost 的 admissible Manhattan / Octile heuristic。

## 验证与导出

行为测试位于 `Tests/EditMode/FrameworkValidation/Kits/PathKit`，覆盖契约错误、溢出、reopen、buffer 原子性、负坐标、转角规则和动态 policy。性能测试使用独立 synthetic Graph，同时记录规模、算法、耗时、expanded、路径长度、成本、迭代、粗粒度堆趋势和 checksum。Catalog 的 `pathkit` profile 只导出 Core，`pathkit.gridkit` 通过依赖闭包导出 Core、GridKit 和适配器；Samples 与 Tests/Verification 不进入运行时包。

PathKit V1 Core Semantics 已冻结：`None = 0` 表示 default/unexecuted，`Success` 只表示真实成功，`FindPath` 永不返回 `None`；Graph-first、A*/Dijkstra、Closed Reopen、成本溢出保护、原子路径输出和 GridKit 独立适配器契约保持稳定。后续只接受不改变这些语义的内部优化或文档澄清。
