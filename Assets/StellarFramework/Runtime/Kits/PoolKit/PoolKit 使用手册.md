# PoolKit 使用手册

## 1. 架构理念 (Architecture)
`PoolKit` 采用底层统一、上层分治的设计心智：
*   **底层唯一数据结构**：`FactoryObjectPool<T>` 是框架内唯一的池化容器，通过生命周期委托（`factoryMethod`, `allocateMethod`, `recycleMethod`, `destroyMethod`）实现与具体对象类型的彻底解耦。
*   **全局门面 (Facade)**：`PoolKit` 静态类作为纯 C# 对象的全局调度中心，内部利用泛型静态类特性实现 **O(1) 无锁无字典的极速存取**。

---

## 2. 使用指南 (How To Use)

### 2.1 纯 C# 对象池 (网络消息、事件参数)
**适用场景**：高频创建与销毁的纯数据结构。
**规范约束**：强烈建议实现 `IPoolable` 接口，以便在出入池时自动清理脏数据。

**定义数据类：**
```csharp
public class DamageEventData : IPoolable
{
    public int TargetId;
    public float DamageValue;

    public void OnAllocated() 
    {
        TargetId = 0;
        DamageValue = 0f;
    }

    public void OnRecycled() 
    {
        TargetId = 0;
    }
}
```

**业务调用（极简 API）：**
```csharp
// 1. 申请 (O(1) 极速命中静态池)
DamageEventData evt = PoolKit.Allocate<DamageEventData>();
evt.TargetId = 1001;
evt.DamageValue = 99.5f;

// 2. 派发事件...
EventKit.Dispatch(evt);

// 3. 回收 (谁申请，谁回收)
PoolKit.Recycle(evt);
```

### 2.2 Unity 游戏物体池 (子弹、特效、UI)
**适用场景**：依赖 `Instantiate` 和 `Destroy` 的引擎级表现对象。
**规范约束**：严禁将 GameObject 直接塞入 `PoolKit` 门面。必须在对应的业务 Manager 内部实例化 `FactoryObjectPool<T>` 并注入生命周期委托。

**在 Manager 中管理：**
```csharp
public class BulletManager : MonoBehaviour
{
    public Bullet BulletPrefab;
    private FactoryObjectPool<Bullet> _pool;

    void Start() 
    {
        _pool = new FactoryObjectPool<Bullet>(
            factoryMethod: () => Instantiate(BulletPrefab),
            allocateMethod: b => b.gameObject.SetActive(true),
            recycleMethod: b => 
            { 
                b.gameObject.SetActive(false); 
                b.transform.position = Vector3.zero; 
            },
            destroyMethod: b => { if (b != null) Destroy(b.gameObject); }
        );
    }

    public void Fire() 
    {
        Bullet b = _pool.Allocate();
        // 初始化子弹逻辑...
    }
}
```

---

## 3. 防御性编程与常见坑点 (Pitfalls)
1.  **脏数据残留**：纯 C# 对象若不实现 `IPoolable.OnRecycled` 清理内部的 `List` 或引用类型字段，极易导致内存泄漏或逻辑串线。
2.  **游离引用（悬垂指针）**：调用 `Recycle` 后，原变量依然指向该内存地址。若后续逻辑继续修改该变量，将直接污染池内备用对象。**规范：回收后立即将变量置为 null，或严格限制变量的作用域。**
