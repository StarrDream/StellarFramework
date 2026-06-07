# FSMKit / 状态机说明文档

## 模块定位

`FSMKit` 是一个轻量运行时状态机模块，适合处理：

- 角色状态
- UI 流程状态
- 小型 AI 状态
- 简单业务流程

它的目标不是做复杂行为树或可视化流程图，而是给项目提供一个清晰、稳定、低耦合的状态切换骨架。

## 模块组成

- `FSM<T>`
  状态机容器
- `FSMState<T>`
  状态基类
- `IPayloadState<TPayload>`
  带参数进入状态接口

## 典型接入方式

### 定义状态类型

```csharp
public enum PlayerState
{
    Idle,
    Move
}
```

### 定义状态类

```csharp
public sealed class IdleState : FSMState<PlayerState>
{
    public override void OnEnter()
    {
    }

    public override void OnUpdate()
    {
    }

    public override void OnExit()
    {
    }
}
```

### 创建和驱动状态机

```csharp
FSM<PlayerState> fsm = new FSM<PlayerState>(PlayerState.Idle);
fsm.AddState(new IdleState());
fsm.ChangeState<IdleState>();
fsm.OnUpdate();
```

## 生命周期

状态对象的主要回调包括：

- `OnInit(...)`
- `OnEnter()`
- `OnUpdate()`
- `OnFixedUpdate()`
- `OnGUI()`
- `OnExit()`

如果状态需要参数进入，则实现：

```csharp
IPayloadState<TPayload>
```

然后通过带 payload 的状态切换方法进入。

## 运行规则

- 状态机本身不会自动驱动
- 外部需要在合适的 `MonoBehaviour`、`Service` 或系统循环里转发：
  - `OnUpdate()`
  - `OnFixedUpdate()`
  - `OnGUI()`
- 状态切换前必须先注册状态
- 清理后的状态机不可继续复用

## 适用场景

- 角色站立 / 移动 / 受击 / 死亡
- 引导流程 / 菜单流程 / 弹窗流程
- 小型 AI 行为状态
- 简单战斗阶段切换

## 不适用场景

- 节点量非常大且需要可视化编辑的复杂流程
- 依赖多分支并行和复杂回溯的行为系统

## 常见问题

- 切换无效
  通常是目标状态没有注册。
- 状态需要参数
  实现 `IPayloadState<TPayload>`。
- `Update` 没执行
  外部需要主动转发调用。
- 想在状态切换时访问更多上下文
  把上下文作为 `Owner` 或 payload 传入，而不是在状态里全局乱取引用。

## 相关文档

- [FSMKit 源码文档](FSMKit-状态机-源码文档-Guide.md)
