# PoolKit / 对象池源码文档

## 源码位置

- `Runtime/Kits/PoolKit/PoolKit.cs`
- `Runtime/Kits/PoolKit/FactoryObjectPool.cs`
- `Runtime/Kits/PoolKit/IPoolable.cs`

## 核心类型

- `PoolKit`：静态对象池入口。
- `FactoryObjectPool<T>`：泛型对象池，持有栈和工厂委托。
- `IPoolable`：对象分配和回收生命周期接口。

## 关键方法

- `PoolKit.Allocate<T>`：从类型对应池取对象，不存在则 new。
- `PoolKit.Recycle<T>`：调用回收生命周期并放回池。
- `PoolKit.Recycle(object)`：运行时类型回收入口。
- `FactoryObjectPool<T>.Allocate`：从栈取或工厂创建。
- `FactoryObjectPool<T>.Recycle`：执行 reset 并压回栈。

## 数据流

每个类型拥有独立静态池。业务 Allocate 时从池中取对象并执行 `OnAllocate`。Recycle 时执行 `OnRecycle`，清理后进入池。下次 Allocate 复用同一实例。

## 依赖关系

- 纯 C#，不依赖 Unity。
- 被 ActionKit、EventKit、ResKit 等内部短生命周期对象使用。

## 扩展点

- 新增对象池类型：使用 `FactoryObjectPool<T>` 注入 create/reset/destroy。
- 需要统计时，可在 PoolKit 外层加审计，不要污染通用对象池 API。
- 池化对象应实现 `IPoolable` 清理内部引用。

## 测试入口

- 验证 Allocate/Recycle 后对象状态清理。
- 验证重复回收和回收 null 的行为。
