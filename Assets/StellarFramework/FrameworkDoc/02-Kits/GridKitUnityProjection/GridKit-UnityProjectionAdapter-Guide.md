# GridKit.UnityProjectionAdapter

独立 Unity Adapter，用来把已有 Terrain / MeshCollider / scene geometry 投影成 GridKit 逻辑网格的基础通行数据。

## 边界

- 依赖 GridKit.Core + UnityEngine。
- 不依赖 WorldKit、WorldGenKit、PlacementKit、PathKit、SaveKit。
- PathKit 组合由项目或 Sample 的 IGridPathTraversalPolicy 完成。
- GridKit.Core 不引用 Terrain、Mesh、Physics 或其他 Unity 类型。

## 核心流程

1. GridProjectionBakeSettings 定义 GridRect、世界原点、CellSize、采样方向/距离、坡度和移动成本策略。
2. IGridProjectionSource 提供显式采样边界。
3. TerrainGridProjectionSource 直接采样 TerrainData。
4. PhysicsGridProjectionSource 使用显式 LayerMask Raycast，可覆盖 MeshCollider / scene geometry。
5. GridProjectionBaker 写入 caller-owned DenseGrid<GridBakeCell>，并要求 caller-owned scratch。
6. DenseGrid<GridTraversalOverrideCell> 独立保存手工 ForceWalkable / ForceBlocked / Cost Override。
7. GridTraversalComposer 将 AutoBakeBase + ManualOverride 组合为最终通行语义。

## Rebake

Rebake 只替换 AutoBakeBase。ManualOverride 是独立数据，重新采样 Terrain/Mesh 时不会被静默清除。

## Hard Safety

默认组合规则：

- 没有采样 Surface 时，即使 ForceWalkable 也保持 blocked。
- Obstacle 标记默认是 hard block。

这两个规则通过 GridTraversalCompositionSettings 显式控制，不做隐藏 fallback。

## 性能

- Baker 不要求 LINQ。
- Destination 与 scratch 都由调用方持有，可复用。
- 只有全部 Cell 成功采样后才 CopyFrom 到 destination；采样源失败不会留下半张新网格。
- Runtime 高频动态导航不是本 Adapter 的目标；动态实体通过 occupancy / traversal / project policy 叠加，而不是每帧全图 Rebake。
