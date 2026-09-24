# UIAdaptationKit / UI 适配系统源码文档

## 源码位置

Runtime：

```text
Assets/StellarFramework/Runtime/Kits/UIAdaptationKit
├─ StellarFramework.UIAdaptationKit.asmdef
└─ Runtime
   ├─ UIDisplayGeometry.cs
   ├─ UIAdaptationProfile.cs
   ├─ UIAdaptationController.cs
   ├─ UICutoutAwareLayout.cs
   └─ UILayoutVariant.cs
```

Editor：

```text
Assets/StellarFramework/Editor/StellarToolsHub/Modules/UIAdaptationKit
├─ StellarFramework.ToolsHub.UIAdaptationKit.Editor.asmdef
└─ UIAdaptationKitHubModule.cs
```

命名空间继续使用：

```csharp
StellarFramework.UI.Adaptation
```

这是有意保留的兼容约束，避免已有代码、Prefab 和 Scene 因目录拆分而产生无意义迁移成本。

## Assembly 边界

### Runtime

```text
StellarFramework.UIAdaptationKit
└─ UnityEngine.UI
```

禁止反向引用：

```text
UIKit
ResKit
SingletonKit
ToolsHub
```

### Editor

```text
StellarFramework.ToolsHub.UIAdaptationKit.Editor
├─ StellarFramework.ToolsHub.Editor
├─ StellarFramework.UIAdaptationKit
└─ UnityEngine.UI
```

## UIDisplayGeometry

这是 Runtime 的平台边界模型。

重要字段：

```csharp
int Width
int Height
Rect SafeArea
bool SafeAreaDataValid
bool HasSafeAreaInsets
IReadOnlyList<Rect> Cutouts
```

`UIAdaptationController` 默认从 Unity Screen API 创建 Geometry。

外部平台 Adapter 也可以把原生窗口信息转换成相同 Geometry，再调用：

```csharp
controller.Apply(width, height, safeArea, cutouts);
```

## UIAdaptationController

组件要求：

```csharp
[RequireComponent(typeof(CanvasScaler))]
```

主要职责：

1. 解析安全屏幕尺寸；
2. 计算 Breakpoint；
3. 配置 CanvasScaler；
4. 归一化 SafeArea；
5. 归一化 Cutouts；
6. 更新一个或多个 SafeAreaRoot；
7. 缓存 `CurrentGeometry`；
8. 仅在 Geometry 真正变化时触发 `DisplayGeometryChanged`；
9. 仅在 Breakpoint 真正变化时触发 `BreakpointChanged`。

普通 Panel 不需要自己轮询 `Screen.safeArea`。

系统快照路径在每帧比较宽高和原始 safeArea；这两类变化会立即重新读取并应用屏幕快照。若二者未变化，则每 0.5 秒最多读取一次 `Screen.cutouts`。探测器直接把该数组与已缓存的归一化矩形逐项比较，跳过非法或裁剪后为空的输入，不创建中间集合；确认有效变化后，将同一份数组传给 `ApplyGeometry`，不二次读取系统属性。公开 `Apply(...)` 仍用于外部显式提供几何快照；`RefreshDisplayGeometry()` 会切回系统快照和 cutout 自动探测。

## UIAdaptationProfile

Profile 负责数据，不负责 Scene 行为。

Breakpoint 匹配由：

```text
ShapeAspect + Orientation
```

决定。

ShapeAspect 与横竖屏无关：

```csharp
max(width, height) / min(width, height)
```

Orientation 单独解析。

## UICutoutAwareLayout

### Author Mode

```csharp
UIDisplayAvoidanceMode
```

- None
- SafeArea
- PreciseCutout

### Fallback

```csharp
UIDisplayFallbackMode
```

- Automatic
- SafeArea
- EdgePadding
- None

### Resolved Mode

```csharp
UIDisplayResolvedMode
```

- None
- SafeArea
- PreciseCutout
- EdgePadding

### Precise solver

`UICutoutLayoutSolver.CalculateOffset(...)`：

1. 检查 Target 是否与 exclusion 相交；
2. 枚举合法左右/上下候选位移；
3. 过滤越出允许区域的候选；
4. 选择最小平方位移；
5. 多 exclusion 时迭代处理。

### Containment solver

`CalculateContainmentOffset(...)` 用于 SafeArea / EdgePadding fallback，把 Target 收回允许矩形。

### 性能

- 不在每个 Layout 上独立轮询系统 API；
- Controller 统一发布 Geometry 变化；
- exclusion buffer 复用；
- world corners 数组复用；
- 没有 Cutout / Geometry 变化时不重复重算。

## UILayoutVariant

用于 Breakpoint 级布局快照。

Breakpoint 变化顺序：

```text
Controller Resolve Breakpoint
    ↓
BreakpointChanged
    ↓
UILayoutVariant Apply
    ↓
DisplayGeometryChanged
    ↓
UICutoutAwareLayout 基于新布局重新避让
```

这样 Cutout offset 不会基于旧 Breakpoint 布局累积。

## ToolsHub

`UIAdaptationKitHubModule` 提供：

- Profile 创建；
- 16:9 / 20:9 / 4:3 / Portrait Preview；
- Safe Insets；
- Center Punch；
- Left Punch；
- Dynamic Island；
- 自定义 Cutout；
- Controller 配置；
- Layout Variant Capture；
- Validator；
- Mode / Fallback / Effective 诊断。

## 回归要求

至少覆盖：

- Landscape / Portrait；
- ShapeAspect orientation independent；
- SafeArea anchors；
- Center Punch；
- Corner Punch；
- Dynamic Island；
- Manual exclusion；
- Precise -> SafeArea；
- Precise -> EdgePadding；
- invalid SafeArea provider；
- LayoutVariant；
- 不依赖 UIKit 的 clean-project import。

## 兼容迁移约束

从旧 `Runtime/Kits/UIKit/Adapters/Adaptation` 拆出时：

- `.cs.meta` GUID 保持不变；
- namespace 保持 `StellarFramework.UI.Adaptation`；
- Scene/Prefab MonoScript 引用不应丢失；
- 旧 `uikit.adaptation` 分发 ID 保留为兼容别名，但新项目使用 `uiadaptation.*`。

## 相关文档

- [UIAdaptationKit 使用文档](UIAdaptationKit-使用文档-Guide.md)
- [UIAdaptationKit 说明文档](UIAdaptationKit-说明文档-Guide.md)
