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

Controller 挂在 UIRoot，仅在 `Screen.width / height / safeArea` 变化时重新应用；刷新时同时采集 `Screen.cutouts`：

- `CanvasScaler.ScaleWithScreenSize`。
- reference resolution。
- breakpoint match。
- 一个或多个 `SafeAreaRoot` normalized anchors（标准 UIRoot 的 Static/Dynamic Canvas 各有一套）。
- 当前 Display Geometry（Safe Area + Cutouts），供精确避让组件复用。

每个 Panel 不需要自行逐帧读取 `Screen.safeArea`。

### FullScreen / SafeArea Region

标准 UIRoot 的每个 Canvas Role 都包含 `FullScreenRoot` 与 `SafeAreaRoot`，两者各自拥有 Bottom / Middle / Top / Popup / System 五层。

`UIPanelBase.PanelLayoutRegion` 决定 Panel 被放到哪一组 Layer。默认 `FullScreen` 保持旧项目语义，适合背景、遮罩和转场；顶部按钮、导航、文字等需要避开刘海/圆角的 Panel 可显式选择 `SafeArea`。旧 UIRoot 若没有区域节点，UIKit 会回退到原有 Layer 结构。

### 两套危险区避让方案

UIKit.Adaptation 同时提供两套方案，二者不是互斥替代关系：

1. `SafeAreaRoot`：保守矩形安全区。适合登录页、设置页、商城、表单、普通导航等。整个关键 UI 区域避开刘海、圆角、Home Indicator 等系统危险边缘，测试成本最低。
2. `UICutoutAwareLayout`：精确 Cutout 避让。适合游戏 HUD、顶部状态栏等希望继续利用屏幕边缘空间的界面。Panel 通常保持 `FullScreen`，只把返回按钮、标题、金币等关键 RectTransform 注册为 Target。Runtime 根据 `Screen.cutouts` 判断哪些 Target 真正与危险区相交，仅移动发生碰撞的 Target。

例如中央灵动岛只与标题重叠时，左右按钮保持原位，标题按最小合法位移移动到危险区外；左上挖孔只影响左侧返回按钮时，右侧金币不会一起下移。

`UICutoutAwareLayout` 支持：

- `Mode=None`：完全不做危险区避让。适合背景、遮罩、视频、特效、过场等可全屏铺满内容。
- `Mode=SafeArea`：目标控件必须进入系统 Safe Area；对应“整条危险区避让”的保守方案。
- `Mode=PreciseCutout`：优先仅避开真实 Cutout；对应“只避危险区”的高空间利用率方案。
- `Fallback=Automatic`：推荐默认。PreciseCutout 无可靠 cutout 数据时，优先退到非全屏 SafeArea；SafeArea 也不可用或系统只返回 FullScreen 时，再退到 Reference Edge Padding。
- `Fallback=SafeArea`：只允许退到 SafeArea；SafeArea 本身不可用时停止额外移动。
- `Fallback=EdgePadding`：直接退到保守边距。
- `Fallback=None`：不做降级，仅适合明确受控的固定设备项目。
- `System`：只使用 `Screen.cutouts`，推荐真机默认。
- `Manual`：只使用归一化手动危险区，用于特殊硬件、厂商数据缺失或固定设备。
- `SystemAndManual`：系统危险区与项目自定义危险区合并。
- Top / Bottom / Any 边缘策略。
- Auto / Horizontal / Vertical 移动约束。
- Reference Pixel Padding；按设计短边与设备短边比例换算，不按具体手机型号写死。

运行时会暴露 `EffectiveMode`，用于诊断当前设备最终采用的是 `PreciseCutout / SafeArea / EdgePadding / None` 中哪一级。UI 业务不应根据 Android、iOS、HarmonyOS、设备品牌或型号自行分支。

### 推荐 UI 制作流程

UI 作者继续使用项目统一设计分辨率（例如 1920x1080）制作，不需要为具体手机型号单独做一套 Prefab。

普通页面推荐：

```text
PanelLayoutRegion = SafeArea
UICutoutAwareLayout = 不需要
```

适用于登录、设置、商城、表单、常规弹窗、重要正文和普通导航。

全屏视觉内容推荐：

```text
PanelLayoutRegion = FullScreen
Display Avoidance = None
```

适用于背景、全屏图、视频、遮罩、过场和允许被刘海/挖孔遮住的纯装饰内容。

游戏 HUD / 顶部状态栏推荐：

```text
PanelLayoutRegion = FullScreen
UICutoutAwareLayout.Mode = PreciseCutout
Fallback = Automatic
Targets = BackButton / Title / CoinPanel / MiniMap ...
```

只有注册进 `Targets` 的关键 RectTransform 参与精确避让；背景条、装饰和无关节点不需要加入。

如果项目明确希望“整条顶部一起下移”，则有两种方式：

```text
方式 A（普通页面首选）
PanelLayoutRegion = SafeArea

方式 B（同一个 HUD 内按 Target 控制）
UICutoutAwareLayout.Mode = SafeArea
Fallback = Automatic
```

### 自动降级链

推荐默认链路：

```text
作者选择 PreciseCutout
        ↓
系统 Cutout 数据有效？
 ├─ Yes → PreciseCutout
 └─ No
      ↓
SafeArea 有效且不是 FullScreen？
 ├─ Yes → SafeArea
 └─ No  → EdgePadding
```

作者选择 `SafeArea` 时：

```text
SafeArea 有效
  ↓
SafeArea

SafeArea 无效
  ↓
Automatic → EdgePadding
```

这套链路用于兼容老 Android、厂商 ROM、iOS 新旧设备、缺少完整 Cutout 信息的平台以及后续 HarmonyOS Adapter。原则是：新设备尽量使用屏幕空间，异常/老设备优先保证关键 UI 可操作。

### 不应该做的适配方式

不要在业务 UI 中维护：

```csharp
if (isHuawei) { ... }
if (isIPhone) { ... }
if (model == "某型号") { ... }
```

业务只声明 `None / SafeArea / PreciseCutout` 设计意图；平台差异由 Display Geometry 与 fallback 管线承担。

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
- None / Center Punch / Dynamic Island / Left Punch Cutout 模拟，也可直接输入 Cutout 像素矩形。
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
  Safe Area、Cutout 精确避让、Breakpoint、Device Preview、Layout Variant Capture 与布局风险检查
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

- [UIKit 使用文档](UIKit-界面系统-使用文档-Guide.md)
- [UIKit 源码文档](UIKit-界面系统-源码文档-Guide.md)
