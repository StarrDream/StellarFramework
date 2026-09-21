# StellarFramework World Framework — P0 Design Pressure Review

> Status: REVIEWED  
> Purpose: challenge the P0 contracts against representative project shapes before Runtime implementation.

---

## 1. Review rule

The architecture is rejected if a representative case requires:

- modifying an unrelated Core Kit;
- pulling in the whole World stack for a small feature;
- per-cell dynamic object/string lookup in a hot path;
- UnityEngine types in pure Core;
- a game-domain concept in WorldKit/WorldGenKit Core.

---

## 2. PathKit-only project

Need:

- custom waypoint / road / room graph;
- A* or Dijkstra.

Required:

`PathKit.Core`

Result: **PASS**

Current `IPathGraph` already provides the correct generic boundary.

---

## 3. Existing Terrain/Mesh -> manually editable navigation grid

Need:

- existing Terrain/Mesh;
- grid projection;
- slope/obstacle auto-bake;
- manual blocked/walkable/cost paint;
- optional pathfinding.

Required:

```text
GridKit.Core
GridKit.UnityProjectionAdapter
[optional] PathKit.Core + PathKit.GridKitAdapter
```

Result: **PASS**

Required composition:

```text
AutoBakeBase + ManualOverride = FinalTraversalState
```

WorldKit/WorldGenKit are not required.

---

## 4. Stardew-like fixed Tilemap

Need:

- finite authored map;
- runtime ground/crop/object changes.

Result: **PASS**

WorldGenKit can be omitted when procedural/import pipeline capabilities are unnecessary.

---

## 5. RimWorld-like square simulation map

Need:

- square grid;
- multiple logical data layers;
- pathfinding;
- simulation;
- occupancy.

Result: **PASS**

WorldKit does not need to duplicate Grid storage, occupancy or pathfinding.

---

## 6. Civilization-like Hex map

Need:

- Hex cell topology;
- Cell/Edge/Vertex semantics;
- rivers on edges;
- resources/features on cells.

Result: **PASS WITH P1 REQUIREMENT**

Current GridKit still needs generic topology + Hex + Cell/Edge/Vertex work.
That belongs to GridKit P1, not WorldKit.

---

## 7. Factorio-like infinite planar world

Need:

- infinite logical coordinates;
- deterministic chunk generation;
- generated-on-demand resources;
- save only modifications;
- floating origin where required.

Result: **PASS**

Supporting P0 decisions:

- `WorldChunkCoord(long,long)`;
- finite/infinite extent distinction;
- stable per-stage/per-chunk seed derivation;
- WorldKit Delta separate from SaveKit storage;
- Unity position is not authoritative logical identity.

---

## 8. Banished / Settlement Survival-like 3D settlement world

Need:

- 3D heightfield world;
- resources;
- buildable areas;
- farms/villages;
- pathfinding/simulation.

Result: **PASS**

Separation:

- terrain/resource generation -> WorldGenKit;
- building legality -> PlacementKit;
- path -> PathKit;
- production/citizen simulation -> domain systems + SimulationKit.

---

## 9. Taxi / manually authored continuous city

Need:

- large continuous city;
- road graph;
- spatial queries;
- optional chunk streaming;
- no procedural generation requirement.

Result: **PASS**

Possible composition:

```text
WorldKit
SpatialKit
PathKit
future Road/Traffic domain
```

GridKit and WorldGenKit may both be omitted.

---

## 10. Terraria-like finite 2D block world

Need:

- X/Y block storage;
- foreground/background/liquid/wire layers;
- procedural caves/features/resources.

Result: **PASS AS ADAPTER/EXTENSION**

WorldGen Stage/Channel/Feature contracts remain useful.
A dedicated block storage/presentation adapter is required instead of a heightfield adapter.

---

## 11. Dyson-like spherical planets

Result: **FUTURE-COMPATIBLE, NOT V1 IMPLEMENTED**

P0 deliberately treats planar coordinates as the V1 implementation instead of pretending one coordinate type covers planets.
Future Planet extensions own spherical topology while reusing compatible stable-ID/generation contracts.

---

## 12. Minecraft-like 3D voxel world

Result: **FUTURE-COMPATIBLE, NOT V1 IMPLEMENTED**

Future Voxel extensions own 3D chunk coordinates/storage.
Applicable generation/determinism/version concepts remain reusable.

---

## 13. Cross-plan typed handle review

Risk found in the first draft:

```text
ChannelHandle<T> = Index only
```

A handle from Plan A could accidentally use the same slot number in Plan B.

Correction:

```text
Index + RegistryGeneration
```

or an equivalent owner token.

Same protection applies to WorldKit data-layer handles.

Result: **CORRECTED IN P0**

---

## 14. Cross-Kit occupancy review

Risk found in the first conceptual design:

One universal occupancy type owned by WorldGenKit would force:

```text
PlacementKit.Core -> WorldGenKit.Core
```

and break standalone PlacementKit.

Correction:

- GridKit keeps grid occupancy;
- WorldGen Resource/Feature owns generation reservations;
- PlacementKit owns placement claims/categories;
- adapters bridge equivalent semantic IDs/policies.

Result: **CORRECTED IN P0**

This is intentional architecture separation, not accidental duplication.

---

## 15. API complexity review

Rejected direction:

```text
IWorld<TTopology,TCoordinate,TStorage,TLayer,TChunk,...>
```

Reason:

- theoretically universal;
- poor usability;
- rare world types leak complexity into simple projects.

Accepted direction:

- clear V1 planar implementation;
- simple common APIs;
- explicit Extension Kits for fundamentally different topology/storage models.

Result: **PASS**

---

## 16. Review conclusion

The current P0 direction survives the representative cases without requiring a mandatory all-in-one stack.

Two material issues were found and corrected before Runtime implementation:

1. typed runtime handles require registry ownership/generation protection;
2. occupancy semantics must be bridged across independent Kits instead of being universally owned by WorldGenKit.

Runtime implementation should continue only after these corrected boundaries are recorded in the development ledger.

