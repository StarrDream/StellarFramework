# Architecture / 架构说明文档

## 模块定位

`Architecture` 是 StellarFramework 的基础运行时架构层。

它的目标不是做“完整业务框架”，而是明确项目里最核心的职责边界：

- `Model`
  管理状态和数据
- `Service`
  管理业务逻辑和系统能力
- `View`
  管理界面表现和交互

## 适用场景

适合以下情况：

- 希望把状态、逻辑和表现层明确拆开
- 不希望 MonoBehaviour 之间直接乱找引用
- 希望运行时模块有统一的注册、初始化和访问入口
- 希望 View 默认只读状态、不直接修改 Model

## 模块组成

主要组成包括：

- `Architecture<T>`
  架构容器入口
- `IArchitecture / IReadOnlyArchitecture`
  可变 / 只读访问接口
- `IModel / IService / IView`
  模块分层接口
- `AbstractModel / AbstractService / StellarView`
  常用基类

## 基本关系

```text
Architecture<T>
├─ 注册 Model
├─ 注册 Service
└─ 对外提供查询入口

View
├─ 读取 Model
└─ 调用 Service

Service
├─ 读取 Model
└─ 调用其他 Service
```

## 最小接入流程

### 1. 定义架构入口

```csharp
public sealed class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}
```

### 2. 定义 Model

```csharp
public sealed class PlayerModel : AbstractModel
{
    public readonly BindableProperty<int> Hp = new BindableProperty<int>(100);
}
```

### 3. 定义 Service

```csharp
public sealed class PlayerService : AbstractService
{
    public void TakeDamage(int damage)
    {
        PlayerModel model = GetModel<PlayerModel>();
        model.Hp.Value = Mathf.Max(0, model.Hp.Value - damage);
    }
}
```

### 4. 定义 View

```csharp
public sealed class PlayerHudView : StellarView
{
    public override IReadOnlyArchitecture Architecture => GameApp.Interface;

    public override void OnBind()
    {
        this.GetReadOnlyModel<IPlayerReadOnlyModel>()
            ?.RegisterWithInitValue(OnHpChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void OnClickDamage()
    {
        this.GetService<PlayerService>()?.TakeDamage(10);
    }

    private void OnHpChanged(int hp)
    {
    }

    public override void OnUnbind()
    {
    }
}
```

## 生命周期

### 初始化

1. 通过 `GameApp.Interface` 获取实例
2. 调用 `Init()`
3. 进入 `InitModules()`
4. 注册 `Model / Service`
5. 依次调用各模块的 `Init()`

### 销毁

1. 调用 `Dispose()`
2. 依次执行 `Service.Deinit()`
3. 再执行 `Model.Deinit()`
4. 清空注册表和实例引用

### View 绑定

- `StellarView.Start()` 触发 `OnBind()`
- `StellarView.OnDestroy()` 触发 `OnUnbind()`

## 使用约束

- `InitModules()` 只做注册，不做重逻辑和耗时加载
- `Model` 不直接操作场景对象
- `Service` 不直接依赖具体 UI 面板
- `View` 默认只读状态，通过 `Service` 驱动行为
- `Model / Service` 只在初始化阶段注册

## 多架构使用

适用于：

- 一个全局架构
- 一个或多个场景 / 玩法局部架构

例如：

- `GlobalApp`
  常驻数据、账号、设置
- `BattleApp`
  战斗状态、战斗服务

关键原则：

- 全局架构常驻
- 场景架构随场景初始化和销毁
- `View` 必须明确返回自己所属的 `Architecture`

## 常见问题

- `GetModel` / `GetService` 返回空
  通常是没 `Init()`、没注册，或架构已经销毁。
- View 生命周期里重复监听
  需要把监听绑定到 `OnBind / OnUnbind` 或生命周期解绑接口。
- 想给 View 暴露只读状态
  用 `IReadOnlyModel` 契约，不要直接暴露可变 Model。

## 相关文档

- [Architecture 源码文档](Architecture-MSV-架构源码文档-Guide.md)
