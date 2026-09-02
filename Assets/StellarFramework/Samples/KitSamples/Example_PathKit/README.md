# PathKit Core Sample

`PathKit_Playable` 使用一个完全自定义的有向加权 Graph，不引用 GridKit。可切换 A* / Dijkstra、Start/Goal、边显示和 Reset，结果面板显示状态、成本、路径长度、展开数与节点序列。默认 `1 -> 12` 的最优路线是 `1-3-5-9-12`，总成本为 `8`；`1-2-4-7-11-12` 是总成本 `9` 的加权备选。

重点观察：PathNodeId 稳定 tie-break、加权边不等于最少边数、Disconnected 节点返回 NoPath，以及路径输出始终为 Start -> Goal。示例节点的二维坐标只用于绘制，无法证明 traversal-cost lower bound，因此 Core Sample 的 A* `EstimateCost` 固定为 `H=0`；GridKit Adapter Sample 才演示 minimum-cost Manhattan / Octile heuristic。
