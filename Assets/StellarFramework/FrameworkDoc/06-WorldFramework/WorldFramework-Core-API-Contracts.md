# StellarFramework World Framework — Core API Contracts

> Status: Stable contract  
> Purpose: define the stable public API shape and compatibility boundaries for Runtime implementations.

This document intentionally freezes **boundaries and semantics**, not every internal data structure.
Storage hot-path details that require benchmarks remain implementation decisions rather than public contract.

---

## 1. Namespace and assembly policy

Large new systems use dedicated namespaces to avoid further pollution of the root `StellarFramework` namespace.

Planned:

```text
Assembly:  StellarFramework.WorldKit.Core
Namespace: StellarFramework.WorldKit

Assembly:  StellarFramework.WorldGenKit.Core
Namespace: StellarFramework.WorldGenKit

Assembly:  StellarFramework.PlacementKit.Core
Namespace: StellarFramework.PlacementKit
```

Adapters use explicit sub-namespaces:

```text
StellarFramework.WorldKit.Adapters.GridKit
StellarFramework.WorldKit.Adapters.SaveKit
StellarFramework.WorldGenKit.Adapters.WorldKit
StellarFramework.PlacementKit.Adapters.GridKit
```

Rules:

- Core assemblies use `noEngineReferences=true` unless an ADR explicitly changes it.
- Runtime Core never references Editor assemblies.
- No runtime assembly scanning/reflection discovery.
- Explicit registration only.

---

## 2. Stable ID convention

Stable authored/persisted IDs use validated ordinal strings.

Examples:

```text
world.main
terrain.height
terrain.surface
resource.iron_ore
feature.village
layer.vegetation
```

Do not use CLR type names, enum ordinals, array positions or `string.GetHashCode()` as persistent identity.

Canonical V1 format:

- lower-case;
- dot-separated semantic segments;
- each segment uses letters, digits and underscore;
- examples: `terrain.height`, `feature.ancient_tower`, `resource.iron_ore`.

Hyphenated/external IDs may be accepted only by an explicit adapter/import policy; Core-authored framework IDs use the canonical form above.

Recommended shape:

```csharp
public readonly struct WorldId : IEquatable<WorldId>
{
    public const int MaxLength = 128;
    public string Value { get; }
    public bool IsValid { get; }

    public static WorldId From(string value);
    public static bool TryCreate(string value, out WorldId result, out string error);
}
```

Equivalent validation style is used for:

- `WorldDataLayerId`
- `WorldDataChannelId`
- `WorldGenerationStageId`
- `WorldRuleId`
- `WorldLayerId`
- `ResourceDefinitionId`
- `WorldFeatureId`
- future stable Placement definition IDs where required.

Runtime-only handles use compact numeric identities instead.

---

## 3. WorldKit.Core — identity and planar coordinate contract

V1 formally implements planar finite/infinite worlds.

Future spherical/voxel implementations are extensions, not fake modes inside the planar coordinate structs.

### 3.1 WorldChunkCoord

```csharp
public readonly struct WorldChunkCoord : IEquatable<WorldChunkCoord>
{
    public long X { get; }
    public long Y { get; }

    public WorldChunkCoord(long x, long y);
}
```

Decisions:

- `(0,0)` is valid.
- negative coordinates are valid.
- use `long` so the logical world is not coupled to Unity float range or an artificial int-sized map.
- no Unity `Vector2Int`.

### 3.2 WorldRegionCoord

Same rules as `WorldChunkCoord`.

Region is a logical organization/generation domain, not necessarily a resident object.

### 3.3 WorldPoint2D

Continuous logical planar coordinate:

```csharp
public readonly struct WorldPoint2D : IEquatable<WorldPoint2D>
{
    public double X { get; }
    public double Y { get; }
}
```

Purpose:

- continuous world queries;
- floating-origin-ready logical coordinates;
- Unity adapter maps logical coordinates to local `Vector3`.

Height/elevation remains world data unless a specific adapter/domain requires a 3D logical point.

Do not treat Unity Transform position as authoritative infinite-world identity.

---

## 4. World extent

```csharp
public enum WorldExtentKind
{
    Finite = 0,
    Infinite = 1
}
```

Conceptual:

```csharp
public readonly struct WorldExtent
{
    public WorldExtentKind Kind { get; }
    public bool IsFinite { get; }
    public bool IsInfinite { get; }
    public WorldChunkBounds FiniteChunkBounds { get; }
}
```

Rules:

- requesting finite bounds from an infinite extent must fail loudly or use a `TryGet...` API;
- infinite worlds must not masquerade as a huge finite rectangle;
- finite bounds use half-open semantics consistent with existing GridKit conventions where practical.

---

## 5. Chunk lifecycle

V1 lifecycle is deliberately infrastructure-oriented.

```csharp
public enum WorldChunkState
{
    Unloaded = 0,
    Metadata = 1,
    DataReady = 2,
    Active = 3
}
```

Meaning:

- `Unloaded`: no resident chunk data.
- `Metadata`: lightweight metadata may exist.
- `DataReady`: world data required for normal queries is resident.
- `Active`: chunk is activated by the owning runtime policy.

`Active` does **not** mean:

- GameObjects must exist;
- simulation must be running;
- rendering must be enabled.

Those are adapter/domain decisions.

### 5.1 Explicit transition result

```csharp
public enum WorldChunkTransitionError
{
    None = 0,
    InvalidChunk,
    InvalidTransition,
    AlreadyInState,
    DataUnavailable,
    Busy
}
```

```csharp
public readonly struct WorldChunkTransitionResult
{
    public bool Success { get; }
    public WorldChunkTransitionError Error { get; }
    public WorldChunkState PreviousState { get; }
    public WorldChunkState CurrentState { get; }
}
```

No swallowed transition failures.

---

## 6. WorldKit data layer registration

WorldKit owns world/chunk data organization but not fixed terrain concepts.

Forbidden fixed WorldKit fields:

```text
HeightMap
BiomeMap
WaterMap
ResourceIndex
BuildingIndex
```

### 6.1 Stable authoring identity

```csharp
WorldDataLayerId
```

### 6.2 Compiled typed runtime identity

Conceptual:

```csharp
public readonly struct WorldDataLayerHandle<T> : IEquatable<WorldDataLayerHandle<T>>
{
    public int Index { get; }
    public int RegistryGeneration { get; }
    public bool IsValid { get; }
}
```

Registration boundary:

```csharp
WorldDataLayerHandle<T> Register<T>(WorldDataLayerId id, ...);
```

Properties:

- stable strings are resolved during registration/build;
- hot paths use typed handles;
- no per-query string dictionary lookup;
- type mismatch is detected at registration/access boundary.
- a handle is valid only against the registry generation that created it.

This prevents a handle from one world schema accidentally addressing the same numeric slot in another schema.

Exact storage accessor API is intentionally kept outside the public contract and may evolve with performance evidence.

---

## 7. World delta contract

WorldKit owns semantic world changes, not persistence implementation.

Required concepts:

```text
WorldDelta
WorldDeltaSet
WorldDeltaVersion
Target World / Region / Chunk
```

Rules:

- delta types are versionable;
- deltas are deterministic/applicable in documented order;
- SaveKit adapter serializes/migrates them;
- WorldKit.Core does not know JSON, file paths, cloud storage or serializers.

Conceptual application:

```text
Base Data
  + Authoring Override
  + Runtime Delta
  + Persistent Patch
  = Final World State
```

---

## 8. WorldGenKit.Core — Channel identity

### 8.1 WorldDataChannelId

Stable authored identity:

```text
terrain.height
terrain.moisture
soil.fertility
game.magic_density
```

### 8.2 ChannelHandle<T>

```csharp
public readonly struct ChannelHandle<T> : IEquatable<ChannelHandle<T>>
{
    public int Index { get; }
    public int RegistryGeneration { get; }
    public bool IsValid { get; }
}
```

Rules:

- handle is created only by channel registration/compiled pipeline;
- generic `T` is part of runtime type safety;
- default handle is invalid;
- no boxing required for normal typed access;
- stable string lookup does not occur in per-sample hot paths.
- cross-registry / cross-plan handle use must be detectable instead of silently hitting an equal numeric index.

---

## 9. Channel registration

Conceptual builder:

```csharp
public sealed class WorldChannelRegistryBuilder
{
    public ChannelHandle<T> Register<T>(
        WorldDataChannelId id,
        WorldChannelStorageDescriptor storage);
}
```

Registration failures:

- invalid stable ID;
- duplicate ID;
- same ID registered with different value type;
- unsupported storage descriptor;
- incompatible scope/storage combination.

No silent replacement.

---

## 10. Storage capability model

V1 contract recognizes:

```csharp
public enum WorldChannelStorageKind
{
    Dense = 0,
    Sparse = 1,
    Chunked = 2,
    Constant = 3,
    Computed = 4,
    External = 5
}
```

Semantics:

- Dense: values for most samples in a generation domain.
- Sparse: only exceptional/non-default entries.
- Chunked: independently pageable chunk storage.
- Constant: one value over a domain.
- Computed: derived on demand/cache policy.
- External: data supplied by an imported/project source.

Important:

> This enum does not require one giant virtual `IChannelStorage<T>` to be used in every hot loop.

The concrete high-performance accessor design is an implementation concern validated by benchmarks.

Performance validation must cover:

- direct dense array access;
- sparse lookup;
- compiled handle resolution;
- chunk page acquisition;
- allocation behavior.

---

## 11. Generation Stage contract

Stage registration is explicit.

No reflection discovery.

Conceptual public contract:

```csharp
public interface IWorldGenerationStage
{
    WorldGenerationStageId Id { get; }

    void Describe(WorldGenerationStageDescriptorBuilder builder);

    WorldGenerationStageResult Execute(
        in WorldGenerationContext context);
}
```

The exact context storage access API remains an implementation decision.

### 11.1 Descriptor semantics

Stage declares:

```text
Requires
OptionalInputs
Produces
Mutates
SeedScope
```

Example:

```text
SlopeStage
Requires: terrain.height<float>
Produces: terrain.slope<float>
```

### 11.2 Compile-time validation

Pipeline compiler must detect:

- missing required producer/source;
- duplicate producer when not explicitly permitted;
- circular dependency;
- incompatible channel type;
- invalid stage ID;
- stage requiring its own later output;
- illegal mutation ownership.

Optional diagnostics:

- unused produced channel;
- redundant stage;
- unreachable branch/profile output.

---

## 12. Pipeline contract

Conceptual:

```csharp
public sealed class WorldGenerationPipelineBuilder
{
    public WorldChannelRegistryBuilder Channels { get; }
    public void AddStage(IWorldGenerationStage stage);
    public WorldGenerationCompileResult Compile();
}
```

Compile result:

```csharp
public readonly struct WorldGenerationCompileResult
{
    public bool Success { get; }
    public WorldGenerationPlan Plan { get; }
    public WorldGenerationDiagnostic[] Diagnostics { get; }
}
```

Compiled `WorldGenerationPlan`:

- immutable after compilation;
- deterministic execution order;
- channel handles resolved;
- dependency graph validated;
- stable plan identity/hash where needed for save/version diagnostics.

No late runtime stage scanning.

---

## 13. Deterministic seed contract

Persistent generation determinism uses framework-owned stable hashing.

Conceptual:

```csharp
public readonly struct WorldGenerationSeed
{
    public ulong Value { get; }
}
```

Derivation:

```text
WorldSeed
 + World/Chunk/Region logical key
 + StageStableId
 + optional feature/resource local key
 = DerivedSeed
```

Requirements:

- chunk generation order does not affect output;
- stage order changes only affect stages whose inputs/plan actually changed;
- cross-session persistent identity does not use `string.GetHashCode()`;
- Unity `Random` global state is not the authoritative source.

---

## 14. Stage result / failure semantics

No boolean-only ambiguous failure.

Conceptual:

```csharp
public enum WorldGenerationStageStatus
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2
}
```

Result includes:

- status;
- stage ID;
- stable diagnostic/error code;
- optional bounded diagnostic text;
- produced/dirty scope metadata where useful.

Programming invariant violations throw.

Expected data/validation failures return explicit results.

Do not convert exceptions into fake success.

---

## 15. Rule contract

Rule system must support two broad outcomes:

1. eligibility: can this candidate exist?
2. score/weight: how suitable is this candidate?

Do not force every rule through a single `object Evaluate(object)`.

Initial built-in semantic families:

- Range
- Threshold
- Curve
- Inverse
- Noise
- Distance
- Tag
- WeightedSum
- Multiply
- Min
- Max
- And
- Or

Custom projects may register code-defined rules.

Rules read registered channels/context through explicit handles/references.

---

## 16. Layer / category contract

`WorldLayerId` is a WorldGen semantic content/reservation layer ID.

It is not:

- Unity LayerMask;
- a fixed enum;
- a single-value resource slot.

Default definitions may include:

```text
layer.ground
layer.surface
layer.vegetation
layer.mineral
layer.underground
layer.decoration
layer.building
layer.road
layer.water
```

Projects may register custom layers.

Important modularity rule:

> `WorldLayerId` is **not** a universal dependency that PlacementKit.Core or GridKit.Core must import.

Independent Kits may own their equivalent typed IDs:

- GridKit: existing grid occupancy/occupant concepts;
- WorldGen resource/feature modules: generation reservation/content layers;
- PlacementKit.Core: placement categories/claims.

When Kits are composed, an Adapter maps semantically aligned stable IDs/policies.

This intentionally trades a tiny amount of adapter mapping for correct standalone dependency boundaries.

---

## 17. Occupancy / reservation contract

Occupancy represents claims over logical space, but there is no mandatory cross-Kit occupancy assembly in V1.

Conceptual:

```csharp
public readonly struct WorldGenerationOccupancyClaim
{
    public WorldLayerId Layer { get; }
    public int Priority { get; }
    // footprint/reference supplied by owning adapter/domain
}
```

Resolver responsibilities:

- allow overlap;
- reject conflict;
- choose priority where configured;
- reserve future feature/placement space;
- report explicit conflict reason.

Examples:

```text
Vegetation + UndergroundMineral -> allowed
Building + Vegetation           -> usually conflict
Decoration + Road               -> policy controlled
```

WorldGen Resource/Feature modules consume the generation-side claim/reservation contract.

PlacementKit.Core owns its own placement claim/rule semantics.

GridKit retains `GridOccupancy` for grid-local occupancy.

Integration profiles bridge these systems explicitly.

This avoids:

```text
PlacementKit.Core -> WorldGenKit.Core
GridKit.Core      -> WorldGenKit.Core
```

Exact geometry representation is intentionally not frozen because Grid and continuous placement require different adapters.

---

## 18. Resource module API boundary

Resource generation lives above WorldGenKit.Core.

Stable:

```text
ResourceDefinitionId
ResourceDefinition
ResourceSpawnCandidate
ResourceSpawnRecord
ResourceGenerationSettings
```

Definition parameters remain distinct:

- occurrence/density;
- coverage;
- cluster/vein size;
- richness/amount;
- spacing;
- quota/budget.

Player settings never mutate the base definition.

---

## 19. Feature / POI API boundary

Stable:

```text
WorldFeatureId
WorldFeatureDefinition
WorldFeatureCandidate
WorldFeatureReservation
WorldFeatureInstanceData
```

Feature kinds:

- Landmark
- Area
- Compound

The semantic output contains:

- feature ID;
- logical location;
- orientation/parameters where applicable;
- deterministic local seed;
- reservation/footprint reference;
- generated sub-data.

It does not instantiate a Unity prefab.

---

## 20. PlacementKit.Core — universal semantics

The contract intentionally does **not** force one universal footprint geometry on all projects.

A square building footprint, a freeform 3D foundation and a Hex district do not need the same Core geometry type.

The stable contract includes:

```text
PlacementRequest
PlacementContext
PlacementRule
PlacementResult
PlacementFailureReason
PlacementConnectionRequirement
```

Conceptual:

```csharp
public interface IPlacementRule
{
    PlacementRuleId Id { get; }
    PlacementRuleResult Evaluate(
        in PlacementContext context,
        in PlacementRequest request);
}
```

Adapters supply:

- Grid footprint;
- world query;
- slope/water/zone data;
- continuous geometry;
- connection ports.

PlacementKit.Core owns rule composition and explicit result semantics.

PlacementKit must define its own stable placement category/claim IDs rather than referencing `WorldLayerId`.
Adapters may map equivalent stable semantic values when combined with WorldGen/Grid systems.

---

## 21. Placement result semantics

Result must explain failure without log parsing.

Conceptual:

```csharp
public enum PlacementFailureReason
{
    None = 0,
    InvalidRequest,
    OutOfBounds,
    Occupied,
    Blocked,
    SlopeExceeded,
    WaterRuleFailed,
    ZoneRuleFailed,
    MissingConnection,
    CustomRuleFailed
}
```

The enum may be refined compatibly; the important contract is:

> Failure is structured and extensible. A generic `false` is not sufficient.

Result should expose the first failure and optionally bounded diagnostics/all failures for editor probing.

---

## 22. Terrain/Mesh -> Grid adapter API boundary

Planned assembly:

`StellarFramework.GridKit.UnityProjectionAdapter`

Unity-facing, therefore engine references are allowed here.

Core output remains project-neutral.

Conceptual source:

```csharp
public interface IGridProjectionSource
{
    bool TrySample(..., out GridProjectionSample sample);
}
```

Unity implementations may sample:

- Terrain;
- MeshCollider;
- Physics scene;
- custom height surface.

Conceptual sample:

```text
Hit
Height
Slope
Surface category/mask
Obstacle state
```

The adapter converts samples into an application-owned bake layer.

GridKit.Core does not gain Terrain/Mesh/Collider concepts.

---

## 23. Manual walkability override contract

Minimum semantic values:

```csharp
public enum GridWalkabilityOverride
{
    None = 0,
    ForceWalkable = 1,
    ForceBlocked = 2
}
```

Movement cost override is separate so walkability and cost are not conflated.

Composition:

```text
AutoBakeBase
  + ManualWalkabilityOverride
  + ManualCostOverride
  = FinalTraversalState
```

Rebake only replaces `AutoBakeBase`.

---

## 24. Adapter to current PathKit

Do not change PathKit.Core for Terrain navigation.

The final grid traversal state implements/adapts:

```csharp
IGridPathTraversalPolicy
```

Current PathKit Grid adapter remains responsible for converting grid traversal into `IPathGraph`.

Hex support may introduce a topology-aware adapter alongside the current square adapter rather than breaking the frozen V1 square API.

---

## 25. Error handling policy

Use three distinct categories:

### Programmer/invariant error

Examples:

- duplicate impossible registration in code path that promised uniqueness;
- invalid compiled handle used against another plan;
- internal state corruption.

Action:

> throw; do not hide.

### Expected validation/configuration failure

Examples:

- missing channel producer;
- invalid rule reference;
- unsupported storage selection.

Action:

> explicit compile/validation result with diagnostics.

### Runtime world operation failure

Examples:

- chunk cannot transition;
- placement blocked;
- requested chunk data unavailable.

Action:

> explicit typed result/error.

Never catch-all and convert to Success.

---

## 26. Allocation policy

Public API design must allow:

- caller-owned buffers;
- `Span<T>` / `ReadOnlySpan<T>` where Unity/C# compatibility allows;
- reusable workspaces for path/generation jobs;
- pooled temporary buffers where ownership is explicit.

Avoid in hot paths:

- LINQ;
- iterator/yield allocations;
- per-cell dictionaries;
- per-cell boxed values;
- per-cell string lookup;
- reflection;
- implicit temporary lists.

Jobs/Burst are not required in Core API V1.

Data layout must remain compatible with future Jobs/Burst adapters.

---

## 27. Threading policy

Core contracts must not assume Unity main thread unless the adapter explicitly requires it.

Examples:

- channel generation may run off main thread when storage/stage allows;
- Terrain/Mesh Unity sampling adapter may require main thread;
- presentation adapters generally require main thread;
- SaveKit adapter follows serializer/storage capability rules.

Thread-safety is explicit metadata/capability, never assumed.

---

## 28. Versioning policy

Persisted procedural worlds must record enough metadata to explain/reproduce generation:

```text
WorldId
WorldSeed
WorldProfileId
WorldProfileVersion
GeneratorVersion
Compiled Plan identity/hash where useful
Resolved player generation settings
Resource/Feature overrides
```

Generator version changes must not silently rewrite existing generated/modified chunks.

Migration/regeneration is explicit.

---

## 29. What is intentionally NOT frozen

These are deferred because premature freezing would create unnecessary complexity:

1. Exact Dense/Sparse/Chunked accessor class hierarchy.
2. Exact backing array/native container choice.
3. Jobs/Burst scheduling API.
4. Universal Placement footprint geometry.
5. Voxel storage API.
6. Planet topology API.
7. Road/traffic graph APIs.
8. Village internal procedural layout API.
9. Full ToolsHub UI data model.

They must respect the boundaries defined in this document when implemented later.

---

## 30. Contract acceptance tests

Policy tests verify:

- WorldKit.Core asmdef has no Grid/Save/WorldGen dependency.
- WorldGenKit.Core asmdef has no WorldKit/Grid/Unity dependency.
- PlacementKit.Core asmdef has no Grid/World dependency.
- existing Foundation Kits do not acquire World stack dependencies.
- adapters reference only their declared Core sides.
- Runtime Core contains no `using UnityEngine`.
- Runtime Core contains no reflection/assembly scanning APIs.

The first existing-Foundation boundary test is already present:

`WorldFrameworkFoundationBoundaryTests`.

Tests for new assemblies become executable only after those assemblies are created.

