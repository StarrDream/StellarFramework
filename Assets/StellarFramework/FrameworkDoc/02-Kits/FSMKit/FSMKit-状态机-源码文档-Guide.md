# FSMKit / 状态机源码文档

## 模块职责

`FSMKit` 提供轻量有限状态机实现，用于管理一个持有者对象在多个状态之间的切换。

它的目标是：

- 保持纯 C#、低依赖
- 明确状态进入、更新、退出生命周期
- 拦截非法切换和切换重入

## 源码文件

- `Runtime/Kits/FSMKit/FSMKit.cs`

## 总体结构

```text
FSM<T>
├─ Owner
├─ CurrentState
├─ PreviousState
└─ _stateCache

FSMState<T>
├─ FSM
├─ Owner
├─ StateStartTime
└─ 生命周期方法

IPayloadState<TPayload>
└─ OnEnter(TPayload payload)
```

## 调用链

### 初始化

1. 业务创建 `FSM<T>(owner)`
2. 调用 `AddState(...)` 注册状态
3. 调用 `ChangeState<TState>()` 或 `ChangeState<TState, TPayload>(payload)`

### 切换状态

1. `TryPrepareChangeState(...)` 校验状态机可用性
2. 校验目标状态已注册
3. 设置 `_isTransitioning = true`
4. `ExecuteStateChange(newState)` 执行旧状态退出和当前状态替换
5. 刷新 `StateStartTime`
6. 调用新状态 `OnEnter()` 或 `OnEnter(payload)`
7. 切换结束后恢复 `_isTransitioning = false`

## 类型详解

## `IPayloadState<TPayload>`

### 作用

为支持“带参数进入”的状态定义统一接口。

### 方法

- `OnEnter(TPayload payload)`

### 使用方式

当状态类同时继承 `FSMState<T>` 并实现 `IPayloadState<TPayload>` 时，可通过：

`ChangeState<TState, TPayload>(payload)`

进入该状态。

## `FSMState<T>`

### 作用

定义状态对象的基础行为和生命周期。

### 字段

- `FSM : FSM<T>`
  所属状态机。
- `Owner : T`
  状态机持有者。
- `StateStartTime : float`
  当前状态进入时间。

### 属性

- `Duration`
  当前状态已持续时间，等于 `Time.time - StateStartTime`。

### 方法

- `OnInit(FSM<T> fsm, T owner)`
  注册到状态机时调用。
- `InternalRecordStartTime()`
  内部方法，记录进入状态时间。
- `OnEnter()`
- `OnUpdate()`
- `OnFixedUpdate()`
- `OnExit()`
- `OnGUI()`

### 设计意图

把进入时刻记录逻辑封装到基类，避免子类漏记。

## `FSM<T>`

### 作用

状态机核心实现，负责状态缓存、状态切换和状态驱动。

### 字段 / 属性

- `Owner`
  状态机持有者。
- `CurrentState`
  当前状态。
- `PreviousState`
  上一个状态。
- `_stateCache : Dictionary<Type, FSMState<T>>`
  已注册状态缓存。
- `_isTransitioning : bool`
  当前是否处于切换中。
- `_isCleared : bool`
  是否已被清理。
- `CurrentStateName`
  当前状态名，用于日志输出。

### 方法

#### `FSM(T owner)`

构造函数。

约束：

- `owner` 不能为空

若为空，会把 `_isCleared` 置为 `true`，后续 API 都会被拒绝。

#### `AddState(FSMState<T> state)`

注册状态实例。

职责：

- 拒绝空状态
- 拒绝重复注册
- 调用 `state.OnInit(this, Owner)`
- 写入 `_stateCache`

#### `AddState<TState>()`

自动创建并注册状态实例。

约束：

- `TState : FSMState<T>, new()`

#### `ChangeState<TState>()`

无参切换状态。

职责：

- 校验目标状态存在
- 退出旧状态
- 刷新 `PreviousState / CurrentState`
- 调用 `CurrentState.OnEnter()`

#### `ChangeState<TState, TPayload>(TPayload payload)`

带参切换状态。

职责：

- 与无参切换相同
- 最后调用 `IPayloadState<TPayload>.OnEnter(payload)`

#### `RevertToPreviousState()`

回退到上一个状态。

约束：

- `PreviousState` 必须存在
- `PreviousState` 不能等于 `CurrentState`
- 不能在重入切换中调用

#### `OnUpdate() / OnFixedUpdate() / OnGUI()`

驱动当前状态对应生命周期。

#### `Clear()`

清空状态机。

副作用：

- 清空状态缓存
- 清空当前状态和上一个状态
- 清空 `Owner`
- 设置 `_isCleared = true`

### 内部方法

#### `TryPrepareChangeState(...)`

状态切换前的统一校验入口。

校验内容：

- 状态机未被清理
- `Owner` 不为空
- 目标状态类型不为空
- 当前不处于状态切换重入
- 目标状态已注册
- 目标状态不是当前状态

#### `ExecuteStateChange(...)`

真正执行旧状态退出、新状态切换和起始时间刷新。

#### `EnsureUsable(...)`

统一校验状态机当前是否可用。

## 设计约束

- `FSM<T>` 不负责自动驱动，外部必须主动调用 `OnUpdate()` 等方法
- 状态切换严格防重入
- 未注册状态不能切换
- 清理后状态机不可继续复用

## 常见误用

- 忘记先 `AddState` 就调用 `ChangeState`
- 在 `OnEnter` / `OnExit` 中再次触发非法重入切换
- `Clear()` 后继续使用旧状态机

## 测试建议

建议至少覆盖：

- 状态注册与重复注册
- 无参切换
- 带参切换
- 回退上一个状态
- 未注册状态切换
- 清理后 API 拒绝访问
