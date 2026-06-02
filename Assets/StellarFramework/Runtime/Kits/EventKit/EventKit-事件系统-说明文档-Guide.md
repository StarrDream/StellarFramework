# EventKit / 事件系统说明文档

EventKit 提供两类全局事件：枚举 key 事件和类型事件。它适合解耦临时广播，不适合代替所有业务状态。

## 入口 API

- `GlobalEnumEvent.Register(key, callback)`：注册枚举事件。
- `GlobalEnumEvent.Broadcast(key, args...)`：广播枚举事件。
- `GlobalEnumEvent.UnRegister(key, callback)`：手动反注册。
- `GlobalTypeEvent.Register<T>(callback)`：注册结构体/类型事件。
- `GlobalTypeEvent.Broadcast(eventData)`：广播类型事件。
- `IUnRegister`：注册返回的反注册句柄。
- `UnRegisterWhenGameObjectDestroyed(...)` / `UnRegisterWhenDisabled(...)`：生命周期绑定。

## 使用模板

```csharp
public enum GameEvent
{
    BattleStarted
}

private void OnEnable()
{
    GlobalEnumEvent.Register(GameEvent.BattleStarted, OnBattleStarted)
        .UnRegisterWhenGameObjectDestroyed(gameObject);
}

private void OnBattleStarted()
{
}
```

类型事件：

```csharp
public struct PlayerLevelUpEvent : ITypeEvent
{
    public int Level;
}

GlobalTypeEvent.Register<PlayerLevelUpEvent>(OnLevelUp);
GlobalTypeEvent.Broadcast(new PlayerLevelUpEvent { Level = 10 });
```

## ToolsHub 关联

- `EventKit 链路追踪` 用于排查事件注册、广播和生命周期反注册问题。

## 常见问题

- 事件重复触发：检查是否重复注册。
- 场景切换后还有回调：注册后绑定 GameObject 生命周期。
- 参数类型不匹配：同一个枚举 key 不要混用不同 delegate 签名。
