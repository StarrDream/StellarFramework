# StellarFramework

A modular Unity framework that you can use kit by kit. This is the **General user release** and a complete Unity project for trying the framework or exporting selected Kits.

Release: 1.0.0
Source: StellarFramework.Dev@92c2dd1b2cdea9e5ecbc5b6df0a525267ad16035

## Start here

1. Clone this repository, or choose **Code → Download ZIP** on GitHub and extract it. Install Unity Hub, then open the cloned or extracted repository root with **Unity 2022.3.62f3c1**. This is a complete Unity project.
2. Wait for asset import and Package Manager dependency resolution. If Unity asks to import TMP Essentials, follow the prompt once.
3. Open **StellarFramework → Tools Hub**.
4. Go to **Start Here → Quick Start**, then open the onboarding scene:

   `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`

5. Press Play. The demo has a repeatable gameplay loop, a panel you can close and reopen, and a runtime Chinese / English switch.

That is enough for a first look. You do not need to read every architecture document before trying the framework.

## Use it in your own project

Choose Kits here and import only the resulting package into your game project:

1. Open **StellarFramework → Export** from the Tools Hub.
2. Select one Kit Profile for a minimal setup, or choose a Recommended Profile for a ready-made group.
3. The exporter includes the declared StellarFramework dependency closure so you do not need to guess which framework Kits are required.
4. In your game project, use **Assets → Import Package → Custom Package…** and import the generated `.unitypackage`. Keep its `.meta` files.
5. Install required UPM packages from the exporter summary and the selected Kit guide. Do not add optional backends your project does not use.

**An export contains the selected capabilities and required framework dependencies. Your Unity project still manages Unity Package Manager dependencies.** See the [Kit usage guides](Assets/StellarFramework/FrameworkDoc/02-Kits) for behavior, dependencies, and starter examples.

### Common first choices

| Goal | Start with |
| --- | --- |
| State-driven UI and service boundaries | ArchitectureDemo, then Architecture, BindableKit, and UIKit |
| Load prefabs, images, and other assets | ResKit.Core; add an Addressables, AssetBundle, or YooAsset adapter only when needed |
| Switch languages at runtime | LocalizationKit.Core; add the UGUI / TMP adapters and editor tools you need |
| Handle notches, safe areas, and changing screen ratios | UIAdaptationKit.Core; UIKit is not required |
| Update C# code at runtime | Read the HotUpdate and ResKit guides first; a typical setup uses YooAsset, HybridCLR, UniTask, and target-platform build tools |
| Start with a related set of capabilities | Use a Recommended Profile such as ResKit Complete, Localization Complete, or UIAdaptationKit Complete |

Recommended Profiles include their declared framework dependencies. Use the exporter and Kit guide for exact adapters and UPM versions.

## What is included

- **Foundation and flow**: Architecture, BindableKit, EventKit, PoolKit, SingletonKit, TimeKit, FSMKit, ActionKit.
- **Data and services**: ConfigKit, SettingsKit, SaveKit, LogKit, HttpKit.
- **Presentation and content**: UIKit, UIAdaptationKit, LocalizationKit, AudioKit, ResKit.
- **Resource backends**: built-in Resources support, with optional AssetBundle, Addressables, and YooAsset adapters.
- **Editor tooling**: one Tools Hub, with optional per-Kit tools. Editor-only features can be kept out of a player Runtime export.

**Algorithms, World, Flow, and HybridCLR HotUpdate** are optional extensions released separately in [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions). Follow its README to combine extension sources and export a selected Kit.

## Find guides and examples

- First session: Tools Hub Quick Start and ArchitectureDemo.
- Individual Kits: open the relevant guide under `Assets/StellarFramework/FrameworkDoc/02-Kits`.
- Editor tools: `Assets/StellarFramework/FrameworkDoc/04-ToolsHub`.
- Demo overview: `Assets/StellarFramework/Samples/README.md`.
- To change framework source, use [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev). Do not maintain a feature fork in this release repository.

## Troubleshooting

**Unity is still importing or downloading packages**
Wait for asset import and Package Manager to finish. The first import takes longer than later launches.

**A namespace or external type is missing**
Make sure Package Manager resolved the project dependencies. For a single-Kit export, add UPM packages listed by the exporter and Kit guide.

**I want to add another Kit later**
Export the additional Kit here, or select a Recommended Profile that contains the capabilities you need. Preserve `.meta` files.

**TMP text is not being localized**
TMP integration is optional. Import TMP Essentials and select the TMP adapter / tooling profiles you use.

**How does UIAdaptationKit relate to UIKit?**
UIAdaptationKit can be used on its own. Add the UIKit adapter only if your UI flow needs it.

## Release information

Dev generates this repository. `RELEASE-MANIFEST.json` records the release version, source commit, included profiles, required UPM packages, and validation status. File issues or make changes in Dev, then publish a validated release.
