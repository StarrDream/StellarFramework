# UIKit 使用手册
**版本**: v3.0 (Strategy-Based Multi-Loader Support)  
**适用**: StellarFramework  
**定位**: 自动化层级管理、强类型数据传递、基于策略模式与异步流的 UI 框架。

---

## 1. 核心特性

*   **策略模式加载**：底层解耦，通过 `IUILoadStrategy` 接口支持任意资源加载方案（Resources、AssetBundle、Addressables 等）。默认内置 `ResKitUILoadStrategy`。
*   **强类型数据流转**：彻底废弃 `object` 弱类型传参，强制使用继承自 `UIPanelDataBase` 的数据类，杜绝值类型装箱与运行时类型异常。
*   **约定优于配置**：`UIKit.OpenPanelAsync<LoginPanel>()` 直接通过类名加载，无需配置 Key-Value 映射。
*   **层级管理**：内置 `Bottom/Middle/Top/Popup/System` 五层栈，自动处理遮挡关系。
*   **全异步驱动**：全流程基于 `UniTask`，支持 `await` 等待界面加载与初始化完毕。

---

## 2. 目录与资源规范 (必读)

在使用默认的 `ResKitUILoadStrategy` 时，请严格遵守以下目录结构：

### 2.1 存放位置
所有 UI 面板 Prefab **必须** 存放在：
> `Assets/StellarFramework/Resources/UIPanel/` (或您配置的 ResKit 资源目录下)

### 2.2 命名规则
*   **Prefab 文件名**：必须与 **C# 类名** 完全一致。
*   **示例**：
    *   脚本：`public class LoginPanel : UIPanelBase`
    *   Prefab：`UIPanel/LoginPanel.prefab`

---

## 3. 环境搭建与初始化

### 3.1 生成 UIRoot
首次使用前，需生成 UI 根节点（含 Canvas, EventSystem, Camera）。
1.  通过 Tools Hub 调用生成逻辑（底层调用 `UIKitEditor.CreateUIRootPrefab()`）。
2.  生成位置：`Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`。

### 3.2 初始化框架
在游戏入口 (`GameEntry.cs`) 中调用初始化方法。支持同步与异步两种方式：

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
如果面板需要接收参数，必须定义一个继承自 `UIPanelDataBase` 的类：
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
        // 使用基类提供的强类型取参方法，自带类型校验与错误日志
        if (TryGetPanelData<LoginPanelData>(data, out var loginData))
        {
            Debug.Log("Default Account: " + loginData.DefaultAccount);
        }
    }

    // 3. 刷新时 (在界面已打开的情况下，再次调用 OpenPanel 或 RefreshPanel 时执行)
    public override void OnRefresh(UIPanelDataBase data)
    {
        // 处理数据刷新逻辑
    }

    // 4. 关闭时
    public override void OnClose()
    {
        // 清理逻辑
    }
}
```

### 第三步：制作 Prefab
1.  在 Hierarchy 选中 Canvas 或 Layer 节点，右键 -> `StellarFramework -> UIKit -> Panel Template` 创建标准模板。
2.  **重命名**：改为 `LoginPanel` (与类名一致)。
3.  **挂载脚本**：挂载 `LoginPanel.cs`。
4.  **保存**：拖入 `UIPanel/` 文件夹，并从场景中删除。

### 第四步：调用 API
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

// 强制销毁所有面板
UIKit.DestroyAllPanels();
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
| **Popup** | 弹窗层 | 确认框、物品详情弹窗 (自带 CanvasGroup 方便控制阻断)。 |
| **System** | 系统层 | Loading 界面、断线重连 (最高优先级，遮挡一切)。 |

### 5.2 关闭模式 (Destroy On Close)
在 Prefab 的 Inspector 中设置：
*   **True (勾选)**：`Close` 时直接 `Destroy` 销毁物体，并通知策略层卸载资源。
    *   *适用*：不常用的界面 (如设置、邮件)，节省内存。
*   **False (不勾)**：`Close` 时仅 `SetActive(false)` 与禁用交互。
    *   *适用*：高频打开的界面 (如背包、主界面)，以内存换速度。

### 5.3 屏幕适配
框架默认分辨率为 1920x1080 (Match WidthOrHeight = 0.5)。
如需修改，在初始化后调用：
```csharp
UIKit.Instance.SetResolution(new Vector2(1280, 720), 1.0f); // 1.0 = 宽适配
```

---

## 6. 自定义加载策略 (高级)

如果项目不使用默认的 `ResKit`，可以通过实现 `IUILoadStrategy` 接口接入 Addressables 或其他加载方案：

```csharp
public class AddressablesUILoadStrategy : IUILoadStrategy
{
    public bool SupportSyncLoad => false; // Addressables 通常不支持纯同步

    public GameObject LoadUIRoot() => throw new NotImplementedException();
    
    public async UniTask<GameObject> LoadUIRootAsync()
    {
        // 实现 Addressables 加载 UIRoot 逻辑
    }

    // ... 实现其他接口方法 ...
}
```
在 `UIKit.Instance.InitAsync()` 之前调用 `UIKit.Instance.Configure(new AddressablesUILoadStrategy())` 即可完成替换。

---

## 7. 常见问题 (FAQ)

### Q1: 报错 `Configure 失败: UIKit 已初始化`
*   **原因**：`Configure` 必须在 `Init` 或 `InitAsync` 之前调用。初始化后禁止动态切换加载策略以防资源泄漏。
*   **解决**：调整代码顺序，确保注入策略在最前面。

### Q2: 报错 `面板数据类型不匹配` 或 `面板数据为空`
*   **原因**：外部调用 `OpenPanel` 传入的 `UIPanelDataBase` 子类，与面板内部 `TryGetPanelData<T>` 期望的泛型类型不一致，或者未传参。
*   **解决**：检查调用方传入的数据类型是否正确。

### Q3: 界面打开了，但是点不动 (穿透)？
*   **原因 1**：场景中缺少 `EventSystem`。(`UIRoot` 自带一个，请勿删除)。
*   **原因 2**：被更高层级的透明 Panel 遮挡。检查 Hierarchy 中 `System` 或 `Popup` 层是否有未关闭的全屏透明物体。
*   **原因 3**：CanvasGroup 的 `Interactable` 或 `Blocks Raycasts` 被设为了 false。