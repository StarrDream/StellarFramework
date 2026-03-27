# ResKit Resources 模式使用手册

**适用**: StellarFramework ResKit 模块

## 1. 适用场景
Resources 模式是基础的加载方式。
*   **优点**：无需打包配置，直接通过相对路径加载。
*   **限制**：包体容易变大，无法进行热更新。
*   **建议**：适用于全局常驻的轻量级配置文件（如 ConfigKit 的 JSON）或极少数核心预制体。大型资源建议使用 AB 或 AA 方案。

---

## 2. 基础加载
资源需存放在工程的 `Resources` 目录下。

```csharp
public class ConfigLoader : MonoBehaviour
{
    private IResLoader _loader;

    void Start()
    {
        _loader = ResKit.Allocate<ResourceLoader>();
        // 路径不需要带 "Resources/"，也不需要带后缀名
        TextAsset txt = _loader.Load<TextAsset>("Configs/GameSetting");
    }

    void OnDestroy()
    {
        if (_loader != null) ResKit.Recycle(_loader);
    }
}
```

---

## 3. 内存释放与阈值惰性卸载

### 3.1 Unity 原生 API 限制
Unity 提供的 `Resources.UnloadAsset(Object)` API 不支持传入 GameObject 或 Component。
这意味着通过 Resources 加载的 Prefab 无法使用该 API 单独剔除，通常需要调用全局的 `Resources.UnloadUnusedAssets()`。

### 3.2 阈值惰性卸载机制 (Threshold Lazy Unload)
为了缓解频繁调用 `UnloadUnusedAssets` 导致的帧率波动，ResKit 在 `ResMgr` 中引入了阈值惰性卸载。

**运行原理**：
1. 当 `ResourceLoader` 卸载非 GameObject 资源（如 Texture, AudioClip）时，直接调用 `Resources.UnloadAsset`。
2. 当卸载 GameObject 资源时，框架会将其计入待清理计数器。
3. 当计数器达到阈值（默认 10 个）时，框架会在后台触发一次 `Resources.UnloadUnusedAssets()` 进行批量清理。

开发者只需遵循 `Allocate -> Load -> Recycle` 的生命周期调用即可，底层会自动处理清理时机。