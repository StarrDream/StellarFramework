# PoolKit / 对象池说明文档

## 模块定位

`PoolKit` 提供纯 C# 对象池，适合：

- 短生命周期对象
- 事件令牌
- 动作链实例
- 临时数据对象

它不是 `GameObject` 池。

## 模块组成

- `PoolKit`
  统一静态入口
- `IPoolable`
  池化生命周期接口
- `FactoryObjectPool<T>`
  通用对象池实现

## 基本用法

```csharp
public sealed class DamageEvent : IPoolable
{
    public int Value;

    public void OnAllocated()
    {
    }

    public void OnRecycled()
    {
        Value = 0;
    }
}

DamageEvent e = PoolKit.Allocate<DamageEvent>();
e.Value = 10;
PoolKit.Recycle(e);
```

## 使用规则

- 池化对象尽量实现 `IPoolable`
- 在 `OnRecycled()` 里清空内部状态
- 回收后不要继续持有对象引用
- 强类型回收优先，不要依赖弱类型回收

## 适用场景

- 高频 `new` 的小对象
- 框架内部中间对象
- 临时结构体包装对象

## 不适用场景

- `GameObject` 实例池
- 复杂层级管理对象池

## 常见问题

- 回收后还在用
  回收后对象状态不再可靠。
- 状态污染
  在 `OnRecycled()` 中清理字段。
- 想池化 GameObject
  使用专门的对象池方案，不要直接拿 `PoolKit` 处理。

## 相关文档

- [PoolKit 源码文档](PoolKit-对象池-源码文档-Guide.md)
