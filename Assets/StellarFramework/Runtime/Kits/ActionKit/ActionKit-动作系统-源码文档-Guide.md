# ActionKit / 动作系统源码文档

## 源码位置

- `Runtime/Kits/ActionKit/ActionKit.cs`：动作链入口和 `UniActionChain`。
- `Runtime/Kits/ActionKit/TweenExtensions.cs`：缓动枚举、插值函数和动作链扩展。
- `Runtime/Kits/ActionKit/ActionEngine`：配置化动作资产、策略和 Runner。

## 核心类型

- `ActionKit`：静态入口，创建 `UniActionChain` 或等待动作。
- `UniActionChain`：可复用动作链，内部持有异步步骤并实现 `IPoolable`。
- `Ease` / `Easing`：缓动类型和计算函数。
- `TweenKit`：底层插值执行器。
- `TweenExtensions`：把移动、缩放、旋转、淡入、颜色变化注册到动作链。
- `ActionEngineAsset`：ScriptableObject 动作配置资产。
- `ActionNodeData`：配置化动作节点数据。
- `IActionStrategy`：配置化动作策略接口。
- `GameObjectActiveStrategy`、`LocalMoveStrategy`、`LocalRotateStrategy`、`ScaleStrategy`、`CanvasFadeStrategy`、`ImageColorStrategy`：内置动作策略。
- `ActionEngineRunner`：解释 `ActionEngineAsset` 并播放。
- `ActionPlayer`：MonoBehaviour 播放组件。

## 关键方法

- `ActionKit.Sequence(...)`：从对象创建动作链。
- `UniActionChain.Play(...)`：按顺序执行链上步骤。
- `TweenKit.To(...)`：根据 duration 和 ease 循环计算插值。
- `ActionEngineRunner.InitSnapshot(...)`：记录目标对象初始状态。
- `ActionEngineRunner.RestoreSnapshot(...)`：恢复记录状态。
- `ActionEngineRunner.Play(...)`：遍历节点并执行策略。
- `IFastForwardable.FastForward(...)`：把策略推进到终态。

## 数据流

代码动作链：业务创建 `UniActionChain`，链上扩展方法追加步骤，`Play` 逐步 await，完成后回收到 PoolKit。

配置化动作：`ActionPlayer` 持有 `ActionEngineAsset`，播放时交给 `ActionEngineRunner`，Runner 读取 `ActionNodeData`，创建或调用对应 `IActionStrategy`，策略操作 Transform、CanvasGroup 或 Graphic。

## 依赖关系

- 依赖 UniTask 执行异步等待和取消。
- 依赖 PoolKit 回收 `UniActionChain`。
- UI 相关策略依赖 Unity UI 类型。
- 不依赖 ResKit；动作资源由业务或样例自行引用。

## 扩展点

- 新增缓动：扩展 `Ease` 和 `Easing.Evaluate`。
- 新增动作链扩展：在 `TweenExtensions` 添加方法并向 `UniActionChain` 追加步骤。
- 新增配置化动作：实现 `IActionStrategy`，必要时实现 `IFastForwardable`。
- 新增编辑器可配置字段：同步更新 `ActionNodeData` 和样例资产。

## 测试入口

- 修改异步动作链后跑相关 ActionKit 样例。
- 修改配置化动作后打开 FrameworkValidation 或 ActionKit Playable 场景。
- 修改 Pool 回收时检查 `UniActionChain` 是否重复使用安全。
