# ResKit 核心使用手册

**版本**: v3.1 (IoC & Generic Refactoring)  
**适用**: StellarFramework 资源管理核心

## 1. 设计理念 (Design Philosophy)
Unity 的资源加载方式多种多样（Resources, AssetBundle, Addressables），接口各异且异步逻辑复杂。
ResKit 旨在提供一套**底层解耦、上层统一**的资源调度方案。

### 核心特性
*   **统一接口**: 无论底层是 Resources 还是 AA/AB，上层业务逻辑只需调用 `IResLoader`。
*   **引用计数 (Ref Counting)**: 全自动管理资源生命周期。A 和 B 都加载了资源 C，只有当 A 和 B 都释放了，C 才会真正进入卸载流程。
*   **并发去重 (Task Deduplication)**: 同一帧内对同一 URL 发起 10 次异步请求，底层只会执行 1 次 IO 加载，所有请求共享结果，杜绝并发浪费。
*   **零 GC 句柄**: 加载器 (`ResLoader`) 本身是池化的，使用完回收即可，不产生堆内存垃圾。
*   **控制反转 (IoC)**: 底层 `ResMgr` 彻底移除了枚举硬编码，卸载逻辑由各个 Loader 实例通过委托注入，完全符合开闭原则。

---

## 2. 核心架构 (Under the hood)

### 2.1 引用计数机制
`ResMgr` 维护了一个全局缓存 `Dictionary<string, ResData>`。
*   **Load**:
    *   如果缓存有：`RefCount++`，直接返回。
    *   如果缓存无：执行加载，`RefCount = 1`，存入缓存。
*   **Unload**:
    *   `RefCount--`。
    *   如果 `RefCount == 0`：调用 `ResData` 中注入的 `UnloadAction` 委托真正卸载，并从缓存移除。

### 2.2 任务去重 (Task Deduplication)
当 `LoadAsync("A")` 正在进行中（尚未返回），如果再次调用 `LoadAsync("A")`，系统**不会**发起新的 IO 请求，而是直接返回正在进行的 `UniTask`。这在列表加载（List View）中非常有用，防止因瞬间刷新导致对同一张图片发起几十次重复请求。

---

## 3. 使用指南 (How To Use)

### 3.1 基础流程：分配 -> 加载 -> 释放
这是使用 ResKit 的标准三部曲，缺一不可。

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
        // 4. 回收加载器 (非常重要！)
        // 回收时会自动释放该加载器加载过的所有资源的引用计数
        ResKit.Recycle(_loader);
        _loader = null;
    }
}
```

### 3.2 单个资源的精准卸载
如果你不需要回收整个加载器，只是想单独释放某一个资源：
```csharp
// 仅将 "Hero" 的引用计数 -1
_loader.Unload("Characters/Hero");
```

### 3.3 批量预加载 (Loading 界面)
适用于场景切换时的 Loading 进度条。

```csharp
public async UniTask PreloadAssets()
{
    var paths = new List<string> { "Prefabs/Boss", "Textures/Bg", "Audio/BGM" };
    var loader = ResKit.Allocate<AssetBundleLoader>();
    
    // PreloadAsync 内部做了时间切片优化，不会卡死主线程
    await loader.PreloadAsync(paths, (progress) => {
        Debug.Log($"加载进度: {progress * 100}%");
    });
    
    Debug.Log("预加载完成");
}
```

---

## 4. 扩展指南：自定义加载器 (Custom Loader)
得益于 v3.1 的架构重构，扩展自定义加载器变得极其简单，无需修改任何框架底层代码。

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

### Q1: 我调用 `loader.Unload("Hero")`，会不会导致其他正在使用 "Hero" 的界面变成白块？
**绝对不会。**
在 ResKit 的设计哲学里，业务层调用的 `Unload` 真实语义是：**“当前加载器不再需要这个资源了”**。
底层会将其引用计数减 1。只要还有其他加载器（比如其他 UI 面板）在使用这个资源，它的 `RefCount` 就大于 0，底层**绝对不会**执行物理卸载。只有当所有加载器都释放了它，内存才会被真正清空。请放心调用。

### Q2: 为什么我 Destroy 了 GameObject，内存还没降下来？
因为 `Destroy(gameObject)` 只是销毁了场景中的**实例 (Instance)**，并没有销毁内存中的**资产 (Asset/Prefab)**。
你必须调用 `loader.Unload("路径")` 或者直接 `ResKit.Recycle(loader)`，触发引用计数归零，资产才会被卸载。

### Q3: 强力内存清理怎么用？
```csharp
// 仅在场景切换、收到系统低内存告警时调用
ResMgr.GarbageCollect();
```
该方法会强制触发 `GC.Collect()` 和 `Resources.UnloadUnusedAssets()`，会引起主线程阻塞，**严禁**在战斗或高频逻辑中调用。