# StellarFramework MSV 架构开发指南

> **版本**: Pure (纯净版)  
> **核心理念**: 零依赖、强类型、高性能、逻辑与表现分离

---

## 1. 为什么这么设计？(Design Philosophy)

在 Unity 开发中，随着项目规模扩大，代码往往会变成“面条代码”：逻辑写在 UI 里，数据散落在各个脚本中，修改一个功能导致三个功能报错。

**StellarFramework MSV** 旨在解决以下痛点：

1.  **解决耦合**：强制将 **数据 (Model)**、**逻辑 (Service)**、**表现 (View)** 分离。
2.  **解决混乱**：提供统一的入口 (Architecture) 管理所有模块，不再需要 `FindObjectOfType` 或到处写单例。
3.  **解决性能**：核心容器基于 `Dictionary<Type, object>`，查找复杂度为 O(1)，无装箱拆箱，无反射开销（仅在注册时使用一次类型推断）。
4.  **保持纯粹**：这是一个**无依赖**的架构。它不强制你使用特定的事件系统（如 UniRx），也不强制你继承特定的基类（Service 是纯 C# 类）。

---

## 2. 核心概念 (Core Concepts)

架构由四个核心部分组成：

### M - Model (数据层)
*   **职责**：只存储数据。
*   **特征**：不包含复杂逻辑，**绝对不能引用 View**。
*   **生命周期**：随架构启动而初始化，随架构销毁而销毁。

### S - Service (逻辑层)
*   **职责**：处理业务逻辑（计算、网络请求、存档）。
*   **特征**：修改 Model，通知 View（通过事件或轮询），**不直接引用 View**。
*   **扩展**：如果是纯逻辑，继承 `AbstractService`；如果需要 Unity 生命周期（如 Update），建议内部挂载一个隐藏的 GameObject。

### V - View (表现层)
*   **职责**：显示数据、接收用户输入。
*   **特征**：继承自 `StellarView` (MonoBehaviour)。
*   **规则**：**只做表现，不做逻辑**。点击按钮后，调用 Service 处理，然后等待数据变化刷新 UI。

### Architecture (架构容器)
*   **职责**：IOC 容器，负责维护 Model 和 Service 的实例，管理初始化顺序。

---

## 3. 快速上手 (Getting Started)

### 第一步：定义架构入口
创建一个类继承 `Architecture<T>`，并在 `InitModules` 中注册模块。

```csharp
// GameApp.cs
using StellarFramework;

public class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}
```

### 第二步：定义 Model
```csharp
// PlayerModel.cs
public class PlayerModel : AbstractModel
{
    public int HP { get; set; } = 100;
}
```

### 第三步：定义 Service
```csharp
// PlayerService.cs
public class PlayerService : AbstractService
{
    public void Heal(int amount)
    {
        var model = GetModel<PlayerModel>();
        model.HP += amount;
        // 可以在这里发送事件通知 View
    }
}
```

### 第四步：定义 View
```csharp
// PlayerView.cs
public class PlayerView : StellarView
{
    protected override void OnBind()
    {
        // 获取模块，绑定事件
        var model = GameApp.Interface.GetModel<PlayerModel>();
        Debug.Log($"当前血量: {model.HP}");
    }

    protected override void OnUnbind()
    {
        // 清理事件
    }
    
    public void OnClickHeal()
    {
        GameApp.Interface.GetService<PlayerService>().Heal(10);
    }
}
```

### 第五步：启动架构
在游戏入口脚本（挂在场景物体上）调用 Init。

```csharp
// GameEntry.cs
void Awake()
{
    GameApp.Interface.Init();
}
```

---

## 4. 进阶案例：不同的交互模式

由于架构是纯粹的，你可以选择不同的方式来实现 **Model -> View** 的通信。

### 案例 A：使用原生 C# 事件 (推荐，标准做法)
*   **优点**：性能最好，无依赖。
*   **缺点**：需要手动注销事件。

```csharp
// Model
public class ScoreModel : AbstractModel {
    public event Action<int> OnScoreChanged;
    private int _score;
    public int Score {
        get => _score;
        set {
            if (_score != value) {
                _score = value;
                OnScoreChanged?.Invoke(_score); // 推送消息
            }
        }
    }
}

// View
public class ScoreView : StellarView {
    private ScoreModel _model;
    
    protected override void OnBind() {
        _model = GameApp.Interface.GetModel<ScoreModel>();
        _model.OnScoreChanged += UpdateUI; // 订阅
        UpdateUI(_model.Score);
    }
    
    protected override void OnUnbind() {
        _model.OnScoreChanged -= UpdateUI; // !必须注销!
    }
    
    private void UpdateUI(int score) { /* 更新 UI */ }
}
```

### 案例 B：引入 BindableKit (极简响应式)
如果你有 `BindableKit` (StellarFramework 的扩展包)，代码会更简洁。

*   **优点**：代码少，支持自动注销。
*   **缺点**：引入了额外的类。

```csharp
// Model
public class ScoreModel : AbstractModel {
    // 使用 BindableProperty
    public BindableProperty<int> Score = new BindableProperty<int>(0);
}

// View
public class ScoreView : StellarView {
    protected override void OnBind() {
        var model = GameApp.Interface.GetModel<ScoreModel>();
        
        // 链式调用，自动管理生命周期
        model.Score.RegisterWithInitValue(score => {
            scoreText.text = score.ToString();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }
    
    protected override void OnUnbind() { 
        // 不需要写任何代码，自动注销
    }
}
```

### 案例 C：轮询模式 (无事件)
适用于高频变化或极其简单的逻辑。

```csharp
// View
void Update() {
    // 每帧主动去拉取数据，不依赖通知
    if (_model.Score != _lastScore) {
        _lastScore = _model.Score;
        UpdateUI();
    }
}
```

---

## 5. 常见问题与坑点 (FAQ & Pitfalls)

### Q1: Service 太多怎么办？如何拓展？
如果 `GameApp.cs` 里注册了 50 个 Service，文件会很大。

**解决方案 1：使用 partial class (分部类)**
```csharp
// GameApp.cs
public partial class GameApp : Architecture<GameApp> { ... }

// GameApp.Modules.cs
public partial class GameApp {
    protected override void InitModules() {
        RegisterSystemModules();
        RegisterBattleModules();
    }
    private void RegisterSystemModules() { ... }
}
```

**解决方案 2：多架构拆分**
不要把所有东西都塞进 `GameApp`。
*   `GlobalApp` (全局单例)：存用户信息、设置、网络连接。
*   `BattleApp` (场景级)：存战斗数据、怪物列表。
*   进入战斗场景 `BattleApp.Init()`，退出场景 `BattleApp.Dispose()`。

### Q2: Service 如何使用 Unity 的 Update 或 Coroutine？
**坑点**：不要让 Service 继承 MonoBehaviour。
**正解**：在 Service `Init()` 时，动态创建一个 GameObject 挂载辅助脚本。

```csharp
public class TimerService : AbstractService {
    private MonoBehaviour _runner;
    public override void Init() {
        var go = new GameObject("TimerRunner");
        GameObject.DontDestroyOnLoad(go);
        _runner = go.AddComponent<ServiceRunner>(); // 一个空的 Mono
    }
    public void StartTimer() {
        _runner.StartCoroutine(CountDown());
    }
}
```

### Q3: 为什么我的 View 报空指针？
**检查**：
1.  是否在 `Awake` 里调用了 `GameApp.Interface.Init()`？
2.  View 的 `Start` 可能比架构 `Init` 执行得早。建议在 Unity 的 Script Execution Order 中设置 `GameEntry` 最先执行。

### Q4: 内存泄漏的根源？
**最大坑点**：在 `OnBind` 里 `+=` 了事件，但忘记在 `OnUnbind` 里 `-=`。
**后果**：View 被销毁了，但 Model 还持有 View 的引用，导致报错 `MissingReferenceException` 并且内存不释放。

---

## 6. 底层原理 (Under the Hood)

1.  **单例容器**：`Architecture<T>` 维护了两个 `Dictionary<Type, object>`。
2.  **依赖注入 (Service Locator)**：
    *   当你调用 `GetModel<T>()` 时，架构直接通过 `typeof(T)` 哈希查找字典，速度极快。
3.  **生命周期钩子**：
    *   `StellarView` 利用 Unity 的 `Start` 自动调用 `Bind`。
    *   `StellarView` 利用 Unity 的 `OnDestroy` 自动调用 `Unbind`。

---

## 7. 总结

StellarFramework MSV 不是一个限制你写法的框架，而是一个**管理依赖的容器**。

*   它告诉你：**数据放 Model，逻辑放 Service，界面放 View**。
*   它帮你：**管理它们的创建、获取和销毁**。
*   剩下的：**由你自由发挥**。