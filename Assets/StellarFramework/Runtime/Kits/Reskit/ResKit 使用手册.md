# ResKit 核心使用手册

**适用**: StellarFramework 资源管理核心

## 1. 设计理念 (Design Philosophy)
Unity 的资源加载方式多种多样（Resources, AssetBundle, Addressables）。
ResKit 旨在提供一套底层解耦、上层统一的资源调度方案。

### 核心特性
*   **统一接口**: 无论底层是 Resources 还是 AA/AB，上层业务逻辑只需调用 `IResLoader`。
*   **引用计数 (Ref Counting)**: 自动管理资源生命周期。当资源的引用计数归零时，进入卸载流程。
*   **并发去重 (Task Deduplication)**: 同一帧内对同一路径发起多次异步请求，底层只会执行 1 次 IO 加载，共享结果，减少并发开销。
*   **低 GC 句柄**: 加载器 (`ResLoader`) 本身是池化的，使用完回收即可。
*   **控制反转 (IoC)**: 底层 `ResMgr` 移除了枚举硬编码，卸载逻辑由各个 Loader 实例通过委托注入，符合开闭原则。

---

## 2. 核心架构 (Under the hood)

### 2.1 引用计数机制
`ResMgr` 维护了一个全局缓存 `Dictionary<string, ResData>`。
*   **Load**:
    *   如果缓存有：`RefCount++`，直接返回。
    *   如果缓存无：执行加载，`RefCount = 1`，存入缓存。
*   **Unload**:
    *   `RefCount--`。
    *   如果 `RefCount == 0`：调用 `ResData` 中注入的 `UnloadAction` 委托执行卸载，并从缓存移除。

### 2.2 任务去重 (Task Deduplication)
当 `LoadAsync("A")` 正在进行中（尚未返回），如果再次调用 `LoadAsync("A")`，系统不会发起新的 IO 请求，而是直接返回正在进行的 `UniTask`。

---

## 3. 使用指南 (How To Use)

### 3.1 基础流程：分配 -> 加载 -> 释放
这是使用 ResKit 的标准流程。

```csharp
public class HeroLoader : MonoBehaviour
{
    // 1. 持有一个加载器
    private IResLoader _loader;

    void Start()
    {
        // 2. 分配加载器 (从对象池获取)
        _loader = ResKit.Allocate<ResourceLoader>();
        LoadAvatar();
    }

    async void LoadAvatar()
    {
        // 3. 异步加载资源
        var prefab = await _loader.LoadAsync<GameObject>("Characters/Hero");
        if (prefab != null) 
        {
            Instantiate(prefab, transform);
        }
    }

    void OnDestroy()
    {
        // 4. 回收加载器
        // 回收时会自动释放该加载器加载过的所有资源的引用计数
        if (_loader != null)
        {
            ResKit.Recycle(_loader);
            _loader = null;
        }
    }
}
```

### 3.2 单个资源的精准卸载
如果不需要回收整个加载器，只是想单独释放某一个资源：
```csharp
// 仅将 "Characters/Hero" 的引用计数 -1
_loader.Unload("Characters/Hero");
```

### 3.3 批量预加载 (Loading 界面)
适用于场景切换时的预加载。
```csharp
public async UniTask PreloadAssets()
{
    var paths = new List<string> { "Prefabs/Boss", "Textures/Bg", "Audio/BGM" };
    var loader = ResKit.Allocate<AssetBundleLoader>();
    
    await loader.PreloadAsync(paths, (progress) => {
        Debug.Log($"加载进度: {progress * 100}%");
    });
    
    Debug.Log("预加载完成");
}
```

---

## 4. 扩展指南：自定义加载器 (Custom Loader)
只需继承 `ResLoader`，提供专属的 `LoaderName`，并实现加载与卸载逻辑即可：

```csharp
public class RawTextLoader : ResLoader
{
    public override string LoaderName => "RawText";

    protected override ResData LoadRealSync(string path)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, path);
        string content = File.ReadAllText(fullPath);
        return new ResData { Asset = new TextAsset(content) { name = path } };
    }

    protected override async UniTask<ResData> LoadRealAsync(string path)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, path);
        using (StreamReader reader = new StreamReader(fullPath))
        {
            string content = await reader.ReadToEndAsync();
            return new ResData { Asset = new TextAsset(content) { name = path } };
        }
    }

    protected override void UnloadReal(ResData data)
    {
        if (data.Asset != null) UnityEngine.Object.Destroy(data.Asset);
    }
}
```

---

## 5. 常见问题解答 (FAQ)

### Q1: 调用 `loader.Unload("Hero")`，会不会导致其他正在使用 "Hero" 的界面异常？
不会。业务层调用的 `Unload` 语义是：“当前加载器不再需要这个资源了”。底层会将其引用计数减 1。只要还有其他加载器在使用这个资源（`RefCount > 0`），底层不会执行物理卸载。

### Q2: 为什么 Destroy 了 GameObject，内存还没降下来？
`Destroy(gameObject)` 只是销毁了场景中的实例 (Instance)，并没有销毁内存中的资产 (Asset/Prefab)。必须调用 `loader.Unload("路径")` 或者 `ResKit.Recycle(loader)`，触发引用计数归零，资产才会被卸载。

### Q3: 强力内存清理怎么用？
```csharp
// 仅在场景切换、收到系统低内存告警时调用
ResMgr.GarbageCollect();
```
该方法会触发 `GC.Collect()` 和 `Resources.UnloadUnusedAssets()`，会引起主线程阻塞，避免在高频逻辑中调用。