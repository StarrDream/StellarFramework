# PathKit Core Sample

`PathKit_Playable` 使用一个完全自定义的有向加权 Graph，不引用 GridKit。可切换 A* / Dijkstra、Start/Goal、边显示和 Reset，结果面板显示状态、成本、路径长度、展开数与节点序列。

重点观察：PathNodeId 稳定 tie-break、加权边不等于最少边数、Disconnected 节点返回 NoPath，以及路径输出始终为 Start -> Goal。
