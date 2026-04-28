# UIStackManager / 堆栈管理

**适用**: StellarFramework UI 模块

---

## 1. 核心理念 (Design Philosophy)
在重度 UI 的项目中，UI 面板的层层叠加会增加 DrawCall 与渲染开销。
`UIStackManager` 采用横向扩展的设计，在不修改 `UIKit` 底层逻辑的前提下，实现了基于栈的遮挡剔除机制。

### 核心特性
*   **视觉剔除 (Visual Culling)**：当一个全屏面板被压入栈顶时，底层的面板会通过调整 `CanvasGroup.alpha = 0` 和 `blocksRaycasts = false` 进行隐藏。减少了直接调用 `SetActive(false)` 引发的 Rebuild 开销。
*   **生命周期联动**：提供 `OnPause` 和 `OnResume` 钩子，便于在面板被遮挡时暂停特效或网络轮询。
*   **状态同步**：被动监听 `UIPanelBase.OnPanelClosedGlobal`，当开发者调用 `CloseSelf()` 时，栈状态能自动更新。

---

## 2. 快速上手 (Quick Start)

### 2.1 标记全屏面板
在制作 UI Prefab 时，选中挂载了 `UIPanelBase` 的根节点，在 Inspector 中设置 **`Is Full Screen`**。
*   **True**：当它在栈顶时，下方的所有面板都会被隐藏（如主界面、全屏背包）。
*   **False**：当它在栈顶时，下方的面板依然可见（如弹窗）。

### 2.2 压栈 (Push)
使用 `PushPanel` 替代 `OpenPanel` 将界面纳入栈管理。

```csharp
// 同步压栈
UIStackManager.Instance.PushPanel<InventoryPanel>();

// 异步压栈 (带参数)
var data = new ItemDetailData { ItemId = 1001 };
await UIStackManager.Instance.PushPanelAsync<ItemDetailPanel>(data);
```

### 2.3 弹栈 (Pop)
当玩家点击返回或关闭时调用。

```csharp
// 弹出当前栈顶面板
UIStackManager.Instance.PopPanel();

// 连续弹栈，直到露出指定面板
UIStackManager.Instance.PopToPanel<MainCityPanel>();

// 清空整个 UI 栈
UIStackManager.Instance.ClearStack();
```

---

## 3. 生命周期钩子 (Lifecycle Hooks)
在 Panel 脚本中重写以下方法，以响应遮挡变化：

```csharp
public class InventoryPanel : UIPanelBase
{
    public override void OnOpen(UIPanelDataBase data)
    {
        Debug.Log("背包打开");
    }

    // 当打开了一个全屏面板遮住了背包时触发
    public override void OnPause()
    {
        Debug.Log("背包被遮挡，暂停背景特效");
    }

    // 当上层全屏面板关闭，背包重新暴露在最上层时触发
    public override void OnResume()
    {
        Debug.Log("背包重新可见，恢复特效");
    }
}
```

---

## 4. 混合调用规范：UIKit vs UIStackManager
`UIKit` 负责底层的加载与显示，`UIStackManager` 负责应用层的导航。两者可以混合使用。

### 4.1 混用表现
*   **用 Stack 压栈，用 UIKit 关闭**：兼容。面板触发 `CloseSelf()` 时会广播事件，栈管理器监听到后会自动将其剔除并恢复底层界面。
*   **用 UIKit 打开，用 Stack 弹栈**：通过 `UIKit.OpenPanel` 打开的界面不进入栈记录，调用 `PopPanel` 不会影响这些界面。

### 4.2 实践建议
*   **系统主干流转，使用 Stack (Push/Pop)**：适用于 `Middle` 层的全屏或半屏主界面。利用 `IsFullScreen` 属性优化 DrawCall。
*   **独立覆盖层，使用 UIKit (Open/Close)**：适用于 `Top`、`Popup`、`System` 层的界面（如提示框、跑马灯）。这些界面不应打断玩家的导航历史。

### 4.3 典型流转示例
```csharp
// 1. 玩家在主城 (入栈)
UIStackManager.Instance.PushPanel<MainCityPanel>();

// 2. 玩家打开英雄界面 (入栈，MainCityPanel 触发 OnPause 被隐藏)
UIStackManager.Instance.PushPanel<HeroListPanel>();

// 3. 弹出属性确认框 (游离态，不入栈，HeroListPanel 依然可见)
UIKit.OpenPanel<HeroDetailPopup>();

// 4. 关闭弹窗 (游离态销毁)
UIKit.ClosePanel<HeroDetailPopup>();

// 5. 退出英雄界面 (出栈，MainCityPanel 触发 OnResume 恢复显示)
UIStackManager.Instance.PopPanel();
```

---

## 5. 常见问题 (FAQ)

### Q1: 为什么 Push 了一个面板，底下的面板没有隐藏？
*   **检查**：请确认新面板在 Inspector 中的 `Is Full Screen` 属性是否勾选为 `True`。

### Q2: 栈管理和 Layer 层级冲突吗？
*   **不冲突**。`UIKit` 的 Layer 决定渲染顺序，`UIStackManager` 决定逻辑导航历史。通常只建议对 `Middle` 层的全屏界面使用栈管理。