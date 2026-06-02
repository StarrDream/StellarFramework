# FSMKit / 状态机源码文档

## 源码位置

- `Runtime/Kits/FSMKit/FSMKit.cs`

## 核心类型

- `IPayloadState<TPayload>`：声明状态可接收切换参数。
- `FSMState<T>`：状态基类，持有 owner FSM 和 state key。
- `FSM<T>`：状态机容器，管理状态表和当前状态。

## 关键方法

- `FSM<T>.AddState`：注册状态实例。
- `FSM<T>.ChangeState`：退出旧状态，进入新状态。
- `FSM<T>.Update`：调用当前状态 `OnUpdate`。
- `FSMState<T>.OnEnter` / `OnUpdate` / `OnExit`：状态生命周期。

## 数据流

业务创建 FSM，注册 key 到 state 的映射。切换状态时，FSM 调用旧状态 `OnExit`，更新当前状态引用，再调用新状态 `OnEnter`。每帧业务主动调用 `Update`，FSM 转发给当前状态。

## 依赖关系

- 不依赖 Unity，可用于纯 C# 逻辑。
- 常由 MonoBehaviour 或 Service 持有并驱动。
- 可与 EventKit、BindableKit 组合，但本身不依赖它们。

## 扩展点

- 新增带参数切换：使用或扩展 `IPayloadState<TPayload>`。
- 新增状态共享上下文：在状态构造函数注入 owner 或业务上下文。
- 新增调试：在 ChangeState 处记录状态迁移。

## 测试入口

- FSMKit 样例。
- 修改切换流程后应测试：未注册状态、重复切换同状态、带参数切换、退出顺序。
