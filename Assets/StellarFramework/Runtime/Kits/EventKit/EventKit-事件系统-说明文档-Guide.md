# EventKit / 事件系统说明文档

## 模块定位

`EventKit` 提供轻量级全局事件机制，用来做运行时广播解耦。

当前支持两种模式：

- 枚举 key 事件
- 类型事件

它适合做消息广播，不适合替代状态管理。

## 模块组成

- `GlobalEnumEvent`
- `GlobalTypeEvent`
- `IUnRegister`
- 生命周期解绑辅助接口和触发器

## 两种事件模型

### 枚举事件

适合：

- 固定事件表
- 事件键比较明确的全局广播

示例：

```csharp
public enum GameEvent
{
    BattleStarted
}

GlobalEnumEvent.Register(GameEvent.BattleStarted, OnBattleStarted)
    .UnRegisterWhenGameObjectDestroyed(gameObject);
```

### 类型事件

适合：

- 事件数据结构清晰
- 希望直接通过类型表达参数

示例：

```csharp
public struct PlayerLevelUpEvent : ITypeEvent
{
    public int Level;
}

GlobalTypeEvent.Register<PlayerLevelUpEvent>(OnLevelUp);
GlobalTypeEvent.Broadcast(new PlayerLevelUpEvent { Level = 10 });
```

## 生命周期解绑

推荐注册后立刻绑定：

- `UnRegisterWhenGameObjectDestroyed(...)`
- `UnRegisterWhenDisabled(...)`

这样可以避免：

- 场景切换后残留监听
- 对象销毁后回调继续触发

## ToolsHub 关联

- `EventKit 链路追踪`
  查看事件注册、广播和生命周期解绑问题

## 使用约束

- 不要把 EventKit 当作状态容器
- 同一个枚举 key 不要混用不同签名的 delegate
- 事件广播应尽量保持简单、可追踪

## 常见问题

- 事件重复触发
  通常是重复注册。
- 场景切换后还有回调
  注册后要绑定对象生命周期。
- 参数类型不匹配
  同一枚举 key 不要混用不同委托签名。

## 相关文档

- [EventKit 源码文档](EventKit-事件系统-源码文档-Guide.md)
