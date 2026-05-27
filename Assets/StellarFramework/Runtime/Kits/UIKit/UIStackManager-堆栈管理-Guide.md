# UIStackManager / 堆栈兼容 Guide

`UIStackManager` 现在是 UIKit 的内部导航服务。新代码推荐使用 `UIKit.Push/PushAsync/Pop/PopTo`，旧项目中的 `UIStackManager.PushPanel/PopPanel/PopToPanel` 仍可继续工作。

## 推荐写法

```csharp
await UIKit.PushAsync<MainMenuPanel>();
await UIKit.PushAsync<InventoryPanel>();

UIKit.Pop();
UIKit.PopTo<MainMenuPanel>();
```

旧兼容写法：

```csharp
await UIStackManager.PushPanelAsync<InventoryPanel>();
UIStackManager.PopPanel();
UIStackManager.PopToPanel<MainMenuPanel>();
```

## 栈行为

- Push 会先通过 UIKit 打开 Panel，再把 Panel 记录到栈顶。
- Pop 会关闭当前栈顶 Panel。
- PopTo 会连续关闭栈顶，直到目标 Panel 暴露。
- Panel 调用 `CloseSelf()` 时，Stack 会收到全局关闭事件并自动移除该 Panel。
- `IsFullScreen=true` 的 Panel 在栈顶时，会通过 `CanvasGroup` 隐藏下层 Panel，并触发下层 `OnPause/OnResume`。

## 适用场景

- 主界面、背包、角色、关卡选择等页面流转：用 `Push/Pop`。
- Toast、确认框、系统提示、跑马灯：用 `Open/Close`，不要进入导航栈。
- 常驻 HUD：放到 `StaticCanvas`，通常不进 Stack。

## 验收建议

- 连续 Push 三个全屏 Panel，再 Pop 两次，确认 `OnPause/OnResume` 顺序正确。
- Push 后直接 `CloseSelf()`，确认 Stack 不残留关闭的 Panel。
- 混合 `Open` 弹窗和 `Push` 页面，确认弹窗关闭不会破坏页面栈。
- 压力跑 100 次 Open/Close 后打印 `UIKit.LogSnapshot()`，确认 `Loading=0`。

## 常见问题

- Push 后下层仍可见：检查顶层 Panel 的 `Is Full Screen`。
- Pop 没反应：该 Panel 可能是通过 `Open` 打开的，没有进入 Stack。
- CloseSelf 后再次 Pop 关闭了错误页面：确认自定义 Panel 的 `OnClose()` 重写中调用了 `base.OnClose()`。
