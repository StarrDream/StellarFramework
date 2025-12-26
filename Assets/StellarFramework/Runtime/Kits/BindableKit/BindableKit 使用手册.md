# BindableKit 使用手册

## 1. 设计理念 (Why)
在 M-S-V 架构中，View 层需要监听 Model 层的数据变化。
*   **C# event / Action**：
    *   **GC 问题**：`+=` 和 `-=` 操作会产生委托合并的内存分配。
    *   **内存泄漏**：如果 View 销毁时忘记 `-=`，Model 会一直持有 View 的引用，导致 View 无法被 GC。
*   **UniRx / R3**：功能极其强大，但对于轻量级项目来说过于重型，且学习成本高。

**BindableKit 的特性：**
*   **0GC (Zero Garbage Collection)**：这是核心设计目标。内部使用**双向链表** + **静态对象池**管理观察者节点。除了首次创建节点外，后续的注册、注销、通知全程无 GC。
*   **防忘注销**：内置 `UnRegisterWhenGameObjectDestroyed`，利用 Unity 的 Component 机制自动绑定生命周期。
*   **集合支持**：不仅支持基础属性，还支持 `List` 和 `Dictionary` 的增删改查通知。

---

## 2. 核心架构 (Under the hood)

### 2.1 数据结构
不同于 `event Action<T>` 使用的多播委托（数组），`BindableProperty` 内部维护了一个 `ObserverNode` 的双向链表：
```csharp
class ObserverNode {
    Action<T> Action;
    ObserverNode Previous;
    ObserverNode Next;
}
```
*   **注册**：从静态池取一个 Node，挂到链表尾部。O(1)。
*   **注销**：将 Node 从链表中移除，放回静态池。O(1)。
*   **通知**：遍历链表执行 Action。

### 2.2 自动注销机制
当调用 `.UnRegisterWhenGameObjectDestroyed(gameObject)` 时，框架会尝试在该 GameObject 上获取（或添加）一个隐藏组件 `EventUnregisterTrigger`。
当该 GameObject 被销毁时，Unity 会调用 `EventUnregisterTrigger.OnDestroy`，进而触发所有注册在该 Trigger 上的注销逻辑。

---

## 3. 使用指南 (How)

### 3.1 基础属性 (BindableProperty)
适用于：血量、金币、等级、名称、开关状态等单值数据。

**AbstractModel 层定义：**
```csharp
public class PlayerModel : AbstractModel
{
    // 定义一个初始值为 100 的属性
    public BindableProperty<int> HP = new BindableProperty<int>(100);
    
    public void Init() { }
}
```

**StellarView 层监听：**
```csharp
public class PlayerUI : StellarView
{
    public Text hpText;

    void Start()
    {
        var model = this.GetModel<PlayerModel>();

        // 1. Register: 仅注册监听
        // 2. RegisterWithInitValue: 注册并立即执行一次回调 (常用于 UI 初始化刷新)
        // 3. UnRegisterWhenGameObjectDestroyed: 绑定生命周期 (必加!)
        
        model.HP.RegisterWithInitValue(OnHPChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    // 回调函数 (推荐使用方法组，避免 Lambda 闭包 GC)
    void OnHPChanged(int newHp)
    {
        hpText.text = $"HP: {newHp}";
    }
}
```

**AbstractService 层修改：**
```csharp
public class DamageSystem : AbstractService
{
    public void ApplyDamage(int damage)
    {
        var model = this.GetModel<PlayerModel>();
        // 赋值即触发通知
        model.HP.Value -= damage;
    }
}
```

### 3.2 响应式列表 (BindableList)
适用于：背包物品、任务列表、好友列表。

```csharp
// 定义
public BindableList<string> Inventory = new BindableList<string>();

// 监听
Inventory.Register(OnListChanged).UnRegisterWhenGameObjectDestroyed(gameObject);

// 回调
void OnListChanged(ListEvent<string> e)
{
    switch (e.Type)
    {
        case ListEventType.Add:
            Debug.Log($"新增物品: {e.Item} at {e.Index}");
            break;
        case ListEventType.Remove:
            Debug.Log($"移除物品: {e.Item}");
            break;
        case ListEventType.Replace:
            Debug.Log($"替换物品: {e.OldItem} -> {e.Item}");
            break;
        case ListEventType.Clear:
            Debug.Log("清空背包");
            break;
    }
}
```

### 3.3 响应式字典 (BindableDictionary)
适用于：装备属性、成就状态、配置表索引。

```csharp
// 定义
public BindableDictionary<string, int> Stats = new BindableDictionary<string, int>();

// 操作
Stats.Add("STR", 10);  // 触发 Add
Stats["STR"] = 20;     // 触发 Update (因为 Key 已存在)
Stats.Remove("STR");   // 触发 Remove
```

---

## 4. 常见坑点 (Pitfalls)

### Q1: 为什么值变了没收到通知？
*   **机制**：`BindableProperty` 内部使用了 `EqualityComparer`。如果新值和旧值相等（例如 `HP` 从 100 变成 100），为了性能，默认**不会**触发通知。
*   **解决**：如果确实需要强制通知（例如引用类型内容变了但引用没变），请使用 `.SetValueForceNotify(val)` 或手动调用 `.Notify()`。

### Q2: 列表操作 `items[i].xxx = yyy` 没反应？
*   **机制**：`BindableList` 只能监听列表本身的结构变化（Add/Remove/Replace）。如果你修改了列表中某个对象的**内部字段**，列表是感知不到的。
*   **解决**：
    1.  替换整个对象：`list[i] = newObj;` (触发 Replace)。
    2.  或者对象内部属性也使用 `BindableProperty`。

### Q3: 闭包 GC 问题 (性能优化)
虽然框架本身是 0GC 的，但如果你在 `Register` 时使用了**捕获变量的 Lambda 表达式**，C# 编译器会为你生成一个闭包类，这会产生 GC。

*   **Bad (产生 GC)**:
    ```csharp
    int id = 10;
    prop.Register(val => Debug.Log(id + val)); // Lambda 捕获了外部变量 id
    ```
*   **Good (0 GC)**:
    ```csharp
    prop.Register(OnValueChanged); // 使用方法组
    ```