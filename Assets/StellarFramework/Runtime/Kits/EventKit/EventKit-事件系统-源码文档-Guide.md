# EventKit / 事件系统源码文档

## 源码位置

- `Runtime/Kits/EventKit/EventCore.cs`：反注册接口和生命周期触发器。
- `Runtime/Kits/EventKit/GlobalEnumEvent.cs`：枚举 key 事件。
- `Runtime/Kits/EventKit/GlobalTypeEvent.cs`：类型事件。

## 核心类型

- `IUnRegister`：统一反注册接口。
- `CustomUnRegister`：用委托封装反注册动作。
- `EventUnregisterTrigger`：GameObject 销毁时反注册。
- `EventUnregisterOnDisableTrigger`：GameObject Disable 时反注册。
- `GlobalEnumEvent`：按 enum key 存储 delegate 表。
- `GlobalTypeEvent`：按事件类型存储订阅者。
- `ITypeEvent`：类型事件标记接口。
- `EventToken` / `EnumEventToken<T>`：可回收反注册令牌。

## 关键方法

- `GlobalEnumEvent.Register`：检查 key 和 delegate 类型，加入事件表，返回 token。
- `GlobalEnumEvent.Broadcast`：按 key 找到 delegate 并调用。
- `GlobalEnumEvent.UnRegister`：从事件表移除回调。
- `GlobalTypeEvent.Register<T>`：注册类型事件回调。
- `GlobalTypeEvent.Broadcast<T>`：广播指定类型事件。
- `TryAttachDestroyTrigger` / `TryAttachDisableTrigger`：把反注册动作绑定到 Unity 生命周期。

## 数据流

注册时，EventKit 把 callback 放入全局事件表，并返回 `IUnRegister`。业务把句柄绑定到生命周期。广播时按 enum key 或类型取出订阅者并执行。对象销毁或 Disable 时，触发器遍历句柄并调用 `UnRegister()`。

## 依赖关系

- 依赖 Unity GameObject 和 MonoBehaviour 只用于生命周期触发器。
- 可与 BindableKit、Architecture、UIKit 搭配使用。
- 不依赖 ResKit 或 ToolsHub。

## 扩展点

- 新增生命周期绑定：新增 trigger 组件并实现句柄管理。
- 新增事件追踪：在注册、反注册、广播处记录快照，供 ToolsHub 使用。
- 修改 delegate 表时要保持同 key 同签名，避免运行时强转错误。

## 测试入口

- EventKit 样例。
- ToolsHub `EventKit 链路追踪`。
- 修改 token 池或生命周期触发器时，应验证重复注册、反注册、销毁解绑、Disable 解绑。
