# BindableKit / 数据绑定源码文档

## 源码位置

- `Runtime/Kits/BindableKit/BindableProperty.cs`
- `Runtime/Kits/BindableKit/BindableList.cs`
- `Runtime/Kits/BindableKit/BindableDictionary.cs`
- `Runtime/Kits/BindableKit/BindableExtensions.cs`

## 核心类型

- `IReadOnlyBindableProperty<T>`：只读绑定属性接口。
- `BindableProperty<T>`：保存值和观察者链表。
- `ListEventType` / `ListEvent<T>`：列表变化类型和事件数据。
- `BindableList<T>`：包装 List 并广播 add/remove/clear/set 等变化。
- `DictEventType` / `DictEvent<K,V>`：字典变化类型和事件数据。
- `BindableDictionary<K,V>`：包装 Dictionary 并广播 key/value 变化。
- `BindableExtensions`：把普通值和集合快速转为可绑定对象。

## 关键方法

- `BindableProperty<T>.Value`：赋值时比较并触发通知。
- `Register(...)`：添加观察者并返回 `IUnRegister`。
- `RegisterWithInitValue(...)`：注册后立即回调当前值。
- `UnRegister(...)`：移除观察者。
- `BindableList<T>.Add/Remove/Clear`：修改集合并广播 `ListEvent<T>`。
- `BindableDictionary<K,V>.Add/Remove/Clear`：修改字典并广播 `DictEvent<K,V>`。

## 数据流

Model 持有 bindable 数据。View 注册回调后，BindableKit 把回调封装成 observer node。数据变化时，节点链表按注册顺序触发。View 销毁时通过 EventKit 的生命周期反注册工具移除观察者。

## 依赖关系

- 依赖 EventKit 的 `IUnRegister` 生命周期抽象。
- 常与 Architecture 的 Model 和 View 搭配使用。
- 不依赖 Unity 对象，生命周期绑定由 EventKit 扩展提供。

## 扩展点

- 新增集合类型时保持事件对象包含变化类型、key/index、旧值、新值。
- 新增注册方式时必须返回 `IUnRegister`。
- 修改通知链表时要注意回调中反注册的安全性。

## 测试入口

- 在 BindableKit 样例中验证属性、列表、字典变化；如需集中回归，可使用外置验证区的 FrameworkValidation 场景。
- 修改 observer 回收或反注册逻辑时，应补注册、重复注册、回调中解绑的 EditMode 测试。
