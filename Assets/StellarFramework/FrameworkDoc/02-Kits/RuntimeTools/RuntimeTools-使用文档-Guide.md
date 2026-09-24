# RuntimeTools / 使用文档

## 1. 最小导入

只需要运行时工具时导入：

```text
RuntimeTools.Core
```

需要 ToolsHub 快速挂载和诊断时再额外导入：

```text
RuntimeTools.Tools
```

`RuntimeTools.Core` 没有其他 StellarFramework Runtime Kit 依赖。

## 2. WeightedRandom

```csharp
using StellarFramework.RuntimeTools;

var rewards = new[]
{
    new WeightedValue<string>("Gold", 70f),
    new WeightedValue<string>("Gem", 25f),
    new WeightedValue<string>("Rare", 5f)
};

if (WeightedRandom.TryChoose(rewards, out string reward))
{
    Debug.Log(reward);
}
```

需要确定性回放/测试时，直接提供 `[0,1]` 采样值：

```csharp
WeightedRandom.TryChoose(rewards, 0.75f, out string reward);
```

规则：负权重、NaN、Infinity 或全部为 0 时返回 `false`，不会偷偷修正错误数据。

## 3. TransformSnapshot / TransformUtil

```csharp
TransformSnapshot snapshot = TransformSnapshot.Capture(transform);

transform.SetPositionY(5f);
transform.SetLocalPositionX(2f);

snapshot.Restore(transform);
```

重置：

```csharp
transform.ResetLocal();
```

## 4. RandomPointUtil

出生点半径随机：

```csharp
Vector3 spawn = spawnCenter.position + RandomPointUtil.InsideCircleXZ(2f);
```

Bounds 内散布：

```csharp
Vector3 point = RandomPointUtil.InsideBounds(areaBounds);
```

## 5. FollowTarget

挂到跟随者上：

```csharp
FollowTarget follow = follower.AddComponent<FollowTarget>();
follow.SetTarget(playerHead, captureOffset: true);
```

Inspector 可分别开启位置、旋转、缩放，`preserveInitialOffset` 会把场景当前相对关系作为偏移。

`Smooth Speed = 0` 表示立即跟随；大于 0 使用帧率无关指数平滑。

## 6. Rotator

适合展示模型、转盘、指示器等简单持续旋转：

```csharp
Rotator rotator = gameObject.AddComponent<Rotator>();
rotator.SetSpeed(new Vector3(0f, 90f, 0f));
```

不要用它替代正式 Animator/Tween 状态机。

## 7. UniversalBillboard

世界 UI / 角色名牌：

```csharp
UniversalBillboard billboard = label.AddComponent<UniversalBillboard>();
billboard.SetCamera(mainCamera);
```

`YAxisOnly` 适合站立名牌；`Full` 适合真正需要完全朝向相机的图标/特效面片。

## 8. PhysicsProbe

Ray：

```csharp
PhysicsProbeRequest request = PhysicsProbeRequest.Ray(
    transform.position,
    transform.forward,
    10f,
    obstacleLayers,
    QueryTriggerInteraction.Ignore);

if (PhysicsProbe.TryCast(in request, out PhysicsProbeHit hit))
{
    Debug.Log($"{hit.Collider.name} / {hit.Distance}");
}
```

Sphere / Box / Capsule 通过 `PhysicsProbeRequest` 的 Shape、Radius、HalfExtents、CapsuleHeight 与 Orientation 配置。

## 9. GroundChecker

直接挂组件即可。常见角色配置：

```text
Shape               Sphere
Local Offset        脚底附近
Direction           (0,-1,0)
Distance            0.1 ~ 0.3
Radius              角色脚底半径
Trigger Interaction Ignore
Stable Frames       2
```

代码侧可读取：

```csharp
checker.IsGrounded
checker.LastHit.Point
checker.LastHit.Normal
```

也可以订阅 `Grounded / Airborne`。

## 10. BoundsUtility

一次性/低频：

```csharp
BoundsUtility.TryCalculateColliderBounds(root, true, out Bounds bounds);
```

如果需要高频调用，必须复用 scratch List：

```csharp
private readonly List<Collider> _scratch = new List<Collider>(32);

BoundsUtility.TryCalculateColliderBounds(root, true, _scratch, out Bounds bounds);
```

## 11. TriggerRelay / CollisionRelay

把 Relay 挂到物理对象后，可以直接在 Inspector 绑定 UnityEvent，也可以代码订阅：

```csharp
relay.Entered += OnEntered;
relay.Exited += OnExited;
```

支持 LayerMask 和可选 Tag 过滤。

`TriggerRelay` 通常需要 `Collider.isTrigger = true`，并遵守 Unity 原生规则：交互双方至少一侧需要 Rigidbody 才能产生对应物理事件。

## 12. FrameRateSampler / FrameRateMonitor

只需要数值、不需要组件：

```csharp
var sampler = new FrameRateSampler(sampleWindow: 30, refreshInterval: 0.5f);

if (sampler.PushFrame(Time.unscaledDeltaTime))
{
    FrameRateSnapshot stats = sampler.Snapshot;
    Debug.Log($"FPS {stats.CurrentFps:F1} / Avg {stats.AverageFps:F1}");
}
```

如果希望直接由 Unity 生命周期驱动，挂 `FrameRateMonitor`：

```csharp
monitor.Updated += snapshot =>
{
    // 自己决定显示到 UGUI、TMP、日志或遥测系统。
};
```

它**不会创建 OnGUI、Texture 或显示字符串**。RuntimeTools 只提供采样数据，不拥有你的 UI。

## 13. PhysicsOverlap

无分配 Sphere Overlap：

```csharp
private readonly Collider[] _results = new Collider[32];

PhysicsOverlapRequest request = PhysicsOverlapRequest.Sphere(
    transform.position,
    3f,
    enemyLayers,
    QueryTriggerInteraction.Ignore);

int count = PhysicsOverlap.QueryNonAlloc(in request, _results);
for (int i = 0; i < count; i++)
{
    Collider target = _results[i];
}
```

Box / Capsule 使用 `PhysicsOverlapRequest.Box(...)`、`Capsule(...)`。

注意：结果数组满时 Unity 只会返回能够写入的数量；如果业务要求“绝不能漏结果”，由项目根据最大密度决定数组容量或执行分批策略，RuntimeTools 不在内部偷偷扩容。

## 14. TransformShake

适合无 Cinemachine 依赖的轻量视觉抖动：

```csharp
TransformShake shake = cameraShakePivot.AddComponent<TransformShake>();
shake.AddTrauma(0.4f);
```

推荐结构：

```text
CameraRig / WeaponRoot     <- 正式移动逻辑
└─ ShakePivot              <- TransformShake
   └─ Camera / Visual
```

不要让角色移动系统和 `TransformShake` 同时争抢同一个 Transform 的 `localPosition/localRotation`。如果必须主动改变 ShakePivot 基线，可调用 `RecaptureBaseTransform()`。

## 15. RendererPropertyBlockController

需要改变单个 Renderer 的颜色/数值，又不想因为 `renderer.material` 产生材质实例：

```csharp
private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");

propertyBlock.SetFloat(HitFlashId, 1f);
```

支持 `Float / Int / Color / Vector / Texture`。

同一个 Renderer 可能被多个系统共享 PropertyBlock，因此 RuntimeTools 每次写入都会先读取现有 block，再修改指定值，避免直接覆盖其他属性。`ClearAll()` 则明确表示清空整块数据，调用前要确认所有权。

高频路径建议缓存 `Shader.PropertyToID`，不要每帧使用字符串转 ID。

## 16. ToolsHub

打开：

```text
StellarFramework -> Tools Hub -> Runtime Tools
```

推荐用途：

1. 在 Hierarchy 选择目标；
2. 快速添加常用 RuntimeTools 组件；
3. 使用 Selection Validation 看明显配置错误；
4. 使用 Bounds Diagnostics 核对模型/碰撞范围；
5. PlayMode 查看 FrameRateMonitor 的 Current / Avg / Min / Max；
6. 检查 PropertyBlock 材质槽配置；
7. 临时修改 Transform 前先 Capture，完成检查后 Restore。

## 17. 选择原则

如果功能已经属于一个完整能力域，优先用正式 Kit，而不是 RuntimeTools：

```text
对象池     -> PoolKit
时间调度   -> TimeKit
资源加载   -> ResKit
UI 页面    -> UIKit
流程编排   -> FlowKit
```

RuntimeTools 的目标是解决小而明确的问题，不控制项目架构。
