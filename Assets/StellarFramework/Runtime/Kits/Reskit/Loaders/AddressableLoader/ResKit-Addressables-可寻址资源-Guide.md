# ResKit Addressables / 可寻址资源

**适用**: StellarFramework ResKit 模块

## 1. 核心概念
ResKit 提供了一套 AssetBundle 构建工作流：
*   **可视化规则**：支持在工具面板中拖拽文件夹进行批量配置。
*   **Shader 归集**：扫描资源的依赖，将 Shader 提取到独立的 `shaders` 包中，并在游戏启动时预热 (WarmUp)，缓解材质加载时的耗时。
*   **路径映射**：构建时生成 `AssetMap.cs`，运行时加载只需传入原始的 `Assets/...` 路径，底层会自动映射到对应的 Bundle。

---

## 2. 构建工作流 (Editor Pipeline)

### 步骤 1：打开构建工具
在 Unity 顶部菜单栏点击 `StellarFramework -> Tools Hub`，在左侧边栏选择 **[框架核心] -> 资源打包 (AssetBundle)**。

### 步骤 2：配置打包规则
1. 将 Project 窗口中的文件夹或文件拖拽到工具左侧的区域。
2. 工具会自动分配一个 Bundle Name（默认基于文件夹路径生成，如 `assets_art_props`）。
3. 可以在右侧面板手动修改 Bundle Name。

### 步骤 3：应用规则与生成代码
点击顶部的 **"应用规则 & 生成代码"** 按钮。
*   **底层行为**：工具会遍历规则中的文件，设置其 `assetBundleName`。
*   **Shader 剥离**：将依赖的 Shader 标记为 `shaders` 包。
*   **代码生成**：在 `Assets/StellarFramework/Generated/AssetMap/AssetMap.cs` 生成 `AssetMap.cs`。

### 步骤 4：执行构建
点击顶部的 **"增量构建"** 按钮。
*   构建产物输出到 `StreamingAssets/AssetBundles/[平台名称]` 目录下。
*   工具会自动清理旧的冗余 Bundle 文件。

---

## 3. 运行时代码编写 (Runtime Coding)

### 3.1 异步初始化 (预热 Shader)
在游戏启动的入口处，需要异步初始化 `AssetBundleManager`，以加载 Manifest 并预热 Shader。

```csharp
using StellarFramework.Res.AB;

public class GameEntry : MonoBehaviour
{
    async void Start()
    {
        await AssetBundleManager.Instance.InitAsync();
        Debug.Log("AB 系统与 Shader 预热完成");
    }
}
```

### 3.2 资源加载与实例化
使用 `AssetBundleLoader` 进行资源加载。传入在 AssetMap 中注册的完整路径。

```csharp
using StellarFramework.Res;
using Cysharp.Threading.Tasks;

public class PropLoader : MonoBehaviour 
{
    private IResLoader _loader;

    void Start() 
    {
        _loader = ResKit.Allocate<AssetBundleLoader>();
        LoadPropAsync().Forget();
    }

    async UniTaskVoid LoadPropAsync() 
    {
        string path = "Assets/Art/Props/TreasureBox.prefab";
        var prefab = await _loader.LoadAsync<GameObject>(path);
        if (prefab != null) 
        {
            Instantiate(prefab, transform);
        }
    }

    void OnDestroy() 
    {
        if (_loader != null) ResKit.Recycle(_loader);
    }
}
```

---

## 4. 内存管理规范 (重要)

为了解决 AB 模式下的内存镜像冗余问题，底层卸载逻辑采用 `bundle.Unload(true)`。

### 4.1 `Unload(true)` 说明
当一个 Bundle 的引用计数归零时，框架会销毁该 Bundle 及其加载出来的所有内存资产（Texture, Mesh, Material 等）。

### 4.2 业务层约束
**不建议**在场景中还有该 Bundle 实例化的 GameObject 存活时，调用 `ResKit.Recycle`。
如果提前 Recycle 导致引用计数归零，场景中的物体可能会丢失材质或网格。

**建议做法**：
先 `Destroy(gameObject)`，然后再 `ResKit.Recycle(loader)`。

---

## 5. 并发加载限制
Unity 底层不支持对同一个 AssetBundle 同时进行异步加载和同步加载。
框架在 `AssetBundleManager` 中加入了状态拦截：如果 Bundle A 正在 `LoadAssetAsync`，此时调用 `LoadAssetSync` 请求 Bundle A，框架会输出错误日志并拦截请求。建议在项目中统一使用异步加载链路。
