# WorldGenKit.Feature — 地标 / POI / Area / Compound Feature 指南

WorldGenKit.Feature 是建立在 WorldGenKit.Core 之上的独立世界结构生成扩展。

它负责决定大型或特殊结构出现的位置、优先级、世界/Region 配额、空间 Reservation，以及需要的地形适配请求。

它不负责 Unity Prefab/GameObject 实例化、建筑经济、NPC、Terrain/Tilemap/Mesh 表现、SaveKit 文件格式、Resource Scatter 或 PlacementKit 规则实现。这些能力通过 Adapter 组合。

## 1. Core Boundary

Core 依赖：

    WorldGenKit.Feature
            ↓
    WorldGenKit.Core

可选 Adapter：

    Feature.ResourcesAdapter
    Feature.PlacementAdapter
    Feature.AuthoringAdapter
    Feature.WorldKitAdapter
    Feature.SaveKitAdapter

Feature Core 不依赖以上任何 Adapter。

## 2. Stable IDs

Feature 使用稳定字符串 ID，例如：

    feature.border_tower
    feature.rice_paddy
    feature.village
    feature.shipwreck
    feature.world_tree

Category 同样使用 Stable ID。

Stable ID 用于持久化、Catalog、ToolsHub 和跨版本映射；Resolver 热路径使用编译后的 featureIndex。

## 3. Feature Kind

Feature Core 当前支持：

    Landmark
    Area
    Compound

Landmark：高塔、神庙、巨树、沉船、特殊遗迹。

Area：水田、沼泽、陨石坑、废墟区域等连续区域。

Compound：村庄、营地、堡垒、地牢等内部由多个语义成员组成的结构。

## 4. Footprint / Reservation

基础 Footprint：

    Rectangle
    Circle

支持 Rotation，并生成连续逻辑空间 AABB Reservation。

Reservation 使用 half-open overlap：

    边缘接触 != 重叠
    内部相交  = 重叠

## 5. Deterministic Resolver

Resolver 排序：

    Priority DESC
    Score DESC
    DeterministicKey ASC
    Stable Feature ID Rank ASC
    X
    Y
    Rotation

候选数组输入顺序不会改变最终 accepted Feature。

Resolver 会在写入 accepted output 前预校验 candidate index、world/region count、existing reservation 与 scratch/output capacity。

## 6. World / Region Quota

WorldFeatureQuota 支持：

    MaxPerWorld
    MaxPerRegion

常用：

    WorldFeatureQuota.Unlimited()
    WorldFeatureQuota.UniquePerWorld()

default(WorldFeatureQuota) 明确无效，避免默认 0/0 被误解释为 Unlimited。

## 7. Terrain Adaptation

Feature Core 只声明修改意图：

    Flatten
    Carve
    Fill
    Stamp

请求保存 FeatureIndex、Bounds、PrimaryValue、Falloff 和可选 Stable Stamp ID。

Feature.AuthoringAdapter 负责把这些请求连接到 Authoring 模块，并返回 Dirty Region propagation。

Stamp 必须由调用方提供明确的 Stamp applicator，不做伪实现。

## 8. Resource Reservation

正确顺序：

    Base Terrain / Channels
            ↓
    Feature Candidate
            ↓
    Feature Resolver
            ↓
    Feature Reservation
            ↓
    Resource Scatter

Feature.ResourcesAdapter 将连续 Reservation 光栅化到 WorldResourcePlanarDomain Occupancy。

Adapter 先 preflight 全部目标 sample，全部无冲突后才 apply；如果资源已经错误地先占据冲突位置，会显式失败且不产生部分 Reservation。

## 9. Placement Validation

Feature Core 不硬编码坡度、水深、Zone、道路连接、冲突层或自定义游戏规则。

Feature.PlacementAdapter 将 Feature Candidate + Footprint 转换为 PlacementRequest，再交给 PlacementKit.Core。

因此高塔可要求低坡度，沉船可要求水深，村庄可要求道路/Zone，水田可要求低坡度和水域条件。

## 10. Compound Feature

Compound Template 由 Stable Template/Slot/ElementType ID 和 parent-local transform 组成。

WorldCompoundFeatureLayoutBuilder 根据父 Feature Candidate 的位置与旋转确定性地生成语义成员。

例如 village 可以生成：

    house.small
    house.large
    well
    storage
    plaza

这些仍是语义结果，不包含 Prefab、居民或经济系统。

## 11. WorldKit Usage Tracking

Feature.WorldKitAdapter 提供 WorldFeatureUsageState。

它维护：

    per-Feature World Count
    per-Feature per-Region Count

运行时用 Catalog index，持久化 Snapshot 使用 Stable Feature ID，因此 Catalog 顺序变化后仍能 Restore。

WorldKit data layer ID：

    worldgen.feature.usage

Feature Core 不依赖 WorldKit。

## 12. SaveKit Persistence

Feature.SaveKitAdapter 提供 WorldFeatureUsageSaveSection。

Section ID：

    worldgen.feature.usage

已验证真实流程：

    Generate Unique Tower
    → Commit Usage
    → SaveKit Save
    → Clear Runtime State
    → SaveKit Load
    → Generate Tower Again
    → Quota Reject

所以 Unique-per-world 跨存档加载仍有效。

## 13. Four Acceptance Scenarios

Tower：

    Landmark
    UniquePerWorld
    Placement slope rule
    Resource reservation

Rice Paddy：

    Area
    low-slope / water placement
    Flatten terrain adaptation
    Resource reservation

Village：

    Compound
    large reservation
    semantic internal template
    optional road / zone rule

Shipwreck：

    Landmark
    water-depth placement rule
    optional coast-distance custom rule

## 14. Standalone Composition

最小 Feature：

    WorldGenKit.Core
    + WorldGenKit.Feature

需要资源协同时额外导入 Feature.ResourcesAdapter。

需要放置规则时额外导入 Feature.PlacementAdapter。

需要地形手工修改时额外导入 Feature.AuthoringAdapter。

需要 World runtime usage tracking 时额外导入 Feature.WorldKitAdapter。

需要 SaveKit persistence 时额外导入 Feature.SaveKitAdapter。

开发者不需要为了一个 Landmark 系统导入完整 World Framework。

## 15. Performance Principles

- Stable ID 只在 Catalog/Profile/Save boundary 使用。
- Resolver 热路径使用 integer feature index。
- order/count/output scratch 由调用方提供。
- 不使用运行时反射扫描。
- Feature Core 无 UnityEngine。
- Snapshot/Save 允许分配，因为不是 per-cell 热路径。
- 大型 Feature 数量通常远低于 Resource candidate 数量。

## 16. Scope Boundary

Feature 不负责 prefab instantiate、settlement economy、citizen AI、building construction、traffic/logistics、quest/story 或 Unity rendering。

这些系统通过 Stable Feature/Element ID 消费 Feature 输出。
