# FSMKit / 状态机

## 1. 简介 (Introduction)
**FSMKit** 是一个专为 Unity 开发设计的泛型有限状态机 (Generic Finite State Machine)。

### 核心特性
*   **低 GC 设计**：状态实例在初始化时创建并缓存。运行时切换状态不会重复创建对象。支持基于泛型接口与结构体的参数传递。
*   **泛型宿主 (Generic Context)**：状态类直接持有具体类型的 `Owner`，编写逻辑时无需进行强制类型转换。
*   **纯 C# 实现**：不依赖 `MonoBehaviour`，可以在任何 C# 类中使用。

---

## 2. 快速入门：怪物 AI 实战 (Quick Start)

### 第一步：定义宿主 (The Context)
宿主是持有状态机的主体。

```csharp
using UnityEngine;
using StellarFramework.FSM;

public class MonsterAI : MonoBehaviour
{
    [Header("参数配置")]
    public Transform playerTarget;
    public float detectRange = 5.0f; 
    public float moveSpeed = 3.0f;

    // 1. 声明状态机
    private FSM<MonsterAI> _fsm;

    private void Start()
    {
        // 2. 初始化状态机，传入 this
        _fsm = new FSM<MonsterAI>(this);
        
        // 3. 注册状态
        _fsm.AddState<PatrolState>();
        _fsm.AddState<ChaseState>();
        
        // 4. 启动初始状态
        _fsm.ChangeState<PatrolState>();
    }

    private void Update()
    {
        // 5. 每帧驱动状态机
        _fsm?.OnUpdate();
    }
    
    private void OnDestroy()
    {
        _fsm?.Clear();
    }

    public float GetDistToPlayer()
    {
        if (playerTarget == null) return 999f;
        return Vector3.Distance(transform.position, playerTarget.position);
    }
}
```

### 第二步：编写状态 (The States)

**状态 1：巡逻 (PatrolState)**
```csharp
// 泛型填 <MonsterAI>，这样 Owner 就是 MonsterAI 类型
public class PatrolState : FSMState<MonsterAI>
{
    public override void OnEnter()
    {
        Debug.Log("[PatrolState] 进入巡逻模式");
    }

    public override void OnUpdate()
    {
        Owner.transform.Rotate(0, 50 * Time.deltaTime, 0);

        if (Owner.GetDistToPlayer() < Owner.detectRange)
        {
            FSM.ChangeState<ChaseState>();
        }
    }
}
```

**状态 2：追逐 (ChaseState)**
```csharp
public class ChaseState : FSMState<MonsterAI>
{
    public override void OnEnter()
    {
        Debug.Log("[ChaseState] 开始追逐！");
    }

    public override void OnUpdate()
    {
        if (Owner.playerTarget != null)
        {
            Vector3 dir = (Owner.playerTarget.position - Owner.transform.position).normalized;
            Owner.transform.position += dir * Owner.moveSpeed * Time.deltaTime;
            Owner.transform.LookAt(Owner.playerTarget);
        }

        // 使用 * 1.2f 作为缓冲，防止在临界点频繁切换状态 (防抖动)
        if (Owner.GetDistToPlayer() > Owner.detectRange * 1.2f)
        {
            FSM.ChangeState<PatrolState>();
        }
    }
}
```

---

## 3. 带参数的状态切换 (Payload)
FSMKit 提供了基于泛型接口 `IPayloadState<T>` 的参数传递机制。建议使用 Struct 传递参数以减少堆内存分配。

**1. 定义载荷结构体**
```csharp
public struct DamagePayload 
{
    public Vector3 HitDirection;
    public int DamageValue;
}
```

**2. 实现带参状态**
```csharp
public class HitState : FSMState<MonsterAI>, IPayloadState<DamagePayload>
{
    private Vector3 _hitDirection;
    private int _damage;

    public void OnEnter(DamagePayload payload)
    {
        _hitDirection = payload.HitDirection;
        _damage = payload.DamageValue;
        Debug.Log($"受到伤害: {_damage}");
    }

    public override void OnUpdate()
    {
        // Duration 属性由基类提供，记录状态持续时间
        if (Duration > 0.5f)
        {
            FSM.RevertToPreviousState();
        }
    }
}
```

**3. 触发带参状态切换**
```csharp
public void TakeDamage(Vector3 dir, int damage)
{
    DamagePayload payload = new DamagePayload 
    { 
        HitDirection = dir, 
        DamageValue = damage 
    };
    _fsm.ChangeState<HitState, DamagePayload>(payload);
}
```

---

## 4. 核心机制与 API 说明

### 生命周期 (Lifecycle)
*   `OnInit(fsm, owner)`: 仅在 `AddState` 时调用一次。
*   `OnEnter()`: 无参切换到该状态时调用。
*   `OnEnter(TPayload)`: 带参切换到该状态时调用（需实现 `IPayloadState`）。
*   `OnUpdate()`: 需在宿主中按需驱动。
*   `OnFixedUpdate()`: 需在宿主中按需驱动。
*   `OnExit()`: 离开该状态时调用。

### 常用 API
*   `FSM.ChangeState<TState>()`: 无参切换。
*   `FSM.ChangeState<TState, TPayload>(TPayload payload)`: 带参切换。
*   `FSM.RevertToPreviousState()`: 返回上一个状态。
*   `State.Duration`: 获取当前状态已持续的秒数。

---

## 5. 注意事项

### 脏数据 (Dirty Data)
**原理**：状态实例是复用的。当重新进入某个状态时，其成员变量保持着上次退出时的值。
**规范**：**必须**在 `OnEnter` 中手动重置所有动态变量（如计时器）。

```csharp
public class AttackState : FSMState<Player>
{
    private float _timer;
    
    public override void OnEnter()
    {
        _timer = 0f; // 每次进入状态时手动重置
    }
}
```

### 必须先注册
调用 `ChangeState` 之前，必须确保目标状态已通过 `AddState` 注册，否则会记录错误日志并中断切换。

---

## 6. 进阶架构：结合孤岛式 Animator (MSV 规范)

在商业化项目中，为了解决 Animator 连线（Transitions）难以维护、容易产生面条式网状结构的问题，我们强制推行 **孤岛式 Animator** 配合 **MSV (Model-Service-View)** 架构。

### 核心理念
*   **孤岛节点**：Animator Controller 中不创建任何连线，只放置独立的 Animation Clip 节点。
*   **逻辑剥离**：FSM (Service 层) 负责决定当前该处于什么状态，绝对不允许直接调用 `GetComponent<Animator>()`。
*   **数据驱动**：FSM 将期望的动画 Hash 和过渡时间写入 Model，View 层监听 Model 变化并调用 `Animator.CrossFade` 实现丝滑切换。

### 实现规范与步骤

**1. 定义动画哈希与载荷（零 GC）**
必须在静态类中缓存 Hash，杜绝运行时字符串分配。
```csharp
public struct AnimRequestPayload
{
    public readonly int StateHash;
    public readonly float TransitionDuration;
    public AnimRequestPayload(int hash, float duration) { StateHash = hash; TransitionDuration = duration; }
}

public static class AnimHashes
{
    public static readonly int Idle = Animator.StringToHash("Idle");
    public static readonly int Run = Animator.StringToHash("Run");
}
```

**2. Model 层分发事件**
Model 仅负责存储数据与分发事件，不依赖 Unity 组件。
```csharp
public class CharacterModel
{
    public event Action<AnimRequestPayload> OnAnimStateChanged;
    
    public void RequestAnimation(AnimRequestPayload payload)
    {
        OnAnimStateChanged?.Invoke(payload);
    }
}
```

**3. View 层执行 CrossFade**
View 层挂载在带有 Animator 的 GameObject 上，监听 Model 事件。
```csharp
[RequireComponent(typeof(Animator))]
public class CharacterView : MonoBehaviour
{
    private Animator _animator;

    public void BindModel(CharacterModel model)
    {
        _animator = GetComponent<Animator>();
        model.OnAnimStateChanged += (payload) => 
        {
            // 使用 CrossFade 实现无连线的丝滑过渡
            _animator.CrossFade(payload.StateHash, payload.TransitionDuration, 0);
        };
    }
}
```

**4. FSM 状态层下发指令**
在具体的 FSM 状态中，通过宿主获取 Model，并下发表现指令。
```csharp
public class IdleState : FSMState<CharacterService>
{
    public override void OnEnter()
    {
        // 0.2f 为混合过渡时间，实现丝滑切换
        AnimRequestPayload payload = new AnimRequestPayload(AnimHashes.Idle, 0.2f);
        Owner.Model.RequestAnimation(payload);
    }
}
```

### 优势总结
通过此架构，FSMKit 彻底与 Unity 表现层解耦。不仅杜绝了 Animator 连线地狱，还使得逻辑代码完全可进行无头（Headless）单元测试，极大地提升了代码的可维护性与复用性。