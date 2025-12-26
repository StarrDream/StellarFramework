# UIKit 使用手册

## 1. 设计理念 (Why)
UI 开发通常面临层级混乱、加载繁琐、脚本与 Prefab 绑定松散等问题。
**UIKit 的特性：**
*   **层级管理**：内置 Bottom/Middle/Top/Popup/System 五层结构，自动排序。
*   **泛型加载**：`OpenPanelAsync<THomePanel>()`，直接通过类名加载，无需配置字符串路径（约定大于配置）。
*   **生命周期**：`OnInit` (加载), `OnOpen` (显示), `OnClose` (隐藏/销毁) 标准化流程。
*   **异步驱动**：全流程基于 `UniTask`，支持 `await` 等待界面打开动画播放完毕。

---

## 2. 环境搭建 (Setup)

### 第一步：生成 UIRoot
在 Unity 菜单栏点击：`Tools -> UIKit -> Create UI Root Prefab`。
这会在 `Resources/UIPanel` 下生成一个标准的 `UIRoot.prefab`，包含 Canvas、Scaler、Camera 和层级节点。

### 第二步：初始化 UIKit
在游戏入口 (GameEntry) 调用：
```csharp
void Start() {
    // 初始化 UI 系统 (默认使用 Resources 加载)
    // 如果使用 Addressables，传入 ResLoaderType.Addressable
    UIKit.Instance.Init(ResLoaderType.Resources);
}
```

---

## 3. 开发工作流 (Workflow)

### 3.1 创建脚本
创建一个继承自 `UIPanelBase` 的脚本，例如 `LoginPanel.cs`。

```csharp
using StellarFramework.UI;
using Cysharp.Threading.Tasks;

public class LoginPanel : UIPanelBase
{
    // 绑定 UI 组件
    public Button btnLogin;

    public override void OnInit()
    {
        // 只执行一次，适合 AddListener
        btnLogin.onClick.AddListener(OnLoginClicked);
    }

    public override async UniTask OnOpen(object uiData = null)
    {
        await base.OnOpen(uiData);
        // 每次打开执行，适合刷新数据
        Debug.Log("登录界面打开");
    }

    private void OnLoginClicked()
    {
        CloseSelf(); // 关闭自己
    }
}
```

### 3.2 制作 Prefab
1.  在 Hierarchy 右键 -> `UIKit -> Panel Template` 创建模板。
2.  重命名为 `LoginPanel` (**必须与类名完全一致**)。
3.  挂载 `LoginPanel.cs` 脚本。
4.  在 Inspector 设置 `Layer` (如 Middle) 和 `Destroy On Close`。
5.  将 Prefab 拖入 `Resources/UIPanel/` 文件夹 (**路径必须固定**)。

### 3.3 打开界面
```csharp
// 在任何地方调用
UIKit.OpenPanelAsync<LoginPanel>();
```

---

## 4. 高级功能

### 4.1 传递参数
```csharp
// 打开时传参
UIKit.OpenPanelAsync<UserInfoPanel>(new UserData { Name = "Jack" });

// 接收参数
public override async UniTask OnOpen(object uiData = null)
{
    await base.OnOpen(uiData);
    if (uiData is UserData data) {
        nameText.text = data.Name;
    }
}
```

### 4.2 层级详解
*   **Bottom**: 底图、背景。
*   **Middle**: 普通全屏界面（默认）。
*   **Popup**: 弹窗，会盖在 Middle 之上。
*   **Top**: 跑马灯、顶层提示。
*   **System**: Loading 图、断线重连提示（最高层级，阻挡一切点击）。

### 4.3 关闭模式
在 Inspector 中设置 `Destroy On Close`：
*   **True**: 关闭时直接 `Destroy` 销毁物体（省内存，下次打开慢）。
*   **False**: 关闭时仅 `SetActive(false)`（费内存，下次打开快）。

---

## 5. 常见坑点 (Pitfalls)

### Q1: 报错 "Panel Prefab not found"
*   **原因**：Prefab 名字和类名不一致，或者没有放在 `Resources/UIPanel/` 目录下。
*   **解决**：UIKit 默认使用“约定大于配置”原则，Prefab 名必须等于类名，且路径固定。

### Q2: 界面打开了但点不动
*   **原因**：
    1.  `CanvasGroup` 的 `Interactable` 或 `Blocks Raycasts` 被勾掉了。
    2.  被更高层级的透明 Panel 遮挡了（检查 Hierarchy 下方是否有全屏透明物体）。
    3.  场景里丢失了 `EventSystem` 组件。

### Q3: 初始化顺序问题
*   **现象**：在 `Awake` 里调用 `UIKit.OpenPanel` 报错。
*   **原因**：`UIKit.Init` 还没执行。
*   **解决**：确保 `UIKit.Init` 在所有 UI 逻辑之前执行。