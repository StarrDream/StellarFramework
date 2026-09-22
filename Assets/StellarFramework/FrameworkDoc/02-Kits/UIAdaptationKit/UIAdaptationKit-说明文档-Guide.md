# UIAdaptationKit / UI 适配系统说明文档

## 定位

`UIAdaptationKit` 是一个可以脱离 UIKit 单独使用的 UGUI 屏幕适配 Kit。

它解决的是：同一套 UI 在不同分辨率、横竖屏、刘海、挖孔、Dynamic Island、Safe Area 不完整、老设备和异常系统数据下，仍然保持关键内容可见、可点击和可维护。

它不负责：

- UI 页面生命周期；
- Panel 路由；
- UI 资源加载；
- ResKit；
- SingletonKit；
- UIKit。

运行时只依赖 Unity UGUI。

## 设计目标

开发者只表达 UI 设计意图，不维护设备型号表：

```text
None
SafeArea
PreciseCutout
```

框架根据当前设备能力解析实际策略：

```text
PreciseCutout
    ↓ cutout 不可靠 / 不存在
SafeArea
    ↓ safeArea 也不能提供有效避让
EdgePadding
```

核心原则：

> 新设备尽量利用屏幕空间；老设备、异常 ROM 和未知平台优先保证关键 UI 可用。

## 独立依赖边界

Runtime assembly：

```text
StellarFramework.UIAdaptationKit
```

依赖：

```text
UnityEngine
UnityEngine.UI
```

不依赖任何 StellarFramework Runtime Kit。

Tools assembly：

```text
StellarFramework.ToolsHub.UIAdaptationKit.Editor
```

仅在需要 ToolsHub Preview / Validator 时使用。

## 核心组成

### UIDisplayGeometry

平台无关的屏幕几何快照：

```text
Width / Height
SafeArea
SafeAreaDataValid
HasSafeAreaInsets
Cutouts
```

### UIAdaptationProfile

定义：

- 设计分辨率；
- CanvasScaler Match；
- Safe Area 是否启用；
- Aspect Breakpoints；
- Portrait / Landscape 约束。

### UIAdaptationController

负责：

- CanvasScaler；
- SafeAreaRoot；
- 当前 UIDisplayGeometry；
- Breakpoint；
- 屏幕方向 / safeArea / cutout 变化后的重新应用。

### UICutoutAwareLayout

用于关键 Target 的危险区避让。

支持：

- `Mode=None`；
- `Mode=SafeArea`；
- `Mode=PreciseCutout`；
- `Fallback=Automatic / SafeArea / EdgePadding / None`；
- System Cutouts；
- Manual Exclusion Zones；
- Top / Bottom / Any；
- Auto / Horizontal / Vertical 位移策略。

### UILayoutVariant

用于真正需要不同布局结构的 Breakpoint 快照。

不要用它替代 CanvasScaler，也不要为每台手机建立一个 Variant。

## 推荐层级

完全不使用 UIKit 时：

```text
Canvas + CanvasScaler + UIAdaptationController
│
├─ FullScreenRoot
│  ├─ Background
│  └─ HUD + UICutoutAwareLayout
│
└─ SafeAreaRoot
   └─ NormalPage
```

其中：

- 背景、视频、特效放 `FullScreenRoot`；
- 普通重要 UI 放 `SafeAreaRoot`；
- 需要尽量利用屏幕边缘的 HUD 使用 `UICutoutAwareLayout`。

## 与 UIKit 的关系

关系是：

```text
UIAdaptationKit     UIKit
       ↑              │
       └── optional ───┘
```

UIAdaptationKit 不引用 UIKit。

UIKit Complete 可以组合 UIAdaptationKit，但：

```text
只导 UIAdaptationKit -> 可用
只导 UIKit.Core      -> 可用
导 UIKit Complete    -> 两者组合
```

已有 UIKit 的 `PanelLayoutRegion.SafeArea` 仍属于 UIKit 自己的 Panel 路由语义；底层屏幕几何适配能力不再属于 UIKit。

## 横竖屏

Shape Aspect 使用：

```text
max(width, height) / min(width, height)
```

因此同一屏幕的：

```text
2400 x 1080
1080 x 2400
```

会得到相同 Shape Aspect，但 `ResolveOrientation()` 仍分别返回 Landscape / Portrait。

这允许：

- 同一设备共用屏幕形状 Breakpoint；
- 必要时再通过 Orientation 对布局进行约束。

## 兼容策略

不推荐：

```csharp
if (isHuawei) { ... }
if (isIPhone) { ... }
if (deviceModel == "...") { ... }
```

推荐能力检测：

```text
Screen.cutouts
Screen.safeArea
Screen.width / height
```

未来 HarmonyOS 或其他平台若需要原生适配，只需要提供对应 `UIDisplayGeometry`，不需要重写布局算法。

## 分发

最小运行时：

```text
UIAdaptationKit.Core
```

带 Preview / Validator：

```text
UIAdaptationKit.Tools
```

推荐完整开发包：

```text
UIAdaptationKit Complete
```

## 相关文档

- [UIAdaptationKit 使用文档](UIAdaptationKit-使用文档-Guide.md)
- [UIAdaptationKit 源码文档](UIAdaptationKit-源码文档-Guide.md)
