# ResKit AssetBundle (AB) 完整开发与构建指南
**版本**: v2.2 (Build Pipeline & Shader Warmup)  
**适用**: StellarFramework ResKit 模块

## 1. 核心概念与优势
相比于原生的 AssetBundle 开发，ResKit 提供了一套高度自动化的工作流：
*   **可视化规则**：告别手动在 Inspector 底部敲名字，支持拖拽文件夹批量配置。
*   **Shader 自动归集**：自动扫描所有资源的依赖，将散落的 Shader 提取到一个独立的 `shaders` 包中，并在游戏启动时自动预热 (WarmUp)，彻底解决**材质变粉**和**首次特效卡顿**问题。
*   **强类型路径映射**：构建时自动生成 `AssetMap.cs`，运行时加载只需传入原始的 `Assets/...` 路径，底层自动映射到对应的 Bundle。

---

## 2. 构建工作流 (Editor Pipeline)

### 步骤 1：打开构建工具
在 Unity 顶部菜单栏点击 `StellarFramework -> Tools Hub` (或快捷键 `Ctrl/Cmd + Shift + T`)，在左侧边栏选择 **[框架核心] -> 资源打包 (AssetBundle)**。

### 步骤 2：配置打包规则
1. 将 Project 窗口中的**文件夹**或**单个文件**拖拽到工具左侧的“拖拽到此处”区域。
2. 工具会自动为其分配一个 Bundle Name（默认基于文件夹路径生成，全小写，如 `assets_art_props`）。
3. 你可以在右侧面板点击选中规则，手动修改 Bundle Name。

### 步骤 3：应用规则与生成代码
点击顶部的 **"应用规则 & 生成代码"** 按钮。
*   **底层行为**：工具会遍历规则中的所有文件，自动设置它们的 `assetBundleName`。
*   **Shader 剥离**：工具会自动查找这些资源的依赖项，将所有 Shader 强行标记为 `shaders` 包。
*   **代码生成**：在 `Assets/StellarFramework/Generated/` 目录下生成 `AssetMap.cs`，记录 `资源路径 -> Bundle名` 的映射关系。

### 步骤 4：执行构建
点击顶部的 **"增量构建"** 按钮。
*   构建产物会输出到 `StreamingAssets/AssetBundles/[平台名称]` 目录下。
*   工具会自动清理旧的、不再使用的冗余 Bundle 文件。

---

## 3. 运行时代码编写 (Runtime Coding)

### 3.1 强制初始化 (预热 Shader)
在游戏启动的入口处（如 `GameEntry`），**必须**异步初始化 `AssetBundleManager`。
这一步会加载 Manifest 依赖树，并加载 `shaders` 包执行 `WarmUp`。

```csharp
using StellarFramework.Res.AB;

public class GameEntry : MonoBehaviour
{
    async void Start()
    {
        // 必须异步初始化，以兼容 WebGL 平台并预热 Shader
        await AssetBundleManager.Instance.InitAsync();
        Debug.Log("AB 系统与 Shader 预热完成！");
        
        // 进入后续游戏逻辑...
    }
}
```

### 3.2 资源加载与实例化
使用 `AssetBundleLoader` 进行资源加载。你不需要关心它在哪个 Bundle 里，直接传入它在工程中的完整路径即可。

```csharp
using StellarFramework.Res;
using Cysharp.Threading.Tasks;

public class PropLoader : MonoBehaviour 
{
    private IResLoader _loader;

    void Start() 
    {
        // 1. 申请加载器
        _loader = ResKit.Allocate<AssetBundleLoader>();
        LoadPropAsync().Forget();
    }

    async UniTaskVoid LoadPropAsync() 
    {
        // 2. 传入在 AssetMap 中注册的完整路径
        string path = "Assets/Art/Props/TreasureBox.prefab";
        var prefab = await _loader.LoadAsync<GameObject>(path);
        
        if (prefab != null) 
        {
            Instantiate(prefab, transform);
        }
    }

    void OnDestroy() 
    {
        // 3. 回收加载器 (触发引用计数 -1)
        ResKit.Recycle(_loader);
    }
}
```

---

## 4. 内存管理与严格规范 (极度重要)

在 v2.1 版本中，为了彻底解决 AB 模式下的**内存镜像冗余**问题，底层卸载逻辑已从 `bundle.Unload(false)` 升级为 `bundle.Unload(true)`。

### 4.1 什么是 `Unload(true)`？
当一个 Bundle 的引用计数归零时，框架会直接销毁该 Bundle 及其加载出来的**所有内存资产**（Texture, Mesh, Material 等）。

### 4.2 业务层强制约束 (防变粉警告)
**严禁**在场景中还有该 Bundle 实例化的 GameObject 存活时，调用 `ResKit.Recycle`。
如果提前 Recycle 导致引用计数归零，场景中的物体会瞬间丢失材质（变粉）或丢失网格。

**正确做法**：
必须先 `Destroy(gameObject)`，然后再 `ResKit.Recycle(loader)`。

---

## 5. 并发死锁防御机制
Unity 底层严禁对同一个 AssetBundle 同时进行异步加载和同步加载。
框架已在 `AssetBundleManager` 中加入了严格的自旋锁与状态拦截：
*   如果 Bundle A 正在 `LoadAssetAsync`，此时任何地方调用 `LoadAssetSync` 请求 Bundle A，框架会**直接拦截并报错**，防止主线程死锁。
*   **规范**：在商业项目中，请统一使用 `LoadAsync` 异步链路。