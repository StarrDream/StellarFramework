# ResKit / 统一资源

## 定位

`ResKit` 是框架的统一资源加载入口。上层统一通过 `IResLoader` 工作，底层负责共享加载、引用计数、加载器池化和取消收口。

## 核心特性

- 统一接口：上层统一走 `IResLoader`
- 引用计数：自动管理资源生命周期
- 异步去重：同一资源并发加载时共享物理加载结果
- 加载器池化：`ResLoader` 本身可回收复用
- 可扩展：通过继承 `ResLoader` 接入自定义来源

## 标准流程

### 分配 -> 加载 -> 回收

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

public class HeroLoader : MonoBehaviour
{
    private IResLoader _loader;
    private CancellationToken _destroyToken;

    void Start()
    {
        _loader = ResKit.Allocate<ResourceLoader>();
        _destroyToken = this.GetCancellationTokenOnDestroy();
        LoadAvatar().Forget();
    }

    async UniTaskVoid LoadAvatar()
    {
        var prefab = await _loader.LoadAsync<GameObject>("Characters/Hero", _destroyToken);
        if (prefab != null)
        {
            Instantiate(prefab, transform);
        }
    }

    void OnDestroy()
    {
        if (_loader != null)
        {
            ResKit.Recycle(_loader);
            _loader = null;
        }
    }
}
```

### 单个资源精准卸载

```csharp
_loader.Unload("Characters/Hero");
```

### 批量预加载

```csharp
public async UniTask PreloadAssets(CancellationToken token)
{
    var paths = new List<string> { "Prefabs/Boss", "Textures/Bg", "Audio/BGM" };
    var loader = ResKit.Allocate<AssetBundleLoader>();

    await loader.PreloadAsync(paths, progress =>
    {
        Debug.Log($"加载进度: {progress * 100}%");
    }, token);
}
```

## 异步与取消

当前 `IResLoader` 的异步接口都支持 `CancellationToken`：

```csharp
UniTask<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default);
UniTask PreloadAsync(IList<string> paths, Action<float> onProgress = null, CancellationToken cancellationToken = default);
```

建议在这些场景始终传 token：

- UI 面板生命周期绑定
- 场景切换预加载
- 临时弹窗和短生命周期对象

加载器内部会把异步请求与当前加载器实例绑定。即使加载器被回收复用，旧请求也不会把新实例的引用计数误扣掉。

## 自定义加载器

只需继承 `ResLoader`，提供专属的 `LoaderName`，并实现加载与卸载逻辑即可：

```csharp
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class RawTextLoader : ResLoader
{
    public override string LoaderName => "RawText";

    protected override ResData LoadRealSync(string path)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, path);
        string content = File.ReadAllText(fullPath);
        return new ResData { Asset = new TextAsset(content) { name = path } };
    }

    protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, path);
        using (StreamReader reader = new StreamReader(fullPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string content = await reader.ReadToEndAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return new ResData { Asset = new TextAsset(content) { name = path } };
        }
    }

    protected override void UnloadReal(ResData data)
    {
        if (data.Asset != null)
        {
            UnityEngine.Object.Destroy(data.Asset);
        }
    }
}
```

## 常见问题

### Q1: 调用 `Unload` 会不会影响别的持有者？
不会。它只会把当前加载器对该资源的引用减 1。只要全局 `RefCount > 0`，底层就不会物理卸载。

### Q2: 为什么销毁了实例，内存还没下来？
`Destroy(gameObject)` 只销毁场景实例，不会自动释放底层资产。还需要：

- `loader.Unload("路径")`
- 或 `ResKit.Recycle(loader)`

### Q3: 取消加载后会发生什么？
- 当前调用方取消后，不会再收到结果
- 共享加载会尽量避免重复发起物理 IO
- 如果所有等待者都取消，底层会尝试中断共享加载，并避免把无人持有的结果长期留在缓存中

### Q4: 强力清理怎么用？

```csharp
ResMgr.GarbageCollect();
```

该方法会触发 `GC.Collect()` 和 `Resources.UnloadUnusedAssets()`，不要在高频逻辑里调用。

## 对应示例

- 代码示例：[RawTextLoader.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_ResKit/RawTextLoader.cs:1>)
- 额外文档：
  - [ResKit-Resources-内置资源-Guide.md](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/ResourceLoader/ResKit-Resources-内置资源-Guide.md>)
  - [ResKit-AssetBundle-资源包-Guide.md](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/ResKit-AssetBundle-资源包-Guide.md>)
  - [ResKit-Addressables-可寻址资源-Guide.md](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader/ResKit-Addressables-可寻址资源-Guide.md>)
