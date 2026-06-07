# EventKit / 事件系统源码文档

## 模块职责

`EventKit` 提供两套全局事件机制：

- `GlobalTypeEvent`
  按事件类型广播
- `GlobalEnumEvent`
  按枚举键广播

同时提供统一的注销接口和与 Unity 生命周期绑定的自动注销机制。

## 源码文件

- `Runtime/Kits/EventKit/EventCore.cs`
  注销接口、通用注销对象、生命周期触发器。
- `Runtime/Kits/EventKit/GlobalTypeEvent.cs`
  类型事件实现。
- `Runtime/Kits/EventKit/GlobalEnumEvent.cs`
  枚举事件实现。

## 总体结构

```text
IUnRegister
├─ UnRegister()
├─ UnRegisterWhenGameObjectDestroyed(...)
└─ UnRegisterWhenDisabled(...)

CustomUnRegister
├─ 包装注销动作
└─ 绑定 Unity 生命周期

GlobalTypeEvent
└─ EventBox<T>
   ├─ Subscribers
   ├─ CallbackSet
   └─ EventToken

GlobalEnumEvent
└─ EventBox<TEnum>
   ├─ EventTable
   ├─ LookupTable
   ├─ DelegateTypeByKey
   └─ EnumEventToken<TEnum>
```

## 调用链

### 注册

1. 业务调用 `Register(...)`
2. 框架校验回调不为空
3. 检查是否重复注册
4. 写入事件表
5. 返回 `IUnRegister`
6. 业务可继续把该令牌绑定到 `GameObject` 生命周期

### 广播

1. 业务调用 `Broadcast(...)`
2. 框架查找目标事件表
3. 校验回调签名匹配
4. 依次调用监听者

### 注销

1. 业务主动调用 `UnRegister()`
2. 或由 `OnDestroy / OnDisable` 触发器自动调用
3. 从事件表中移除对应回调
4. 回收事件令牌

## 类型详解

## `IUnRegister`

### 作用

统一所有事件注销令牌的最小接口。

### 方法

- `UnRegister()`
- `UnRegisterWhenGameObjectDestroyed(GameObject gameObject)`
- `UnRegisterWhenGameObjectDestroyed(MonoBehaviour mono)`
- `UnRegisterWhenDisabled(MonoBehaviour mono)`

## `CustomUnRegister`

### 作用

用委托包装注销行为，并提供生命周期绑定能力。

### 字段

- `_onUnRegister : Action`
  实际注销动作。
- `_isUnregistered : bool`
  是否已经注销，防止重复执行。

### 方法

- `UnRegister()`
  执行一次性注销。
- `UnRegisterWhenGameObjectDestroyed(...)`
  绑定 `OnDestroy`。
- `UnRegisterWhenDisabled(...)`
  绑定 `OnDisable`。
- `TryAttachDestroyTrigger(...)`
  尝试挂载销毁触发器。
- `TryAttachDisableTrigger(...)`
  尝试挂载失活触发器。

## `EventUnregisterTrigger`

### 作用

挂在 `GameObject` 上，负责在 `OnDestroy()` 时批量注销。

### 字段

- `_unRegisters : HashSet<IUnRegister>`

### 方法

- `Add(IUnRegister unRegister)`
- `OnDestroy()`

## `EventUnregisterOnDisableTrigger`

### 作用

挂在 `GameObject` 上，负责在 `OnDisable()` 时批量注销。

### 字段

- `_unRegisters : HashSet<IUnRegister>`

### 方法

- `Add(IUnRegister unRegister)`
- `OnDisable()`

## `ITypeEvent`

### 作用

类型事件的标记接口。

任何用于 `GlobalTypeEvent` 的事件类型都必须实现它。

## `GlobalTypeEvent`

### 作用

基于事件类型的全局广播系统。

### 方法

- `Register<T>(Action<T> onEvent)`
- `Broadcast<T>(T e)`
- `Broadcast<T>() where T : new()`

### 内部结构

#### `EventBox<T>`

每个事件类型一份静态事件盒。

字段：

- `Subscribers : Action<T>`
- `TokenPool : Stack<EventToken>`
- `CallbackSet : HashSet<Delegate>`

职责：

- 保存订阅者
- 检测重复注册
- 回收注销令牌

#### `EventToken`

类型事件的注销令牌实现。

字段：

- `Handler`
- `IsRecycled`
- `IsRegistered`

方法：

- `UnRegister()`
- `UnRegisterWhenGameObjectDestroyed(...)`
- `UnRegisterWhenDisabled(...)`

## `GlobalEnumEvent`

### 作用

基于枚举键的全局广播系统，支持不同签名的委托。

### 内部结构

#### `CallbackKey<T>`

由 `事件键 + 委托实例` 组成的联合键，用于定位具体注册项。

#### `EventBox<T>`

每个枚举类型一份静态事件盒。

字段：

- `EventTable : Dictionary<T, Delegate>`
- `TokenPool : Stack<EnumEventToken<T>>`
- `LookupTable : Dictionary<CallbackKey<T>, List<EnumEventToken<T>>>`
- `DelegateTypeByKey : Dictionary<T, Type>`

#### `EnumEventToken<T>`

枚举事件的注销令牌。

字段：

- `Key`
- `Callback`
- `IsInUse`
- `IsRegistered`

### 方法

- `Register<T>(T key, Action callback)`
- `Register<T, T1>(...)`
- `Register<T, T1, T2>(...)`
- `Register<T, T1, T2, T3>(...)`
- `UnRegister(...)`
- `Broadcast(...)`
- `ClearAll<T>()`

### 关键内部方法

- `EnsureDelegateTypeMatches(...)`
  同一 key 下强制委托签名一致。
- `ContainsRegistration(...)`
  检测重复注册。
- `AddToLookup(...) / RemoveFromLookup(...)`
  维护回调索引。
- `AddToEventTable(...) / RemoveFromEventTable(...)`
  维护事件表。
- `TryGetDelegate(...)`
  广播前进行委托签名校验。

## 设计约束

- `GlobalTypeEvent` 不允许重复注册同一回调
- `GlobalTypeEvent.UnRegister<T>()` 已被禁用，防止粗暴清空整个类型事件
- `GlobalEnumEvent` 要求同一枚举 key 下委托签名一致
- 生命周期绑定依赖 `GameObject` 所在场景有效

## 常见误用

- 回调为空仍尝试注册
- 同一回调重复注册
- 使用不一致的委托签名注册到同一个枚举 key
- 把生命周期绑定到无效对象或未进入场景的对象

## 测试建议

建议至少覆盖：

- 重复注册拦截
- 主动注销
- `OnDestroy` 自动注销
- `OnDisable` 自动注销
- 枚举 key 委托签名校验
- `Broadcast` 类型不匹配保护
