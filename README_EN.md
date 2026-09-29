# StellarFramework

A modular, exportable, production-oriented Unity framework for general project infrastructure.

> This repository is the **General user release**. Framework development happens only in `StarrDream/StellarFramework.Dev`. Advanced Algorithms, World, Flow and HybridCLR hot-update capabilities are released from `StarrDream/StellarFramework.Extensions`.

Release: `1.0.0`
Source: `StellarFramework.Dev@b5b63c1379152c402beaefcf19ce6df10074b9fb`

## General capabilities

- Architecture / MSV, EventKit, PoolKit, SingletonKit, TimeKit, FSMKit and ActionKit.
- ConfigKit, SettingsKit, SaveKit, LogKit and HttpKit.
- UIKit, UIAdaptationKit, LocalizationKit and AudioKit.
- ResKit with Resources / AssetBundle / Addressables / YooAsset backends.
- Tools Hub, Kit export and standalone Architecture / Extensions source export.

## Quick start

1. Open the repository with Unity `2022.3 LTS` (development baseline: `2022.3.62f3c1`).
2. Let Package Manager resolve dependencies.
3. Open `StellarFramework -> Tools Hub`.
4. Use Quick Start or `StellarFramework -> Export` to select only the Kits your project needs.

## Extensions

Optional advanced capabilities live in `StarrDream/StellarFramework.Extensions`:

- **Algorithms**: GridKit, SpatialKit, PathKit and SimulationKit.
- **World**: WorldKit, WorldGenKit, PlacementKit and adapters.
- **Flow**: FlowKit Core, Unity integration and editor tooling.
- **HotUpdate**: HybridCLRKit and HotUpdate Publisher.

General Runtime never depends on Extensions Runtime.

## Architecture rules

- Model owns state, Service owns business rules and state transitions, View owns presentation and input translation.
- Unity APIs, third-party SDKs, resource backends and platform differences are isolated behind adapters.
- Core Kits never depend back on adapters.
- Failures remain explicit; broad fallback paths must not turn real failures into silent success.
- Hot paths avoid unnecessary allocations, LINQ and reflection-based scanning.

## Repository model

```text
StellarFramework.Dev              single development source of truth
        |\
        | \----> StellarFramework.Extensions   advanced user release
        |
        +------> StellarFramework              general user release
```

Fixes and features must land in Dev first and are then republished after validation.
