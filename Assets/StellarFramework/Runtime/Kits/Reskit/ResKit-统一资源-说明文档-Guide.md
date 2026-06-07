# ResKit / 统一资源说明文档

## 模块定位

`ResKit` 是框架的统一资源入口。业务层只依赖 `IResLoader`，不直接关心底层到底是 `Resources`、`AssetBundle`、`Addressables` 还是第三方资源系统。

它解决的是两类问题：

- 统一资源加载写法
- 统一资源生命周期和释放规则

## 模块结构

运行时主要由三层组成：

- `ResKit`
  对外门面，负责分配 loader、注册自定义后端、回收 loader
- `ResLoader`
  loader 基类，负责本地持有记录、异步等待、与 `ResMgr` 协作
- `ResMgr`
  全局共享缓存和引用计数中心

具体后端目前包括：

- `ResourceLoader`
- `AssetBundleLoader`
- `AddressableLoader`

## 后端模式

### Resources

适合：

- 默认配置
- 默认 UI 资源
- 小体量、固定资源

特点：

- 不需要额外构建
- 直接依赖 Unity `Resources`
- 不适合生产热更新

### AssetBundle

适合：

- 已经明确采用 AB 管线的项目
- 需要保留 `AssetMap`、依赖和本地 AB 工作流

特点：

- 依赖 `AssetBundleManager`
- 依赖 ToolsHub 的 `资源打包 (AssetBundle)` 和生成的 `AssetMap`

### Addressables

适合：

- 生产资源管理
- 本地内置 AA
- 远端热更 AA

特点：

- 通过 `Custom loader` 接入
- 资源 key 推荐使用完整 `Assets/...` 路径
- 生产模式只建议异步加载

### Custom

适合：

- `YooAsset`
- 项目自有资源系统
- 第三方资源插件

特点：

- 只要求实现 `ResLoader`
- 不改变业务层的 `IResLoader` 用法

## 标准入口

### 推荐生产入口

```csharp
IResLoader loader = ResKit.Allocate(
    ResLoaderRequest.Custom("Addressables", "BattlePreload"));
```

### 指定内置后端

```csharp
IResLoader resources = ResKit.Allocate(ResLoadBackend.Resources, "Local");
IResLoader assetBundle = ResKit.Allocate(ResLoadBackend.AssetBundle, "LegacyAB");
```

### 兼容旧式 typed loader

```csharp
ResourceLoader loader = ResKit.Allocate<ResourceLoader>();
```

## 标准使用流程

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public sealed class PrefabSpawner : MonoBehaviour
{
    private IResLoader _loader;
    private const string PrefabPath = "Assets/Game/Prefabs/Hero.prefab";

    private async UniTaskVoid Start()
    {
        _loader = ResKit.Allocate(ResLoaderRequest.Custom("Addressables", nameof(PrefabSpawner)));
        GameObject prefab = await _loader.LoadAsync<GameObject>(PrefabPath, destroyCancellationToken);

        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    private void OnDestroy()
    {
        if (_loader == null)
        {
            return;
        }

        _loader.Unload(PrefabPath);
        ResKit.Recycle(_loader);
        _loader = null;
    }
}
```

## 路径规则

### Resources

- 不带 `Resources/`
- 不带扩展名

示例：

```csharp
TextAsset txt = await loader.LoadAsync<TextAsset>("Configs/GameSetting");
```

### AssetBundle / Addressables

- 推荐统一使用完整 `Assets/...` 路径

示例：

```csharp
GameObject prefab = await loader.LoadAsync<GameObject>(
    "Assets/Game/Prefabs/Hero.prefab",
    token);
```

这样做的好处是：

- `AB` 和 `AA` 可以共用同一套业务资源 key
- 工具链更容易定位真实资产来源

## Addressables 说明

`Addressables` 在 `ResKit` 里不是单独枚举，而是作为 `Custom loader` 接入。

典型注册形式：

```csharp
ResKit.RegisterCustomLoader("Addressables", request => new AddressableLoader());
```

发布模式分成两类：

- 本地内置 AA
  资源和 `HotUpdateManifest.json` 进入 `StreamingAssets/aa`
- 远端热更 AA
  从远端 Manifest、catalog、hash、bundle 更新资源

## AssetBundle 说明

`AssetBundle` 后端依赖：

- `AssetBundleManager`
- `Generated/AssetMap/AssetMap.cs`
- ToolsHub 的 `资源打包 (AssetBundle)`

使用前通常需要：

```csharp
await AssetBundleManager.Instance.InitAsync();
```

若使用严格卸载模式：

- 先销毁场景实例
- 再释放 loader 和 bundle 引用

否则可能出现资源对象被提前销毁。

## Resources 说明

`Resources` 后端适合框架默认资源和轻量固定资源。

不适合：

- 大体量内容
- 高频版本更新内容
- 需要正式热更新管理的资源

## 自定义 Loader

### 注册

```csharp
ResKit.RegisterCustomLoader("YooAsset", request => new YooAssetResLoader());
```

### 分配

```csharp
IResLoader loader = ResKit.Allocate(
    ResLoaderRequest.Custom("YooAsset", "Startup"));
```

要求：

- 继承 `ResLoader`
- 实现同步 / 异步真实加载
- 实现自己的 `RecycleToPool()`

## 释放规则

业务层需要遵守：

- 加载后由当前 loader 持有引用
- `Unload(path)` 只释放当前 loader 对单个路径的持有关系
- `ReleaseAll()` 释放当前 loader 全部持有关系
- `ResKit.Recycle(loader)` 是标准收口动作

不要只销毁场景对象而不回收 loader，也不要只回收 loader 而不处理场景中还在使用的实例对象。

## ToolsHub 关联

- `资源打包 (AssetBundle)`
  负责 AB 规则、构建和 `AssetMap` 生成
- `ResKit 资源审计`
  查看 loader、共享缓存、资源引用和持有者
- `AA 配置与发布`
  负责 Addressables 的本地内置和远端热更发布流程

## 常见问题

- Addressables 同步加载返回空
  生产模式的 Addressables 只支持异步加载。
- AssetBundle 加载失败
  先确认 `AssetBundleManager.Instance.InitAsync()` 已执行，且 `AssetMap` 已生成。
- 自定义 loader 分配失败
  检查 `CustomKey` 是否已注册。
- 资源释放后对象丢失
  先销毁场景实例，再释放 loader 和底层资源引用。

## 相关文档

- [ResKit 源码文档](ResKit-统一资源-源码文档-Guide.md)
