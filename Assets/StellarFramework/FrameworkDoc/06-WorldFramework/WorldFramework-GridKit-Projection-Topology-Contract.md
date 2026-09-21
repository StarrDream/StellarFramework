# World Framework — GridKit Projection & Topology Contract

> Status: Stable contract  
> Scope: preserve current GridKit V1 APIs while defining the additive topology capability and the independent Terrain/Mesh -> Grid workflow.

---

## 1. Existing GridKit APIs are compatibility surface

The following existing V1 concepts remain valid:

- `GridCoord`
- `GridOffset`
- `GridSize`
- `GridRect`
- `DenseGrid<T>`
- `IGrid<T>`
- `IReadOnlyGrid<T>`
- `GridFootprint`
- `GridTransform`
- `GridOccupancy`
- square 4-way / 8-way neighbor helpers.

Topology extensions must not reinterpret `GridCoord` as a fake universal coordinate.

Existing square projects should continue to compile with minimal/no source changes.

---

## 2. Topology expansion principle

GridKit gains topology as an additional capability.

It does not become WorldKit.

Minimal conceptual contract:

```csharp
public interface IGridTopology<TCoord>
    where TCoord : struct
{
    int MaxNeighborCount { get; }

    int WriteNeighbors(
        TCoord center,
        Span<TCoord> destination);

    long GetDistance(
        TCoord from,
        TCoord to);
}
```

Notes:

- caller owns destination buffers;
- no iterator allocation is required;
- topology calls are pure logical operations;
- concrete built-ins may use structs/static optimized paths if interface dispatch becomes measurable in hot loops.

---

## 3. Built-in topology targets

### 3.1 Orthogonal4

Coordinate:

`GridCoord`

Neighbors:

- north
- east
- south
- west

Distance:

Manhattan.

### 3.2 Orthogonal8

Coordinate:

`GridCoord`

Neighbors:

- orthogonal + diagonal.

Distance:

Chebyshev / movement-policy-specific heuristic where appropriate.

### 3.3 Hex

Add a dedicated value type:

```csharp
public readonly struct HexCoord : IEquatable<HexCoord>
{
    public int Q { get; }
    public int R { get; }
}
```

Axial coordinate is the public storage form.

Cube coordinate may be used internally for:

- distance;
- line/ring/range math;
- validation.

Required Hex helpers:

- neighbor by direction;
- all 6 neighbors;
- distance;
- ring;
- range;
- line if stable/no-allocation implementation is practical.

Do not encode Hex using odd/even-row special cases throughout unrelated systems.

---

## 4. Cell / Edge / Vertex ownership

The topology capability must support the concept that data can live on more than a cell.

Examples:

```text
Cell:
terrain
resource
city
unit slot

Edge:
river
wall
border
road boundary

Vertex:
junction
corner marker
special attachment point
```

Do not force one universal edge/vertex representation if square and Hex require different efficient identities.

Preferred contract pattern:

```text
IGridTopology<TCell>
IGridEdgeTopology<TCell, TEdge>       // optional capability
IGridVertexTopology<TCell, TVertex>   // optional capability
```

Built-in topologies provide stable value types for their supported edge/vertex identities.

WorldKit does not own these identities.

---

## 5. Isometric is not a new topology by default

An isometric Tilemap usually remains a square logical topology with a different coordinate-to-world projection.

Model:

```text
GridCoord
   ↓
Isometric Grid<->World Mapper
   ↓
Unity world/screen position
```

Do not duplicate pathfinding/generation topology simply because presentation is diamond-shaped.

---

## 6. Existing PathKit adapter compatibility

Current:

`GridPathGraph`

is a square-grid adapter and should remain valid.

Hex support does not require breaking it.

Options for later work:

1. add a Hex-specific PathKit adapter;
2. add a topology-aware adapter if benchmarks/API clarity justify it.

Do not modify `PathKit.Core` merely to add Hex navigation.

---

## 7. Unity Grid Projection Adapter

Planned assembly:

```text
StellarFramework.GridKit.UnityProjectionAdapter
```

Dependencies:

- GridKit.Core
- UnityEngine

Must NOT depend on:

- WorldKit
- WorldGenKit
- PlacementKit
- PathKit
- SaveKit.

PathKit integration remains a separate optional composition.

---

## 8. Projection source boundary

Conceptual Unity-facing API:

```csharp
public readonly struct GridProjectionQuery
{
    public GridCoord Coord { get; }
    public Vector3 SampleOrigin { get; }
    public Vector3 SampleDirection { get; }
    public float MaxDistance { get; }
}
```

```csharp
public readonly struct GridProjectionSample
{
    public bool Hit { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public float Height { get; }
    public float SlopeDegrees { get; }
    public int SurfaceCategory { get; }
    public bool ObstacleHit { get; }
}
```

```csharp
public interface IGridProjectionSource
{
    bool TrySample(
        in GridProjectionQuery query,
        out GridProjectionSample sample);
}
```

Unity implementations may use:

- Terrain sampling;
- Physics raycasts;
- MeshCollider;
- custom scene geometry provider.

The interface is in the Unity adapter, not GridKit.Core.

---

## 9. Bake configuration

Conceptual:

```csharp
public sealed class GridProjectionBakeSettings
{
    public GridRect Bounds { get; }
    public Vector3 WorldOrigin { get; }
    public Vector2 CellSize { get; }
    public float MaxWalkableSlope { get; }
    public long DefaultMovementCost { get; }
    public long SteepMovementCost { get; }
}
```

Settings must validate:

- non-empty bounds where bake requires cells;
- positive cell size;
- finite world-space values;
- non-negative/positive cost semantics as defined;
- slope range.

No silent clamping of invalid authoring values.

---

## 10. Auto bake data

Initial adapter-owned data type:

```csharp
[Flags]
public enum GridBakeFlags
{
    None = 0,
    HasSurface = 1 << 0,
    BaseWalkable = 1 << 1,
    Obstacle = 1 << 2
}
```

```csharp
public struct GridBakeCell
{
    public GridBakeFlags Flags;
    public float Height;
    public float SlopeDegrees;
    public long BaseMovementCost;
    public int SurfaceCategory;
}
```

Typical container:

```csharp
DenseGrid<GridBakeCell>
```

This is intentionally adapter-owned so GridKit.Core does not acquire Terrain/slope/physics concepts.

---

## 11. Manual override layer

Manual data is stored separately from auto-bake data.

```csharp
public enum GridWalkabilityOverride
{
    None = 0,
    ForceWalkable = 1,
    ForceBlocked = 2
}
```

```csharp
public struct GridTraversalOverrideCell
{
    public GridWalkabilityOverride Walkability;
    public bool HasMovementCostOverride;
    public long MovementCost;
}
```

Typical container:

`DenseGrid<GridTraversalOverrideCell>`

Rebake rule:

```text
Replace AutoBakeBase
Keep ManualOverride
Re-compose FinalTraversalState
```

Manual data may only be discarded through an explicit user action or an explicit incompatible-grid remap flow.

---

## 12. Final traversal composition

Conceptual policy:

```csharp
public sealed class BakedGridTraversalPolicy : IGridPathTraversalPolicy
{
    // Reads auto bake + manual overrides.
}
```

This type belongs to an integration/adapter assembly that is allowed to reference:

- GridKit.Core
- PathKit.GridKitAdapter contract side as appropriate.

Alternative: keep the policy in the sample/project if a new formal assembly is not justified.

Important:

PathKit.Core remains untouched.

---

## 13. Composition rules

Walkability:

```text
ForceBlocked   -> blocked
ForceWalkable  -> walkable unless an explicitly configured hard safety rule forbids it
None           -> AutoBakeBase
```

Movement cost:

```text
Manual Cost Override -> override
otherwise            -> BaseMovementCost
```

Hard safety rule examples, if enabled:

- outside grid bounds;
- no sampled surface;
- invalid numeric sample.

These are explicit settings/policies, not hidden fallback behavior.

---

## 14. Bake result

Do not return only bool.

Conceptual:

```csharp
public enum GridProjectionBakeError
{
    None = 0,
    InvalidSettings,
    SourceUnavailable,
    SampleFailed,
    Cancelled
}
```

```csharp
public readonly struct GridProjectionBakeResult
{
    public bool Success { get; }
    public GridProjectionBakeError Error { get; }
    public int TotalCells { get; }
    public int WalkableCells { get; }
    public int BlockedCells { get; }
    public int MissingSurfaceCells { get; }
}
```

Per-cell diagnostics should be optional/editor-oriented to avoid mandatory large allocations.

---

## 15. Editor / ToolsHub responsibilities

Editor tooling may:

- draw projected grid overlay;
- color walkable/blocked/cost;
- paint ForceWalkable;
- paint ForceBlocked;
- paint/reset movement cost;
- inspect slope/height/sample;
- rebake selected region;
- clear/remap manual override explicitly;
- preview PathKit route.

Runtime GridKit.Core has no Editor dependency.

---

## 16. Persistence

The adapter must not force SaveKit for simple projects.

Authoring options may include:

- ScriptableObject;
- custom asset;
- binary/json adapter;
- project-owned serialization.

If SaveKit is installed, a separate integration may serialize runtime traversal modifications.

---

## 17. Compatibility gates

GridKit topology work must pass:

1. all existing `GridKitTests`;
2. all existing `PathKitGridKitAdapterTests`;
3. current square sample remains functional;
4. new Hex tests;
5. no `UnityEngine` reference in GridKit.Core;
6. no WorldKit/WorldGenKit dependency in GridKit.Core;
7. no continuous allocations in neighbor hot-path benchmark;
8. negative coordinates remain supported.

---

## 18. Planned tests

### Grid topology

- Orthogonal4 stable order.
- Orthogonal8 stable order.
- Hex six neighbors.
- Hex negative coordinates.
- Hex distance symmetry.
- ring count.
- range count.
- caller buffer too small fails explicitly.

### Projection adapter

- flat plane produces walkable cells.
- slope above threshold blocks.
- obstacle classification blocks.
- missing surface handled explicitly.
- manual ForceBlocked overrides auto walkable.
- manual ForceWalkable overrides soft auto block.
- manual movement cost survives rebake.
- rebake does not clear override layer.

### Path integration

- baked grid routes around blocked cells.
- painted blocked corridor changes route.
- clearing override restores base route.

---

## 19. Decision summary

The stable contract is:

- existing square GridKit API remains compatible;
- Hex uses a dedicated coordinate/topology implementation;
- topology lives in GridKit, not WorldKit;
- Terrain/Mesh projection lives in a Unity adapter, not GridKit.Core;
- manual traversal overrides are separate from auto bake;
- PathKit.Core remains generic and unchanged;
- World Framework is not required for this workflow.

