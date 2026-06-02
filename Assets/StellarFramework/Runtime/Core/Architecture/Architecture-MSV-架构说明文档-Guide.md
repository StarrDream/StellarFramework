# Architecture / MSV 架构说明文档

MSV 是 StellarFramework 的默认业务架构：`Model` 保存状态，`Service` 处理业务能力，`View` 只读取状态并调用服务。它的目标不是限制写法，而是让新人知道“数据放哪、逻辑放哪、UI 调谁”。

## 适用场景

- 需要把游戏入口、状态、服务和界面分开管理。
- 希望 View 不直接 new Service，也不在 MonoBehaviour 间乱找引用。
- 需要在样例、工具和测试里复用同一套业务入口。

## 入口 API

- `Architecture<T>.Interface`：获取架构单例入口。
- `Init()`：初始化架构并注册模块。
- `RegisterModel(model)`：注册数据模块。
- `RegisterService(service)`：注册服务模块。
- `GetModel<T>()`、`GetService<T>()`：在架构内部读取模块。
- `StellarView`：View 基类，提供 `OnBind()`、`OnUnbind()` 生命周期函数。
- `IView` 扩展方法：View 内用 `this.GetModel<T>()`、`this.GetService<T>()` 访问架构。

## 最小模板

```csharp
using StellarFramework;
using StellarFramework.Bindable;

public sealed class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}

public sealed class PlayerModel : AbstractModel
{
    public readonly BindableProperty<int> Hp = new BindableProperty<int>(100);
}

public sealed class PlayerService : AbstractService
{
    public void Damage(int value)
    {
        PlayerModel model = GetModel<PlayerModel>();
        model.Hp.Value -= value;
    }
}
```

## View 模板

```csharp
using StellarFramework;
using UnityEngine;

public sealed class PlayerHudView : StellarView
{
    public override IArchitecture Architecture => GameApp.Interface;

    public override void OnBind()
    {
        this.GetModel<PlayerModel>().Hp.RegisterWithInitValue(OnHpChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void OnDamageClicked()
    {
        this.GetService<PlayerService>().Damage(1);
    }

    private void OnHpChanged(int hp)
    {
        Debug.Log(hp);
    }

    public override void OnUnbind()
    {
    }
}
```

## 使用规则

- `InitModules()` 只做注册，不做耗时加载。
- Model 不直接操作 Unity 场景对象。
- Service 可以持有业务能力，但不要直接把 UI 面板当依赖。
- View 通过 `StellarView` 绑定生命周期，关闭或销毁时释放监听。
- 只读场景可以暴露 `IReadOnlyArchitecture` 和 `IReadOnlyModel`，避免外部修改状态。

## ToolsHub 关联

- `Quick Start` 会生成并打开 FrameworkValidation 场景，让新人先看 MSV 和 Kit 的组合方式。
- `文档中心 (Docs)` 可直接阅读架构说明文档和源码文档。

## 样例和测试

- 样例：`Samples/ArchitectureDemo`
- 集中验证场景：`Samples/KitSamples/Scenes/FrameworkValidation_Playable.unity`
- 源码阅读：[Architecture 源码文档](Architecture-MSV-架构源码文档-Guide.md)

## 常见问题

- 找不到 Model：确认架构入口调用过 `Init()`，并且 `InitModules()` 注册了对应 Model。
- View 调不到 Service：确认 View 的 `Architecture` 属性返回正确的架构实例。
- 监听重复触发：确认 View 销毁或禁用时解绑，优先使用 `UnRegisterWhenGameObjectDestroyed` 等生命周期辅助方法。
