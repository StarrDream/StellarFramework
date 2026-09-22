# UIKit / 界面系统说明文档

## 模块定位

`UIKit` 是框架内 UI 的统一入口，负责：

- 初始化 `UIRoot`
- 管理面板加载和缓存
- 打开、关闭、预加载面板
- 管理页面栈
- 管理全屏面板遮挡和恢复
- 输出运行时快照

业务层不应直接 `Instantiate UI Prefab`，而应通过 `UIKit` 统一驱动 UI 生命周期。

## 模块组成

- `UIKit`
  UI 运行时主入口
- `UIPanelBase`
  面板基类
- `UIPanelDataBase`
  面板数据基类
- `IUILoadStrategy`
  加载策略接口
- `ResKitUILoadStrategy`
  默认加载策略
- `UIKitSettings`
  UI 默认配置

## 初始化流程

### 推荐方式

```csharp
await UIKit.Instance.InitAsync();
```

初始化时会：

- 确保加载策略存在
- 加载 `UIRoot`
- 建立 `Canvas / Layer` 映射
- 准备好面板缓存和页面栈相关状态

## 打开面板

### 普通打开

```csharp
await UIKit.OpenAsync<LoginPanel>(new LoginPanelData
{
    DefaultAccount = "player01"
});
```

### 栈式打开

```csharp
await UIKit.PushAsync<MainMenuPanel>();
await UIKit.PushAsync<InventoryPanel>();
```

### 栈操作

```csharp
UIKit.Pop();
UIKit.PopTo<MainMenuPanel>();
UIKit.ClearStack();
```

## 面板数据

如果面板需要打开参数，应定义一个 `UIPanelDataBase` 子类：

```csharp
public sealed class ShopPanelData : UIPanelDataBase
{
    public int TabIndex;
}
```

然后在面板里通过：

```csharp
TryGetPanelData<ShopPanelData>(data, out ShopPanelData shopData)
```

取出实际参数。

## 页面栈

`UIKit` 的页面栈适合做：

- 菜单层级
- 子页面压栈
- 全屏页面覆盖

运行规则：

- `Push` 的面板才进入页面栈
- `Pop` 关闭栈顶
- 若栈顶是全屏面板，下层面板会暂停和隐藏
- 当上层关闭时，下层会恢复

## 预加载

如果某个面板加载成本高，可以先预加载：

```csharp
await UIKit.PreloadAsync<ShopPanel>();
```

预加载会把面板实例放进缓存，但不自动打开。

## 运行时诊断

`UIKit` 提供：

- `TakeSnapshot()`
- `LogSnapshot()`

用于查看当前：

- 是否已初始化
- 当前加载策略
- 缓存面板数
- 激活面板数
- 加载中面板数

## 多机型 UI 适配

`UIKit.Adaptation` 是可选 Adapter，`UIKit.Core` 不直接依赖它；`UIKit Complete` 默认组合该能力。

### UIAdaptationProfile

Profile 按屏幕特征而不是手机型号描述适配规则：

- Design Resolution。
- Default `matchWidthOrHeight`。
- Safe Area 开关。
- Aspect / Orientation Breakpoints，例如 PhoneTall、Tablet、Landscape。

Breakpoint 的 Aspect 使用“长边 / 短边”得到 >= 1 的 Shape Aspect，Orientation 单独判断。因此同一台 20:9 设备在横屏与竖屏都使用约 2.22 的比例区间，不需要维护两套倒数配置。Breakpoint 只决定当前适配类别和 CanvasScaler Match，不在 Runtime 猜测设计意图。

### UIAdaptationController

Controller 挂在 UIRoot，仅在 `Screen.width / height / safeArea` 变化时重新应用：

- `CanvasScaler.ScaleWithScreenSize`。
- reference resolution。
- breakpoint match。
- 一个或多个 `SafeAreaRoot` normalized anchors（标准 UIRoot 的 Static/Dynamic Canvas 各有一套）。

每个 Panel 不需要自行逐帧读取 `Screen.safeArea`。

### FullScreen / SafeArea Region

标准 UIRoot 的每个 Canvas Role 都包含 `FullScreenRoot` 与 `SafeAreaRoot`，两者各自拥有 Bottom / Middle / Top / Popup / System 五层。

`UIPanelBase.PanelLayoutRegion` 决定 Panel 被放到哪一组 Layer。默认 `FullScreen` 保持旧项目语义，适合背景、遮罩和转场；顶部按钮、导航、文字等需要避开刘海/圆角的 Panel 可显式选择 `SafeArea`。旧 UIRoot 若没有区域节点，UIKit 会回退到原有 Layer 结构。

### Layout Variant

`UILayoutVariant` 用于真正需要差异布局的 Panel。设计师在 ToolsHub 选择 Breakpoint（例如 `tablet`），调整 RectTransform 后执行 `Capture 当前布局`，保存该 Panel 子树的：

- anchors / pivot。
- anchoredPosition / sizeDelta。
- localScale。
- activeSelf。

Runtime 只监听 `UIAdaptationController.BreakpointChanged`，Breakpoint 真正变化时应用一次对应快照，不进行逐帧布局重写。

### ToolsHub

`Tools Hub -> UIKit UI适配` 提供：

- 16:9 / 20:9 / 4:3 / 19.5:9 Portrait Preview。
- 自定义 Width / Height / Safe Insets。
- Breakpoint / Match 预览。
- Adaptation Controller 一键配置。
- 推荐 Adaptation Profile 一键创建（Tablet / Phone / PhoneTall）。
- Layout Variant Capture / Preview。
- 缺少 SafeAreaRoot、错误 CanvasScaler、中心 Anchor 靠边、Breakpoint 重叠等风险检查。

Validator 只报告风险，不会擅自重排 UI。

## ToolsHub 关联

- `UIKit 工具`
  UI 工作区、绑定代码生成、样例修复
- `UIKit UI适配`
  Safe Area、Breakpoint、Device Preview、Layout Variant Capture 与布局风险检查
- `文档中心`
  查看 UIKit 说明和源码文档

## 使用约束

- 使用前必须先初始化
- 面板 prefab 必须包含目标 `UIPanelBase` 组件
- 同步打开只适用于支持同步加载的策略
- 热更新和远端资源场景优先使用异步接口

## 常见问题

- 面板打不开
  检查 `UIKitSettings`、`UIRoot`、Prefab 路径和加载策略。
- Full Screen 页面下层不显示
  这是预期行为，下层被暂停和隐藏。
- 热更 UI 加载失败
  优先使用异步接口，并确认 `ResKit / Addressables` 地址可加载。
- 页面栈行为混乱
  确认是否混用了普通 `Open` 和 `Push`。

## 相关文档

- [UIKit 源码文档](UIKit-界面系统-源码文档-Guide.md)
