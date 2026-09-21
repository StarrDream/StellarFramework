# WorldFramework.ToolsHub — 生产 Authoring 与诊断指南

WorldFramework.ToolsHub 是 World Framework 的 Editor-only 生产工具入口。

它解决两个问题：

1. 常见世界不修改 Core 代码，也能配置并验证生成 Profile。
2. 复杂项目仍通过现有 Runtime 接口写自定义 Stage / Rule / Adapter，而不是把业务逻辑硬编码进 ToolsHub。

## 依赖边界

该 tooling Profile 直接依赖：

- ToolsHub.Core
- WorldKit.Core
- WorldGenKit.Core
- WorldGenKit.Builtins
- WorldGenKit.Resources
- WorldGenKit.Feature
- PlacementKit.Core

它不要求 WorldKit.Streaming、SaveKit、Presentation Adapter、Addressables 或 HybridCLR。
Runtime 也不会反向依赖该工具程序集。

## 打开入口

打开 StellarFramework -> Tools Hub -> World Framework。

界面分成：

- Basic：Profile 编译摘要、Memory Report、Runtime World/Chunk 概览。
- Advanced：Compiled Pipeline / Channel / Stage 与 Resource Candidate Heatmap。
- Diagnostics：统一 Validator、Placement Probe、GenerationReport、Data Layer / Delta 明细。

## World Generation Profile

点击“新建 Profile”创建 WorldGenerationAuthoringProfile。

Profile 是 Unity Editor 的持久化 Authoring 载体，不是 WorldGenKit Core 的唯一数据表示。

当前可编辑：

- Profile ID / Version
- Width / Height / SampleStep
- typed Float / Int / Byte Channel
- Storage Kind / Scope / SourceMode
- Height / Moisture / WaterDepth / Slope
- Biome / Surface / Buildable
- Resource Definition / Distribution / Occupancy
- Feature / POI / Footprint / Quota
- Placement Probe

## Validate / Compile

Validate / Compile 不生成一套 Editor 专用规则。

它会：

1. 将 typed Channel 注册到真实 WorldGenerationPipelineBuilder。
2. 创建真实 Builtins Stage。
3. 由 Core Compiler 检查 producer、依赖关系与 Pipeline 合法性。
4. 创建真实 WorldSurfaceCatalog / WorldBiomeCatalog。
5. 创建真实 WorldOccupancyRegistry / WorldResourceCatalog。
6. 创建真实 WorldFeatureCatalog。
7. 校验 Placement Probe 配置。
8. 输出统一 Validator Report 与 Memory Report。

因此 ToolsHub 的 PASS 代表当前配置能编译到现有 Runtime contract，而不是“表单字段看起来合法”。

## Resource Candidate Heatmap

Advanced 页面可选择 Resource Index、Preview Width/Height 和 Seed。

Heatmap 使用真实 WorldResourceCandidateGenerator、WorldResourceScatterResolver、WorldResourceBudget、Occupancy 与 MinSpacing。

- 绿色：accepted candidate density
- 红色：rejected candidate density

同时显示 Generated、Accepted、Rejected Occupancy、Rejected Budget、Rejected Spacing。

同 Profile + Seed + Preview Domain 的结果保持确定性。

为防止极端 ClusterSize / Preview Domain 造成 Editor 巨额数组申请，Preview 有显式 candidate safety limit；超限直接报错，不做静默降级。

## Memory Report

Memory Report 只对能确定大小的存储给出下限：

- Dense = sampleCount * sizeof(T)
- Constant = sizeof(T)

Sparse、Chunked、Computed、External 标记为 variable storage，不伪造精确数字。

Known Fixed Channel Bytes 因此是可解释的已知下限，不代表整个 World 最终内存占用。

## Runtime Diagnostics

项目 Editor bridge 可显式注册 IWorldFrameworkDiagnosticsSource，提供：

- WorldId / Extent
- Chunk lifecycle counts
- Streaming tier counts
- Dirty / Delta counts
- Approximate managed memory
- 当前 WorldGenerationPlan
- 最近一次 WorldGenerationReport

ToolsHub 不会在 Runtime 放一个全局 World singleton，也不会扫描场景对象来猜当前 World。

需要 Region / Chunk / DataLayer / Delta 明细时，再实现可选 IWorldFrameworkDetailDiagnosticsSource。

它返回不可变 Editor DTO：

- WorldChunkDiagnosticSnapshot
- WorldDataLayerDiagnosticSnapshot
- WorldDeltaDiagnosticSnapshot

项目可以自行控制采样、过滤、Streaming 显示标签和内存估算；WorldKit.Core 不需要为 Editor 暴露新的可变集合。

## Placement Probe

Placement Probe 直接使用 PlacementRequest、PlacementSiteFacts、Built-in Rules 与 PlacementEvaluator。

失败时展示 Runtime 的 PlacementFailureId，例如 slope / water / zone / conflict / connection。

## 高级代码扩展

ToolsHub Profile 覆盖通用配置。高级项目仍可以：

- 实现 IWorldGenerationStage
- 使用 WorldGenerationPipelineBuilder
- 注册项目自定义 Channel
- 编写项目 Rule / Candidate producer
- 编写 Resource / Feature / Placement Adapter
- 通过项目自己的 Editor bridge 接入 diagnostics

不需要修改 WorldGenKit / WorldKit Core。

这保持生产 Authoring 的开闭边界：Basic Authoring 配置化，高级语义代码扩展化。

## 错误处理

以下情况会明确 FAIL：

- Stable ID 非法或重复
- Channel 类型与 Builtin Stage 不匹配
- producer 缺失
- fallback Biome 非 unconditional
- Surface / Biome 引用不存在
- Resource distribution 越界
- Occupancy ID 不存在或重复
- Feature ID / footprint / quota 非法
- Placement Probe 配置非法
- Preview 超过 candidate safety limit

先修配置，再重新 Validate / Compile。

