# BindableKit / 数据绑定说明文档

## 模块定位

`BindableKit` 把运行时状态变成“可订阅”的数据结构，适合放在 `Model` 中供 `View` 层观察。

当前提供三类绑定容器：

- `BindableProperty<T>`
- `BindableList<T>`
- `BindableDictionary<K, V>`

## 适合场景

- 血量、金币、经验值
- 背包列表、任务列表、队伍列表
- key-value 型状态表

## 模块组成

- `BindableProperty<T>`
  单值绑定
- `BindableList<T>`
  列表绑定
- `BindableDictionary<K, V>`
  字典绑定
- `IReadOnlyBindableProperty<T>`
  只读属性接口
- 生命周期解绑接口

## 标准使用方式

### 单值绑定

```csharp
public sealed class PlayerModel : AbstractModel
{
    public readonly BindableProperty<int> Hp = 100.ToBindable();
}
```

```csharp
public sealed class PlayerHudView : StellarView
{
    public override IReadOnlyArchitecture Architecture => GameApp.Interface;

    public override void OnBind()
    {
        this.GetModel<PlayerModel>().Hp.RegisterWithInitValue(UpdateHp)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void UpdateHp(int hp)
    {
    }

    public override void OnUnbind()
    {
    }
}
```

### 列表绑定

```csharp
BindableList<string> items = new BindableList<string>();
items.Register(OnListChanged);
```

### 字典绑定

```csharp
BindableDictionary<string, int> stats = new BindableDictionary<string, int>();
stats.Register(OnDictChanged);
```

## 运行规则

- 监听可以绑定到 Unity 生命周期
- 支持“注册并立即收到当前值”
- 通知中禁止递归修改同一集合
- 监听节点会复用，减少运行时分配

## 使用约束

- 长生命周期监听要记得解绑
- 不要在回调里再次修改同一个集合容器
- 绑定适合做状态观察，不适合做复杂业务计算

## 常见问题

- 回调重复触发
  检查是否重复注册且没有解绑。
- 列表变化不知道原因
  读取 `ListEvent<T>.Type`。
- 字典变化不知道哪个 key 变了
  读取 `DictEvent<K, V>.Key`。

## 相关文档

- [BindableKit 源码文档](BindableKit-数据绑定-源码文档-Guide.md)
