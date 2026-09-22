# UIAdaptationKit / UI 适配系统使用文档

## 适用范围

本文面向实际制作 UI 的程序、美术和 UI 设计人员，重点说明 UIAdaptationKit 多机型适配时：

- 普通页面怎么做；
- 全屏背景怎么做；
- 刘海 / 挖孔 / Dynamic Island 怎么避让；
- 什么时候选整块 SafeArea；
- 什么时候选 PreciseCutout；
- 老设备、异常 ROM、缺少 Cutout 数据时如何自动降级。

不需要按手机品牌或型号制作多套 Prefab。

## 一、先按正常设计分辨率制作

仍按项目统一设计分辨率制作，例如：

```text
1920 x 1080
```

不要因为存在刘海或挖孔就在设计稿阶段手工为某个具体型号空出一块固定区域。

UIAdaptationKit Runtime 会根据当前设备的：

```text
Screen.width
Screen.height
Screen.safeArea
Screen.cutouts
```

生成统一的 `UIDisplayGeometry`。

## 二、先决定这个 UI 属于哪一类

### 1. 普通页面：优先 SafeArea

例如：

- 登录页；
- 设置页；
- 商城；
- 背包；
- 表单；
- 重要弹窗；
- 普通顶部导航。

推荐层级：

```text
Canvas
├─ FullScreenRoot
└─ SafeAreaRoot   <- UIAdaptationController 管理
   └─ PageContent
```

效果是整个关键 UI 区域进入系统安全矩形。UIAdaptationKit 不要求项目存在 UIKit。

这就是“整条危险区一起避开”的方案，稳定性最高。

### 2. 全屏视觉内容：FullScreen

例如：

- 背景图；
- 视频；
- 遮罩；
- 过场；
- 特效；
- 允许被挖孔覆盖的装饰。

推荐：

```text
Canvas
└─ FullScreenRoot
   └─ Background / Video / FX

Display Avoidance = None
```

背景可以一直铺到屏幕物理边缘。

### 3. 游戏 HUD / 顶部状态栏：PreciseCutout

例如：

```text
返回按钮        [刘海 / 挖孔 / Dynamic Island]        金币
```

希望继续使用危险区左右仍然可用的空间时，推荐：

```text
HUD 保持在 FullScreenRoot
UICutoutAwareLayout.Mode = PreciseCutout
UICutoutAwareLayout.Fallback = Automatic
```

然后只把真正重要的节点加入 `Targets`：

```text
Targets
├─ BackButton
├─ Title
├─ CoinPanel
└─ MiniMap
```

背景条和纯装饰节点不需要加入。

## 三、Mode 怎么选

`UICutoutAwareLayout.Mode` 有三种设计意图：

### None

```text
Mode = None
```

不做危险区避让。

适合纯视觉内容。

### SafeArea

```text
Mode = SafeArea
```

Target 必须收进 SafeArea。

适合你明确要求：

> 整条顶部 / 底部一起避开危险区域。

如果整个页面都要这么做，优先把页面放到 `SafeAreaRoot`；只有需要在 FullScreen HUD 内局部控制 Target 时才使用这个 Mode。

### PreciseCutout

```text
Mode = PreciseCutout
```

只移动真正与 Cutout 相交的 Target。

例如中央 Dynamic Island：

```text
BackButton         Dynamic Island         CoinPanel
                       ↓
                    Title 下移
```

左右没有撞到危险区的控件保持原位。

## 四、Fallback 怎么选

生产项目推荐：

```text
Fallback = Automatic
```

自动降级链：

```text
PreciseCutout
    ↓ Cutout 数据不可用
SafeArea
    ↓ SafeArea 也无法提供有效避让信息
EdgePadding
```

### Automatic

默认推荐。

适合面向大量未知终端的正式项目。

### SafeArea

```text
Fallback = SafeArea
```

只允许降到系统 SafeArea。

若 SafeArea 本身也不可用，则不再继续额外移动。

### EdgePadding

```text
Fallback = EdgePadding
```

不依赖系统 Cutout 几何，直接使用 Reference Pixel Padding 形成保守边距。

适合特殊设备或明确希望固定兜底边距的项目。

### None

```text
Fallback = None
```

不降级。

不建议普通正式项目使用。更适合固定硬件、实验或专项验证。

## 五、如何理解 EffectiveMode

运行时可查看：

```text
UICutoutAwareLayout.EffectiveMode
```

可能是：

```text
None
SafeArea
PreciseCutout
EdgePadding
```

它表示当前设备最终实际采用的策略。

例如作者配置：

```text
Mode = PreciseCutout
Fallback = Automatic
```

不同设备最终可能是：

```text
新 Android 挖孔机      -> PreciseCutout
iPhone / 有 SafeArea   -> PreciseCutout 或 SafeArea
旧设备无 Cutout        -> SafeArea
SafeArea 数据也异常     -> EdgePadding
```

业务逻辑不需要根据 `EffectiveMode` 再写平台分支，它主要用于调试和验证。

## 六、System / Manual Cutout Source

### System

```text
Source = System
```

使用 Unity 当前平台提供的 `Screen.cutouts`。

正常手机项目优先使用。

### Manual

```text
Source = Manual
```

使用手工配置的归一化危险区域。

适合：

- 特殊定制设备；
- 车机；
- 固定工业终端；
- 平台 API 暂时拿不到真实危险区；
- Editor 专项模拟。

### SystemAndManual

同时合并系统和项目自定义危险区。

## 七、推荐 Inspector 配置

### 普通页面

```text
Canvas
└─ SafeAreaRoot
   └─ PageContent
```

在 Canvas 根节点挂 `UIAdaptationController`，把 `SafeAreaRoot` 配给它即可；普通页面不需要 `UICutoutAwareLayout`。

### 游戏 HUD

```text
FullScreenRoot
└─ HUD

HUD / UICutoutAwareLayout
  Mode = PreciseCutout
  Fallback = Automatic
  Source = System
  Edge = Top
  Cutout Padding = 20
  Edge Padding = 12
  Targets = BackButton / Title / CoinPanel
```

### FullScreen 背景

```text
FullScreenRoot
└─ Background

Cutout Avoidance = Disabled / None
```

## 八、ToolsHub 怎么测试

打开：

```text
Tools Hub -> UIAdaptationKit
```

可以模拟：

```text
None
Center Punch
Dynamic Island
Left Punch
自定义 Cutout X/Y/W/H
```

选中 UIRoot 后，ToolsHub 会显示当前各个 `UICutoutAwareLayout` 的：

```text
Mode
Fallback
Effective
```

Validator 会提示：

- 开启避让但没有 Target；
- `Fallback=None` 的生产风险；
- Manual Source 没有 Manual Exclusion Zone；
- SafeAreaRoot 缺失；
- CanvasScaler 配置错误；
- Breakpoint / Anchor 风险。

## 九、测试工程

验证工程：

```text
C:\CodingToolsWorkerCenter\StellarFramework-test
```

测试场景：

```text
Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.unity
```

运行后可以切换：

```text
A  SafeArea
B  Precise Cutout

System
Center Punch
Dynamic Island
Left Punch
Legacy SafeArea
Legacy Unknown
```

其中：

```text
Legacy SafeArea
```

模拟“没有 Cutout 数据，但系统仍能提供 SafeArea”，Precise + Automatic 应显示：

```text
Effective = SafeArea
```

```text
Legacy Unknown
```

模拟“Cutout 和可靠 SafeArea 都拿不到”的老设备 / 未支持平台，Precise + Automatic 应显示：

```text
Effective = EdgePadding
```

## 十、跨平台原则

不要在 UI 代码中写：

```csharp
if (isAndroid) { ... }
if (isIOS) { ... }
if (isHuawei) { ... }
if (deviceModel == "...") { ... }
```

UI 只声明设计意图。

平台 Adapter 负责提供 `UIDisplayGeometry`，UIAdaptationKit 负责选择实际避让策略。

如果项目同时使用 StellarFramework UIKit，UIKit 只是把自己的 `FullScreenRoot / SafeAreaRoot` 接入 UIAdaptationKit；反过来 UIAdaptationKit 不依赖 UIKit。

目标原则：

> 新设备尽量利用屏幕空间；老设备、异常系统和未知平台优先保证关键 UI 可见、可点击、不会被危险区域遮挡。

## 相关文档

- [UIAdaptationKit 说明文档](UIAdaptationKit-说明文档-Guide.md)
- [UIAdaptationKit 源码文档](UIAdaptationKit-源码文档-Guide.md)
