# PoolKit 使用手册

## 1. 设计理念 (Why)
在游戏开发中，频繁的 `Instantiate` 和 `Destroy` 是造成 GC 峰值和帧率卡顿的主要原因之一。
**PoolKit 的特性：**
*   **极简 API**：`Allocate` (申请) 和 `Recycle` (回收)，符合直觉。
*   **LIFO 策略**：内部使用 `Stack` 存储。最近回收的对象最可能还在 CPU 缓存中，立即拿出来用性能最高。
*   **双模式**：支持纯 C# 对象和 GameObject。

---

## 2. 使用指南 (How)

### 2.1 纯 C# 对象池 (SimpleObjectPool)
适用于：网络消息包、临时数据结构、事件参数对象。

**定义类：**
```csharp
// 实现 IPoolable 接口（可选，但推荐）
public class MyMsg : IPoolable
{
    public int Id;
    
    // 相当于构造函数/Awake
    public void OnAllocated() 
    {
        Id = 0;
    }
    
    // 相当于 Dispose/OnDestroy
    public void OnRecycled() 
    {
        Id = 0;
    }
}
```

**使用：**
```csharp
// 1. 申请
MyMsg msg = PoolKit.Allocate<MyMsg>();
msg.Id = 100;

// ... 使用完毕 ...

// 2. 回收
PoolKit.Recycle(msg);
```

### 2.2 Unity 组件/物体池 (FactoryObjectPool)
虽然 `PoolKit` 提供了基础支持，但在处理 GameObject 时，通常建议配合 `FactoryObjectPool` 在 Manager 内部使用。

**示例（在管理器内部）：**
```csharp
private FactoryObjectPool<Bullet> _bulletPool;

void Start() {
    _bulletPool = new FactoryObjectPool<Bullet>(
        factoryMethod: () => Instantiate(bulletPrefab),
        resetMethod: (b) => { 
            b.gameObject.SetActive(false); 
            b.transform.position = Vector3.zero; 
        },
        destroyMethod: (b) => Destroy(b.gameObject)
    );
}

public void Fire() {
    var bullet = _bulletPool.Allocate();
    bullet.gameObject.SetActive(true);
}

public void ReturnBullet(Bullet b) {
    _bulletPool.Recycle(b);
}
```

---

## 3. 常见坑点 (Pitfalls)

### Q1: 对象状态不正确（脏数据）
*   **原因**：回收时没有重置数据。
*   **解决**：务必在 `OnRecycled` 或 `resetMethod` 中将对象的所有字段恢复到初始状态（清空 List，重置 bool 等）。

### Q2: 引用了已回收的对象
*   **原因**：`Recycle(msg)` 后，逻辑层还持有 `msg` 的引用并继续修改它。
*   **解决**：这是对象池最危险的 Bug。回收后请立即将变量设为 `null`，或者严格遵守“谁申请谁负责”的原则。