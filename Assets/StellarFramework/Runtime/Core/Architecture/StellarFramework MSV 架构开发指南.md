# StellarFramework MSV 架构开发指南

**版本**: v2.1 (Multi-Architecture Support)  
**核心理念**: 关注点分离 (Separation of Concerns) - 数据、逻辑、表现解耦。

---

## 1. 架构概览 (Overview)

本架构采用 **Model-Service-View (MSV)** 设计模式。

### 核心分层
1.  **Model (数据层)**
    *   **职责**：仅存储运行时数据。
    *   **规则**：不可引用 View，不可包含复杂业务逻辑。
2.  **Service (服务层)**
    *   **职责**：处理业务逻辑（计算、网络、存档），修改 Model。
    *   **规则**：不可直接引用 View。
3.  **View (表现层)**
    *   **职责**：显示数据，接收用户输入。
    *   **规则**：不可直接修改 Model，必须调用 Service 处理逻辑。

### 数据流向
> **View** (用户操作) -> 调用 **Service** -> 修改 **Model** -> 发送通知 -> **View** (刷新界面)

---

## 2. 快速上手 (Quick Start)

### 第一步：定义架构入口
创建一个类继承 `Architecture<T>`。

```csharp
// GameApp.cs (全局架构)
public class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}
```

### 第二步：定义 Model & Service
```csharp
public class PlayerModel : AbstractModel
{
    public BindableProperty<int> HP = new BindableProperty<int>(100);
}

public class PlayerService : AbstractService
{
    public void Heal()
    {
        var model = GetModel<PlayerModel>();
        model.HP.Value += 10;
    }
}
```

### 第三步：定义 View
继承 `StellarView` 并重写 `Architecture` 属性，指定该 View 属于哪个架构。

```csharp
public class PlayerView : StellarView
{
    // [关键] 指定此 View 归属于 GameApp
    public override IArchitecture Architecture => GameApp.Interface;

    public override void OnBind()
    {
        var model = this.GetModel<PlayerModel>();
        model.HP.Register(OnHpChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void OnClickHeal()
    {
        this.GetService<PlayerService>().Heal();
    }

    private void OnHpChanged(int hp) { /* Update UI */ }
    public override void OnUnbind() { }
}
```

---

## 3. 核心机制 (Core Mechanics)

### 3.1 为什么引入 `IView` 接口？
C# 不支持多重继承。如果你的脚本必须继承 `Button`、`ScrollRect` 或第三方插件的类，就无法继承 `StellarView`。
**解决方案**：实现 `IView` 接口，并使用框架提供的扩展方法 `this.GetModel<T>()`。

```csharp
public class MyButton : Button, IView
{
    public IArchitecture Architecture => GameApp.Interface; // 指定架构

    protected override void Start() { base.Start(); OnBind(); }
    protected override void OnDestroy() { OnUnbind(); base.OnDestroy(); }

    public void OnBind() { /* ... */ }
    public void OnUnbind() { /* ... */ }
}
```

### 3.2 为什么 View 不能直接改 Model？
如果 View 直接 `model.HP = 0`，业务逻辑就散落在 UI 代码里了。当需要在扣血时增加“无敌判断”或“播放音效”时，你需要去修改每一个相关的 UI 脚本。
**正确做法**：View 调用 `Service.TakeDamage()`，所有逻辑在 Service 中收口。

---

## 4. 多架构管理 (Multi-Architecture Strategy)

在复杂项目中，通常需要多个架构并存。例如：**全局架构**（一直存在）和 **战斗架构**（随场景销毁）。

### 4.1 定义多个架构
```csharp
// 1. 全局架构 (存放用户信息、设置、网络连接)
public class GlobalApp : Architecture<GlobalApp> { ... }

// 2. 战斗架构 (存放怪物列表、技能状态、战斗结算)
public class BattleApp : Architecture<BattleApp> { ... }
```

### 4.2 生命周期管理
*   **GlobalApp**：在游戏启动时 (`GameEntry.Awake`) 初始化，永不销毁。
*   **BattleApp**：在进入战斗场景时初始化，退出战斗场景时销毁。

```csharp
// BattleEntry.cs (挂在战斗场景的物体上)
public class BattleEntry : MonoBehaviour
{
    void Awake()
    {
        // 初始化战斗架构
        BattleApp.Interface.Init();
    }

    void OnDestroy()
    {
        // [重要] 退出场景时销毁架构，释放内部所有 Model 和 Service
        BattleApp.Interface.Dispose();
    }
}
```

### 4.3 View 的归属
View 必须明确自己属于哪个架构。

```csharp
// 血条 UI (属于战斗架构)
public class HealthBar : StellarView {
    public override IArchitecture Architecture => BattleApp.Interface;
    // ... GetModel<BattleModel>()
}

// 设置弹窗 (属于全局架构)
public class SettingsWindow : StellarView {
    public override IArchitecture Architecture => GlobalApp.Interface;
    // ... GetModel<SettingsModel>()
}
```

### 4.4 跨架构通信 (Cross-Architecture)
如果战斗中的 Service 需要读取全局的用户数据，怎么办？

**方案 A：直接访问 (推荐)**
Service 是纯 C# 类，可以直接访问其他架构的静态 Interface。
```csharp
// BattleService.cs
public void OnGameWin()
{
    // 1. 处理战斗数据
    var battleModel = GetModel<BattleModel>();
    
    // 2. [跨架构] 获取全局 UserService 增加经验值
    GlobalApp.Interface.GetService<UserService>().AddExp(100);
}
```

**方案 B：事件解耦**
使用 `EventKit` 发送全局事件。
```csharp
// BattleService.cs
GlobalEnumEvent.Broadcast(GlobalEvents.AddExp, 100);
```

---

## 5. 常见坑点 (Pitfalls)

### Q1: 忘记初始化架构
**现象**：View 在 `Start` 中报错 `NullReferenceException`。
**原因**：View 的 `Start` 执行得比架构的 `Init` 早。
**解决**：
1.  确保 `GameEntry` / `BattleEntry` 的 Script Execution Order 排在最前。
2.  或者在 View 中判空（不推荐，治标不治本）。

### Q2: 忘记 Dispose 场景架构
**现象**：重新进入战斗场景时，数据还是上一次战斗的残余数据。
**原因**：`BattleApp` 是静态单例，如果不调用 `Dispose()`，内部的 Model 不会被销毁，数据会一直保留。
**解决**：务必在场景卸载或 Entry 销毁时调用 `Dispose()`。

### Q3: 循环依赖
*   Service 可以调用其他 Service。
*   Service 可以获取 Model。
*   **Model 不应该获取 Service**（Model 只是数据容器）。
*   **Service 不应该获取 View**（逻辑不应依赖 UI）。

### Q4: 模块划分不清
不要把所有东西都塞进 `GlobalApp`。
*   **GlobalApp**: 账号、背包、好友、聊天、设置。
*   **BattleApp**: 敌人、子弹、技能、伤害计算。
*   **HomeApp**: 主城交互、建筑升级。