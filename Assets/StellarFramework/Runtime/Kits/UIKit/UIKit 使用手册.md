# UIKit 使用手册

**适用**: StellarFramework UI 模块  
**定位**: 自动化层级管理、强类型数据传递、基于策略模式与异步流的 UI 框架。

---

## 1. 核心特性
*   **策略模式加载**：底层解耦，通过 `IUILoadStrategy` 接口支持多种资源加载方案（Resources、AssetBundle、Addressables 等）。默认内置 `ResKitUILoadStrategy`。
*   **强类型数据流转**：要求使用继承自 `UIPanelDataBase` 的数据类进行传参，减少值类型装箱与运行时类型异常。
*   **约定优于配置**：`UIKit.OpenPanelAsync<LoginPanel>()` 直接通过类名加载，无需配置映射表。
*   **层级管理**：内置 `Bottom/Middle/Top/Popup/System` 五个层级，处理基础的遮挡关系。
*   **异步驱动**：基于 `UniTask`，支持 `await` 等待界面加载与初始化完毕。

---

## 2. 目录与资源规范
在使用默认的 `ResKitUILoadStrategy` 时，请遵守以下目录结构：

### 2.1 存放位置
UI 面板 Prefab 建议存放在：
> `Assets/StellarFramework/Resources/UIPanel/` (或您配置的 ResKit 资源目录下)

### 2.2 命名规则
*   **Prefab 文件名**：必须与 **C# 类名** 一致。
*   **示例**：
    *   脚本：`public class LoginPanel : UIPanelBase`
    *   Prefab：`UIPanel/LoginPanel.prefab`

---

## 3. 环境搭建与初始化

### 3.1 生成 UIRoot
首次使用前，需生成 UI 根节点（含 Canvas, EventSystem, Camera）。
1.  通过 Tools Hub 调用生成逻辑。
2.  生成位置：`Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`。

### 3.2 初始化框架
在游戏入口 (`GameEntry.cs`) 中调用初始化方法：

```csharp
void Start() 
{
    // 方式 A：使用默认加载策略 (ResKitUILoadStrategy) 同步初始化
    UIKit.Instance.Init();

    // 方式 B：使用默认加载策略异步初始化 (推荐)
    // await UIKit.Instance.InitAsync();

    // 方式 C：注入自定义加载策略 (必须在 Init 之前调用)
    // var myStrategy = new MyAddressablesLoadStrategy();
    // UIKit.Instance.Configure(myStrategy);
    // await UIKit.Instance.InitAsync();
}
```

---

## 4. 开发工作流 (Workflow)

### 第一步：定义面板数据 (可选)
如果面板需要接收参数，定义一个继承自 `UIPanelDataBase` 的类：
```csharp
using StellarFramework.UI;

public class LoginPanelData : UIPanelDataBase
{
    public string DefaultAccount;
}
```

### 第二步：创建脚本
新建 C# 脚本，继承 `UIPanelBase`。
```csharp
using StellarFramework.UI;
using UnityEngine.UI;

public class LoginPanel : UIPanelBase
{
    public Button btnLogin;

    // 1. 初始化 (只在 Prefab 首次实例化时执行一次)
    public override void OnInit()
    {
        btnLogin.onClick.AddListener(CloseSelf); // 使用基类提供的便捷关闭方法
    }

    // 2. 打开时 (每次 Open 都会执行)
    public override void OnOpen(UIPanelDataBase data)
    {
        // 使用基类提供的强类型取参方法
        if (TryGetPanelData<LoginPanelData>(data, out var loginData))
        {
            Debug.Log("Default Account: " + loginData.DefaultAccount);
        }
    }

    // 3. 刷新时 (在界面已打开的情况下，再次调用 OpenPanel 或 RefreshPanel 时执行)
    public override void OnRefresh(UIPanelDataBase data)
    {
    }

    // 4. 关闭时
    public override void OnClose()
    {
    }
}
```

### 第三步：调用 API
```csharp
// 异步打开 (带参数)
var data = new LoginPanelData { DefaultAccount = "Admin" };
await UIKit.OpenPanelAsync<LoginPanel>(data);

// 同步打开 (无参数)
UIKit.OpenPanel<LoginPanel>();

// 刷新已打开的面板
UIKit.RefreshPanel<LoginPanel>(newData);

// 预加载面板但不显示
await UIKit.PreloadPanelAsync<LoginPanel>();

// 关闭面板
UIKit.ClosePanel<LoginPanel>();

// 销毁所有面板
UIKit.DestroyAllPanels();
```

---

## 5. 核心功能详解

### 5.1 层级系统 (Layer)
在 Prefab 的 Inspector 中设置 `Layer` 属性：
*   **Bottom**: 最底层 (主界面背景)。
*   **Middle**: 默认层 (全屏界面)。
*   **Top**: 顶层 (跑马灯、货币栏)。
*   **Popup**: 弹窗层 (确认框)。
*   **System**: 系统层 (Loading 界面)。

### 5.2 关闭模式 (Destroy On Close)
*   **True**: `Close` 时直接 `Destroy` 销毁物体，并通知策略层卸载资源（适用于不常用的界面）。
*   **False**: `Close` 时仅 `SetActive(false)` 与禁用交互（适用于高频打开的界面）。

### 5.3 屏幕适配
框架默认分辨率为 1920x1080。如需修改，在初始化后调用：
```csharp
UIKit.Instance.SetResolution(new Vector2(1280, 720), 1.0f); // 1.0 = 宽适配
```

---

## 6. 常见问题 (FAQ)

### Q1: 报错 `Configure 失败: UIKit 已初始化`
*   **原因**：`Configure` 必须在 `Init` 或 `InitAsync` 之前调用。
*   **解决**：调整代码顺序，确保注入策略在最前面。

### Q2: 界面打开了，但是无法点击？
*   **原因 1**：场景中缺少 `EventSystem`。
*   **原因 2**：被更高层级的透明 Panel 遮挡。
*   **原因 3**：CanvasGroup 的 `Interactable` 或 `Blocks Raycasts` 被设为了 false。