#  FSMKit 使用手册

## 1. 简介 (Introduction)

**FSMKit** 是一个专为 Unity 开发设计的 **泛型有限状态机 (Generic Finite State Machine)**。
它的核心设计目标是：**高性能**、**零 GC**、**类型安全**。

### 为什么选择 FSMKit？
*   **0GC (Zero Garbage Collection)**：状态实例在初始化时创建并缓存。运行时切换状态不会 `new` 任何对象，彻底杜绝了状态切换导致的内存碎片。支持基于泛型接口与结构体的 0GC 参数传递。
*   **泛型宿主 (Generic Context)**：状态类直接持有具体类型的 `Owner`（如 `MonsterAI`），在写逻辑时不需要进行 `(MonsterAI)owner` 这种强制类型转换。
*   **纯 C# 实现**：不依赖 `MonoBehaviour`，可以在任何 C# 类中使用。

---

## 2. 快速入门：怪物 AI 实战 (Quick Start)

我们将实现一个经典的双向分支逻辑：**巡逻 (Patrol) <--> 追逐 (Chase)**。

### 第一步：定义宿主 (The Context)

宿主是持有状态机的主体。

```csharp
using UnityEngine;
using StellarFramework.FSM;

public class MonsterAI : MonoBehaviour
{
    [Header("参数配置")]
    public Transform playerTarget;
    public float detectRange = 5.0f; // 警戒范围
    public float moveSpeed = 3.0f;

    // 1. 声明状态机
    private FSM<MonsterAI> _fsm;

    private void Start()
    {
        // 2. 初始化状态机，传入 this
        _fsm = new FSM<MonsterAI>(this);

        // 3. 注册状态 (这一步至关重要，把逻辑装入脑子)
        _fsm.AddState<PatrolState>();
        _fsm.AddState<ChaseState>();

        // 4. 启动初始状态
        _fsm.ChangeState<PatrolState>();
    }

    private void Update()
    {
        // 5. 每帧驱动状态机
        _fsm.OnUpdate();
    }

    // --- 供状态调用的公共方法 ---
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
        // 行为：原地旋转模拟巡逻
        Owner.transform.Rotate(0, 50 * Time.deltaTime, 0);

        // 分支判断：发现玩家 -> 切换追逐
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
        Debug.Log("[ChaseState] 发现目标！开始追逐！");
    }

    public override void OnUpdate()
    {
        // 行为：冲向玩家
        if (Owner.playerTarget != null)
        {
            Vector3 dir = (Owner.playerTarget.position - Owner.transform.position).normalized;
            Owner.transform.position += dir * Owner.moveSpeed * Time.deltaTime;
            Owner.transform.LookAt(Owner.playerTarget);
        }

        // 分支判断：玩家跑远了 -> 切回巡逻
        // 技巧：使用 * 1.2f 作为缓冲，防止在临界点反复横跳 (防抖动)
        if (Owner.GetDistToPlayer() > Owner.detectRange * 1.2f)
        {
            FSM.ChangeState<PatrolState>();
        }
    }
}
```

---

## 3. 进阶用法：带参数的状态切换 (0GC Payload)

在实际业务中，切换状态时往往需要传递上下文数据（如受击方向、伤害值）。为了保持 0GC，FSMKit 提供了基于泛型接口 `IPayloadState<T>` 的参数传递机制。

### 最佳实践：使用 Struct 传递多个参数

**1. 定义载荷结构体 (Payload Struct)**
使用 `struct` 而非 `class`，确保参数在栈上分配，避免产生堆内存垃圾 (GC Alloc)。

```csharp
public struct DamagePayload 
{
    public Vector3 HitDirection;
    public int DamageValue;
}
```

**2. 实现带参状态 (Payload State)**
让目标状态实现 `IPayloadState<DamagePayload>` 接口。

```csharp
using UnityEngine;
using StellarFramework.FSM;

public class HitState : FSMState<MonsterAI>, IPayloadState<DamagePayload>
{
    private Vector3 _hitDirection;
    private int _damage;

    // 实现接口方法，接收参数
    public void OnEnter(DamagePayload payload)
    {
        _hitDirection = payload.HitDirection;
        _damage = payload.DamageValue;
        
        Debug.Log($"[HitState] 受到伤害: {_damage}, 击退方向: {_hitDirection}");
        // 执行击退动画与逻辑...
    }

    public override void OnUpdate()
    {
        // 模拟受击硬直结束，返回上一个状态
        if (Duration > 0.5f)
        {
            FSM.RevertToPreviousState();
        }
    }
}
```

**3. 触发带参状态切换**
调用时显式指定状态类型和载荷类型。

```csharp
// 在 MonsterAI 或其他伤害结算逻辑中调用
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
*   `OnInit(fsm, owner)`: **仅执行一次**。在 `AddState` 时调用。用于缓存组件引用。
*   `OnEnter()`: 无参切换到该状态时调用。**用于重置变量、播放动画**。
*   `OnEnter(TPayload)`: 带参切换到该状态时调用（需实现 `IPayloadState`）。
*   `OnUpdate()`: 每一帧调用。处理逻辑和状态跳转。
*   `OnFixedUpdate()`: 物理帧调用。处理刚体受力等物理逻辑。
*   `OnExit()`: 离开该状态时调用。用于清理现场、停止动画。

### 常用 API
*   `FSM.ChangeState<TState>()`: 无参切换到指定类型的状态。
*   `FSM.ChangeState<TState, TPayload>(TPayload payload)`: 带参切换到指定类型的状态。
*   `FSM.RevertToPreviousState()`: 返回上一个状态（常用于受击硬直后恢复之前的状态）。
*   `State.Duration`: 获取当前状态已经持续了多少秒（常用于 "蓄力 3秒后释放"）。

---

## 5. 避坑指南 (Troubleshooting)

### [致命陷阱] 脏数据 (Dirty Data)

这是使用 FSMKit 最容易遇到的 Bug。
**原理**：为了 0GC，状态实例是**复用**的。当你从 `StateA` 切出去再切回来，`StateA` 还是内存里原来那个对象，里面的成员变量**保持着上次退出时的值**。

**错误示范**：
```csharp
public class AttackState : FSMState<Player>
{
    private float _timer = 0; // [错误] 这里的初始化只在游戏启动时生效一次！

    public override void OnUpdate()
    {
        _timer += Time.deltaTime;
        if (_timer > 1.0f) FSM.ChangeState<IdleState>();
    }
}
```
*后果：第二次进入攻击状态时，`_timer` 已经是 1.0 了，会瞬间退出状态。*

**正确做法**：
**必须**在 `OnEnter` 中手动重置所有动态变量。

```csharp
public class AttackState : FSMState<Player>
{
    private float _timer;

    public override void OnEnter()
    {
        _timer = 0f; // [正确] 每次进入状态时手动重置
    }
}
```

### [注意事项] 必须先注册

调用 `ChangeState` 之前，必须确保目标状态已经被 `AddState` 注册过。否则会报错并中断切换。建议在宿主的 `Start` 或 `Awake` 中统一完成所有状态的注册。

### [技巧] 状态防抖 (Hysteresis)

在做距离判断时（如 AI 追逐），进入距离和退出距离不要设为同一个值。
*   进入：`distance < 5.0f`
*   退出：`distance > 6.0f` (5.0 * 1.2)
    这能有效防止怪物在临界距离处频繁切换状态产生抽搐。