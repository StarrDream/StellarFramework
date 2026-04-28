# PoolKit / 对象池

## 1. 架构理念 (Architecture)
`PoolKit` 采用底层统一、上层分治的设计：
*   **底层数据结构**：`FactoryObjectPool<T>` 是核心池化容器，通过生命周期委托（`factoryMethod`, `allocateMethod`, `recycleMethod`, `destroyMethod`）实现与具体对象类型的解耦。
*   **全局门面 (Facade)**：`PoolKit` 静态类作为纯 C# 对象的全局调度中心，内部利用泛型静态类特性减少字典查找开销。

---

## 2. 使用指南 (How To Use)

### 2.1 纯 C# 对象池 (网络消息、事件参数)
**适用场景**：高频创建与销毁的纯数据结构。
**规范约束**：建议实现 `IPoolable` 接口，在出入池时清理脏数据。

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

**业务调用：**
```csharp
// 1. 申请
DamageEventData evt = PoolKit.Allocate<DamageEventData>();
evt.TargetId = 1001;
evt.DamageValue = 99.5f;

// 2. 派发事件...
GlobalTypeEvent.Broadcast(evt);

// 3. 回收
PoolKit.Recycle(evt);
```

### 2.2 Unity 游戏物体池 (子弹、特效)
**适用场景**：依赖 `Instantiate` 和 `Destroy` 的表现对象。
**规范约束**：不建议将 GameObject 直接放入 `PoolKit` 门面。应在对应的业务 Manager 内部实例化 `FactoryObjectPool<T>`。

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
            destroyMethod: b => { if (b != null) Destroy(b.gameObject); },
            maxCount: 100
        );
    }

    public void Fire() 
    {
        Bullet b = _pool.Allocate();
        // 初始化子弹逻辑...
    }
    
    public void RecycleBullet(Bullet b)
    {
        _pool.Recycle(b);
    }
}
```

---

## 3. 常见问题 (Pitfalls)

1.  **脏数据残留**：纯 C# 对象若不实现 `IPoolable.OnRecycled` 清理内部的引用类型字段，可能导致逻辑异常。
2.  **游离引用**：调用 `Recycle` 后，原变量依然指向该内存地址。若后续逻辑继续修改该变量，将污染池内对象。建议回收后将变量置为 null。
3.  **双重回收拦截**：在 Editor 或 Development Build 环境下，`FactoryObjectPool` 会通过 `HashSet` 检查同一对象是否被多次 `Recycle`，若触发将通过 `LogKit.Assert` 报错。