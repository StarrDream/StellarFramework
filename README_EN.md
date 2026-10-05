# StellarFramework

A modular C# framework for Unity projects. This repository contains the runnable Tank Arena case study and the Kit export project. Run the sample, then select the capabilities your project needs in Tools Hub.

Release: **1.0.3**

Dev source commit: [4eaff35790c217c9d65994b9d95788865aa458e5](https://github.com/StarrDream/StellarFramework.Dev/commit/4eaff35790c217c9d65994b9d95788865aa458e5)

## Framework overview

Open the repository in Unity Hub and wait for imports to finish. Run Tank Arena, or select individual Kits, Profiles, and resource backends from **StellarFramework → Export** to create a unitypackage. Import it into your Unity project and install any UPM dependencies listed in the package manifest.

Sample scene: `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`. Scripts, prefabs, and art assets are included in the Sample. See the [Tank Arena case study](Assets/StellarFramework/Samples/TankArena/CaseStudy.md) for gameplay and Kit integration.

## Requirements

- Unity Editor 2022.3.62f3c1
- Unity Package Manager access on first open to resolve `Packages/manifest.json`
- Unity Editor is required to run the sample or export Kits, not to read the documentation

## Framework concepts

| Concept | Description |
| --- | --- |
| Runtime | Framework foundation; add Kits as needed |
| Kit | A focused capability such as ResKit, UIKit, or LocalizationKit |
| Adapter / Provider | Connects a Kit to a backend; loading, content updates, and code updates are selectable independently |
| Profile | An exportable selection of Kits and adapters; use individually or combine |
| Tools Hub | Editor setup, build, diagnostics, and export tools; not part of the game Player |

ResKit provides the shared resource entry point. Resources, AssetBundle, Addressables (AA), and YooAsset are optional loading backends. The YooAsset content update Provider manages resource content; the HybridCLR code update Provider manages hot-update assemblies.

## Architecture

`StellarFramework.cs` defines the MSV foundation. `Architecture<T>` registers Models and Services and manages initialization, lookup, and disposal. A View reads Models through the read-only architecture interface and sends interactions to a Service. Services perform application operations and access Models. BindableKit can be added for state notifications.

~~~mermaid
flowchart LR
    Startup["Game startup"] -->|"Init / lifecycle"| Architecture["Architecture<T><br/>registration, lookup, lifecycle"]
    Architecture -->|"register / initialize"| Model["Model<br/>application state and data"]
    Architecture -->|"register / initialize"| Service["Service<br/>application operations and flow"]
    View["View<br/>StellarView / Unity UI"] -->|"interaction: call"| Service
    Service -->|"read / update"| Model
    View -->|"read-only query"| Model
    Model -. "optional: BindableKit state notification" .-> View
~~~

Source: `Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs`. Read the [MSV architecture guide](https://github.com/StarrDream/StellarFramework.Dev/blob/4eaff35790c217c9d65994b9d95788865aa458e5/Assets/StellarFramework/FrameworkDoc/01-Architecture/Architecture/Architecture-MSV-架构说明文档-Guide.md) or the [architecture source guide](https://github.com/StarrDream/StellarFramework.Dev/blob/4eaff35790c217c9d65994b9d95788865aa458e5/Assets/StellarFramework/FrameworkDoc/01-Architecture/Architecture/Architecture-MSV-架构源码文档-Guide.md).

## Kit guide

Choose Kits by task. Tools Hub exports an individual Kit, a Profile, or a combination of resource backends.

| Use case | Kit | What it does | Guide |
| --- | --- | --- | --- |
| Foundation and flow | LogKit | Categorized logging and runtime diagnostics | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/LogKit/LogKit-PerformanceKit-说明文档-Guide.md) |
| Foundation and flow | EventKit | Publish and subscribe to typed events | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/EventKit/EventKit-事件系统-说明文档-Guide.md) |
| Foundation and flow | BindableKit | Observable data and change notifications | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/BindableKit/BindableKit-数据绑定-说明文档-Guide.md) |
| Foundation and flow | ActionKit | Compose and run reusable actions | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/ActionKit/ActionKit-动作系统-说明文档-Guide.md) |
| Foundation and flow | FSMKit | State transitions and lifecycle | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/FSMKit/FSMKit-状态机-说明文档-Guide.md) |
| Foundation and flow | TimeKit | Shared time and timer control | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/TimeKit/TimeKit-时间系统-说明文档-Guide.md) |
| Foundation and flow | PoolKit | Object reuse and allocation management | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/PoolKit/PoolKit-对象池-说明文档-Guide.md) |
| Foundation and flow | SingletonKit | Register singleton objects and manage their lifecycle | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/SingletonKit/SingletonKit-单例系统-说明文档-Guide.md) |
| Data and configuration | ConfigKit | Load and access project configuration | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/ConfigKit/ConfigKit-配置系统-说明文档-Guide.md) |
| Data and configuration | SaveKit | Save data read/write and serialization adapters | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/SaveKit/SaveKit-存档系统-说明文档-Guide.md) |
| Data and configuration | SettingsKit | Player settings and storage adapters | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md) |
| Resources and updates | ResKit | Shared resource loading and release API | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| Resources and updates | Resources / AssetBundle / Addressables (AA) | Optional backends; export separately or combine them | [Backend guide](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| Resources and updates | YooAsset | ResKit resource backend; content update Provider manages versions, downloads, and cache | [Resource guide](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| Resources and updates | HybridCLR | Optional ResKit code update Provider | [Usage guide](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/CodeUpdate/HybridCLR/ResKit-CodeUpdate-HybridCLR-说明文档-Guide.md) |
| UI and presentation | UIKit | UI panel lifecycle, open, and close | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/UIKit/UIKit-界面系统-说明文档-Guide.md) |
| UI and presentation | UIAdaptationKit | Safe area and screen layout adaptation | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/UIAdaptationKit/UIAdaptationKit-说明文档-Guide.md) |
| UI and presentation | LocalizationKit | Language switching and UGUI/TMP localization binding | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/LocalizationKit/LocalizationKit-Guide.md) |
| UI and presentation | AudioKit | Sound and music playback with resource integration | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/AudioKit/AudioKit-音频系统-说明文档-Guide.md) |
| Networking | HttpKit | Asynchronous HTTP requests and response handling | [Guide](Assets/StellarFramework/FrameworkDoc/02-Kits/HttpKit/HttpKit-网络请求-说明文档-Guide.md) |

## Release links

- [GitHub Releases](https://github.com/StarrDream/StellarFramework/releases): download a published version.
- [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions): Algorithms, World, and Flow extensions; use the same release version.
- [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev): framework development source.
- [Tank Arena case study](Assets/StellarFramework/Samples/TankArena/CaseStudy.md)
- [RELEASE-MANIFEST.json](RELEASE-MANIFEST.json): source commit, Profiles, and external dependencies.
