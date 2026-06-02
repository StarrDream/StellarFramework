# BindableKit / 数据绑定说明文档

BindableKit 提供属性、列表、字典三类可观察数据。它适合放在 Model 中，让 View 订阅状态变化并在生命周期结束时解绑。

## 入口 API

- `BindableProperty<T>`：单值绑定。
- `IReadOnlyBindableProperty<T>`：只读属性接口。
- `BindableList<T>`：列表变化通知。
- `BindableDictionary<K,V>`：字典变化通知。
- `ToBindable()`、`ToBindableList()`：扩展方法。
- `RegisterWithInitValue(...)`：注册并立即推送当前值。
- `UnRegisterWhenGameObjectDestroyed(...)`：绑定到 GameObject 生命周期。

## 使用模板

```csharp
public sealed class PlayerModel : AbstractModel
{
    public readonly BindableProperty<int> Hp = 100.ToBindable();
}

public sealed class PlayerHudView : StellarView
{
    public override IArchitecture Architecture => GameApp.Interface;

    public override void OnBind()
    {
        this.GetModel<PlayerModel>().Hp.RegisterWithInitValue(UpdateHp)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void UpdateHp(int hp)
    {
    }

    public override void OnUnbind() {}
}
```

## 常见问题

- 回调重复：确认每次注册都有对应反注册。
- 列表变化不知道原因：读取 `ListEvent<T>.Type`。
- 字典变化不知道 key：读取 `DictEvent<K,V>.Key`。

## 源码阅读

见 [BindableKit 源码文档](BindableKit-数据绑定-源码文档-Guide.md)。
