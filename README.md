# StellarFramework / 仓库入口

面向 Unity 项目的模块化基础框架，包含架构容器、资源加载、UI、事件、数据绑定、对象池、音频、配置、设置系统，以及统一收口到 `Tools Hub` 的编辑器工具。

## 环境要求

- Unity `2021.3+`
- `UniTask`
- `Newtonsoft.Json`

可选依赖：

- `Addressables`
- `HybridCLR`

## 文档快速跳转

- [框架总览 / Framework Overview](Assets/StellarFramework/README.md)
- [示例总览 / Samples Overview](Assets/StellarFramework/Samples/README.md)
- [Architecture 示例 / Architecture Demo](Assets/StellarFramework/Samples/ArchitectureDemo/README.md)
- [Kit 示例 / Kit Samples](Assets/StellarFramework/Samples/KitSamples/README.md)
- [Playable 场景入口 / Scene Index](Assets/StellarFramework/Samples/KitSamples/Scenes/README.md)
- [Tools Hub 文档 / Tools Hub Guide](Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-工具中心-Guide.md)

## 按模块跳转

- [Architecture / 架构指南](Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构指南-Guide.md)
- [ActionKit / 动作系统](Assets/StellarFramework/Runtime/Kits/ActionKit/ActionKit-动作系统-Guide.md)
- [AudioKit / 音频系统](Assets/StellarFramework/Runtime/Kits/AudioKit/AudioKit-音频系统-Guide.md)
- [BindableKit / 数据绑定](Assets/StellarFramework/Runtime/Kits/BindableKit/BindableKit-数据绑定-Guide.md)
- [ConfigKit / 配置系统](Assets/StellarFramework/Runtime/Kits/ConfigKit/ConfigKit-配置系统-Guide.md)
- [EventKit / 事件系统](Assets/StellarFramework/Runtime/Kits/EventKit/EventKit-事件系统-Guide.md)
- [FSMKit / 状态机](Assets/StellarFramework/Runtime/Kits/FSMKit/FSMKit-状态机-Guide.md)
- [HotUpdateKit / 热更新](Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md)
- [HttpKit / 网络请求](Assets/StellarFramework/Runtime/Kits/HttpKit/HttpKit-网络请求-Guide.md)
- [LogKit / 日志性能](Assets/StellarFramework/Runtime/Kits/LogKit/LogKit-PerformanceKit-日志性能-Guide.md)
- [PoolKit / 对象池](Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKit-对象池-Guide.md)
- [ResKit / 统一资源](Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-Guide.md)
- [SettingsKit / 设置系统](Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-Guide.md)
- [SingletonKit / 单例系统](Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonKit-单例系统-Guide.md)
- [UIKit / 界面系统](Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-Guide.md)
- [UIStackManager / 堆栈管理](Assets/StellarFramework/Runtime/Kits/UIKit/UIStackManager-堆栈管理-Guide.md)
- [UniTask / 异步任务](Assets/StellarFramework/Runtime/Kits/StellarFramework-UniTask-异步任务-Guide.md)

## 仓库结构

- `Assets/StellarFramework/Runtime`
  运行时代码与各个 `Kit` 模块。
- `Assets/StellarFramework/Editor`
  框架编辑器工具，统一入口位于 `StellarFramework/Tools Hub`。
- `Assets/StellarFramework/Samples`
  示例场景、示例脚本和配套资源。
- `Assets/StellarFramework/Resources`
  框架与示例共用资源。
- `Assets/StreamingAssets`
  示例配置、RawText 与 `AssetBundle` 运行时资源。

## 程序集策略

- 运行时已按 `Kit` 拆分为独立程序集，业务侧可按需引用。
- `Assets/StellarFramework/StellarFramework.asmdef`
  基础运行时程序集，负责 `Architecture`、协程运行器和通用扩展。
- `Assets/StellarFramework/Samples/StellarFramework.Samples.Runtime.asmdef`
  示例运行时程序集，避免示例代码反向污染框架主体。
- `Assets/StellarFramework/Generated/AssetMap/AssetMap.cs`
  `AssetBundle` 路径映射生成结果。
- `Assets/StellarFramework/Generated/SingletonRegister/SingletonRegister.cs`
  `SingletonKit` 静态注册表生成结果。

## 建议阅读顺序

1. [Assets/StellarFramework/README.md](Assets/StellarFramework/README.md)
2. [Assets/StellarFramework/Samples/README.md](Assets/StellarFramework/Samples/README.md)
3. [Assets/StellarFramework/Samples/ArchitectureDemo/README.md](Assets/StellarFramework/Samples/ArchitectureDemo/README.md)
4. [Assets/StellarFramework/Samples/KitSamples/README.md](Assets/StellarFramework/Samples/KitSamples/README.md)
5. 对应模块目录下的 `English-中文-Guide.md`

## 快速开始

```csharp
using StellarFramework;

public class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
    }
}
```

```csharp
using UnityEngine;

public class GameEntry : MonoBehaviour
{
    private void Start()
    {
        GameApp.Interface.Init();
    }
}
```

```csharp
await UIKit.Instance.InitAsync();
```

```csharp
var loader = ResKit.Allocate<ResourceLoader>();
var prefab = await loader.LoadAsync<GameObject>("Hero");
ResKit.Recycle(loader);
```

## 说明

- `ArchitectureDemo` 用于走通完整业务链路。
- `KitSamples` 用于验证单个模块的最小闭环。
- `SettingsKit` 已提供完整 Example 与 Playable Scene，可直接验证“设置定义 + 存储 + 策略 + 扩展页”的整条链路。
- 如需补齐或重建 `KitSamples` 场景，请在 `StellarFramework -> Tools Hub -> 示例支持 -> 示例构建` 执行。
