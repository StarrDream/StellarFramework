# UIKit / 界面系统

## 定位

`UIKit` 负责 UI 根节点初始化、面板加载、层级管理、强类型面板数据传递和异步打开流程。

## 最小接入清单

在使用默认 `ResKitUILoadStrategy` 前，请先确认：

1. 已导入 `UniTask`
2. 项目里存在 `Resources/UIPanel/UIRoot.prefab`
3. 面板 Prefab 位于 `Resources/UIPanel/`
4. Prefab 名称与面板类名一致
5. 面板脚本继承 `UIPanelBase`

如果你是第一次接触本框架，建议先直接查看 `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/Demo.unity`，不要从空场景手搓 UIKit 环境开始。

## 目录与资源规范

### 存放位置

默认策略下，面板 Prefab 建议存放在：

`Assets/StellarFramework/Resources/UIPanel/`

### 命名规则

- Prefab 文件名必须与 C# 类名一致
- 例如：
  - 脚本：`public class LoginPanel : UIPanelBase`
  - Prefab：`UIPanel/LoginPanel.prefab`

## 初始化

### 生成 UIRoot

首次使用前，先通过 `StellarFramework -> Tools Hub -> 框架核心 -> UIKit 工具` 生成：

- 输出位置：`Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`

### 入口初始化

```csharp
void Start()
{
    UIKit.Instance.Init();
    // 或 await UIKit.Instance.InitAsync();
}
```

### 自定义加载策略

```csharp
var myStrategy = new MyAddressablesLoadStrategy();
UIKit.Instance.Configure(myStrategy);
await UIKit.Instance.InitAsync();
```

`Configure` 必须发生在 `Init` / `InitAsync` 之前。  
如果你使用自定义策略，也需要自己管理其生命周期；默认 `ResKitUILoadStrategy` 会在 `UIKit` 销毁时统一释放。

## 开发工作流

### 第一步：定义面板数据

```csharp
using StellarFramework.UI;

public class LoginPanelData : UIPanelDataBase
{
    public string DefaultAccount;
}
```

### 第二步：创建面板脚本

```csharp
using StellarFramework.UI;
using UnityEngine.UI;

public class LoginPanel : UIPanelBase
{
    public Button btnLogin;

    public override void OnInit()
    {
        btnLogin.onClick.AddListener(CloseSelf);
    }

    public override void OnOpen(UIPanelDataBase data)
    {
        if (TryGetPanelData<LoginPanelData>(data, out var loginData))
        {
            Debug.Log("Default Account: " + loginData.DefaultAccount);
        }
    }
}
```

### 第三步：调用 API

```csharp
var data = new LoginPanelData { DefaultAccount = "Admin" };
await UIKit.OpenPanelAsync<LoginPanel>(data);

UIKit.OpenPanel<LoginPanel>();
UIKit.RefreshPanel<LoginPanel>(newData);
await UIKit.PreloadPanelAsync<LoginPanel>();
UIKit.ClosePanel<LoginPanel>();
UIKit.DestroyAllPanels();
```

### 第四步：验证运行环境

如果 `OpenPanelAsync<T>()` 返回 `null`，优先检查：

1. `UIKit.Instance.InitAsync()` 是否已完成
2. `UIRoot.prefab` 是否存在且结构完整
3. `Resources/UIPanel/目标面板.prefab` 是否存在
4. Prefab 上是否挂了目标 `UIPanelBase` 子类
5. Prefab 名称是否与类名一致

## 常用配置

### Layer

- `Bottom`
- `Middle`
- `Top`
- `Popup`
- `System`

### Destroy On Close

- `true`：关闭即销毁
- `false`：关闭后仅隐藏并缓存

### 分辨率适配

```csharp
UIKit.Instance.SetResolution(new Vector2(1280, 720), 1.0f);
```

## 常见问题

### Q1: 报错 `Configure 失败: UIKit 已初始化`
原因：调用顺序错误。  
处理：把 `Configure` 放到 `Init` 之前。

### Q2: 界面打开了，但是无法点击
可能原因：

- 缺少 `EventSystem`
- 被更高层透明 Panel 遮挡
- `CanvasGroup.interactable` 或 `blocksRaycasts` 为 `false`

### Q3: 为什么 `Example_UIKit` 不能直接跑？
原因：`Example_UIKit` 只是 API 示例脚本，不自带完整场景、`UIRoot` 和面板资源。  
处理：优先运行 `Demo.unity`，或者先按本文规范补齐资源环境。

## 对应示例

- Demo 场景：[Demo.unity](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/ArchitectureDemo/Scene/Demo.unity>)
- 代码示例：[Example_UIKit.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_UIKit/Example_UIKit.cs:1>)
