# EventKit 使用手册

## 1. 设计理念 (Why)
Unity 传统的 C# 事件 (`event Action`) 或 `UnityEvent` 存在耦合度高的问题。而市面上常见的“消息中心”通常使用 `string` 或 `Type` 作为 Key，存在以下严重问题：
*   **装箱 (Boxing)**：使用 `enum` 作为 Key 时，存入 `Dictionary<object, ...>` 会产生 GC。
*   **查找开销**：字典查找虽然快，但在高频事件下仍有开销。
*   **忘记注销**：这是导致空引用异常 (MissingReferenceException) 的头号杀手。

**EventKit 的特性：**
*   **零装箱 (Zero Boxing)**：使用泛型静态类 `EventBox<T>` 物理隔离不同枚举类型的存储。
*   **泛型隔离**：不同的 Enum 类型拥有独立的存储空间，`Dictionary` 的 Key 就是枚举本身，完全避免了 `object` 转换。
*   **极速调用**：结构体事件 (`GlobalStructEvent`) 直接使用静态委托链，速度接近原生调用。

---

## 2. 核心架构 (Under the hood)

### 2.1 泛型静态类
代码中定义了 `private static class EventBox<T>`。
当你调用 `GlobalEnumEvent.Register<GameEvent>(...)` 时，编译器会生成一个名为 `EventBox<GameEvent>` 的类。
当你调用 `GlobalEnumEvent.Register<UIEvent>(...)` 时，编译器会生成另一个名为 `EventBox<UIEvent>` 的类。
**结果**：不同的枚举类型，数据存储在完全不同的内存区域，互不干扰，且不需要任何类型转换。

### 2.2 结构体事件 (TypeEvent)
对于 `GlobalTypeEvent<T>`，底层直接是一个 `public static Action<T> Subscribers;`。
发送事件就是直接调用这个委托，没有任何字典查找，性能达到极致。

---

## 3. 使用指南 (How)

### 3.1 枚举事件 (GlobalEnumEvent)
适合简单的逻辑状态通知，不需要传递复杂参数。

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
    // 1. 注册无参事件
    GlobalEnumEvent.Register(GameEvent.GameStart, OnGameStart)
        .UnRegisterWhenGameObjectDestroyed(gameObject); // 强烈建议加上这句

    // 2. 注册带参事件 (支持最多3个参数)
    GlobalEnumEvent.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged)
        .UnRegisterWhenGameObjectDestroyed(gameObject);
}

void OnGameStart() { ... }
void OnScoreChanged(int newScore) { ... }
```

**发送事件 (Sender)：**
```csharp
// 广播
GlobalEnumEvent.Broadcast(GameEvent.GameStart);
GlobalEnumEvent.Broadcast(GameEvent.ScoreChanged, 100);
```

### 3.2 结构体事件 (GlobalStructEvent)
当需要传递很多参数，或者希望通过类型来区分事件时使用。这是**性能最高**的方式。

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
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 发送
GlobalTypeEvent.Broadcast(new PlayerDamageEvent { 
    Damage = 99, 
    AttackerName = "Boss",
    HitPoint = transform.position
});
```

---

## 4. 常见坑点 (Pitfalls)

### Q1: 为什么我收不到事件？
*   **类型匹配**：`Broadcast` 的参数数量和类型必须与 `Register` 完全一致。
    *   `Broadcast<T, int>` **无法**触发 `Register<T, float>`。
    *   `Broadcast<T>` **无法**触发 `Register<T, int>`。
*   **注册时机**：确保 `Register` 的代码已经执行了（例如检查 `Start` 是否运行）。

### Q2: 报错 "MissingReferenceException"
*   **原因**：你注册了事件，但是对象销毁时没有注销。当事件触发时，委托试图调用一个已经销毁的物体的方法。
*   **解决**：务必在注册后面跟上 `.UnRegisterWhenGameObjectDestroyed(gameObject)`。

### Q3: 枚举重名怎么办？
*   由于使用了泛型隔离，`GameEvent.Start` 和 `UIEvent.Start` 是两个完全不同的 Key，不会冲突。