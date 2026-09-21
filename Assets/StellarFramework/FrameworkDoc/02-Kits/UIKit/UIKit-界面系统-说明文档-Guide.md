# UIKit / 界面系统说明文档

## 模块定位

`UIKit` 是框架内 UI 的统一入口，负责：

- 初始化 `UIRoot`
- 管理面板加载和缓存
- 打开、关闭、预加载面板
- 管理页面栈
- 管理全屏面板遮挡和恢复
- 输出运行时快照

业务层不应直接 `Instantiate UI Prefab`，而应通过 `UIKit` 统一驱动 UI 生命周期。

## 模块组成

- `UIKit`
  UI 运行时主入口
- `UIPanelBase`
  面板基类
- `UIPanelDataBase`
  面板数据基类
- `IUILoadStrategy`
  加载策略接口
- `ResKitUILoadStrategy`
  默认加载策略
- `UIKitSettings`
  UI 默认配置

## 初始化流程

### 推荐方式

```csharp
await UIKit.Instance.InitAsync();
```

初始化时会：

- 确保加载策略存在
- 加载 `UIRoot`
- 建立 `Canvas / Layer` 映射
- 准备好面板缓存和页面栈相关状态

## 打开面板

### 普通打开

```csharp
await UIKit.OpenAsync<LoginPanel>(new LoginPanelData
{
    DefaultAccount = "player01"
});
```

### 栈式打开

```csharp
await UIKit.PushAsync<MainMenuPanel>();
await UIKit.PushAsync<InventoryPanel>();
```

### 栈操作

```csharp
UIKit.Pop();
UIKit.PopTo<MainMenuPanel>();
UIKit.ClearStack();
```

## 面板数据

如果面板需要打开参数，应定义一个 `UIPanelDataBase` 子类：

```csharp
public sealed class ShopPanelData : UIPanelDataBase
{
    public int TabIndex;
}
```

然后在面板里通过：

```csharp
TryGetPanelData<ShopPanelData>(data, out ShopPanelData shopData)
```

取出实际参数。

## 页面栈

`UIKit` 的页面栈适合做：

- 菜单层级
- 子页面压栈
- 全屏页面覆盖

运行规则：

- `Push` 的面板才进入页面栈
- `Pop` 关闭栈顶
- 若栈顶是全屏面板，下层面板会暂停和隐藏
- 当上层关闭时，下层会恢复

## 预加载

如果某个面板加载成本高，可以先预加载：

```csharp
await UIKit.PreloadAsync<ShopPanel>();
```

预加载会把面板实例放进缓存，但不自动打开。

## 运行时诊断

`UIKit` 提供：

- `TakeSnapshot()`
- `LogSnapshot()`

用于查看当前：

- 是否已初始化
- 当前加载策略
- 缓存面板数
- 激活面板数
- 加载中面板数

## ToolsHub 关联

- `UIKit 工具`
  UI 工作区、绑定代码生成、样例修复
- `文档中心`
  查看 UIKit 说明和源码文档

## 使用约束

- 使用前必须先初始化
- 面板 prefab 必须包含目标 `UIPanelBase` 组件
- 同步打开只适用于支持同步加载的策略
- 热更新和远端资源场景优先使用异步接口

## 常见问题

- 面板打不开
  检查 `UIKitSettings`、`UIRoot`、Prefab 路径和加载策略。
- Full Screen 页面下层不显示
  这是预期行为，下层被暂停和隐藏。
- 热更 UI 加载失败
  优先使用异步接口，并确认 `ResKit / Addressables` 地址可加载。
- 页面栈行为混乱
  确认是否混用了普通 `Open` 和 `Push`。

## 相关文档

- [UIKit 源码文档](UIKit-界面系统-源码文档-Guide.md)
