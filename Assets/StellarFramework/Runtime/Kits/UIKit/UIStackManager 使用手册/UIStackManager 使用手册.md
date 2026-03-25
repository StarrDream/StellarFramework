# UIStackManager 栈管理系统使用手册

**版本**: v1.1 (Extension for UIKit)  
**适用**: StellarFramework UI 模块

---

## 1. 核心理念 (Design Philosophy)

在重度 UI 的商业化项目中，随着玩家深入各个系统，UI 面板会层层叠加（例如：主界面 -> 角色面板 -> 装备详情 -> 强化确认框）。如果不加干预，底层被遮挡的 UI 依然会参与渲染和事件轮询，导致 **DrawCall 飙升** 和 **性能浪费**。

`UIStackManager` 采用**横向扩展**的设计，在不修改原有 `UIKit` 底层加载逻辑的前提下，实现了基于栈的自动化遮挡剔除机制。

### 核心特性
*   **视觉剔除 (Visual Culling)**：当一个全屏面板被压入栈顶时，底层的面板会自动通过 `CanvasGroup.alpha = 0` 和 `blocksRaycasts = false` 被隐藏。**不使用 `SetActive(false)`**，避免了破坏 UI 组件生命周期和引发高昂的 Rebuild 开销。
*   **生命周期联动**：提供 `OnPause` 和 `OnResume` 钩子，方便开发者在面板被遮挡时暂停特效、动画或网络轮询。
*   **防脏数据设计**：被动监听 `UIPanelBase.OnPanelClosedGlobal`，即使开发者绕过栈管理器直接调用 `CloseSelf()`，栈状态依然能自动保持正确。

---

## 2. 快速上手 (Quick Start)

### 2.1 标记全屏面板

在制作 UI Prefab 时，选中挂载了 `UIPanelBase` (或其子类) 的根节点，在 Inspector 中勾选 **`Is Full Screen`**。

*   **勾选 (True)**：如“主界面”、“背包全屏页”。当它在栈顶时，下方的所有面板都会被隐藏。
*   **不勾选 (False)**：如“弹窗”、“侧边栏”。当它在栈顶时，下方的面板依然可见。

### 2.2 压栈 (Push)

使用 `PushPanel` 替代原有的 `OpenPanel`。

```csharp
// 同步压栈
UIStackManager.Instance.PushPanel<InventoryPanel>();

// 异步压栈 (带参数)
var data = new ItemDetailData { ItemId = 1001 };
await UIStackManager.Instance.PushPanelAsync<ItemDetailPanel>(data);
```

### 2.3 弹栈 (Pop)

当玩家点击“返回”或“关闭”按钮时调用。

```csharp
// 弹出当前栈顶面板
UIStackManager.Instance.PopPanel();

// 连续弹栈，直到露出指定面板 (常用于从深层系统一键返回主界面)
UIStackManager.Instance.PopToPanel<MainCityPanel>();

// 清空整个 UI 栈
UIStackManager.Instance.ClearStack();
```

---

## 3. 生命周期钩子 (Lifecycle Hooks)

在你的 Panel 脚本中重写以下方法，以响应栈的遮挡变化：

```csharp
public class InventoryPanel : UIPanelBase
{
    public override void OnOpen(UIPanelDataBase data)
    {
        Debug.Log("背包打开，开始播放入场动画");
    }

    // 当打开了一个全屏的【装备详情页】遮住了背包时触发
    public override void OnPause()
    {
        Debug.Log("背包被遮挡，暂停背景特效和模型渲染");
        // 停止高耗时的逻辑...
    }

    // 当【装备详情页】关闭，背包重新暴露在最上层时触发
    public override void OnResume()
    {
        Debug.Log("背包重新可见，恢复特效");
        // 恢复逻辑...
    }
}
```

---

## 4. 混合调用规范：UIKit vs UIStackManager (必读)

在实际开发中，“既要走栈导航，又要独立弹窗”是非常典型的场景。你可以把 `UIKit` 看作是**“底层操作系统”**，而 `UIStackManager` 是建立在它之上的**“应用层导航软件”**。它们完全可以混用，且极其安全。

### 4.1 混用时的底层表现
*   **用 Stack 压栈，用 UIKit 关闭**：绝对安全。面板触发 `CloseSelf()` 时，会向外广播 `OnPanelClosedGlobal` 事件，栈管理器被动监听到后，会自动将其从栈中剔除，并恢复底层界面的显示。
*   **用 UIKit 打开，用 Stack 弹栈**：逻辑隔离。通过 `UIKit.OpenPanel` 打开的界面被称为 **“游离态界面 (Out-of-Band UI)”**，它们根本不会进入栈管理器的记录中，因此调用 `PopPanel` 不会影响到这些游离弹窗。

### 4.2 架构师最佳实践 (主次分治)

*   **规范一：系统主干流转，必须走 Stack (Push/Pop)**
    *   **适用对象**：`Middle` 层的所有全屏或半屏主界面（如：主城、英雄列表、关卡选择、背包）。
    *   **目的**：利用栈管理器的 `IsFullScreen` 属性，自动隐藏底层的 3D 场景或其他 UI，极致压榨 DrawCall 性能。
*   **规范二：独立覆盖层，必须走 UIKit (Open/Close)**
    *   **适用对象**：`Top`、`Popup`、`System` 层的界面（如：断线重连提示、获得物品的小弹窗、跑马灯、常驻的摇杆）。
    *   **目的**：这些界面是“叠加”在主干流程之上的，它们不应该打断玩家的导航历史，也不应该触发底层界面的 `OnPause/OnResume`。

### 4.3 经典实战流转
```csharp
// 1. 玩家在主城 (入栈，栈底)
UIStackManager.Instance.PushPanel<MainCityPanel>();

// 2. 玩家打开英雄界面 (入栈，MainCityPanel 触发 OnPause 被隐藏)
UIStackManager.Instance.PushPanel<HeroListPanel>();

// 3. 玩家点击英雄，弹出一个属性确认框 (游离态，不入栈)
// 注意：这里用 UIKit.Open，HeroListPanel 不会被隐藏，依然可见
UIKit.OpenPanel<HeroDetailPopup>();

// 4. 玩家在弹窗里点击“升级”按钮，直接关闭弹窗 (游离态销毁)
UIKit.ClosePanel<HeroDetailPopup>();

// 5. 玩家点击返回，退出英雄界面 (出栈，MainCityPanel 触发 OnResume 恢复显示)
UIStackManager.Instance.PopPanel();
```

---

## 5. 常见问题 (FAQ)

### Q1: 为什么我 Push 了一个面板，底下的面板没有隐藏？
*   **检查**：请确认你 Push 的这个新面板，在 Inspector 中的 `Is Full Screen` 属性是否勾选为了 `True`。只有全屏面板才有资格遮挡下方的 UI。

### Q2: 栈管理和 Layer 层级冲突吗？
*   **不冲突**。`UIKit` 的 Layer（Middle, Popup, Top 等）决定了 Unity Hierarchy 中的渲染顺序。而 `UIStackManager` 决定的是逻辑上的导航历史。通常只建议对 `Middle` 层的全屏系统界面使用栈管理。