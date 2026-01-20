# ResKit Addressables 实战指南 (本地模式)

**版本**: v1.1
**适用**: StellarFramework (ResKit 模块)
**场景**: 使用 Addressables (AA) 彻底替代 Resources 文件夹，实现零冗余的本地资源加载（不涉及热更新）。

---

## 1. 环境配置 (Setup)

ResKit 默认采用宏定义隔离 AA 代码，防止未安装插件的项目报错。使用前必须完成以下步骤：

### 1.1 安装插件
1.  `Window` -> `Package Manager` -> `Unity Registry`。
2.  搜索并安装 **Addressables**。

### 1.2 开启宏定义 (必做)
1.  `Edit` -> `Project Settings` -> `Player`。
2.  在 **Scripting Define Symbols** 中添加：
    ```text
    UNITY_ADDRESSABLES
    ```
3.  点击 Apply 等待编译。

---

## 2. 资源打包配置 (Build Settings)

我们要实现的是“本地单机模式”，即资源打进包体 (StreamingAssets)，不走网络下载。

### 2.1 创建 Group
1.  打开 `Window` -> `Asset Management` -> `Addressables` -> `Groups`。
2.  点击 `Create Addressables Settings`（如已创建则跳过）。
3.  在窗口中右键 -> `Create New Group` -> `Packed Assets`，命名为 `LocalResGroup`（或者直接用默认的 Default Local Group）。

### 2.2 修改加载路径 (核心)
选中刚才创建的 Group，在 **Inspector** 面板设置：
*   **Build Path**: `LocalBuildPath`
*   **Load Path**: `LocalLoadPath`
*   **Bundle Mode**: `Pack Together` (建议初学者选这个，类似 Resources 打包方式)

> **注意**：绝对不要选 `RemoteBuildPath`，否则资源会被打到工程外的 ServerData 目录，导致真机运行丢失资源。

### 2.3 标记资源与寻址 (Addressing)
1.  选中资源（如 `Assets/Prefabs/Hero.prefab`）。
2.  在 Inspector 勾选 **Addressable**。
3.  **修改 Address Name**：
    *   默认是完整路径：`Assets/Prefabs/Hero.prefab`。
    *   **建议改为短路径**：`Hero` 或 `Prefabs/Hero`。
    *   *ResKit 加载时传的 `path` 参数必须与这里的 Address Name 完全一致。*

---

## 3. 开发工作流 (Workflow)

### 3.1 编辑器模式 (Play Mode Script)
在 Groups 窗口顶部工具栏，修改 **Play Mode Script**：
*   **Use Asset Database (fastest)**: 开发阶段用这个。直接读文件，无需打包，秒进游戏。
*   **Use Existing Build**: 真机打包前测试用。需要先执行 Build 操作，模拟真实的 Bundle 加载。

### 3.2 真机打包流程
在构建 APK/IPA 之前，必须先构建资源包：
1.  打开 Groups 窗口。
2.  点击 `Build` -> `New Build` -> `Default Build Script`。
3.  等待构建完成（文件会生成到 `Assets/StreamingAssets/aa` 目录下）。
4.  执行常规的 `Build Settings` -> `Build` 打包游戏。

---

## 4. 代码接入 (Coding)

### 4.1 全局切换 (UIKit / Audio)
在游戏入口（如 `GameEntry.cs`）初始化时指定加载器类型：

```csharp
void Start()
{
    // UI 系统切换为 AA 加载模式
    UIKit.Instance.Init(ResLoaderType.Addressable);
    
    // 音频系统切换为 AA 加载模式
    AudioManager.Instance.Init(ResLoaderType.Addressable);
}
```

### 4.2 业务逻辑加载
在具体的业务脚本中，申请 `AddressableLoader`。

#### 方式 A：异步加载 (推荐)
```csharp
async UniTask LoadExample()
{
    // 1. 申请加载器
    var loader = ResKit.Allocate<AddressableLoader>();

    // 2. 加载 (参数为 Address Name)
    GameObject prefab = await loader.LoadAsync<GameObject>("Hero");

    if (prefab != null) Instantiate(prefab);

    // 3. 卸载 (引用计数 -1)
    // 建议在 OnDestroy 中调用 loader.Recycle2Cache() 或 ResKit.Recycle(loader)
}
```

#### 方式 B：同步加载 (仅 Unity 2021.2+)
ResKit 已通过 `WaitForCompletion` 实现了同步接口，适合老代码迁移。

```csharp
void LoadSyncExample()
{
    var loader = ResKit.Allocate<AddressableLoader>();
    
    // 会阻塞主线程直到加载完成
    GameObject prefab = loader.Load<GameObject>("Hero");
}
```

---

## 5. 常见问题 (FAQ)

### Q1: 加载出来的模型是粉色的 (Shader丢失)
**原因**: AA/AssetBundle 打包时剔除了未被引用的 Shader 变体。
**解决**:
1.  将 Shader 加入 `Project Settings` -> `Graphics` -> `Always Included Shaders`。
2.  或者将 Shader 放入一个专门的 Group 并打成 Bundle。

### Q2: 报错 `InvalidKeyException`
**原因**: 代码里传的 path 和 Groups 窗口里的 Address Name 不一致。
**解决**: 检查拼写。注意 `Resources.Load` 忽略后缀名，但 AA 的 Address Name 是你自己定的字符串，如果你的 Address 是 `Hero.prefab`，代码里传 `Hero` 就会找不到。

### Q3: 更新资源后真机没变化
**原因**: 没有重新 Build Addressables。
**解决**: 每次修改资源并准备打真机包前，必须执行 `Build` -> `New Build` (或 `Update a Previous Build`)。