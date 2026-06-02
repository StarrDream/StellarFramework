# PoolKit / 对象池说明文档

PoolKit 提供纯 C# 对象池，适合短生命周期对象、事件 token、动作链、临时数据容器等。它不是 GameObject 池。

## 入口 API

- `PoolKit.Allocate<T>() where T : new()`：获取对象。
- `PoolKit.Recycle<T>(obj)`：回收对象。
- `PoolKit.Recycle(object obj)`：按运行时类型回收。
- `IPoolable.OnAllocate()`：对象取出时回调。
- `IPoolable.OnRecycle()`：对象回收时回调。
- `FactoryObjectPool<T>`：可注入创建、重置、销毁逻辑的对象池。

## 使用模板

```csharp
public sealed class DamageEvent : IPoolable
{
    public int Value;

    public void OnAllocate() {}

    public void OnRecycle()
    {
        Value = 0;
    }
}

DamageEvent e = PoolKit.Allocate<DamageEvent>();
e.Value = 10;
PoolKit.Recycle(e);
```

## 常见问题

- 回收后还在使用：对象回收后不要继续持有引用。
- 想池化 GameObject：使用专门 GameObject 池，不要直接套 PoolKit。
- 对象状态污染：实现 `OnRecycle` 清理字段。
