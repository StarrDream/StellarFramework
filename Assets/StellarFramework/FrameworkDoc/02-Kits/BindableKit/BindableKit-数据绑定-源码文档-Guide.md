# BindableKit / 数据绑定源码文档

## 模块职责

`BindableKit` 提供三类可观察容器：

- `BindableProperty<T>`
- `BindableList<T>`
- `BindableDictionary<K, V>`

它们的共同目标是：

- 在值或集合变化时通知观察者
- 返回可注销监听令牌
- 支持和 Unity 生命周期绑定自动注销
- 阻止通知中的递归修改与递归通知

## 源码文件

- `Runtime/Kits/BindableKit/BindableProperty.cs`
- `Runtime/Kits/BindableKit/BindableList.cs`
- `Runtime/Kits/BindableKit/BindableDictionary.cs`
- `Runtime/Kits/BindableKit/BindableExtensions.cs`

## 总体结构

```text
IReadOnlyBindableProperty<T>
└─ BindableProperty<T>

BindableList<T>
└─ ListEvent<T>

BindableDictionary<K, V>
└─ DictEvent<K, V>
```

## 共通设计

- 都使用链表节点保存观察者
- 都通过 `_iteratingCount` 和 `_isNotifying` 控制通知过程
- 都支持延迟删除观察者节点
- 都通过 `IUnRegister` 对接 `EventKit` 生命周期解绑

## 类型详解

## `IReadOnlyBindableProperty<T>`

### 作用

定义只读值绑定接口。

### 成员

- `Value`
- `Register(...)`
- `RegisterWithInitValue(...)`

## `BindableProperty<T>`

### 作用

单值可观察容器。

### 字段

- `_value`
  当前值
- `_head / _tail`
  观察者链表首尾
- `_iteratingCount`
  当前通知遍历层数
- `_isNotifying`
  当前是否正在通知

### 方法

- `Value`
  新旧值不同才触发通知
- `SetValueWithoutNotify(...)`
  直接赋值，不广播
- `SetValueForceNotify(...)`
  无论值是否相同都广播
- `Notify()`
  执行观察者通知
- `Register(...)`
  注册监听
- `RegisterWithInitValue(...)`
  先注册，再立刻回调当前值
- `UnRegisterAll()`
  清空全部监听

### 内部结构

#### `ObserverNode`

字段：

- `Action`
- `Owner`
- `Previous`
- `Next`
- `MarkedForDeletion`

职责：

- 作为监听令牌实现 `IUnRegister`
- 支持生命周期自动解绑
- 节点对象池复用

## `BindableList<T>`

### 作用

列表变化可观察容器。

### 事件类型

- `Add`
- `Remove`
- `Clear`
- `Replace`

### 结构

`ListEvent<T>` 包含：

- `Type`
- `Item`
- `OldItem`
- `Index`

### 方法

- `Add(...)`
- `Remove(...)`
- `RemoveAt(...)`
- `Clear()`
- 索引器设置
- `Register(...)`
- `NotifyRefresh()`
- `UnRegisterAll()`

### 约束

通知回调中禁止修改集合。

## `BindableDictionary<K, V>`

### 作用

字典变化可观察容器。

### 事件类型

- `Add`
- `Remove`
- `Clear`
- `Update`

### 结构

`DictEvent<K, V>` 包含：

- `Type`
- `Key`
- `Value`
- `OldValue`

### 方法

- `Add(...)`
- `Remove(...)`
- `Clear()`
- 索引器设置
- `Register(...)`
- `NotifyRefresh()`
- `UnRegisterAll()`

### 约束

通知回调中禁止修改字典。

## 设计约束

- 通知中禁止递归通知
- 通知中禁止修改集合
- 生命周期解绑依赖 `EventKit` 触发器
- 监听器删除采用延迟清理，避免遍历时断链

## 常见误用

- 在回调里再次修改值或集合
- 忘记解绑长生命周期监听
- 用 `SetValueWithoutNotify` 后期待自动刷新

## 测试建议

- 值变化通知
- 相同值不通知
- `RegisterWithInitValue` 顺序
- 列表和字典通知内容
- 回调中修改集合的保护逻辑
