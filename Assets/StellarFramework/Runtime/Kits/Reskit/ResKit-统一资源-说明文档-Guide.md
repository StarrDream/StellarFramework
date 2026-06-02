# ResKit / 统一资源说明文档

ResKit 是资源加载统一入口。业务层只分配 `IResLoader`，不直接关心底层是 Resources、AssetBundle、Addressables 还是第三方资源系统。

## 五种模式

- `Resources`：内置资源，适合兜底、小配置、开发期资源。
- `AssetBundle`：传统自管 AB 管线，保留完整 manifest、依赖、引用计数链路。
- `Addressables 本地内置`：资源随包放入 `StreamingAssets/aa`，是 AB 模式的上位替代方案。
- `Addressables 远端热更`：catalog/hash/bundle/version/download/cache 由 AA 官方机制处理。
- `Custom`：第三方资源系统接入点，例如 YooAsset。

## 入口 API

- `ResKit.Allocate(ResLoadBackend backend, string ownerName)`
- `ResKit.Allocate(ResLoaderRequest request)`
- `ResKit.Allocate<TLoader>()`
- `ResKit.RegisterCustomLoader(key, factory)`
- `ResKit.Recycle(loader)`
- `IResLoader.Load<T>(path)` / `LoadAsync<T>(path, token)`
- `IResLoader.Unload(path)` / `UnloadAll()`

## 使用模板

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public sealed class PrefabSpawner : MonoBehaviour
{
    private IResLoader _loader;

    private async UniTaskVoid Start()
    {
        _loader = ResKit.Allocate(ResLoadBackend.Addressables, nameof(PrefabSpawner));
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
        ResKit.Recycle(_loader);
        _loader = null;
    }
}
```

## 第三方资源接入

```csharp
ResKit.RegisterCustomLoader("YooAsset", request => new YooAssetResLoader());

IResLoader loader = ResKit.Allocate(ResLoaderRequest.Custom("YooAsset", "Startup"));
```

第三方资源系统只接入 loader，不改变业务层 `ResKit.Allocate(...)` 和 `IResLoader` 调用方式。

## ToolsHub 关联

- `资源打包 (AssetBundle)`：AB 规则、构建、AssetMap 生成。
- `ResKit 资源审计`：查看运行时 loader、资源引用和释放状态。
- `AA 配置与发布`：AA 本地内置和远端热更发布闭环。

## 相关专题

- [Resources 后端](Loaders/ResourceLoader/ResKit-Resources-内置资源-Guide.md)
- [Addressables 后端](Loaders/AddressableLoader/ResKit-Addressables-可寻址资源-Guide.md)
- [AssetBundle 后端](Loaders/AssetBundleLoader/ResKit-AssetBundle-资源包-Guide.md)
- [ResKit 源码文档](ResKit-统一资源-源码文档-Guide.md)

## 常见问题

- AA 同步加载返回空：生产 AA 使用 `LoadAsync<T>`。
- AB 加载失败：先初始化 `AssetBundleManager.Instance.InitAsync()`。
- 自定义 loader 分配失败：确认 `CustomKey` 已注册。
- 资源释放后对象消失：先销毁场景实例，再释放资源引用。
