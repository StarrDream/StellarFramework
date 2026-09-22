# UIKit / 界面系统源码文档

## 模块职责

`UIKit` 负责框架内的面板生命周期和 UI 根节点管理。

主要职责：

- 初始化 `UIRoot`
- 管理 `Canvas / Layer`
- 管理面板缓存
- 管理异步加载中任务
- 打开、关闭、预加载面板
- 管理面板栈和全屏遮挡关系

## 源码文件

- `Runtime/Kits/UIKit/UIKit.cs`
- `Runtime/Kits/UIKit/UIPanelBase.cs`
- `Runtime/Kits/UIKit/LoadStrategy/IUILoadStrategy.cs`
- `Runtime/Kits/UIKit/LoadStrategy/ResKitUILoadStrategy.cs`
- `Runtime/Kits/UIKit/AutoBind/UIAutoBind.cs`
- `Runtime/Kits/UIKit/Adapters/Adaptation/UIAdaptationController.cs`
- `Runtime/Kits/UIKit/Adapters/Adaptation/UIAdaptationProfile.cs`
- `Runtime/Kits/UIKit/Adapters/Adaptation/UIDisplayGeometry.cs`
- `Runtime/Kits/UIKit/Adapters/Adaptation/UICutoutAwareLayout.cs`
- `Runtime/Kits/UIKit/Adapters/Adaptation/UILayoutVariant.cs`
- `Runtime/Kits/UIKit/Editor/UIKitEditor.cs`
- `Runtime/Kits/UIKit/Editor/UIAutoBindEditor.cs`

## 总体结构

```text
UIKit
├─ RootCanvas / StaticCanvas / DynamicCanvas
├─ _layers
├─ _roleLayers
├─ _panelCache
├─ _panelNames
├─ _panelLoadingTasks
├─ _panelStack
└─ _loadStrategy

UIPanelBase
├─ PanelLayer
├─ PanelCanvasRole
└─ 面板生命周期

UIKit.Adaptation
├─ UIAdaptationProfile
├─ UIAdaptationController
├─ UIDisplayGeometry
├─ UICutoutAwareLayout
│  ├─ UIDisplayAvoidanceMode
│  ├─ UIDisplayFallbackMode
│  ├─ UIDisplayResolvedMode
│  ├─ UICutoutTarget
│  └─ UIManualExclusionZone
└─ UILayoutVariant
```

## 初始化调用链

1. `Configure(loadStrategy)` 或 `Configure(settings)`
2. `Init()` 或 `InitAsync()`
3. `EnsureDefaultStrategy()`
4. 加载 `UIRoot`
5. `SetupUIRoot(...)`
6. `BuildLayerMap(...)`
7. 记录 `RootCanvas / StaticCanvas / DynamicCanvas`
8. `RegisterStackCallbacks()`

## 面板打开调用链

1. `OpenPanel<T>()` 或 `OpenPanelAsync<T>()`
2. `GetOrLoadPanelInternalSync/Async<T>()`
3. 若无缓存则 `CreatePanelSync/Async<T>()`
4. `CreatePanelFromPrefab<T>()`
5. 写入 `_panelCache` 和 `_panelNames`
6. `OpenExistingPanel(...)`
7. 调用面板 `OnOpen(data)`

## 堆栈调用链

1. `Push<T>()` 或 `PushAsync<T>()`
2. `PushToStack(panel)`
3. `EvaluateStackVisibility()`
4. 若上层存在全屏面板，则下层被暂停/隐藏
5. `Pop()` / `PopTo<T>()` / `ClearStack()` 通过关闭面板驱动栈变化

## 类型详解

## `UIKitRuntimeSnapshot`

### 作用

用于输出当前 UIKit 运行时状态快照。

### 字段

- `IsInitialized`
- `IsInitializing`
- `IsDisposed`
- `HasRootCanvas`
- `HasStaticCanvas`
- `HasDynamicCanvas`
- `LoadStrategyName`
- `CachedPanels`
- `ActivePanels`
- `LoadingPanels`

### 属性

- `CachedPanelCount`
- `ActivePanelCount`
- `LoadingPanelCount`

### 方法

- `Empty(reason)`
- `ToMultilineString()`
- `ToString()`

## `UIKit`

### 作用

UI 系统核心管理器，继承 `MonoSingleton<UIKit>`。

### 关键字段

- `_loadStrategy`
  当前 UI 加载策略。
- `_settings`
  UI 运行时配置。
- `_isInitialized`
- `_isInitializing`
- `_isDisposed`
- `_layers`
  默认层级映射。
- `_roleLayers`
  按 `Static / Dynamic` 区分的层级映射。
- `_panelCache`
  面板类型到面板实例缓存。
- `_panelNames`
  面板类型到 prefab 名称映射。
- `_panelLoadingTasks`
  面板类型到加载中任务映射。
- `_panelStack`
  当前栈式打开的面板列表。
- `_destroyCts`
  在对象销毁时取消异步任务。
- `_stackCallbacksRegistered`
  是否已注册全局栈回调。

### 关键属性

- `RootCanvas`
- `StaticCanvas`
- `DynamicCanvas`
- `RootScaler`
- `UICamera`

### 关键方法

#### `Configure(IUILoadStrategy loadStrategy)`

显式注入加载策略。

约束：

- 已初始化或初始化中时禁止调用
- `loadStrategy` 不能为空

#### `Configure(UIKitSettings settings)`

用配置构造默认 `ResKitUILoadStrategy`。

#### `Init()`

同步初始化入口。

职责：

- 检查状态
- 确保加载策略存在
- 同步加载 `UIRoot`
- 校验 `UIRoot` 结构
- 建立层级映射

#### `InitAsync()`

异步初始化入口，和 `Init()` 逻辑一致，只是 UIRoot 走异步加载。

#### `EnsureDefaultStrategy()`

当外部没有手动配置策略时，自动从 `UIKitSettings` 创建默认策略。

#### `SetupUIRoot(...)`

职责：

- 实例化 `UIRoot`
- 提取 `Canvas / CanvasScaler / Camera`
- 构建 `Dynamic / Static` 层映射
- 设置 `DontDestroyOnLoad`

#### `BuildLayerMap(...)`

构建 `PanelLayer -> Transform` 映射。

#### `SetResolution(...)`

调整 `CanvasScaler` 参考分辨率。

## 静态 API

### 打开 / 关闭

- `Open<TPanel>(...)`
- `OpenAsync<TPanel>(...)`
- `Close<TPanel>()`

### 预加载

- `Preload<TPanel>()`
- `PreloadAsync<TPanel>()`

### 栈操作

- `Push<TPanel>(...)`
- `PushAsync<TPanel>(...)`
- `Pop()`
- `PopTo<TPanel>()`
- `ClearStack()`

### 诊断

- `TakeSnapshot()`
- `LogSnapshot()`
- `StressOpenCloseAsync<TPanel>(...)`

## 面板创建和缓存

### `OpenPanelInternalSync/Async<TPanel>()`

在确保 UIKit 就绪后：

- 从缓存获取面板
- 或创建新面板
- 再交给 `OpenExistingPanel(...)`

### `OpenExistingPanel(...)`

职责：

- 激活面板
- 调整层级顺序
- 恢复 `CanvasGroup`
- 调用 `OnOpen(data)`

### `CreatePanelSync<TPanel>() / CreatePanelAsyncInternal<TPanel>()`

职责：

- 通过 `_loadStrategy` 加载 prefab
- 交给 `CreatePanelFromPrefab<TPanel>()`

### `CreatePanelFromPrefab<TPanel>()`

职责：

- 实例化 prefab
- 获取 `UIPanelBase` 组件
- 挂到正确 Layer
- 设置 `RectTransform`
- 调用 `OnInit()`
- 写入 `_panelCache` 和 `_panelNames`

## 面板栈

### `PushToStack(...)`

- 把面板放入栈顶
- 若已存在则先移除旧位置
- 重新评估可见性

### `RemoveFromStack(...)`

- 从栈中移除指定面板
- 重新评估可见性

### `TryPop()`

- 关闭栈顶面板

### `TryPopTo<TPanel>()`

- 逐步关闭直到目标面板成为栈顶

### `ClearStackInternal()`

- 关闭全部栈面板

### `EvaluateStackVisibility()`

找到最高层全屏面板，然后决定哪些面板需要：

- `OnPause()`
- `OnResume()`
- `CanvasGroup.alpha = 0/1`
- `interactable`
- `blocksRaycasts`

## `IUILoadStrategy`

### 作用

抽象 UIKit 对 UIRoot 和面板 prefab 的加载方式。

至少承担：

- 加载 UIRoot
- 加载面板 prefab
- 释放缓存资源
- 声明是否支持同步加载

## `ResKitUILoadStrategy`

### 作用

默认 UIKit 加载策略，使用 `ResKit` 和 `UIKitSettings` 组织资源路径。

## `UIKit.Adaptation`

### `UIDisplayGeometry`

平台无关的显示几何快照，保存：

- `Width / Height`
- `SafeArea`
- `Cutouts`

Runtime 默认由 `UIAdaptationController.ApplyCurrentScreen()` 从 Unity `Screen.width / height / safeArea / cutouts` 生成。后续其他平台若需要原生 Window/AvoidArea Adapter，只需转换成同一种 Geometry，不应让业务 UI 感知平台名称。

### `UIAdaptationController`

职责：

- 根据 Profile 配置 `CanvasScaler`。
- 根据 SafeArea 设置一个或多个 `SafeAreaRoot` anchors。
- 解析 Aspect / Orientation Breakpoint。
- 缓存当前 `UIDisplayGeometry`。
- Geometry 真正变化时触发 `DisplayGeometryChanged`。
- Breakpoint 真正变化时触发 `BreakpointChanged`。

Controller 不要求每个 Panel 自己轮询屏幕 API。

### `UIDisplayAvoidanceMode`

这是 UI 作者选择的“设计意图”：

- `None`：允许内容占用整个屏幕。
- `SafeArea`：关键 Target 必须进入系统矩形 SafeArea。
- `PreciseCutout`：优先只避开 Cutout，不浪费 Cutout 左右仍可用空间。

它不是平台枚举，不应该增加 Android/iOS/HarmonyOS/品牌/机型分支。

### `UIDisplayFallbackMode`

控制主策略不可用时的降级规则：

- `Automatic`：生产默认。`PreciseCutout -> SafeArea -> EdgePadding`。
- `SafeArea`：只允许降到 SafeArea。
- `EdgePadding`：直接使用参考像素边距。
- `None`：不降级，只建议固定硬件或专项验证使用。

### `UIDisplayResolvedMode`

表示当前 Geometry 实际采用的层级：

- `None`
- `SafeArea`
- `PreciseCutout`
- `EdgePadding`

用于诊断、测试和日志，不要求业务根据它编写设备分支。

### `UICutoutAwareLayout`

历史名称保留以避免破坏已有 Prefab/Scene 序列化，但当前职责已经扩展为“可选显示危险区避让组件”。

核心执行顺序：

1. 收集 System / Manual exclusion zones。
2. 根据 `Mode` 和 Geometry 解析 `EffectiveMode`。
3. `PreciseCutout` 时只检查 Target 与 exclusion 的相交。
4. 求最小合法位移，优先保持仍可用屏幕空间。
5. 求解失败或 Cutout 缺失时按 Fallback 降级。
6. `SafeArea` / `EdgePadding` 使用 containment solver，把 Target 收进允许矩形。
7. 把 screen-space delta 转回目标父节点 anchored delta。

组件只作用于 `_targets`，不会修改整个 FullScreenRoot。

### `UICutoutLayoutSolver`

纯屏幕空间算法：

- `CalculateOffset(...)`：处理一个或多个 exclusion zone 的精确避让。
- `CalculateContainmentOffset(...)`：把目标 Rect 收回 SafeArea 或 EdgePadding bounds。

Solver 与平台 API 解耦，因此可以直接 EditMode 单测。

### 数据有效性与降级

SafeArea 至少满足：

- width/height > 0；
- 不出现负坐标；
- 不越出当前屏幕尺寸。

Automatic 的核心规则：

```text
PreciseCutout requested
  ├─ relevant cutout exists -> PreciseCutout
  ├─ no cutout + non-full valid SafeArea -> SafeArea
  └─ otherwise -> EdgePadding
```

因此旧设备、异常 ROM、未提供精确 cutout 的平台不会直接把重要 UI 留在危险边缘。

### 与 `SafeAreaRoot` 的关系

两种机制保留：

- `PanelLayoutRegion.SafeArea`：Panel 级整块矩形安全布局，普通页面优先。
- `UICutoutAwareLayout`：Target 级可选布局，HUD/顶部状态栏优先。

不要为了精确 Cutout 新建第三个全局 CutoutRoot；Cutout 可形成非连续可用区域，本质是 Target 约束问题而不是新的矩形 Root。

## 设计约束

- UIKit 必须先初始化
- 加载中的同类型面板必须去重
- 同步打开依赖当前策略支持同步加载
- 栈逻辑只作用于显式 Push 的面板

## 常见误用

- `InitAsync()` 前就直接打开面板
- prefab 不包含目标 `UIPanelBase` 组件
- UIRoot 结构不符合预期层级
- 在栈逻辑之外手动改面板可见性，导致 pause/resume 状态紊乱

## 测试建议

- UIRoot 结构校验
- 同步/异步打开
- 加载去重
- 栈顶全屏遮挡逻辑
- Snapshot 输出
- SafeArea 整块约束
- Center / Left Punch / Dynamic Island 精确避让
- Cutout 缺失时 Precise -> SafeArea 自动降级
- SafeArea 为 FullScreen 或不可用时 -> EdgePadding 降级
- Portrait / Landscape / Tablet / UltraTall
- 老设备/异常 Geometry 模拟

## 相关文档

- [UIKit 使用文档](UIKit-界面系统-使用文档-Guide.md)
- [UIKit 说明文档](UIKit-界面系统-说明文档-Guide.md)
