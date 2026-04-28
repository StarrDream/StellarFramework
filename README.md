# StellarFramework / 仓库入口

面向 Unity 项目的模块化基础框架，包含架构容器、资源加载、UI、事件、数据绑定、对象池、音频、配置、设置系统，以及统一收口到 `Tools Hub` 的编辑器工具。

## 环境要求

- Unity `2021.3+`
- `UniTask`
- `Newtonsoft.Json`

可选依赖：

- `Addressables`
- `HybridCLR`

## 仓库结构

- `Assets/StellarFramework/Runtime`
  运行时代码与各个 `Kit` 模块。
- `Assets/StellarFramework/Editor`
  框架编辑器工具，统一入口在 `StellarFramework/Tools Hub`。
- `Assets/StellarFramework/Samples`
  样例场景、示例脚本和配套资源。
- `Assets/StellarFramework/Resources`
  框架与样例共用资源。
- `Assets/StreamingAssets`
  样例配置、RawText 和 `AssetBundle` 构建产物。

## 程序集策略

- 运行时已按 `Kit` 拆分为独立程序集，业务侧可以按需引用。
- `Assets/StellarFramework/StellarFramework.asmdef`
  基础运行时程序集，负责 `Architecture`、协程运行器和通用扩展。
- `Assets/StellarFramework/Samples/StellarFramework.Samples.Runtime.asmdef`
  样例运行时程序集，避免示例代码反向污染框架主程序集。
- `Assets/StellarFramework/Generated/AssetMap/AssetMap.cs`
  `AssetBundle` 路径映射生成结果。
- `Assets/StellarFramework/Generated/SingletonRegister/SingletonRegister.cs`
  `SingletonKit` 静态注册表生成结果。

## 主要模块

- `ActionKit`
- `AudioKit`
- `BindableKit`
- `ConfigKit`
- `EventKit`
- `FSMKit`
- `HotUpdateKit`
- `HttpKit`
- `LogKit`
- `PoolKit`
- `ResKit`
- `SettingsKit`
- `SingletonKit`
- `UIKit`

## 建议阅读顺序

1. `Assets/StellarFramework/README.md`
2. `Assets/StellarFramework/Samples/README.md`
3. `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/Demo.unity`
4. `Assets/StellarFramework/Samples/KitSamples/Scenes/README.md`
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

- `ArchitectureDemo` 用来跑通完整业务链路。
- `KitSamples` 用来验证单个模块的最小闭环。
- `SettingsKit` 已提供完整 Example 和 `Playable Scene`，可直接验证“设置定义 + 存储 + 策略 + 扩展页”的整条链路。
- 如需补齐或重建 `KitSamples` 场景，请在 `StellarFramework -> Tools Hub -> 样例支持 -> 样例构建` 执行。

更完整的目录说明见 [Assets/StellarFramework/README.md](/c:/GitProjects/StellarFramework/Assets/StellarFramework/README.md)。
