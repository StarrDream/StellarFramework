# PlacementKit.Core — 通用放置验证指南

PlacementKit.Core 是零依赖、无 UnityEngine 的通用放置规则 Foundation Kit。

可用于塔防塔位、RTS 建筑、城建建筑、农场设施、家具、生存建造、WorldGen Feature 候选验证和服务端 placement validation。

## 1. Core Boundary

PlacementKit.Core 的 asmdef：

    references = []
    noEngineReferences = true

Core 不知道 WorldKit、WorldGenKit、GridKit、Unity Terrain、NavMesh、GameObject 或 Prefab。

## 2. Stable IDs

Placement 使用：

    PlacementTypeId
    PlacementRuleId
    PlacementFailureId

失败不是模糊 bool，UI/ToolsHub 可以根据 Failure ID 显示具体原因。

## 3. Footprint

基础形状：

    Rectangle
    Circle

支持 Rotation 与连续空间 AABB。

Bounds 使用 half-open overlap：边缘接触允许，内部相交才算冲突。

## 4. PlacementRequest

PlacementRequest 只保存：

    TypeId
    X / Y
    Rotation
    Footprint

不包含 Terrain、Grid 或对象引用。

## 5. PlacementSiteFacts

内置通用事实：

    MaxSlopeDegrees
    MinWaterDepth
    MaxWaterDepth
    ZoneMask
    ConflictMask
    ConnectionMask
    BaseSuitability

这些 facts 由项目自己的 Terrain/Grid/World Adapter 采样。

## 6. Built-in Rules

当前提供：

    PlacementSlopeRule
    PlacementWaterDepthRule
    PlacementRequiredZoneRule
    PlacementConflictRule
    PlacementConnectionRule
    PlacementBaseSuitabilityRule

每条规则返回 Pass(scoreDelta) 或 Fail(failureId)。

## 7. Evaluation

PlacementEvaluator 支持 collectAllFailures=true，用于编辑器/UI 一次显示多个失败原因。

也支持 collectAllFailures=false，只返回首个失败。

Failure buffer 由调用方提供，Core 不在热路径创建 List。

## 8. Custom Rules

PlacementKit 使用泛型 IPlacementRule<TContext>。

项目可以定义自己的 Temperature、MagicDensity、Pollution、RoadDistance 或服务器权威上下文，而无需修改 PlacementKit.Core。

## 9. WorldGen Feature Adapter

WorldGenKit.Feature.PlacementAdapter 负责：

    Feature Candidate + Feature Footprint
            ↓
    PlacementRequest
            ↓
    PlacementKit Rules

Feature Core 与 PlacementKit Core 仍保持独立。

## 10. Example Policies

Tower：

    Slope <= limit
    No Building Conflict
    Optional Road Connection

Rice Paddy：

    Low Slope
    WaterDepth within range
    Agriculture Zone

Village：

    Settlement Zone
    No reservation conflict
    Road connection

Shipwreck：

    WaterDepth >= minimum
    WaterDepth <= maximum

## 11. Failure Semantics

Placement validation 不偷偷修复输入：

- 非法 Stable ID：抛错。
- 非有限数：抛错。
- failure buffer 不够：规则执行前失败。
- null rule：规则执行前失败。
- score overflow：显式异常。
- default footprint：无效。

## 12. Scope Boundary

PlacementKit.Core 不负责资源扣除、建筑实例化、占地写回、Grid bake、Terrain sampling、存档、网络同步、路径搜索或交通模拟。

它只负责：

    Request + Context + Rules
    → Allowed?
    → Failure IDs
    → Suitability Score
