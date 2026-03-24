# ResKit Resources 模式使用手册
**版本**: v2.0 (Lazy Unload Optimized)  
**适用**: StellarFramework ResKit 模块

## 1. 适用场景
Resources 模式是最原始、最简单的加载方式。
*   **优点**：无需打包，无需配置，直接通过相对路径加载。
*   **缺点**：包体臃肿，应用启动慢，无法热更。
*   **建议**：仅用于全局常驻的轻量级配置文件（如 ConfigKit 的 JSON）、极少数核心 UI 预制体。大型 3D 资源严禁放入 Resources 目录。

---

## 2. 基础加载
资源必须存放在工程的 `Resources` 目录下。

```csharp
public class ConfigLoader : MonoBehaviour
{
    private ResLoader _loader;

    void Start()
    {
        _loader = ResKit.Allocate<ResourceLoader>();
        
        // 路径不需要带 "Resources/"，也不需要带后缀名
        TextAsset txt = _loader.Load<TextAsset>("Configs/GameSetting");
    }

    void OnDestroy()
    {
        ResKit.Recycle(_loader);
    }
}
```

---

## 3. 内存释放痛点与阈值惰性卸载 (核心机制)

### 3.1 Unity 原生 API 的缺陷
Unity 提供了 `Resources.UnloadAsset(Object)` 用于卸载单个资源。但是，**该 API 严禁传入 GameObject 或 Component**。
这意味着，如果你通过 Resources 加载了一个 Prefab，你无法单独将这个 Prefab 从内存中剔除，只能调用全局的 `Resources.UnloadUnusedAssets()`。

### 3.2 阈值惰性卸载机制 (Threshold Lazy Unload)
为了解决上述“伪卸载”导致的内存泄漏，同时避免频繁调用 `UnloadUnusedAssets` 导致严重的帧率卡顿，ResKit 在 `ResMgr` 中引入了**阈值惰性卸载**。

**运行原理**：
1. 当 `ResourceLoader` 卸载非 GameObject 资源（如 Texture, AudioClip）时，直接调用 `Resources.UnloadAsset`，瞬间释放，0 卡顿。
2. 当卸载 GameObject 资源时，框架会将其计入“待清理计数器”。
3. 当计数器达到阈值（默认 10 个）时，框架会在后台自动触发一次 `Resources.UnloadUnusedAssets()`，批量清理内存。

**业务影响**：
开发者无需关心底层的清理时机，只需严格遵守 `Allocate -> Load -> Recycle` 的生命周期规范即可。内存会在不影响流畅度的情况下自动维持在健康水位。