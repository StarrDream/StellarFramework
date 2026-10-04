# StellarFramework

StellarFramework is a modular C# framework for Unity projects. This repository is a complete Unity project: run the Tank Arena sample, or use the exporter to select Kits, adapters, and tools and create a `.unitypackage` for your own project.

Release: **1.0.1**

Source commit: [7fbf5dc8991bca08eb38dea738b1415f8d0adcfc](https://github.com/StarrDream/StellarFramework.Dev/commit/7fbf5dc8991bca08eb38dea738b1415f8d0adcfc)

## Requirements

- Unity Editor **2022.3.62f3c1**
- Unity Package Manager access on first open to resolve dependencies in `Packages/manifest.json`

## Run Tank Arena

1. Clone this repository, or download and extract its GitHub ZIP.
2. Add the repository directory in Unity Hub and open the project. Wait for asset import and package resolution to finish.
3. Open `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`, wait for script compilation, and press Play.
4. On a touch screen, use the left stick to move and the right stick to aim. On desktop, use WASD, the mouse, and Space.

Tank Arena is a playable framework case study with enemy waves, repairs, pause and results screens, localization, local saves, and screen adaptation. Its SYSTEMS panel reports the Kits used by the sample. See `Assets/StellarFramework/Samples/TankArena/CaseStudy.md` for the gameplay flow, Kit responsibilities, and asset locations.

The Editor preview runs the locally compiled sample assembly. It does not download remote hot-update content or exercise the publishing workflow.

## Export Kits to your project

1. Open this project in Unity and select **StellarFramework → Export**.
2. Choose one Profile, a recommended combination, or multiple adapters.
3. Review the framework and UPM dependencies in the export summary, then export the `.unitypackage`.
4. In your Unity project, select **Assets → Import Package → Custom Package…** and import the file. Let the package installer finish configuring dependencies.

The exporter resolves framework dependencies, removes duplicate dependencies in combined exports, and includes a dependency manifest in each package. Resources, AssetBundle, and Addressables (AA) can be exported independently; they can also be combined, for example Resources + AssetBundle. Install third-party UPM dependencies listed by the package. Kit prerequisites and setup examples are under `Assets/StellarFramework/FrameworkDoc/02-Kits`.

Resource loading, content updates, and C# code updates are separate ResKit extensions. YooAsset provides resource loading and content update capabilities; HybridCLR provides code loading. The Addressables adapter handles ResKit loading and release, but does not orchestrate StellarFramework content updates. See `Assets/StellarFramework/FrameworkDoc/01-Architecture/ResourceAndCodeUpdatePlugins.md` for the design and integration flow.

## Find a guide

| Task | Documentation |
| --- | --- |
| Resource loading, AssetBundle, Addressables, or YooAsset | `FrameworkDoc/02-Kits/Reskit` |
| Localization and TMP/UGUI adapters | `FrameworkDoc/02-Kits/LocalizationKit` |
| Safe areas and screen layouts | `FrameworkDoc/02-Kits/UIAdaptationKit` |
| UI panels and resource loading strategies | `FrameworkDoc/02-Kits/UIKit` |
| Code hot update and publishing | `FrameworkDoc/02-Kits/Reskit/CodeUpdate/HybridCLR` |
| Tools Hub | `FrameworkDoc/04-ToolsHub` |

Algorithms, World, and Flow are published in [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions). Use the Extensions release that matches this General release.

## Release contents

`RELEASE-MANIFEST.json` records the Dev source commit, included Profiles, external UPM dependencies, and file count. Framework source and release templates are maintained in [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev). Make framework source changes there.
