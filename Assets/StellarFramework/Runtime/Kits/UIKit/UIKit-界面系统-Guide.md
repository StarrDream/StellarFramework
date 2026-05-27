# UIKit / 界面系统 Guide

UIKit 是框架的唯一 UI 入口。业务层优先使用 `UIKit.Open/OpenAsync/Push/PushAsync/Pop/Close`，旧的 `UIStackManager` API 保留为兼容层。

## 1. 生产定位

- `UIKit` 负责初始化 UIRoot、加载 Panel、挂载层级、打开/关闭、预加载和运行时诊断。
- `UIStackManager` 只做内部导航服务，推荐从 `UIKit.Push/Pop/PopTo` 进入。
- UI 加载通过 ResKit 后端完成，默认 `Resources`，也可以在 `UIKitSettings` 中切换到 `Addressables` 或 `AssetBundle`。
- 生产项目推荐 UI 使用异步加载；同步加载只适合本地 `Resources` 或已经明确支持同步的后端。

## 2. 资源结构

默认路径：

- `Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`
- `Assets/StellarFramework/Resources/UIPanel/{PanelClassName}.prefab`

UIRoot 推荐结构：

```text
UIRoot
  EventSystem
  StaticCanvas
    Bottom / Middle / Top / Popup / System
  DynamicCanvas
    Bottom / Middle / Top / Popup / System
```

`StaticCanvas` 用于 HUD、摇杆、血条等常驻 UI；`DynamicCanvas` 用于窗口、页面、弹窗、系统提示。旧版直接挂在 UIRoot 下的五层结构仍兼容。

## 3. 常用入口

```csharp
await UIKit.Instance.InitAsync();

await UIKit.OpenAsync<LoginPanel>(new LoginPanelData());
UIKit.Close<LoginPanel>();

await UIKit.PushAsync<InventoryPanel>();
UIKit.Pop();
UIKit.PopTo<MainMenuPanel>();

await UIKit.PreloadPanelAsync<RewardPanel>();
UIKit.DestroyAllPanels();
```

Panel 示例：

```csharp
public sealed class LoginPanelData : UIPanelDataBase
{
    public string DefaultAccount;
}

public sealed class LoginPanel : UIPanelBase
{
    public override void OnOpen(UIPanelDataBase data)
    {
        if (TryGetPanelData<LoginPanelData>(data, out var loginData))
        {
            Debug.Log(loginData.DefaultAccount);
        }
    }
}
```

## 4. 后端选择

通过 `UIKitSettings` 配置：

- `Default Load Backend = Resources`：最简单，支持同步/异步，适合内置基础 UI。
- `Default Load Backend = Addressables`：推荐生产热更 UI，使用 `Assets/...` address，优先走 `OpenAsync/PushAsync`。
- `Default Load Backend = AssetBundle`：依赖 `AssetBundleManager.InitAsync()` 和 AssetMap，适合已有 AB 管线。
- `Custom Loader Key`：通过 `ResKit.RegisterCustomLoader` 接入 YooAsset 或业务自定义加载器。

AA 的编辑器模拟加载请使用 Addressables 官方 Play Mode Script；AB 如需编辑器模拟加载，建议新增一个 AssetDatabase 自定义 loader 接到 ResKit，不要假装已经构建了 AB。

## 5. 诊断与压力测试

运行时快照：

```csharp
UIKitRuntimeSnapshot snapshot = UIKit.TakeSnapshot();
UIKit.LogSnapshot();
```

快照包含初始化状态、加载策略、Root/Static/Dynamic Canvas 是否存在、缓存面板、激活面板和正在加载的面板。

压力入口：

```csharp
await UIKit.StressOpenCloseAsync<ExamplePanel>(100, data, yieldEvery: 5);
```

验收标准：

- 压力测试结束后 `Loading=0`。
- `Cached` 数量符合 `DestroyOnClose` 策略。
- Console 中没有重复初始化、空层级、Prefab 缺组件、CanvasGroup 为空等错误。
- 真机上反复切场景、返回前后台、横竖屏/分辨率变化后 UI 仍可点击。

## 6. Playable 示例

打开：

`Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity`

按键：

- `O`：异步打开 `ExamplePanel`
- `P`：Push 到 UI 栈
- `Backspace`：Pop
- `C`：Close
- `S`：执行 100 次 Open/Close 压力测试
- `D`：打印 `UIKitRuntimeSnapshot`

首次运行前可通过 Example Playable Scene Builder 重新生成示例资源，它会补齐 `UIRoot.prefab` 和 `ExamplePanel.prefab`。

## 7. 常见错误排查

- `UIKit 未初始化`：先执行 `await UIKit.Instance.InitAsync()`。
- `当前加载策略不支持同步加载`：AA/UI 热更场景请改用 `OpenAsync/PushAsync`。
- `UIRoot 结构非法`：确认 StaticCanvas/DynamicCanvas 或根节点下存在 `Bottom/Middle/Top/Popup/System`。
- `Prefab 加载为空`：确认 `UIKitSettings` 的路径格式和 ResKit 后端一致。
- `预制体缺少目标组件`：Prefab 文件名、脚本类名和 `UIPanelBase` 子类必须对应。
- UI 打开但不能点击：检查 EventSystem、GraphicRaycaster、CanvasGroup 的 `interactable/blocksRaycasts`。
- Stack 下层没有隐藏：目标 Panel 的 `Is Full Screen` 需要勾选。
