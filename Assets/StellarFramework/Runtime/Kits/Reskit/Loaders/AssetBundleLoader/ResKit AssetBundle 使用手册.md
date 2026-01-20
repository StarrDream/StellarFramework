# ResKit AssetBundle 开发指南

**版本**: v2.0 (Production Ready)  
**适用**: StellarFramework ResKit 模块  
**核心目标**: 提供一套从资源打包到代码加载的完整工作流，解决依赖丢失、Shader 变粉、异步死锁等痛点。

---

## 1. 核心工作流 (Workflow)

ResKit 的 AB 模式设计原则是 **"所见即所得"** 和 **"自动化"**。

1.  **配置 (Editor)**: 拖拽文件夹配置打包规则。
2.  **构建 (Build)**: 一键分析依赖、提取 Shader、生成 AB 包。
3.  **加载 (Runtime)**: 使用 `AssetBundleLoader` 加载资源。

---

## 2. 编辑器配置与打包

### 2.1 打开工具
在 Unity 菜单栏点击：`Tools -> StellarFramework -> Tools Hub`。
在侧边栏选择 **"资源打包 (AssetBundle)"**。

### 2.2 添加打包规则
你不需要手动给每个资源设置 AssetBundle Name，工具会帮你管理。

1.  **拖拽添加**: 将 Project 窗口中的文件夹（如 `Assets/Art/UI`）拖入工具左侧区域。
2.  **规则详解**:
    *   **Bundle Name**: 生成的 AB 包名（全小写）。
    *   **Path**: 资源路径。
    *   **Is Folder**:
        *   ✅ **勾选 (推荐)**: 该文件夹下的所有资源打成一个包（减少 IO 次数）。
        *   ❌ **不勾选**: 仅打包该文件本身。

### 2.3 自动 Shader 归集 (重要)
为了防止真机运行时材质变粉（Shader 丢失），工具内置了自动扫描逻辑：
*   **机制**: 在构建前，工具会自动扫描所有待打包资源引用的 Shader。
*   **结果**: 所有 Shader 会被强制提取并打入一个名为 `shaders` 的独立公共包中。
*   **好处**: 避免 Shader 在多个包中重复，且保证启动时一次性预热所有 Shader。

### 2.4 执行构建
点击工具栏的 **"应用规则 & 生成代码"**，然后点击 **"增量构建"**。
*   **产出目录**: `Assets/StreamingAssets/AssetBundles/[Platform]`
*   **生成代码**: `Assets/StellarFramework/Generated/AssetMap.cs` (用于路径映射)

---

## 3. 代码加载 (Runtime)

### 3.1 初始化 (GameEntry)
在游戏启动时，必须初始化 `AssetBundleManager`。通常在 `GameApp` 或 `GameEntry` 中完成。

```csharp
// 方式 A: 随着 UIKit 初始化 (推荐)
// UIKit 内部会自动初始化 ResKit
UIKit.Instance.Init(ResLoaderType.AssetBundle);

// 方式 B: 手动初始化 (如果不使用 UIKit)
AssetBundleManager.Instance.Init();
```

### 3.2 加载资源
使用 `ResKit.Allocate<AssetBundleLoader>()` 获取加载器。

**异步加载 (推荐):**
```csharp
public class HeroController : MonoBehaviour 
{
    private ResLoader _loader;

    void Start() 
    {
        _loader = ResKit.Allocate<AssetBundleLoader>();
        LoadHero();
    }

    async void LoadHero() 
    {
        // 传入资源路径 (Assets/.../Hero.prefab)
        var prefab = await _loader.LoadAsync<GameObject>("Assets/Art/Prefabs/Hero.prefab");
        if (prefab != null) 
        {
            Instantiate(prefab, transform);
        }
    }

    void OnDestroy() 
    {
        // 必须回收 Loader，否则引用计数无法归零，导致内存泄漏
        ResKit.Recycle(_loader);
    }
}
```

**同步加载:**
```csharp
void LoadSync() 
{
    var loader = ResKit.Allocate<AssetBundleLoader>();
    // 注意：不要对同一个资源同时调用 Load 和 LoadAsync，否则会报错
    var obj = loader.Load<GameObject>("Assets/Art/Data/Config.asset");
}
```

---

## 4. 常见问题与避坑 (Troubleshooting)

### Q1: 为什么加载失败，提示 "Manifest 加载失败"？
*   **原因**: 你可能没有执行构建。
*   **解决**: AB 模式不像 Resources 模式那样可以直接运行。**每次修改资源后，必须在 AB 工具中点击 "增量构建"**，确保 StreamingAssets 里有文件。

### Q2: 报错 "严重并发冲突: Bundle 正在异步加载中..."
*   **场景**: 你在代码 A 处调用了 `LoadAsync("Hero")`，紧接着在代码 B 处调用了 `Load("Hero")` (同步)。
*   **原理**: Unity 底层限制，当一个 AB 包正在后台线程读取时，主线程无法强制介入读取，否则会死锁。
*   **解决**:
    1.  **统一逻辑**: 尽量全盘使用异步 (`async/await`)。
    2.  **等待**: 如果必须同步，请确保之前的异步任务已经 `await` 结束。

### Q3: 材质变粉 (Pink Material)
*   **检查**: 确认是否执行了构建。
*   **检查**: 确认 `AssetBundleManager` 初始化时是否打印了 `[AssetBundleManager] Shader 预热完成`。
*   **解决**: 重新点击工具中的 "应用规则 & 生成代码"，确保 Shader 被正确归类到 `shaders` 包。

### Q4: 路径太长不想写 "Assets/..." 怎么办？
*   **方案**: 框架生成了 `AssetMap` 类。你可以自己封装一个扩展方法，或者直接使用生成的常量（如果有）。
*   **建议**: 保持全路径是最高效的，因为它作为 Key 唯一且直观。

---

## 5. 内存管理机制 (Memory)

ResKit 采用 **引用计数 (Reference Counting)** 管理内存。

1.  **加载**: `RefCount++`。
2.  **复用**: 如果 A 加载了 `TextureA`，B 也加载 `TextureA`，内存中只有一份 `TextureA`，但 `RefCount = 2`。
3.  **卸载**: 必须调用 `ResKit.Recycle(loader)`。此时 `RefCount--`。
4.  **释放**: 当 `RefCount == 0` 时，`AssetBundleManager` 会自动调用 `Bundle.Unload(false)` 释放内存。

> **警告**: 绝对不要手动调用 `Resources.UnloadAsset` 或 `AssetBundle.Unload`，这会破坏引用计数系统，导致程序崩溃。
