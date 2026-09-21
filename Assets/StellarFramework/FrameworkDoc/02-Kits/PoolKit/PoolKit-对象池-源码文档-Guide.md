# PoolKit / 对象池源码文档

## 模块职责

`PoolKit` 提供运行时对象复用能力，目标是减少频繁 `new` 带来的分配和回收成本。

当前实现重点解决两类问题：

- 为每个类型提供独立静态对象池
- 为池化对象提供统一生命周期回调：分配时 `OnAllocated()`，回收时 `OnRecycled()`

## 源码文件

- `Runtime/Kits/PoolKit/PoolKit.cs`
  对外静态入口，负责按泛型类型路由到对应对象池。
- `Runtime/Kits/PoolKit/FactoryObjectPool.cs`
  通用工厂对象池实现。
- `Runtime/Kits/PoolKit/IPoolable.cs`
  池化对象生命周期接口。

## 总体结构

```text
PoolKit
└─ StaticPool<T>
   └─ FactoryObjectPool<T>
      ├─ _pool
      ├─ _factoryMethod
      ├─ _allocateMethod
      ├─ _recycleMethod
      └─ _destroyMethod

IPoolable
├─ OnAllocated()
└─ OnRecycled()
```

## 调用链

### 分配

1. 业务调用 `PoolKit.Allocate<T>()`
2. 进入 `StaticPool<T>.Pool`
3. `FactoryObjectPool<T>.Allocate()` 从栈中弹出对象或通过工厂创建
4. 若对象实现 `IPoolable`，调用 `OnAllocated()`
5. 返回对象给业务层

### 回收

1. 业务调用 `PoolKit.Recycle<T>(obj)`
2. 框架校验对象不为空，且运行时真实类型必须等于泛型类型
3. 进入 `FactoryObjectPool<T>.Recycle(obj)`
4. 若对象实现 `IPoolable`，调用 `OnRecycled()`
5. 对象重新压回池栈

## 类型详解

## `IPoolable`

### 作用

定义池化对象在进出池时的生命周期回调。

### 方法

- `OnAllocated()`
  对象从池中取出时调用。
- `OnRecycled()`
  对象回收到池中时调用。

### 用途

常用于重置字段、清空引用、复位状态。

## `FactoryObjectPool<T>`

### 作用

通用对象池实现，管理某一种泛型对象的缓存栈。

### 字段

- `_pool : Stack<T>`
  实际缓存容器。
- `_factoryMethod : Func<T>`
  创建新对象的工厂方法。
- `_allocateMethod : Action<T>`
  分配时回调。
- `_recycleMethod : Action<T>`
  回收时回调。
- `_destroyMethod : Action<T>`
  池满时的销毁回调。
- `_maxCount : int`
  池容量上限。
- `_checkSet : HashSet<T>`
  仅开发期存在，用于检测重复回收。

### 构造参数

- `factoryMethod`
  必填，决定对象创建方式。
- `allocateMethod`
  可选，分配后执行。
- `recycleMethod`
  可选，回收前执行。
- `destroyMethod`
  可选，池满时执行。
- `maxCount`
  最大缓存数量，默认 50。

### 方法

#### `Allocate()`

职责：

- 若池中有对象则弹出
- 否则调用工厂方法创建
- 开发期从 `_checkSet` 中移除
- 执行 `_allocateMethod`

#### `Recycle(T item)`

职责：

- 拒绝回收空对象
- 若池已满，则走 `_destroyMethod`
- 开发期拦截双重回收
- 执行 `_recycleMethod`
- 压栈缓存

返回值：

- `true` 表示成功进入池
- `false` 表示未进入池，常见于池满或空对象

#### `Clear()`

职责：

- 清空池中所有对象
- 对每个对象执行 `_destroyMethod`
- 开发期清理 `_checkSet`

## `PoolKit`

### 作用

框架对外的静态对象池入口。

### 内部结构

#### `StaticPool<T>`

为每个泛型类型维护独立的静态池实例：

- `Pool : FactoryObjectPool<T>`

创建逻辑固定为：

- `factoryMethod: () => new T()`
- 若实现 `IPoolable`，在分配时执行 `OnAllocated()`
- 若实现 `IPoolable`，在回收时执行 `OnRecycled()`
- `maxCount: 500`

### 方法

#### `Allocate<T>() where T : new()`

从 `StaticPool<T>.Pool` 分配对象。

#### `Recycle<T>(T obj) where T : new()`

强类型回收入口。

约束：

- `obj != null`
- `obj.GetType() == typeof(T)`

设计目的：

显式禁止“按父类或错误泛型类型回收子类实例”，避免错误对象进入错误对象池。

#### `Recycle(object obj)`

弱类型回收入口，当前已显式禁用。

设计目的：

- 禁止运行时通过 `object` 反射式回收
- 强制业务层使用显式泛型回收

## 设计约束

- `PoolKit` 只支持无参构造类型
- 强制按真实类型回收，禁止弱类型回收
- 开发期对双重回收做断言保护
- 对象状态清理由 `IPoolable` 负责，不由对象池猜测

## 常见误用

- 回收 `null`
- 以父类泛型回收子类实例
- 依赖 `Recycle(object)` 做动态回收
- 在 `OnRecycled()` 中未清空引用，导致脏状态复用

## 适用场景

- 高频创建的纯 C# 小对象
- Loader、事件令牌、运行时中间对象
- 短生命周期工具对象

## 测试建议

建议至少覆盖：

- `Allocate -> Recycle -> Allocate` 复用链路
- 双重回收保护
- 池满时对象销毁分支
- `IPoolable` 生命周期回调顺序
