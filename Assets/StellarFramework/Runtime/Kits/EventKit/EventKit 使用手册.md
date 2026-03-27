# EventKit 使用手册

## 1. 设计理念 (Why)
Unity 传统的 C# 事件 (`event Action`) 存在耦合度较高的问题。而常见的基于 `string` 或 `Type` 作为 Key 的消息中心，通常存在装箱 (Boxing) 和字典查找开销。

**EventKit 的特性：**
*   **减少装箱**：使用泛型静态类 `EventBox<T>` 隔离不同枚举类型的存储，避免将 `enum` 转换为 `object` 产生的 GC。
*   **高效调用**：结构体事件 (`GlobalTypeEvent`) 直接使用静态委托链，减少字典查找步骤。
*   **生命周期绑定**：提供 `UnRegisterWhenGameObjectDestroyed` 与 `UnRegisterWhenDisabled` 扩展，规范注销流程，减少 MissingReferenceException 的发生。

---

## 2. 使用指南 (How)

### 2.1 枚举事件 (GlobalEnumEvent)
适合简单的逻辑状态通知。

**定义枚举：**
```csharp
public enum GameEvent
{
    GameStart,
    PlayerDead,
    ScoreChanged
}
```

**注册监听 (Receiver)：**
```csharp
void Start()
{
    // 绑定到 OnDestroy (物体销毁时注销)
    // 向下兼容传入 gameObject，也支持直接传入 this (MonoBehaviour)
    GlobalEnumEvent.Register(GameEvent.GameStart, OnGameStart)
        .UnRegisterWhenGameObjectDestroyed(this); 
}

void OnEnable()
{
    // 绑定到 OnDisable (物体失活时注销，适合频繁显隐的 UI 面板)
    // 限制只能传入 MonoBehaviour，通常直接传 this 即可
    GlobalEnumEvent.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged)
        .UnRegisterWhenDisabled(this); 
}

void OnGameStart() { ... }
void OnScoreChanged(int newScore) { ... }
```

> ⚠️ **强制规范 (极度重要)**：
> 使用 `UnRegisterWhenDisabled` 时，**必须且只能通过 `gameObject.SetActive(false)` 来隐藏/失活物体**。
> 严禁使用 `this.enabled = false` 来伪装失活！因为 `enabled = false` 仅会停止当前脚本的 Update，GameObject 本身依然存活，底层的失活触发器将无法感知，从而导致严重的事件泄漏！

**发送事件 (Sender)：**
```csharp
// 广播
GlobalEnumEvent.Broadcast(GameEvent.GameStart);
GlobalEnumEvent.Broadcast(GameEvent.ScoreChanged, 100);
```

### 2.2 结构体事件 (GlobalTypeEvent)
当需要传递复杂参数，或者希望通过类型来严格区分事件时使用。

**定义结构体：**
```csharp
// 必须实现 ITypeEvent 接口
public struct PlayerDamageEvent : ITypeEvent
{
    public int Damage;
    public string AttackerName;
    public Vector3 HitPoint;
}
```

**使用：**
```csharp
// 注册
GlobalTypeEvent.Register<PlayerDamageEvent>(evt => {
    Debug.Log($"收到伤害: {evt.Damage} 来自 {evt.AttackerName}");
}).UnRegisterWhenGameObjectDestroyed(this);

// 发送
GlobalTypeEvent.Broadcast(new PlayerDamageEvent { 
    Damage = 99, 
    AttackerName = "Boss",
    HitPoint = transform.position
});
```

---

## 3. 常见问题 (Pitfalls)

### Q1: 为什么我收不到事件？
*   **类型匹配**：`Broadcast` 的参数数量和类型必须与 `Register` 完全一致。
    *   `Broadcast<T, int>` **无法**触发 `Register<T, float>`。
*   **注册时机**：确保 `Register` 的代码在 `Broadcast` 之前已经执行。

### Q2: 报错 "MissingReferenceException"
*   **原因**：注册了事件，但是对象销毁时没有注销。当事件触发时，委托试图调用一个已经销毁的物体的方法。
*   **解决**：建议在注册后链式调用 `.UnRegisterWhenGameObjectDestroyed(this)`。

### Q3: 运行时事件链路追踪
在 Editor 环境下，可通过 `StellarFramework -> Tools Hub -> EventKit 链路追踪` 面板，实时监控当前内存中活跃的事件与监听者，辅助排查事件泄漏问题。