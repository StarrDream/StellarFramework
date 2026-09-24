# RuntimeTools / 源码文档

## Assembly

Runtime：

```text
Assets/StellarFramework/Runtime/Tools
└─ StellarFramework.Runtime.Tools.asmdef
```

当前 Runtime assembly 不引用其他 StellarFramework assembly。

Editor ToolsHub：

```text
Assets/StellarFramework/Editor/StellarToolsHub/Modules/RuntimeTools/Core
└─ StellarFramework.ToolsHub.RuntimeTools.Editor.asmdef
```

依赖：

```text
StellarFramework.ToolsHub.Editor
StellarFramework.Runtime.Tools
```

## 命名空间

新 RuntimeTools API 使用：

```csharp
StellarFramework.RuntimeTools
```

历史 `CoroutineRunner` 保留 `StellarFramework` 命名空间以避免已有代码迁移。

## 文件结构

```text
Runtime/Tools
├─ CoroutineRunner.cs
├─ Core
│  ├─ WeightedRandom.cs
│  ├─ TransformSnapshot.cs
│  ├─ TransformUtil.cs
│  ├─ RandomPointUtil.cs
│  └─ FrameRateSampler.cs
├─ Transform
│  ├─ FollowTarget.cs
│  ├─ Rotator.cs
│  ├─ UniversalBillboard.cs
│  └─ TransformShake.cs
├─ Physics
│  ├─ PhysicsProbe.cs
│  ├─ PhysicsOverlap.cs
│  ├─ GroundChecker.cs
│  ├─ BoundsUtility.cs
│  ├─ PhysicsRelayFilter.cs
│  ├─ TriggerRelay.cs
│  └─ CollisionRelay.cs
└─ Rendering
   └─ RendererPropertyBlockController.cs
```

## 关键实现约束

### WeightedRandom

- `IReadOnlyList<T>` 顺序扫描；
- 不使用 LINQ；
- `double` 累加总权重，降低大量权重累计误差；
- `sample01` 有确定性重载；
- 数据非法返回 false，不静默夹紧权重。

### FollowTarget

平滑因子：

```text
1 - exp(-speed * deltaTime)
```

因此不同帧率下收敛速度一致，不使用容易在低帧率越界的 `deltaTime * speed`。

### PhysicsProbe

统一请求支持：

```text
Ray
SphereCast
BoxCast
CapsuleCast
```

结果只包裹 `RaycastHit`，不额外复制命中数据。

Capsule 以 `Orientation * Vector3.up` 作为轴，根据 `CapsuleHeight` 和 `Radius` 计算两个球心。

### GroundChecker

- FixedUpdate 驱动；
- 可通过 `Probe()` 只查询、不改变状态；
- `Evaluate()` 同时更新稳定帧状态；
- Grounded 使用 stableFrames 防抖；
- Airborne 在失去命中后立即切换，避免角色悬空仍延迟保留 Grounded。

### BoundsUtility

便利重载内部会创建 `List<T>`。这是为一次性/低频 authoring 设计。

高频路径必须调用接收 scratch List 的重载，内部只 `Clear()` 并复用容量。

### Physics Relay

不创建自定义 Mouse/Physics EventData；直接透传 `Collider` / `Collision`，避免 Stay 高频路径额外 GC。

### FrameRateSampler

- 固定容量 `float[]` 环形窗口只在构造时分配一次；
- `PushFrame` 只累计 deltaTime / frame count；
- 只有生成新采样时才扫描当前有效窗口计算 Avg/Min/Max；
- NaN / Infinity / 非正 deltaTime 不进入统计；
- `FrameRateMonitor` 只负责 Unity Update 驱动和事件，不包含 OnGUI/TMP/UGUI。

### PhysicsOverlap

- 统一 `OverlapSphereNonAlloc / OverlapBoxNonAlloc / OverlapCapsuleNonAlloc`；
- caller-owned `Collider[]` 决定容量和生命周期；
- 数组满时遵循 Unity NonAlloc 原生语义，不内部扩容；
- Box half extents 取绝对值，Capsule height 不小于直径。

### TransformShake

- 内部噪声时间由 `Tick(deltaTime)` 推进，不直接依赖 `Time.time`，便于回放/测试；
- 固定 seed，不消耗 `UnityEngine.Random` 全局状态；
- Trauma 使用可配置指数曲线；
- Stop/Disable 恢复捕获的本地 Position/Rotation；
- 与正式移动逻辑共享同一个 Transform 会产生所有权冲突，因此推荐使用专用 ShakePivot。

### RendererPropertyBlockController

- 只操作 `MaterialPropertyBlock`，不访问 `renderer.material`；
- 每次写入先 `GetPropertyBlock` 保留其他系统已写属性；
- 内部复用单个 `MaterialPropertyBlock`；
- 材质槽验证使用 `Renderer.GetSharedMaterials(List<Material>)` + 可复用 List，避免 `sharedMaterials` 数组分配；
- `ClearAll()` 明确清空整个 block，不承诺只移除 RuntimeTools 写入的单个属性。

## Tests

EditMode：

```text
RuntimeToolsTests
```

覆盖：

- WeightedRandom 边界和确定采样；
- TransformSnapshot/TransformUtil；
- RandomPoint 区域约束；
- FollowTarget/Rotator/Billboard；
- 真实 PhysicsProbe；
- BoundsUtility；
- GroundChecker stableFrames；
- PhysicsRelayFilter；
- FrameRateSampler 固定窗口统计；
- PhysicsOverlap caller buffer / LayerMask；
- RendererPropertyBlock 属性保留/清理；
- TransformShake 基线恢复；
- FrameRateSampler / PhysicsOverlap 预热后热路径 exact managed allocation = 0 bytes。

PlayMode：

```text
RuntimeToolsPlayModeTests
```

覆盖真实 Unity Physics Trigger Enter/Exit 与 Collision Enter 回调。

## 扩展规则

新增工具进入 RuntimeTools 前必须回答：

1. 是否已有 Kit 能正确解决？有则拒收。
2. 是否高频或容易重复写错？否则拒收。
3. 是否可以保持低依赖？依赖 UGUI/URP 等能力时必须拆 Adapter/Profile。
4. 是否具备测试价值？无法定义明确输入/输出/行为契约的工具通常不应进入 Core。
5. 是否可以写清楚“不应该什么时候用”？如果边界不清晰，先留在项目层验证。

## 相关文档

- [RuntimeTools 使用文档](RuntimeTools-使用文档-Guide.md)
- [RuntimeTools 说明文档](RuntimeTools-说明文档-Guide.md)
