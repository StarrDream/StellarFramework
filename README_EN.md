# StellarFramework

A modular framework for Unity projects. This repository is a complete Unity project with the general-purpose Kits, Tools Hub, and onboarding samples. To use selected capabilities in a game project, export the Kits from this project.

Release: 1.0.0

Source commit: [96e6b093e081b5a5c844341b58be3561583f5e6f](https://github.com/StarrDream/StellarFramework.Dev/commit/96e6b093e081b5a5c844341b58be3561583f5e6f)

## Requirements

- Unity Editor **2022.3.62f3c1**
- Unity Package Manager dependencies declared in this repository's <code>Packages/manifest.json</code>
- Package Manager must finish resolving dependencies on first open

## Run the sample

1. Clone this repository, or download and extract its ZIP from GitHub.
2. Add the repository directory in Unity Hub and open it.
3. Wait for asset import and Package Manager resolution.
4. Open **StellarFramework → Tools Hub** and read the Start Here page.
5. Open <code>Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity</code> and press Play.

ArchitectureDemo is a small interactive Model–Service–View (MSV) loop with UI, localization, and safe-area adaptation. Focused samples are under <code>Assets/StellarFramework/Samples</code>.

## Export Kits to a game project

1. Open the Kit exporter from **StellarFramework → Export**.
2. Select one Kit Profile or a Recommended Profile.
3. Review the export summary for framework dependencies and UPM packages.
4. Export a <code>unitypackage</code>, then import it in the target project through **Assets → Import Package → Custom Package…**.
5. Keep the package's <code>.meta</code> files and configure the target project's UPM dependencies from the summary.

Profiles represent selectable Kits, adapters, Editor tools, or supporting files. The exporter includes the framework dependencies declared in the catalog. The target project still needs to resolve the listed UPM packages. See <code>Assets/StellarFramework/FrameworkDoc/02-Kits</code> for Kit requirements and setup.

## Find a Kit by task

| Task | Start with |
| --- | --- |
| Load game assets | ResKit.Core; add an AssetBundle, Addressables, or YooAsset adapter when needed |
| Localize UI | LocalizationKit.Core and the UGUI or TMP adapter for your UI system |
| Adapt layouts to screens and safe areas | UIAdaptationKit.Core; UIKit is an optional integration |
| Build UI panels | UIKit |
| Configuration, saves, events, pooling, and other foundation services | The guide for the corresponding Kit |

Algorithms, World, Flow, and HybridCLR HotUpdate are published in [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions). Extensions requires the matching General release.

## Documentation and release contents

- Kit guides: <code>Assets/StellarFramework/FrameworkDoc/02-Kits</code>
- Tools Hub: <code>Assets/StellarFramework/FrameworkDoc/04-ToolsHub</code>
- Samples: <code>Assets/StellarFramework/Samples</code>
- Exact release contents and external dependencies: [RELEASE-MANIFEST.json](RELEASE-MANIFEST.json)

This repository is generated from [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev). Report framework issues and make source changes there.