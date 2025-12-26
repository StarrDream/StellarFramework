# ResKit 使用手册

## 1. 设计理念 (Why)
Unity 的资源加载方式多种多样（Resources, AssetBundle, Addressables），接口各异且异步逻辑复杂。
*   **Resources**：API 简单但内存管理困难，打包包体大。
*   **Addressables (AA)**：功能强大但 API 繁琐，容易出现句柄（Handle）泄露，且异步逻辑容易写出 Bug。

**ResKit 的特性：**
*   **统一接口**：无论底层是 Resources 还是 AA，上层业务逻辑只需调用 `IResLoader`。
*   **引用计数 (Ref Counting)**：全自动管理资源生命周期。A 和 B 都加载了资源 C，只有当 A 和 B 都释放了，C 才会真正卸载。
*   **并发去重 (Task Deduplication)**：同一帧内对同一 URL 发起 10 次请求，底层只会执行 1 次加载，所有请求共享结果。
*   **零 GC 句柄**：加载器本身是池化的，使用完回收即可。

---

## 2. 核心架构 (Under the hood)

### 2.1 引用计数机制
`ResMgr` 维护了一个全局缓存 `Dictionary<string, ResData>`。
*   **ResData**：包含 `Asset` 引用、`RefCount` (引用计数) 和 `Handle` (AA专用)。
*   **Load**:
    *   如果缓存有：`RefCount++`，直接返回。
    *   如果缓存无：执行加载，`RefCount = 1`，存入缓存。
*   **Unload**:
    *   `RefCount--`。
    *   如果 `RefCount == 0`：调用底层 API (Resources.UnloadAsset 或 Addressables.Release) 真正卸载，并从缓存移除。

### 2.2 任务去重 (Task Deduplication)
这是 `ResMgr` 的一大亮点。
当 `LoadAsync("A")` 正在进行中（尚未返回），如果再次调用 `LoadAsync("A")`，系统**不会**发起新的 IO 请求，而是直接返回正在进行的 `UniTask`。
这在列表加载（List View）中非常有用，防止因瞬间刷新导致对同一张图片发起几十次重复请求。

---

## 3. 使用指南 (How)

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
        // 默认为 Resources 模式，如需 AA 可传入 ResLoaderType.Addressable
        _loader = ResKit.Allocate<ResourceLoader>();

        LoadAvatar();
    }

    async void LoadAvatar()
    {
        // 3. 异步加载资源
        // 注意：这里返回的是 Unity Object，需要强转或指定泛型
        var prefab = await _loader.LoadAsync<GameObject>("Characters/Hero");
        
        if (prefab != null) {
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

### 3.2 批量预加载 (Loading 界面)
适用于场景切换时的 Loading 进度条。

```csharp
public async void PreloadAssets()
{
    var paths = new List<string> { 
        "Prefabs/Boss", 
        "Textures/Bg", 
        "Audio/BGM_Battle" 
    };

    var loader = ResKit.Allocate<ResourceLoader>();

    // PreloadAsync 内部做了时间切片优化，不会卡死主线程
    await loader.PreloadAsync(paths, (progress) => {
        Debug.Log($"加载进度: {progress * 100}%");
        // Update UI Slider...
    });

    Debug.Log("预加载完成");
    
    // 注意：预加载的 loader 不要立即 Recycle，否则资源刚加载完就卸载了。
    // 通常将这个 loader 传递给下一个场景的管理器，或者在切场景后释放。
}
```

---

## 4. 常见坑点 (Pitfalls)

### Q1: 资源加载了但显示粉色 (Shader 丢失)
*   **原因**：在编辑器中使用 AssetBundle/AA 模式加载时，Shader 变体可能被剔除。
*   **解决**：这是 Unity 编辑器环境的经典问题。
    *   方案A：在 Project Settings -> Graphics -> Always Included Shaders 中添加相关 Shader。
    *   方案B：在 Addressables 设置中开启 "Use Asset Database (fast mode)" 模拟模式。

### Q2: `Recycle` 后资源还在内存中？
*   **原因**：引用计数机制。可能还有其他 Loader (比如全局的 AudioManager) 也加载了该资源且未释放。
*   **验证**：只有当所有引用该资源的 Loader 都执行了 `Recycle` 或 `ReleaseAll`，资源才会真正 Unload。

### Q3: Addressable 模式下报错 "Handle Released"
*   **原因**：你手动调用了 `Addressables.Release`，同时 ResKit 也尝试释放。
*   **解决**：使用 ResKit 后，**绝对禁止**再手动调用原生 API (`Addressables.Load/Release`)，一切交给 Loader 管理。