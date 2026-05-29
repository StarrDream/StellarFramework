# ResKit / 统一资源 Guide

ResKit 是框架的资源加载门户。业务层优先通过 `ResKit.Allocate(request)` 或兼容的 `ResKit.Allocate<TLoader>()` 获取加载器，再用统一的 `IResLoader` 加载资源。

## 1. 后端选择

```csharp
IResLoader loader = ResKit.Allocate(new ResLoaderRequest
{
    Backend = ResLoadBackend.Addressables,
    OwnerName = "BattlePreload"
});
```

可选后端：

- `Resources`：本地内置资源，简单稳定，适合基础配置和兜底 UI。
- `Addressables`：推荐生产热更后端，使用 `Assets/...` address，优先异步加载。
- `AssetBundle`：保留自建 AB 管线，继续使用 `AssetMap` 和 ToolHub AB 构建。
- `Custom`：通过 `ResKit.RegisterCustomLoader` 接入 YooAsset 或业务自定义加载器。

旧代码继续可用：

```csharp
var loader = ResKit.Allocate<ResourceLoader>();
var aaLoader = ResKit.Allocate<AddressableLoader>();
var abLoader = ResKit.Allocate<AssetBundleLoader>();
```

## 2. 标准流程

```csharp
private IResLoader _loader;

private async UniTaskVoid Start()
{
    _loader = ResKit.Allocate(ResLoadBackend.Addressables, "HeroView");
    GameObject prefab = await _loader.LoadAsync<GameObject>(
        "Assets/Game/Prefabs/Hero.prefab",
        destroyCancellationToken);

    if (prefab != null)
    {
        Instantiate(prefab);
    }
}

private void OnDestroy()
{
    if (_loader != null)
    {
        ResKit.Recycle(_loader);
        _loader = null;
    }
}
```

## 3. 模拟加载和构建边界

- AB：框架自管构建，所以 ToolHub 提供 AssetBundle 构建、AssetMap 生成和诊断。
- AA：使用 Addressables 官方 Play Mode Script 模拟加载，使用官方 Groups 窗口构建。
- YooAsset/其他插件：使用插件自己的构建器，ResKit 只提供自定义 loader 接入口。
- 如果 AB 模式想在 Editor 中不构建就预览，可以注册一个 `Custom` AssetDatabase loader；不要把它混同为正式 AB 加载。

## 4. 异步、取消和释放

所有生产加载建议传入 `CancellationToken`：

```csharp
GameObject prefab = await _loader.LoadAsync<GameObject>(path, token);
```

释放：

```csharp
_loader.Unload(path);
ResKit.Recycle(_loader);
```

规则：

- 同一个 loader 多次加载同一路径，会按引用计数释放。
- 不同 loader 加载同一资源，底层资源在全局引用计数归零后才会释放。
- AA 加载失败或取消会释放 Addressables handle。
- AB 默认 `bundle.Unload(false)`，严格释放可在 `ResKitRuntimeSettings` 改为 `DestroyLoadedAssets`。

## 5. 自定义 loader

```csharp
ResKit.RegisterCustomLoader("YooAsset", () => new YooAssetResLoader());

IResLoader loader = ResKit.Allocate(new ResLoaderRequest
{
    Backend = ResLoadBackend.Custom,
    CustomKey = "YooAsset",
    OwnerName = "Startup"
});
```

自定义加载器继承 `ResLoader`，实现同步/异步真实加载和卸载逻辑即可。

## 6. 常见错误排查

- `Addressables is unavailable`：确认安装 Addressables，并让 Unity 重新编译 asmdef version define。
- AA 同步加载返回 `null`：正常行为，生产 AA 请使用 `LoadAsync<T>`。
- AB 未初始化：启动阶段先执行 `await AssetBundleManager.Instance.InitAsync()`。
- AB 缺资源：确认已生成 AssetMap，且传入路径是完整 `Assets/...`。
- 自定义 loader 分配失败：确认 `CustomKey` 已注册。
- 释放后对象丢失：先销毁场景实例，再释放 loader 或资源依赖。

## 7. 可复制模板

### 7.1 最小可用模板

适合：业务层只想要“给我一个加载器，然后加载 prefab”。

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
        _loader = ResKit.Allocate(new ResLoaderRequest
        {
            Backend = ResLoadBackend.Addressables,
            OwnerName = nameof(PrefabSpawner)
        });

        GameObject prefab = await _loader.LoadAsync<GameObject>(PrefabPath, destroyCancellationToken);
        if (prefab == null)
        {
            Debug.LogError($"加载 prefab 失败: {PrefabPath}");
            return;
        }

        Instantiate(prefab);
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

### 7.2 Resources 模板

适合：开发期、本地配置、默认 UI、轻量资源。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public sealed class LocalPrefabSpawner : MonoBehaviour
{
    private ResourceLoader _loader;

    private async UniTaskVoid Start()
    {
        _loader = ResKit.Allocate<ResourceLoader>();
        GameObject prefab = await _loader.LoadAsync<GameObject>("ResKitTest/TestCube_Res", destroyCancellationToken);
        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    private void OnDestroy()
    {
        if (_loader != null)
        {
            ResKit.Recycle(_loader);
            _loader = null;
        }
    }
}
```

### 7.3 Addressables 模板

适合：生产资源热更。Address 使用完整 `Assets/...` 路径。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public sealed class AddressablePrefabSpawner : MonoBehaviour
{
    private IResLoader _loader;

    private async UniTaskVoid Start()
    {
        _loader = ResKit.Allocate(ResLoadBackend.Addressables, "AddressablePrefabSpawner");
        GameObject prefab = await _loader.LoadAsync<GameObject>(
            "Assets/Game/Prefabs/Hero.prefab",
            destroyCancellationToken);

        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    private void OnDestroy()
    {
        if (_loader != null)
        {
            ResKit.Recycle(_loader);
            _loader = null;
        }
    }
}
```

### 7.4 AssetBundle 模板

适合：项目已有 AB 管线或明确需要 AssetMap。

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public sealed class AssetBundlePrefabSpawner : MonoBehaviour
{
    private IResLoader _loader;
    private const string PrefabPath = "Assets/Game/Prefabs/Hero.prefab";

    private async UniTaskVoid Start()
    {
        await AssetBundleManager.Instance.InitAsync(cancellationToken: destroyCancellationToken);

        _loader = ResKit.Allocate(ResLoadBackend.AssetBundle, "AssetBundlePrefabSpawner");
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

### 7.5 自定义 Loader 模板

适合：接 YooAsset 或项目自己的资源系统。

```csharp
using StellarFramework.Res;

public static class LoaderBootstrap
{
    public static void Register()
    {
        ResKit.RegisterCustomLoader("YooAsset", request => new YooAssetResLoader());
    }
}

public sealed class YooAssetResLoader : ResLoader
{
}
```

```csharp
IResLoader loader = ResKit.Allocate(new ResLoaderRequest
{
    Backend = ResLoadBackend.Custom,
    CustomKey = "YooAsset",
    OwnerName = "Startup"
});
```
