# SingletonKit 使用手册

## 1. 设计理念 (Why)
在 Unity 开发中，单例是最常用的模式，但原生写法存在诸多性能和管理问题：
*   **性能隐患**：`FindObjectOfType` 在大场景中性能极差，频繁调用会导致卡顿。
*   **生命周期混乱**：很难区分哪些是“跨场景全局单例”，哪些是“随场景销毁的单例”。
*   **重复创建**：场景切换或误操作容易导致场景中存在多个单例实例。

**SingletonKit 的特性：**
*   **零查找 (No Find)**：彻底移除了 `FindObjectOfType`。无论场景多大，访问单例的开销始终接近 O(1)。
*   **注册表模式**：所有单例在 `Awake` 时主动注册到 `SingletonFactory` 的静态字典中。
*   **严格生命周期**：明确区分 `Global` (全局) 和 `Scene` (场景) 单例。
*   **线程安全**：增加了主线程检查，防止在异步线程访问 Unity 组件导致崩溃。

---

## 2. 核心架构 (Under the hood)

### 2.1 注册表机制
`SingletonFactory` 维护了一个 `Dictionary<Type, ISingleton>`。
*   **MonoSingleton.Awake**：主动调用 `Register`，将自己写入字典。
*   **MonoSingleton.OnDestroy**：主动调用 `Unregister`，从字典移除。
*   **访问 Instance**：直接查字典。如果字典里没有，根据生命周期策略决定是“创建”还是“报错”。

### 2.2 自动创建 (Global Only)
对于 `Global` 单例，如果字典里没有，工厂会：
1.  尝试从 Resources 加载预制体（如果配置了路径）。
2.  或者 `new GameObject` 并挂载组件。
3.  调用 `DontDestroyOnLoad`。
4.  挂载到 `[SingletonContainer]` 下，保持 Hierarchy 整洁。

---

## 3. 使用指南 (How)

### 3.1 全局单例 (Global Singleton)
适用于：`AudioManager`, `UIManager`, `NetworkManager` 等全生命周期存在的管理器。

**特点**：自动创建，自动 `DontDestroyOnLoad`，全生命周期存活。

```csharp
using StellarFramework;
using UnityEngine;

// 1. 标记为 Global
// resourcePath: (可选) Resources 下的预制体路径，如果不填则创建一个空物体
[Singleton(resourcePath: "Managers/MyGameManager", lifeCycle: SingletonLifeCycle.Global)]
public class MyGameManager : MonoSingleton<MyGameManager>
{
    // 2. 替代 Awake 的初始化方法 (只会在首次注册成功时调用一次)
    public override void OnSingletonInit()
    {
        Debug.Log("全局管理器初始化完成");
    }

    public void DoSomething() { }
}

// 3. 调用 (自动创建)
MyGameManager.Instance.DoSomething();
```

### 3.2 场景单例 (Scene Singleton)
适用于：`BattleController`, `LevelManager` 等随场景销毁的控制器。

**特点**：**禁止自动创建**。必须预先在场景中挂载。如果访问 `Instance` 时场景里没有，**直接报错**（不会尝试 Find，避免卡顿）。

```csharp
using StellarFramework;

// 1. 必须标记为 Scene
[Singleton(lifeCycle: SingletonLifeCycle.Scene)]
public class BattleController : MonoSingleton<BattleController>
{
    // 2. 必须在场景中手动挂载此脚本
    
    // 3. 业务逻辑
    public void StartBattle() { }
}

// 4. 调用
// 注意：建议在 Start 中调用，确保 BattleController 已经执行了 Awake 注册
void Start()
{
    BattleController.Instance.StartBattle();
}
```

### 3.3 纯 C# 单例
适用于：`DataCalculator`, `PathFinder` 等不需要继承 MonoBehaviour 的工具类。

```csharp
public class DataHelper : Singleton<DataHelper>
{
    public int Add(int a, int b) => a + b;
}

// 调用
int result = DataHelper.Instance.Add(1, 2);
```

---

## 4. 常见坑点 (Pitfalls)

### Q1: 报错 `[Singleton] 场景单例 xxx 未注册！`
*   **原因**：
    1.  场景里真的没有这个物体。
    2.  物体被 Disable 了（导致 Awake 没执行）。
    3.  你在其他脚本的 `Awake` 里调用了它，但它还没来得及初始化（时序问题）。
*   **解决**：检查场景物体；将调用逻辑移到 `Start`；或手动设置 Script Execution Order 让单例先执行。

### Q2: 报错 `[SingletonFactory] 发现重复单例...`
*   **原因**：Global 单例通常由代码自动创建。如果你手动拖了一个到场景里，代码又创建了一个，就会冲突。
*   **机制**：框架会保留“先注册的”（通常是手动拖在场景里的那个），并销毁“后来的”。日志会提示你。
*   **建议**：Global 单例尽量不要手动拖入场景。