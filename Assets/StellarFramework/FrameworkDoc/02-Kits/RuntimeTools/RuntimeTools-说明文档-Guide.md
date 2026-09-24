# RuntimeTools / 说明文档

## 定位

`RuntimeTools.Core` 是 StellarFramework 的轻量通用工具层。

它只收录满足以下至少一项的能力：

- 多类项目高频重复使用；
- 手写时容易产生语义/坐标/生命周期错误；
- 可以统一低 GC / 无临时集合的实现；
- 适合作为一个小而完整的 MonoBehaviour 或静态 API 独立使用。

RuntimeTools **不是第二套 Kit 系统，也不是杂物箱**。已有正式 Kit 覆盖的能力不会重复实现：

| 需求 | 正确入口 |
| --- | --- |
| 对象池 | PoolKit |
| 时间/延迟/调度 | TimeKit |
| 资源生命周期 | ResKit |
| UI 页面系统 | UIKit |
| 单例 | SingletonKit |
| 流程/编排 | FlowKit |

## 当前能力

### Core

- `WeightedRandom`：低分配带权抽取，支持确定采样值用于回放和测试。
- `TransformSnapshot`：捕获/恢复本地 Position、Rotation、Scale。
- `TransformUtil`：ResetLocal、单轴位置修改、SetParentAndResetLocal。
- `RandomPointUtil`：圆内/圆周/球内/Bounds 内随机点。
- `FrameRateSampler / FrameRateMonitor`：固定窗口帧率采样与 Unity 生命周期驱动；只提供数据，不拥有显示层。

### Transform

- `FollowTarget`：位置/旋转/缩放独立跟随，支持初始偏移和帧率无关指数平滑。
- `Rotator`：局部/世界固定角速度旋转。
- `UniversalBillboard`：完整朝向或仅 Y 轴朝向相机。
- `TransformShake`：固定 seed 的轻量 Trauma 抖动，支持位置/旋转并可外部 Tick。

### Physics

- `PhysicsProbe`：统一 Ray/Sphere/Box/Capsule Cast 请求与结果。
- `GroundChecker`：基于 PhysicsProbe 的低分配地面检测，支持稳定帧。
- `BoundsUtility`：Renderer/Collider 世界 Bounds 计算，并提供 scratch List 低分配重载。
- `TriggerRelay`：Trigger Enter/Stay/Exit 转 UnityEvent + C# event。
- `CollisionRelay`：Collision Enter/Stay/Exit 转 UnityEvent + C# event。
- `PhysicsRelayFilter`：Relay 共用 Layer/Tag 过滤。
- `PhysicsOverlap`：统一 Sphere/Box/Capsule `Overlap*NonAlloc`，结果写入 caller-owned 数组。

### Rendering

- `RendererPropertyBlockController`：复用 `MaterialPropertyBlock` 做单 Renderer 属性覆盖，避免 `renderer.material` 隐式实例化。

### Compatibility

- `CoroutineRunner`：为非 MonoBehaviour 代码提供最小协程宿主。它是兼容工具，不是新异步业务的推荐架构。

## 依赖边界

Runtime assembly：

```text
StellarFramework.Runtime.Tools
└─ UnityEngine
```

不依赖：

```text
TimeKit
PoolKit
UIKit / UGUI
ResKit
SingletonKit
URP
UniTask
```

因此 `RuntimeTools.Core` 可以单独导入已有 Unity 项目。

## 性能原则

- 高频 API 不使用 LINQ。
- PhysicsProbe 不分配命中集合。
- PhysicsOverlap 使用调用方结果数组，不创建 Collider 工作集合。
- WeightedRandom 单次抽取不创建工作列表。
- FrameRateSampler 构造时一次性分配固定环形数组，PushFrame 不创建工作集合。
- BoundsUtility 的便利重载会创建 scratch List；高频调用必须使用传入可复用 List 的重载。
- FollowTarget 使用 `1-exp(-speed*dt)` 指数平滑，避免 `dt*speed` 在低帧率时越界或表现变化。
- Relay 直接透传 Unity Physics 对象，不为每次事件创建自定义 EventData。
- RendererPropertyBlockController 复用同一个 MaterialPropertyBlock；材质槽校验通过可复用 List 获取 SharedMaterials，不读取会分配数组的 `renderer.sharedMaterials`。

## ToolsHub

安装 `RuntimeTools.Tools` 后，ToolsHub 的 `Runtime Tools` 模块提供：

- Transform Snapshot 捕获/恢复；
- Reset Local Transform；
- FollowTarget / Rotator / Billboard / GroundChecker / TriggerRelay / CollisionRelay / TransformShake / FrameRateMonitor / PropertyBlock 快速挂载；
- Renderer / Collider Bounds 检查；
- PlayMode FrameRate 数据查看；
- PropertyBlock 材质槽风险检查；
- FollowTarget 缺 Target、TriggerRelay 缺 Trigger Collider 等基础配置风险提示。

Runtime 永远不引用 ToolsHub。

## 不进入 RuntimeTools 的 Utils 候选

从独立 Utils 仓库评估后，以下能力明确不直接迁入：

- `ObjectPool`：已有 PoolKit；
- `Timer / DelayedAction`：已有 TimeKit；
- `ScreenFader`：应由 UIKit/流程层负责；
- `MiniFPSController / AimRaycast / DynamicScaleLimiter / TriangularScanMesh`：业务或 Demo 属性过强；
- `URPTransparencyController`：URP 专属，未来若有必要只能作为可选 Adapter；
- `CombinedMeshCollider`：框架已有 Editor 侧工具，应统一已有实现而不是复制 Runtime 版本。
- `CameraScreenshot`：文件系统、分辨率、RenderTexture 生命周期和平台权限策略明显属于项目/调试工作流，Unity 也已有 ScreenCapture；不作为通用 Runtime Core。
- `CameraFreeLook / SimpleDragTrigger3D / MouseFollower`：直接绑定旧 `UnityEngine.Input`、鼠标/键盘/触屏策略和具体交互所有权；不应在通用工具层替项目决定输入方案。
- `RaycastTool`：核心检测能力已由 `PhysicsProbe` 覆盖；原实现还混合 Gizmo、Tag List、持续日志与命中状态机，职责过多，不重复迁入。
- `ColliderEventObj`：把输入、3D/2D Raycast、UI 屏蔽、点击/双击/拖拽混在一起，并为交互回调创建 EventData 对象；不符合 RuntimeTools 的低 GC 和职责边界。
- `GizmoDrawer`：主要是 Editor 可视化 authoring，内部动态 List/Find/RemoveAll 也不是 Player Runtime 必需能力；这类需求优先进入 ToolsHub/Editor diagnostics。
- `ParallaxEffect`：具体视觉效果和循环地图策略，不是跨项目基础设施；留给业务表现层。
- `TextTypewriter`：强绑定旧 UGUI Text/AudioSource，并通过逐字符字符串拼接产生持续 GC；未来若框架需要文字揭示效果，应先定义 UI/TMP 可替换接口和零/低 GC 文本策略，而不是迁移当前实现。
- `CollapsibleItem`：具体 LayoutElement/Button 组件和 Accordion 业务交互，属于 UIKit/业务 UI Widget，而不是 Runtime Core。
- `UIDragger / UIInputTrigger`：强绑定 EventSystem/UGUI 交互语义，项目通常还要和输入系统、手柄/触屏、业务状态结合；不放进零依赖 Core。
- `UGUIFollowTarget / Manager`：需求本身有价值，但现有实现依赖全局 Manager singleton 并把可见性策略、Canvas 映射、平滑策略绑在一起。本阶段暂不迁移；若后续进入框架，应作为独立 UI Adapter 并重新设计，而不是原样复制。
- `MathUtil`：DistanceXZ/Remap/SmoothDamp 等多数只是 Unity API 的薄别名，收益不足以抵消 API 面积；不为了“少写两行”扩充 Core。

## 相关文档

- [RuntimeTools 使用文档](RuntimeTools-使用文档-Guide.md)
- [RuntimeTools 源码文档](RuntimeTools-源码文档-Guide.md)
