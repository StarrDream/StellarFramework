# StellarFramework / 框架总览

`Assets/StellarFramework` 是框架主目录，包含运行时代码、编辑器工具、样例、生成代码和共用资源。

## 目录结构

- `Runtime/`
  运行时代码与核心扩展。
- `Editor/`
  编辑器工具与 `Tools Hub`。
- `Samples/`
  样例场景、示例脚本和样例资源。
- `Generated/`
  代码生成产物。
- `Resources/`
  框架与样例共用资源。
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
- 样例运行时：
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

## 样例入口

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
