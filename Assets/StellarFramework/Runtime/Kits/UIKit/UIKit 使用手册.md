# UIKit 使用手册
**版本**: v2.0 (Multi-Loader Support)  
**适用**: StellarFramework  
**定位**: 自动化层级管理、泛型加载、支持异步流的 UI 框架。

---

## 1. 核心特性
*   **无缝切换加载器**：一套代码，通过初始化参数即可在 `Resources` (开发快) 和 `AssetBundle` (发布小) 模式间切换。
*   **约定优于配置**：`OpenPanelAsync<LoginPanel>()` 直接通过类名加载，无需配置 Key-Value 映射。
*   **层级管理**：内置 `Bottom/Middle/Popup/Top/System` 五层栈，自动处理遮挡关系。
*   **异步驱动**：全流程基于 `UniTask`，支持 `await` 等待界面打开动画播放完毕。

---

## 2. 目录与资源规范 (必读)
为了实现不同加载模式的兼容，请严格遵守以下目录结构：

### 2.1 存放位置
所有 UI 面板 Prefab **必须** 存放在：
> `Assets/Resources/UIPanel/`

### 2.2 命名规则
*   **Prefab 文件名**：必须与 **C# 类名** 完全一致。
*   **示例**：
    *   脚本：`public class LoginPanel : UIPanelBase`
    *   Prefab：`Assets/Resources/UIPanel/LoginPanel.prefab`

---

## 3. 环境搭建与初始化

### 3.1 生成 UIRoot
首次使用前，需生成 UI 根节点（含 Canvas, EventSystem, Camera）。
1.  点击菜单：`Tools -> StellarFramework -> UIKit -> 生成 / 覆盖 UIRoot Prefab`。
2.  生成位置：`Assets/Resources/UIPanel/UIRoot.prefab`。

### 3.2 初始化框架
在游戏入口 (`GameEntry.cs`) 中调用 `Init`。根据开发阶段选择模式：

```csharp
void Start() 
{
    // 模式 A：开发阶段 (Resources)
    // 优点：无需打包，修改 Prefab 后直接运行，速度快。
    UIKit.Instance.Init(ResLoaderType.Resources);

    // 模式 B：真机/测试阶段 (AssetBundle)
    // 优点：模拟真实环境，验证 AB 包加载逻辑。
    // 前提：必须先使用 "Tools Hub -> 资源打包" 将 UIPanel 文件夹打成 AB 包。
    // UIKit.Instance.Init(ResLoaderType.AssetBundle);
}
```

---

## 4. 开发工作流 (Workflow)

### 第一步：创建脚本
新建 C# 脚本，继承 `UIPanelBase`。

```csharp
using StellarFramework.UI;
using Cysharp.Threading.Tasks;

public class LoginPanel : UIPanelBase
{
    public Button btnLogin;

    // 1. 初始化 (只执行一次)
    public override void OnInit()
    {
        btnLogin.onClick.AddListener(CloseSelf);
    }

    // 2. 打开时 (每次 Open 都会执行)
    public override async UniTask OnOpen(object uiData = null)
    {
        await base.OnOpen(uiData); // 处理层级和交互开关
        
        // 如果有参数传递
        if (uiData is string userName) {
            Debug.Log("User: " + userName);
        }
        
        // 可以在这里播放入场动画
        // await PlayAnimation();
    }

    // 3. 关闭时
    public override async UniTask OnClose()
    {
        await base.OnClose();
    }
}
```

### 第二步：制作 Prefab
1.  在 Hierarchy 右键 -> `StellarFramework -> UIKit -> Panel Template` 创建标准模板。
2.  **重命名**：改为 `LoginPanel` (与类名一致)。
3.  **挂载脚本**：挂载 `LoginPanel.cs`。
4.  **保存**：拖入 `Assets/Resources/UIPanel/` 文件夹。

### 第三步：调用
```csharp
// 无参打开
UIKit.OpenPanelAsync<LoginPanel>();

// 带参打开
UIKit.OpenPanelAsync<UserInfoPanel>(new UserData { Id = 1001 });

// 关闭
UIKit.ClosePanel<LoginPanel>();
```

---

## 5. 核心功能详解

### 5.1 层级系统 (Layer)
在 Prefab 的 Inspector 中设置 `Layer` 属性：

| 层级 | 说明 | 典型用途 |
| :--- | :--- | :--- |
| **Bottom** | 最底层 | 主界面背景、大地图底图。 |
| **Middle** | 默认层 | 绝大多数全屏界面 (背包、角色、登录)。 |
| **Top** | 顶层 | 跑马灯、货币栏、非阻断式提示。 |
| **Popup** | 弹窗层 | 确认框、物品详情弹窗 (会遮挡 Middle)。 |
| **System** | 系统层 | Loading 界面、断线重连 (最高优先级，遮挡一切)。 |

### 5.2 关闭模式 (Destroy On Close)
在 Prefab 的 Inspector 中设置：
*   **True (勾选)**：`Close` 时直接 `Destroy` 销毁物体。
    *   *适用*：不常用的界面 (如设置、邮件)，节省内存。
*   **False (不勾)**：`Close` 时仅 `SetActive(false)`。
    *   *适用*：高频打开的界面 (如背包、角色详情)，以内存换速度。

### 5.3 屏幕适配
框架默认分辨率为 1920x1080 (Match WidthOrHeight = 0.5)。
如需修改，在初始化后调用：
```csharp
UIKit.Instance.SetResolution(new Vector2(1280, 720), 1.0f); // 1.0 = 宽适配
```

---

## 6. AssetBundle 模式注意事项

如果你决定切换到 `ResLoaderType.AssetBundle`，请务必注意：

1.  **UIRoot 也要打包**：
    `UIRoot.prefab` 也是一个资源。请确保 `Assets/Resources/UIPanel` 文件夹被包含在打包规则中。
2.  **路径一致性**：
    框架内部在 AB 模式下会使用 `Assets/Resources/UIPanel/xxx.prefab` 作为加载路径。请不要随意修改 `UIPanel` 文件夹的存放位置，否则会导致路径拼接失败。

---

## 7. 常见问题 (FAQ)

### Q1: 报错 `Prefab LoginPanel 缺少脚本组件！`
*   **原因**：Prefab 上没有挂载对应的 `LoginPanel` C# 脚本。
*   **解决**：检查 Prefab，确保挂载了继承自 `UIPanelBase` 的正确脚本。

### Q2: 界面打开了，但是点不动 (穿透)？
*   **原因 1**：场景中缺少 `EventSystem`。(`UIRoot` 自带一个，请勿删除)。
*   **原因 2**：被更高层级的透明 Panel 遮挡。检查 Hierarchy 中 `System` 或 `Popup` 层是否有未关闭的全屏透明物体。
*   **原因 3**：CanvasGroup 的 `Interactable` 或 `Blocks Raycasts` 被设为了 false。

### Q3: 为什么 `OnInit` 里获取不到 `uiData`？
*   **机制**：`OnInit` 仅在 Prefab 首次实例化时调用一次，用于绑定按钮事件等初始化工作。
*   **解决**：参数传递请在 `OnOpen(object uiData)` 中处理，因为 `OnOpen` 每次打开都会调用。