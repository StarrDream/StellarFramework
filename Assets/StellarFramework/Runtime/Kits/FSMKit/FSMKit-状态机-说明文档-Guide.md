# FSMKit / 状态机说明文档

FSMKit 是轻量有限状态机，适合角色状态、UI 流程、AI 小状态和简单业务流程。

## 入口 API

- `FSM<T>`：状态机容器，T 通常是状态枚举。
- `FSMState<T>`：状态基类。
- `IPayloadState<TPayload>`：需要切换参数的状态接口。
- `AddState(key, state)`：注册状态。
- `ChangeState(key)`：切换状态。
- `Update()`：驱动当前状态更新。

## 使用模板

```csharp
public enum PlayerState
{
    Idle,
    Move
}

public sealed class IdleState : FSMState<PlayerState>
{
    public override void OnEnter() {}
    public override void OnUpdate() {}
    public override void OnExit() {}
}
```

```csharp
FSM<PlayerState> fsm = new FSM<PlayerState>();
fsm.AddState(PlayerState.Idle, new IdleState());
fsm.ChangeState(PlayerState.Idle);
fsm.Update();
```

## 常见问题

- 切换无效：确认状态已注册。
- 状态需要参数：实现 `IPayloadState<TPayload>`。
- Update 没执行：业务层需要在 MonoBehaviour Update 中转发。
