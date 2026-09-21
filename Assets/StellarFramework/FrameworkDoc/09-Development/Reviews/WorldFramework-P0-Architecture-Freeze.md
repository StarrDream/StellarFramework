# StellarFramework World Framework — P0 Architecture Freeze

> Phase: P0 — Architecture Freeze & Tiny Foundation Integration  
> Status: IN PROGRESS  
> Scope: dependencies, ownership boundaries, minimum-use profiles, contract shapes, validation gates.  
> Runtime implementation is intentionally deferred until these boundaries are accepted by tests and review.

---

## 1. Why P0 exists

World Framework is large enough that implementing terrain/noise first would be the wrong optimization.

P0 exists to prevent three failure modes:

1. A simple project must import the whole world stack just to use one Kit.
2. WorldKit becomes a second GridKit / SaveKit / PathKit / SimulationKit.
3. WorldGenKit accumulates game-domain concepts until every new game requires Core changes.

The architecture is accepted only when small and large projects can both use the same family of Kits without hidden mandatory dependencies.

---

## 2. Current Foundation Audit

The following statements are based on the current repository implementation, not on planned APIs.

### 2.1 GridKit.Core

Current assembly:

`StellarFramework.GridKit.Core`

Current asmdef properties:

- no assembly references;
- `noEngineReferences = true`;
- independently exportable in the distribution catalog.

Current useful primitives include:

- `GridCoord`
- `GridOffset`
- `GridSize`
- `GridRect`
- `DenseGrid<T>`
- `IReadOnlyGrid<T>`
- `IGrid<T>`
- `GridFootprint`
- `GridOccupancy`
- square 4-way / 8-way neighbor utilities.

Current limitation relevant to World Framework:

- coordinate/topology is effectively orthogonal square-grid oriented;
- there is no common topology contract;
- Hex is not implemented;
- Cell / Edge / Vertex topology is not modeled as a general concept.

Decision:

> Keep GridKit independent. Extend topology in P1. Do not implement Hex or Cell/Edge/Vertex inside WorldKit.

### 2.2 PathKit.Core

Current assembly:

`StellarFramework.PathKit.Core`

Current asmdef properties:

- no assembly references;
- `noEngineReferences = true`.

PathKit already consumes an abstract:

`IPathGraph`

with:

- node existence;
- neighbor count;
- neighbor access;
- heuristic cost.

Current algorithms include:

- A*
- Dijkstra
- bounded search.

Decision:

> PathKit.Core is already correctly shaped as an independent generic graph search Kit. World Framework must not add World/Grid dependencies to it.

### 2.3 PathKit.GridKitAdapter

Current assembly:

`StellarFramework.PathKit.GridKitAdapter`

References exactly:

- `StellarFramework.PathKit.Core`
- `StellarFramework.GridKit.Core`

and has:

`noEngineReferences = true`.

The current `IGridPathTraversalPolicy` deliberately delegates:

- walkability;
- edge traversal permission;
- traversal cost

to application-owned state.

It explicitly does not know about terrain, doors, occupancy or a world model.

Decision:

> Preserve this boundary. P1 topology work may generalize/add adapters for Hex, but PathKit.Core itself remains unchanged unless a genuine graph-level requirement is found.

### 2.4 SpatialKit.Core

Current assembly:

`StellarFramework.SpatialKit.Core`

Properties:

- no assembly references;
- `noEngineReferences = true`.

Current responsibility is continuous 2D spatial indexing/query.

Decision:

> WorldKit may provide an optional SpatialKit adapter/profile, but WorldKit must not reimplement general spatial indexing.

### 2.5 SimulationKit.Core

Current assembly:

`StellarFramework.SimulationKit.Core`

Properties:

- no assembly references;
- `noEngineReferences = true`.

Its tests explicitly define it as a scheduler independent from Unity time and business objects.

Decision:

> WorldKit owns world/chunk lifecycle; SimulationKit owns simulation scheduling. An integration profile may connect chunk simulation activation without moving simulation rules into WorldKit.

### 2.6 SaveKit.Core

Current assembly:

`StellarFramework.SaveKit.Core`

Current references:

- `StellarFramework.LogKit`
- `UniTask`

SaveKit owns storage / serialization / container / migration concerns.

Decision:

> WorldKit defines semantic runtime/persistent delta contracts, but actual storage/serialization is delegated to a WorldKit.SaveKitAdapter. WorldKit.Core must not reference SaveKit.Core.

---

## 3. Hard Modularity Contract

These are release requirements, not suggestions.

### 3.1 PathKit standalone

Valid project:

```text
PathKit.Core
```

The project supplies its own `IPathGraph`.

Must not require:

- GridKit
- WorldKit
- WorldGenKit
- PlacementKit
- Unity scene integration

### 3.2 GridKit standalone

Valid project:

```text
GridKit.Core
```

Supports logical grids, topology, storage, footprints and occupancy appropriate to the installed topology capabilities.

Must not require WorldKit.

### 3.3 Grid navigation

Valid project:

```text
GridKit.Core
PathKit.Core
PathKit.GridKitAdapter
```

No WorldKit / WorldGenKit dependency.

### 3.4 Existing Terrain/Mesh mapped to a grid

Valid project:

```text
GridKit.Core
GridKit.UnityProjectionAdapter
```

Optional:

```text
+ PathKit.Core
+ PathKit.GridKitAdapter
```

Must not require WorldKit or WorldGenKit.

### 3.5 WorldKit standalone

Valid project:

```text
WorldKit.Core
```

Use cases:

- manually authored worlds;
- server-authored worlds;
- externally loaded worlds;
- streaming scene data;
- projects with no procedural generation.

### 3.6 WorldGenKit standalone

Valid project:

```text
WorldGenKit.Core
```

Use cases:

- generate data to textures;
- generate custom binary/project formats;
- generate a Mesh via adapter;
- generate Tilemap data via adapter;
- offline generation/baking without WorldKit runtime.

### 3.7 PlacementKit standalone

Valid project:

```text
PlacementKit.Core
```

Application supplies geometry/queries through policy/context boundaries.

GridKit/WorldKit integrations remain optional adapters.

---

## 4. Minimum Dependency Matrix

Legend:

- **R** = required dependency
- **A** = optional adapter/integration
- **-** = no dependency

| Module | GridKit | SpatialKit | PathKit | SaveKit | SimulationKit | TimeKit | WorldKit | WorldGenKit | PlacementKit | Unity |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GridKit.Core | - | - | - | - | - | - | - | - | - | - |
| SpatialKit.Core | - | - | - | - | - | - | - | - | - | - |
| PathKit.Core | - | - | - | - | - | - | - | - | - | - |
| SimulationKit.Core | - | - | - | - | - | - | - | - | - | - |
| WorldKit.Core | - | - | - | - | - | - | - | - | - | - |
| WorldGenKit.Core | - | - | - | - | - | - | - | - | - | - |
| PlacementKit.Core | - | - | - | - | - | - | - | - | - | - |
| PathKit.GridKitAdapter | R | - | R | - | - | - | - | - | - | - |
| WorldKit.GridKitAdapter | R | - | - | - | - | - | R | - | - | - |
| WorldKit.SpatialKitAdapter | - | R | - | - | - | - | R | - | - | - |
| WorldKit.SaveKitAdapter | - | - | - | R | - | - | R | - | - | - |
| WorldKit.SimulationKitAdapter | - | - | - | - | R | - | R | - | - | - |
| WorldGenKit.WorldKitAdapter | - | - | - | - | - | - | R | R | - | - |
| WorldGenKit.GridKitAdapter | R | - | - | - | - | - | - | R | - | - |
| PlacementKit.GridKitAdapter | R | - | - | - | - | - | - | - | R | - |
| PlacementKit.WorldKitAdapter | - | - | - | - | - | - | R | - | R | - |
| GridKit.UnityProjectionAdapter | R | - | - | - | - | - | - | - | - | R |
| WorldGenKit.TilemapAdapter | - | - | - | - | - | - | - | R | - | R |
| WorldGenKit.MeshAdapter | - | - | - | - | - | - | - | R | - | R |
| WorldGenKit.UnityTerrainAdapter | - | - | - | - | - | - | - | R | - | R |

Notes:

1. SaveKit.Core itself currently has its own LogKit/UniTask dependencies; that does not propagate into WorldKit.Core.
2. ToolsHub modules are intentionally omitted from this runtime matrix. Runtime Core never depends on Editor tooling.
3. Resource/Feature modules may depend on WorldGenKit.Core, but WorldGenKit.Core must not depend back on them.

---

## 5. Dependency Direction

Allowed:

```text
Core Kit
   ↑
Adapter / Integration
   ↓
Other Core Kit
```

Examples:

```text
PathKit.Core ← PathKit.GridKitAdapter → GridKit.Core

WorldKit.Core ← WorldKit.SaveKitAdapter → SaveKit.Core

WorldGenKit.Core ← WorldGenKit.WorldKitAdapter → WorldKit.Core
```

Forbidden:

```text
WorldKit.Core -> GridKit.Core
WorldKit.Core -> SaveKit.Core
WorldKit.Core -> WorldGenKit.Core
WorldGenKit.Core -> WorldKit.Core
PathKit.Core -> WorldKit.Core
GridKit.Core -> UnityEngine
```

unless a future ADR explicitly replaces this contract.

---

## 6. Terrain / Mesh -> Grid Projection Contract

This is a first-class independent GridKit use case.

### 6.1 Ownership

Core logical data belongs to GridKit.

Unity scene sampling belongs to:

`GridKit.UnityProjectionAdapter`

Editor visualization/painting belongs to:

`ToolsHub.GridKit` or a GridKit Editor module.

Path search belongs to:

`PathKit.GridKitAdapter`.

### 6.2 Pipeline

```text
Terrain / Mesh / Scene Collider Source
             ↓
      Projection Sampler
             ↓
       Base Bake Layer
     height / slope / hit
   walkability / base cost
             ↓
      Manual Override Layer
 force blocked / force walkable
 movement cost / semantic mask
             ↓
        Final Grid State
             ↓
      Traversal Policy
             ↓
   PathKit.GridKitAdapter
```

### 6.3 Re-bake rule

Never flatten authored overrides into auto-bake data.

Required composition:

```text
AutoBakeBase + ManualOverride = FinalTraversalState
```

Re-baking replaces only `AutoBakeBase`.

Manual overrides survive unless the user explicitly clears or remaps them.

### 6.4 Candidate data contract

Do not force all projects to store all values.

Typical baked channels/layers may include:

- height;
- slope;
- base walkable;
- movement cost;
- surface hit type;
- obstacle mask;
- custom semantic masks.

GridKit.Core should not hardcode Unity Physics results into every cell type.

The adapter/project decides which baked data object/storage to use.

### 6.5 Manual override semantics

Minimum operations:

- Default / no override
- ForceWalkable
- ForceBlocked
- OverrideCost

Optional project masks may add domain semantics, but those semantics are not GridKit.Core concepts.

---

## 7. P1 Grid Topology Direction

Current square APIs are retained for compatibility.

P1 adds topology capability rather than replacing all current GridKit types.

### 7.1 Built-in V1 topology

- Orthogonal 4-way
- Orthogonal 8-way
- Hex

### 7.2 Topological elements

World/strategy/building use cases require distinguishing:

- Cell
- Edge
- Vertex

Examples:

- terrain/resource/city -> Cell
- river/wall/road boundary -> Edge
- corner/junction marker -> Vertex

### 7.3 API principle

Algorithms that only need topology should consume topology behavior rather than:

```csharp
Check(x + 1, y);
Check(x - 1, y);
```

However, performance-sensitive built-in topology implementations should avoid allocation-heavy polymorphism in inner loops.

Expected model:

- clear public topology contracts;
- specialized built-in square/hex implementations;
- caller-owned buffers / spans where practical;
- no iterator allocation in generation hot paths.

---

## 8. WorldKit.Core Contract Shape — P0 Draft

These are semantic contracts to freeze before implementation. Exact member names may still change during P0 review.

### 8.1 Identity

Required value objects:

- `WorldId`
- `WorldChunkCoord`
- `WorldRegionCoord`
- `WorldDataLayerId`
- `WorldDeltaId` or equivalent versioned identity where needed.

Rules:

- stable value semantics;
- no UnityEngine value types in Core;
- negative chunk/region coordinates supported;
- zero/default invalid only when that materially improves diagnostics; coordinate zero itself remains valid.

### 8.2 Extent

World extent must distinguish:

- finite;
- infinite.

Finite bounds are explicit.

Infinite world must not fabricate `int.MaxValue` bounds as a fake finite map.

### 8.3 Chunk lifecycle

Initial lifecycle target:

```text
Unloaded
  -> Metadata
  -> DataReady
  -> Active
  -> DataReady / Metadata / Unloaded
```

Presentation and simulation activation are adapter/integration concerns, not hardcoded gameplay behavior.

Invalid transitions must fail explicitly.

### 8.4 World data

WorldKit owns registration/ownership/access of world/chunk data layers.

It does not define fixed:

```text
HeightMap
BiomeMap
ResourceMap
```

Those are content/data definitions supplied by WorldGenKit or applications.

### 8.5 Delta

WorldKit defines semantic world-change records/sets and versioning boundary.

It does not:

- write JSON;
- choose file paths;
- choose cloud/local storage;
- perform SaveKit migrations directly.

Those belong to SaveKit adapter/integration.

---

## 9. WorldGenKit.Core Contract Shape — P0 Draft

### 9.1 Channel identity

Authoring identity:

`WorldDataChannelId`

Examples:

- `terrain.height`
- `soil.fertility`
- `game.magic_density`

Stable ID validation happens at registration/compile boundaries.

### 9.2 Typed runtime handle

Hot paths must not repeatedly resolve:

```csharp
Dictionary<string, object>
```

Conceptual runtime handle:

```csharp
ChannelHandle<T>
```

Properties:

- typed;
- compact indexed identity after pipeline compilation;
- equality/value semantics;
- invalid/default state detectable.

### 9.3 Storage

Required storage capabilities:

- Dense
- Sparse
- Chunked
- Constant
- Computed / Derived
- External

These are capabilities, not necessarily one giant inheritance hierarchy.

The design must allow a channel to omit storage entirely if unused.

### 9.4 Generation stage

Conceptual contract:

```text
StageId
Requires[]
OptionalInputs[]
Produces[]
Mutates[]
SeedScope
Execute(context)
```

Pipeline compiler resolves dependencies before generation.

Runtime stage execution must not discover dependencies through reflection.

### 9.5 Determinism

Stage seed derivation must use stable framework hashing.

Forbidden:

- `string.GetHashCode()` as deterministic persistent identity;
- shared mutable random stream whose output depends on chunk generation order.

Required conceptual derivation:

```text
StableHash(WorldSeed, Chunk/RegionCoord, StageStableId, optional local key)
```

### 9.6 Rule

Rules read typed channel/context data and produce:

- bool eligibility;
- numeric score/weight;
- or a documented typed result.

Do not hardcode biome logic into WorldGenKit.Core.

---

## 10. Layer / Occupancy Contract Shape — P0 Draft

### 10.1 Layer

Layer means semantic world-data/content layer, not Unity LayerMask.

Built-in/default IDs may include:

- ground;
- surface;
- vegetation;
- mineral;
- underground;
- decoration;
- building;
- road;
- water.

Projects may register additional layers.

### 10.2 Occupancy

Occupancy expresses whether two claims can coexist at the same logical area.

It must support:

- category/layer conflict;
- footprint reservation;
- optional overlap;
- priority/resolution policy.

Resource systems and PlacementKit consume this shared contract or an adapter around it.

Avoid hardcoded checks such as:

```csharp
if (hasTree && placingBuilding) ...
```

---

## 11. Resource Contract Shape — P0 Draft

Resource definition uses stable semantic IDs.

Generation parameters are distinct:

- occurrence/density;
- coverage;
- cluster/vein size;
- richness/amount;
- spacing;
- quota/budget.

Player exposure metadata is part of configuration/definition, not a mutation of base rules.

Effective generation rule is layered:

```text
Definition
  * ProjectProfile
  * WorldPreset
  * PlayerGenerationSettings
  * ScenarioModifier
```

Default mutation policy for already generated worlds:

`NewChunksOnly`.

---

## 12. Feature / POI Contract Shape — P0 Draft

Unified semantic model:

- Landmark
- Area Feature
- Compound Feature

Required concepts:

- stable FeatureId;
- candidate score;
- deterministic seed;
- footprint/reservation;
- quota/count/uniqueness;
- distance/adjacency constraints;
- typed channel conditions;
- optional terrain adaptation request;
- resulting semantic Feature instance data.

Feature Core result must not directly instantiate a prefab.

Unity adapters perform presentation.

---

## 13. PlacementKit.Core Contract Shape — P0 Draft

PlacementKit answers:

> Can this thing be placed here under this policy/context, and why?

Required concepts:

- `PlacementRequest`
- `PlacementFootprint` or footprint boundary
- `PlacementRule`
- `PlacementContext`
- `PlacementResult`
- explicit `PlacementFailureReason`
- optional connection requirements

PlacementKit does not own:

- building prefab lifecycle;
- economy;
- construction time;
- worker AI;
- inventory consumption.

Grid and World lookups arrive through adapters/policies.

---

## 14. ToolsHub Boundary

ToolsHub may:

- create/edit definitions;
- preview topology;
- bake Terrain/Mesh into grid data;
- paint manual overrides;
- compile/validate pipelines;
- show heatmaps;
- inspect chunks/deltas/memory.

Runtime Core must not:

- reference Editor assemblies;
- require ScriptableObject to represent every definition;
- depend on a ToolsHub-generated scene singleton.

ScriptableObject is an authoring/persistence option in Unity integration, not the only Core data representation.

---

## 15. P0 Tiny Foundation Integration Matrix

Before P0 can close, run actual Unity EditMode tests for at least:

- `GridKitTests`
- `PathKitCoreTests`
- `PathKitGridKitAdapterTests`
- `SpatialKitTests`
- `SimulationKitTests`
- `SaveKitCoreTests`

and run distribution/policy tests relevant to standalone Kit export when the new profiles/asmdefs are added.

Current baseline validation status after the first P0 foundation run:

| Test group | Status |
| --- | --- |
| GridKitTests | RUN / PASS — 17/17 |
| PathKitCoreTests | RUN / PASS — 15/15 |
| PathKitGridKitAdapterTests | RUN / PASS — 11/11 |
| SpatialKitTests | RUN / PASS — 13/13 |
| SimulationKitTests | RUN / PASS — 17/17 |
| SaveKitCoreTests | RUN / PASS — 29/29 |
| TimeKitTests | RUN / PASS — 6/6 |

Foundation baseline total:

**108/108 PASS, 0 failed, 0 skipped.**

Additional P0 architecture policy:

- `WorldFrameworkFoundationBoundaryTests`: RUN / PASS — 3/3.

Current verified P0 total:

**111/111 PASS, 0 failed, 0 skipped.**

The new policy test initially exposed a test-source string escaping compile error. That source error was corrected,
Unity then compiled with 0 errors / 0 warnings, and the policy test was rerun to the PASS result above.

No PASS may be inferred from existing test source.

---

## 16. P0 Exit Gates

P0 is complete only when:

1. dependency matrix is accepted and represented in planned distribution profiles;
2. independent Kit use cases remain possible;
3. Terrain/Mesh -> Grid projection boundary is frozen;
4. Grid topology ownership is frozen in GridKit;
5. WorldKit / WorldGenKit / PlacementKit responsibilities have no circular dependency;
6. Core contract shapes are documented;
7. foundation regression tests have actually run and passed;
8. compile status is clean;
9. Development Status ledger is updated with exact results.

Until all gates pass, Runtime world implementation stays blocked by design.

---

## 17. Planned distribution profiles — frozen P0 boundary

These IDs are the planned package/export identities. They are **not** added to
`KitDistributionCatalog.json` until the corresponding source/asmdef actually exists and can be validated.

| Profile ID | Tier | Category | Required profiles |
| --- | --- | --- | --- |
| `worldkit.core` | foundation | world | none |
| `worldgenkit.core` | extension | world | none |
| `worldgenkit.resource` | extension | world | worldgenkit.core |
| `worldgenkit.feature` | extension | world | worldgenkit.core |
| `placementkit.core` | foundation | world | none |
| `gridkit.unity-projection` | adapter | world | gridkit |
| `worldkit.gridkit` | adapter | world | worldkit.core, gridkit |
| `worldkit.spatialkit` | adapter | world | worldkit.core, spatialkit |
| `worldkit.savekit` | adapter | world | worldkit.core, savekit.core |
| `worldkit.simulationkit` | adapter | world | worldkit.core, simulationkit |
| `worldgenkit.worldkit` | adapter | world | worldgenkit.core, worldkit.core |
| `worldgenkit.gridkit` | adapter | world | worldgenkit.core, gridkit |
| `placementkit.gridkit` | adapter | world | placementkit.core, gridkit |
| `placementkit.worldkit` | adapter | world | placementkit.core, worldkit.core |

Presentation/import profiles are added only with real implementations, e.g.:

- `worldgenkit.tilemap`
- `worldgenkit.mesh`
- `worldgenkit.unity-terrain`
- `worldgenkit.heightmap-import`

ToolsHub profiles remain Editor/tooling and are not runtime dependencies.

P0 rule:

> A profile becomes `availability=available` only after its source closure, asmdef closure, standalone export validation,
> compile, tests and required docs/sample closure actually pass.

---

## 18. P0 Freeze Result

P0 architecture is considered **FROZEN / PASS** for starting P1 because:

- existing Foundation boundaries were audited;
- independent-Kit minimum use cases were frozen;
- dependency direction was frozen;
- Terrain/Mesh -> Grid adapter ownership was frozen;
- concrete Core API shape was pressure-reviewed;
- two material design issues were found and corrected before Runtime coding;
- existing foundation behavior baseline passed;
- architecture policy gates passed;
- UnitySkills final diagnostics reported 0 errors / 0 warnings;
- `git diff --check` passed.

P0 freeze does not claim any not-yet-created WorldKit/WorldGenKit/PlacementKit runtime code has passed validation.

