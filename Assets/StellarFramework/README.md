# StellarFramework / 框架总览

`Assets/StellarFramework` 是框架主目录，包含运行时代码、编辑器工具、样例、生成代码和共用资源。

## 文档快速跳转

- [仓库入口 / Repository README](../../README.md)
- [示例总览 / Samples Overview](Samples/README.md)
- [Architecture 示例 / Architecture Demo](Samples/ArchitectureDemo/README.md)
- [Kit 示例 / Kit Samples](Samples/KitSamples/README.md)
- [Playable 场景入口 / Scene Index](Samples/KitSamples/Scenes/README.md)
- [Tools Hub 文档 / Tools Hub Guide](Editor/StellarToolsHub/StellarToolsHub-工具中心-Guide.md)

## 按模块跳转

- [Architecture / 架构指南](Runtime/Core/Architecture/Architecture-MSV-架构指南-Guide.md)
- [ActionKit / 动作系统](Runtime/Kits/ActionKit/ActionKit-动作系统-Guide.md)
- [AudioKit / 音频系统](Runtime/Kits/AudioKit/AudioKit-音频系统-Guide.md)
- [BindableKit / 数据绑定](Runtime/Kits/BindableKit/BindableKit-数据绑定-Guide.md)
- [ConfigKit / 配置系统](Runtime/Kits/ConfigKit/ConfigKit-配置系统-Guide.md)
- [EventKit / 事件系统](Runtime/Kits/EventKit/EventKit-事件系统-Guide.md)
- [FSMKit / 状态机](Runtime/Kits/FSMKit/FSMKit-状态机-Guide.md)
- [HotUpdateKit / 热更新](Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md)
- [HttpKit / 网络请求](Runtime/Kits/HttpKit/HttpKit-网络请求-Guide.md)
- [LogKit / 日志性能](Runtime/Kits/LogKit/LogKit-PerformanceKit-日志性能-Guide.md)
- [PoolKit / 对象池](Runtime/Kits/PoolKit/PoolKit-对象池-Guide.md)
- [ResKit / 统一资源](Runtime/Kits/Reskit/ResKit-统一资源-Guide.md)
- [SettingsKit / 设置系统](Runtime/Kits/SettingsKit/SettingsKit-设置系统-Guide.md)
- [SingletonKit / 单例系统](Runtime/Kits/SingletonKit/SingletonKit-单例系统-Guide.md)
- [UIKit / 界面系统](Runtime/Kits/UIKit/UIKit-界面系统-Guide.md)
- [UIStackManager / 堆栈管理](Runtime/Kits/UIKit/UIStackManager-堆栈管理-Guide.md)
- [UniTask / 异步任务](Runtime/Kits/StellarFramework-UniTask-异步任务-Guide.md)

## 目录结构

- `Runtime/`
  运行时代码与核心扩展。
- `Editor/`
  编辑器工具与 `Tools Hub`。
- `Samples/`
  示例场景、示例脚本和示例资源。
- `Generated/`
  代码生成产物。
- `Resources/`
  框架与示例共用资源。
- `GameApp.cs`
  默认架构入口示例。
- `GameEntry.cs`
  默认场景入口示例。

## 主要模块

- `Architecture`
  MSV 架构容器与模块注册入口。
- `ResKit`
  `Resources / AssetBundle / Addressables` 统一加载接口。
- `UIKit`
  面板加载、层级管理与导航能力。
- `EventKit`
  枚举事件与结构体事件。
- `BindableKit`
  属性、列表和字典绑定。
- `PoolKit`
  纯 C# 对象池与工厂对象池。
- `AudioKit`
  BGM、音效与 Mixer 管理。
- `ConfigKit`
  配置加载、覆盖与保存。
- `SettingsKit`
  设置定义、存储、即时应用、扩展页和参考菜单。
- `HttpKit`
  HTTP 请求与图片下载封装。
- `FSMKit`
  轻量状态机。
- `SingletonKit`
  全局单例与场景单例管理。

## 程序集结构

- 基础运行时：
  `StellarFramework.Runtime`
- 按 Kit 拆分：
  `ActionKit / AudioKit / BindableKit / ConfigKit / EventKit / FSMKit / HotUpdateKit / HttpKit / LogKit / PoolKit / ResKit / SettingsKit / SingletonKit / UIKit`
- 示例运行时：
  `StellarFramework.Samples.Runtime`
- 生成代码：
  `Generated/AssetMap/AssetMap.cs`
  `Generated/SingletonRegister/SingletonRegister.cs`

## 依赖

必选：

- `UniTask`
- `Newtonsoft.Json`

可选：

- `Addressables`
  使用前请添加宏 `UNITY_ADDRESSABLES`
- `HybridCLR`
  使用前请添加宏 `HYBRIDCLR_ENABLE`

## 文档命名

- `README.md`
  目录索引文件。
- `English-中文-Guide.md`
  专题文档。

## 快速开始

初始化架构：

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

初始化 `UIKit`：

```csharp
await UIKit.Instance.InitAsync();
```

加载资源：

```csharp
var loader = ResKit.Allocate<ResourceLoader>();
var prefab = await loader.LoadAsync<GameObject>("Hero");
ResKit.Recycle(loader);
```

注册事件：

```csharp
GlobalEnumEvent.Register(GameEvent.Start, OnGameStart)
    .UnRegisterWhenGameObjectDestroyed(gameObject);
```

## 示例入口

优先查看：

1. `Samples/README.md`
2. `Samples/ArchitectureDemo/README.md`
3. `Samples/KitSamples/README.md`

主要运行入口：

- `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/Demo.unity`
- `Assets/StellarFramework/Samples/KitSamples/Scenes/*.unity`

其中 `SettingsKit_Playable.unity` 专门用于验证设置系统的完整链路。

## 使用建议

- 先从 `ArchitectureDemo` 跑通完整链路。
- 再按模块查看 `KitSamples`。
- 最后回到对应模块目录下的 `English-中文-Guide.md` 对照接入细节。
