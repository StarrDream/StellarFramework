# ActionKit / 动作系统源码文档

## 模块职责

`ActionKit` 提供两条动作执行路线：

- 代码式动作链：`UniActionChain`
- 配置式动作系统：`ActionEngine`

前者强调链式调用、低样板和对象池复用；后者强调可配置、可复用、可快进和可还原。

## 源码文件

- `Runtime/Kits/ActionKit/ActionKit.cs`
- `Runtime/Kits/ActionKit/TweenExtensions.cs`
- `Runtime/Kits/ActionKit/ActionEngine/ActionData.cs`
- `Runtime/Kits/ActionKit/ActionEngine/ActionStrategy.cs`
- `Runtime/Kits/ActionKit/ActionEngine/ActionEngineRunner.cs`
- `Runtime/Kits/ActionKit/ActionEngine/ActionEngineAsset.cs`
- `Runtime/Kits/ActionKit/ActionEngine/ActionPlayer.cs`

## 总体结构

```text
ActionKit
├─ Sequence(...)
└─ Delay(...)

UniActionChain
├─ _target
├─ _steps
├─ _selfCts
├─ _state
└─ 对象池生命周期

TweenKit / Easing
└─ 插值执行器

ActionEngine
├─ ActionEngineAsset
├─ ActionNodeData
├─ IActionStrategy
├─ IFastForwardable
├─ ActionEngineRunner
└─ ActionPlayer
```

## 两条执行链路

### 代码式动作链

1. `ActionKit.Sequence(...)`
2. 分配 `UniActionChain`
3. 通过扩展方法追加步骤
4. `Start()` 或 `Await()`
5. 完成后回收到 `PoolKit`

### 配置式动作系统

1. `ActionPlayer` 绑定 `ActionEngineAsset`
2. `ActionEngineRunner.InitSnapshot(...)`
3. `ActionEngineRunner.Play(...)`
4. 逐节点解析 `ActionNodeData`
5. 匹配 `IActionStrategy`
6. 正放或倒放执行

## 类型详解

## `ActionKit`

### 作用

代码式动作链静态入口。

### 关键方法

#### `Sequence(GameObject target)`

职责：

- 校验目标不为空
- 从 `PoolKit` 分配 `UniActionChain`
- 设置目标对象

#### `Sequence(Component component)`

从组件取 `gameObject` 后复用 `Sequence(GameObject)`。

#### `Delay(float seconds, Action callback, GameObject target, ...)`

快速构造一个：

- 先等待
- 再回调
- 然后自动启动

的链式动作。

## `UniActionChain`

### 作用

可复用、可池化的动作链实例。

### 内部状态 `ChainState`

- `None`
- `Idle`
- `Running`
- `Cancelled`
- `Completed`
- `Faulted`
- `Recycled`

### 核心字段

- `_target`
  当前动作链绑定的目标对象。
- `_steps`
  顺序执行的异步步骤列表。
- `_onComplete`
- `_onCancel`
- `_onError`
- `_selfCts`
  当前链自己的取消源。
- `_ignoreTimeScale`
  是否忽略时间缩放。
- `_state`
  当前状态。
- `_version`
  版本号，用于区分不同生命周期轮次。

### 核心属性

- `IsIgnoreTimeScale`

### 生命周期方法

#### `OnAllocated()`

对象池分配回调。

职责：

- 清空旧步骤
- 重置目标和回调
- 生成新的 `_selfCts`
- 把状态切回 `Idle`
- `_version++`

#### `OnRecycled()`

对象池回收回调。

职责：

- 清空步骤和回调
- 取消并释放 `_selfCts`
- 把状态设为 `Recycled`
- `_version++`

### 构建方法

#### `SetTarget(GameObject target)`

只允许在 `Idle` 状态设置目标。

#### `SetUpdate(bool ignoreTimeScale)`

配置时间缩放策略。

#### `AppendTask(Func<CancellationToken, UniTask> task)`

追加原始异步步骤。

#### `Delay(float seconds)`

追加时间等待步骤。

#### `DelayFrame(int frames)`

追加帧等待步骤。

#### `Callback(Action action, ...)`

追加纯回调步骤。

#### `Until(Func<bool> condition)`

追加等待条件成立的步骤。

#### `Parallel(params Func<CancellationToken, UniTask>[] asyncActions)`

把多个异步动作合并为一个并行步骤。

#### `OnComplete(Action onComplete)`
#### `OnCancel(Action onCancel)`
#### `OnError(Action<Exception> onError)`

注册链结束回调。

### 运行方法

#### `Cancel()`

取消当前链。

约束：

- 已完成、已取消、已故障状态下不再重复取消

#### `Start()`

启动动作链，不等待结果。

职责：

- 校验目标
- 切换状态到 `Running`
- 异步 fire-and-forget 执行 `RunAsync(...)`

#### `Await()`

启动并等待动作链执行完成。

### 私有执行方法

#### `RunAsync(int runVersion)`

真正的运行循环。

执行流程：

1. 取目标销毁令牌
2. 与 `_selfCts` 建立 linked token
3. 顺序执行 `_steps`
4. 根据结果进入：
   - `Completed`
   - `Cancelled`
   - `Faulted`
5. 最终回收到对象池

### 守卫方法

#### `EnsureUsable(...)`
#### `EnsureBuildable(...)`
#### `EnsureRunnable(...)`

分别负责：

- 是否已回收
- 是否允许继续构建
- 是否允许启动

## `Ease`

### 作用

定义缓动函数类型。

包括：

- `Linear`
- `In/Out/InOut Quad`
- `In/Out/InOut Cubic`
- `In/Out/InOut Sine`
- `In/Out/InOut Back`
- `In/Out/InOut Bounce`

## `Easing`

### 作用

根据 `Ease` 计算 0~1 的插值曲线值。

### 关键方法

- `Evaluate(Ease ease, float t)`

## `TweenKit`

### 作用

插值执行器，负责真正按时间推进数值。

### 核心方法

- `To(float start, float end, ...)`
- `To(Vector3 start, Vector3 end, ...)`
- `To(Color start, Color end, ...)`
- `ToRotation(Quaternion start, Quaternion end, ...)`

### 关键细节

- 支持 `CancellationToken`
- 支持 `ignoreTimeScale`
- 支持 `IProgress<float>`
- Editor 非 Play 模式下通过 `EditorApplication.timeSinceStartup` 计算 deltaTime

## `TweenExtensions`

### 作用

给 `UniActionChain` 增加常用动画扩展。

### 主要扩展方法

- `MoveTo(...)`
- `LocalMoveTo(...)`
- `ScaleTo(...)`
- `RotateTo(...)`
- `FadeTo(CanvasGroup ...)`
- `FadeTo(Graphic ...)`
- `ColorTo(...)`
- `ValueTo(...)`

## `AxisFlags`

### 作用

配置式动作里用于控制哪些轴参与变化。

### 枚举值

- `None`
- `X`
- `Y`
- `Z`
- `All`

## `IActionStrategy`

### 作用

配置式动作节点的统一执行接口。

### 方法

- `Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse, IProgress<float> progress = null)`

## `IFastForwardable`

### 作用

为配置式动作提供“快进到目标状态”的能力。

### 方法

- `FastForward(GameObject target, ActionNodeData data)`

## `ActionNodeEvent`

### 作用

对 `ActionNodeData` 的生命周期事件做 UnityEvent 封装。

## `ActionNodeData`

### 作用

描述一个动作节点。

### 核心字段

- `NodeName`
- `IsExpanded`
- `EditorPosition`
- `Children`
- `Strategy`
- `TargetPath`
- `ComponentName`
- `PropertyName`
- `AxisControl`
- `TargetVector`
- `TargetColor`
- `TargetFloat`
- `TargetBool`
- `Duration`
- `Delay`
- `Ease`
- `OnStartEvent`
- `OnUpdateEvent`
- `OnCompleteEvent`
- `OnStart`
- `OnUpdate`
- `OnComplete`

### 方法

- `InvokeStart()`
- `InvokeUpdate(float p)`
- `InvokeComplete()`

## 内置策略

### GameObject 策略

- `GameObjectActiveStrategy`

### Transform 策略

- `LocalMoveStrategy`
- `LocalRotateStrategy`
- `ScaleStrategy`

### UI 策略

- `CanvasFadeStrategy`
- `ImageColorStrategy`

这些策略都可以：

- 正放执行
- 倒放执行
- 快进到目标状态

## `ObjectSnapshot`

### 作用

记录一个对象在动作执行前的关键状态。

### 字段

- `IsActive`
- `LocalPosition`
- `LocalRotation`
- `LocalScale`
- `HasCanvasGroup`
- `CanvasGroupAlpha`
- `HasImage`
- `ImageColor`

### 方法

- 构造函数：抓取快照
- `Restore(GameObject target)`：恢复状态

## `ActionEngineRunner`

### 作用

配置式动作运行器。

### 核心字段

- `_rootSnapshots`
  以根对象为 key 的快照缓存表。

### 关键方法

#### `InitSnapshot(rootTarget, asset, forceOverwrite = false)`

为整棵动作树收集目标对象快照。

#### `RestoreSnapshot(rootTarget)`

把根对象对应的所有目标恢复到初始状态。

#### `Play(rootTarget, asset, isReverse, token)`

配置式动作主入口。

执行顺序：

1. 保证快照存在
2. 先恢复到初始状态
3. 先整棵树快进，固化所有节点的起点和终点
4. 正放时再次恢复到起点，再逐节点执行
5. 倒放时直接从终态开始反向执行

#### `FastForwardTree(...)`

递归对整棵树做快进。

#### `RunNode(...)`

递归执行节点。

特点：

- 正放时：先执行当前节点，再并行执行子节点
- 倒放时：先倒放子节点，再倒放当前节点

#### `ResolveTarget(...)`

按 `TargetPath` 在根对象下查找目标对象。

#### `ClearSnapshot(...)`

在对象销毁后清理快照缓存。

## `ActionEngineAsset`

### 作用

配置式动作资产。

### 字段

- `TargetPrefab`
- `RootNode`

## `ActionPlayer`

### 作用

场景中的配置式动作桥接组件。

### 字段

- `_actionAsset`
- `_playOnAwake`
- `_resetOnDisable`
- `_playbackCts`

### 生命周期函数

#### `Awake()`

若有资产，则初始化快照。

#### `OnEnable()`

若启用 `playOnAwake`，则自动正放。

#### `OnDisable()`

- 停止当前播放
- 按配置恢复快照

#### `OnDestroy()`

- 停止播放
- 清理快照缓存

### 关键方法

- `PlayForward()`
- `PlayReverse()`
- `PlayInternal(bool isReverse)`
- `Stop()`
- `ResetToStart()`
- `SetAssetAndRefresh(newAsset)`

## 设计约束

- `UniActionChain` 依赖对象池复用，回收后禁止继续使用
- 配置式动作的目标对象必须满足策略所需组件
- 倒放依赖快照和快进逻辑，不能跳过初始化步骤

## 常见误用

- 回收后的动作链继续追加步骤
- 配置式动作忘记初始化快照
- UI 淡入目标缺失 `CanvasGroup`
- 目标对象销毁后仍未传入取消令牌

## 测试建议

- 动作链顺序执行
- 取消链路
- 池化复用
- 配置式动作正放 / 倒放
- 快照恢复
- 各内置策略对 Transform / UI 的效果
