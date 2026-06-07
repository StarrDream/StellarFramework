# Architecture / 架构源码文档

## 模块职责

`Architecture` 是 StellarFramework 的核心运行时容器，负责：

- 定义模块分层契约：`Model / Service / View`
- 维护架构生命周期：未初始化、初始化中、已初始化、销毁中、已销毁
- 维护模块注册与查询容器
- 为 `View` 提供只读访问入口
- 为 `Service` 提供对 `Model / Service` 的受控访问

这一层不直接处理业务、UI、资源或热更新逻辑，只提供项目内部模块协作的基础结构。

## 源码文件

- `Runtime/Core/Architecture/StellarFramework.cs`
  架构核心实现，包含接口、扩展、容器实现、模块基类和视图基类。

## 总体结构

```text
IArchitecture / IReadOnlyArchitecture
├─ 负责模块查询
├─ 对外暴露生命周期状态
└─ 由 Architecture<T> 实现

IModule
├─ IModel
├─ IService
└─ 负责 Init / Deinit 生命周期

IView
├─ 只暴露 IReadOnlyArchitecture
├─ OnBind / OnUnbind
└─ 由 StellarView 提供默认绑定时机

Architecture<T>
├─ _models
├─ _readOnlyModels
├─ _services
├─ State
└─ Interface
```

## 生命周期调用链

### 架构初始化

1. 业务调用 `GameApp.Interface.Init()`
2. `Architecture<T>.Init()` 校验当前状态
3. 状态切换到 `Initializing`
4. 调用子类实现的 `InitModules()`
5. 在 `InitModules()` 中注册 `Model` 和 `Service`
6. 框架依次调用所有 `Model.Init()`
7. 框架依次调用所有 `Service.Init()`
8. 状态切换到 `Initialized`

### 架构销毁

1. 业务调用 `Dispose()`
2. 状态切换到 `Disposing`
3. 依次调用 `Service.Deinit()`
4. 依次调用 `Model.Deinit()`
5. 清空内部容器
6. 清理静态实例 `Interface`
7. 状态切换到 `Disposed`

### 视图绑定

1. `StellarView.Start()` 被 Unity 调用
2. 若当前未绑定，则执行 `OnBind()`
3. 视图通过扩展方法获取只读模型或服务
4. `StellarView.OnDestroy()` 被 Unity 调用
5. 若当前已绑定，则执行 `OnUnbind()`

## 类型详解

## `ArchitectureState`

### 作用

描述架构实例当前所处的生命周期状态。

### 枚举值

- `Uninitialized`
  实例已创建，但尚未开始初始化。
- `Initializing`
  正在执行 `InitModules()` 与模块初始化。
- `Initialized`
  初始化完成，可安全提供模块访问。
- `Disposing`
  正在执行销毁流程。
- `Disposed`
  已销毁，不允许继续复用。

### 使用位置

- `IArchitecture.State`
- `IReadOnlyArchitecture.State`
- `Architecture<T>._state`
- `Init()` / `Dispose()` / `GetModel()` / `GetService()` / `GetReadOnlyModel()`

## `IArchitecture`

### 作用

定义可变架构接口，允许获取 `Model` 和 `Service`。

### 成员

- `ArchitectureState State`
  当前架构状态。
- `T GetModel<T>() where T : class, IModel`
  获取可变模型。
- `T GetService<T>() where T : class, IService`
  获取服务。

### 依赖关系

- 被 `Architecture<T>` 实现
- 被 `AbstractModel`、`AbstractService`、旧式 `IView.GetModel()` 调用链使用

## `IReadOnlyArchitecture`

### 作用

定义只读架构接口，供视图层读取只读模型和服务。

### 成员

- `ArchitectureState State`
- `T GetReadOnlyModel<T>() where T : class, IReadOnlyModel`
- `T GetService<T>() where T : class, IService`

### 设计意图

视图层默认只拿到 `IReadOnlyArchitecture`，避免直接修改可变模型。

## `IModule`

### 作用

定义所有可注册运行时模块的统一生命周期契约。

### 成员

- `IArchitecture Architecture { get; set; }`
  回指所属架构。
- `void Init()`
  初始化回调。
- `void Deinit()`
  反初始化回调。

### 实现者

- `IModel`
- `IService`
- 对应实现类一般继承 `AbstractModel` 或 `AbstractService`

## `IModel`

### 作用

标记可注册到架构中的模型模块。

### 继承关系

- 继承 `IModule`

### 用途

用于保存业务状态、业务配置缓存或只读查询能力的底层状态对象。

## `IReadOnlyModel`

### 作用

标记可作为只读模型契约暴露给 `View` 层的接口。

### 用途

如果某个 `Model` 实现了额外的只读接口，并且该接口继承了 `IReadOnlyModel`，框架会在注册模型时自动把该接口映射到 `_readOnlyModels`。

## `IService`

### 作用

标记可注册到架构中的服务模块。

### 继承关系

- 继承 `IModule`

### 用途

用于封装业务逻辑、流程编排、跨模型协作和系统级能力。

## `IView`

### 作用

定义视图层与架构层的最小协作接口。

### 成员

- `IReadOnlyArchitecture Architecture`
  只读架构入口。
- `void OnBind()`
  视图建立绑定时调用。
- `void OnUnbind()`
  视图解除绑定时调用。

### 设计约束

- 视图不应直接持有可变 `IArchitecture`
- 视图读取数据优先通过 `GetReadOnlyModel<T>()`

## `StellarArchitectureExtensions`

### 作用

提供面向 `IView` 的快捷扩展方法。

### 方法

- `GetModel<T>(this IView view)`
  旧式可变模型访问入口，已标记 `[Obsolete]`。
- `GetReadOnlyModel<T>(this IView view)`
  读取只读模型。
- `GetService<T>(this IView view)`
  获取服务。

### 失败路径

以下情况会返回 `null` 并输出错误日志：

- `view == null`
- `view.Architecture == null`
- 视图请求可变模型，但当前只暴露只读架构

### 设计意图

把常见的空检查、架构存在性检查、错误日志统一收口到扩展层，减少视图代码重复判断。

## `Architecture<T>`

### 作用

泛型单例架构容器，是整个模块系统的核心实现。

### 泛型约束

- `where T : Architecture<T>, new()`

要求具体架构类型可无参构造，并使用 Curiously Recurring Template Pattern 约束静态入口类型。

### 字段

- `_models : Dictionary<Type, IModel>`
  保存所有可变模型实例，键为注册类型。
- `_readOnlyModels : Dictionary<Type, object>`
  保存只读模型契约到模型实例的映射，键通常为只读接口类型。
- `_services : Dictionary<Type, IService>`
  保存服务实例。
- `_state : ArchitectureState`
  当前生命周期状态。
- `_instance : T`
  静态入口实例，供 `Interface` 返回。

### 属性

- `State`
  返回 `_state`。
- `Interface`
  统一的静态访问入口。

#### `Interface` 行为

- `_instance == null` 时创建新实例
- `_instance.State == Disposed` 时重建新实例
- 仅返回实例，不自动调用 `Init()`

### 方法

#### `Init()`

初始化入口。

职责：

- 拒绝重复初始化
- 拒绝在 `Initializing / Disposing / Disposed` 非法状态下继续初始化
- 调用 `InitModules()`
- 调用所有模块的 `Init()`
- 成功后切换到 `Initialized`

失败分支：

- 初始化过程中发现空 `Model`
- 初始化过程中发现空 `Service`
- 在非法状态下调用

#### `Dispose()`

销毁入口。

职责：

- 调用所有 `Service.Deinit()`
- 调用所有 `Model.Deinit()`
- 清空内部容器
- 清理 `_instance`
- 切换到 `Disposed`

#### `InitModules()`

抽象方法，由子类实现模块注册逻辑。

通常在这里调用：

- `RegisterModel(...)`
- `RegisterService(...)`

#### `RegisterModel<TM>(TM model)`

注册模型。

前置条件：

- `model != null`
- 当前状态必须是 `Uninitialized` 或 `Initializing`
- 相同类型不能重复注册

副作用：

- 设置 `model.Architecture = this`
- 写入 `_models`
- 自动调用 `RegisterReadOnlyModelContracts(model)`

#### `RegisterService<TS>(TS service)`

注册服务。

前置条件：

- `service != null`
- 当前状态必须是 `Uninitialized` 或 `Initializing`
- 相同类型不能重复注册

副作用：

- 设置 `service.Architecture = this`
- 写入 `_services`

#### `GetModel<TM>()`

获取可变模型。

约束：

- 仅允许在 `Initialized` 或 `Initializing` 状态调用
- 未注册时返回 `null`

#### `GetService<TS>()`

获取服务。

约束：

- 仅允许在 `Initialized` 或 `Initializing` 状态调用
- 未注册时返回 `null`

#### `GetReadOnlyModel<TR>()`

获取只读模型契约。

约束：

- 仅允许在 `Initialized` 或 `Initializing` 状态调用
- 未注册对应只读契约时返回 `null`

#### `RegisterReadOnlyModelContracts<TM>(TM model)`

扫描模型实现的所有接口，把继承自 `IReadOnlyModel` 的接口注册到 `_readOnlyModels`。

设计目的：

- 允许单个模型以多个只读接口暴露给视图层
- 让 View 不必依赖具体可变模型类型

失败分支：

- 同一个只读契约被重复注册时记录错误日志

### 使用示例

```csharp
public sealed class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}
```

## `AbstractModel`

### 作用

为模型提供最小默认实现。

### 成员

- `IArchitecture Architecture { get; set; }`
- `virtual void Init()`
- `virtual void Deinit()`

### 用途

业务模型通常直接继承它，而不是手写 `IModel` 生命周期实现。

## `AbstractService`

### 作用

为服务提供最小默认实现和跨模块查询助手。

### 成员

- `IArchitecture Architecture { get; set; }`
- `virtual void Init()`
- `virtual void Deinit()`
- `protected T GetModel<T>()`
- `protected T GetService<T>()`

### 设计意图

服务内部可以通过受控方式访问其他模型和服务，不需要手动持有容器引用。

### 失败分支

当 `Architecture == null` 时，辅助方法会返回 `null` 并记录错误日志。

## `StellarView`

### 作用

提供视图绑定生命周期的默认实现。

### 字段

- `_isBound : bool`
  标记当前视图是否已经执行过 `OnBind()`。

### 属性

- `abstract IReadOnlyArchitecture Architecture`
  子类必须实现并返回所属只读架构。

### 方法

#### `Start()`

- 若未绑定，则执行 `OnBind()`
- 绑定完成后把 `_isBound` 设为 `true`

#### `OnDestroy()`

- 若已绑定，则执行 `OnUnbind()`
- 解绑完成后把 `_isBound` 设为 `false`

#### `OnBind()`

抽象方法，由视图子类实现具体绑定逻辑。

#### `OnUnbind()`

抽象方法，由视图子类实现解绑逻辑。

### 设计约束

- 推荐在 `OnBind()` 中注册监听
- 推荐在 `OnUnbind()` 中释放监听或引用

## 数据结构关系

### 容器关系

```text
Architecture<T>
├─ _models          保存可变模型
├─ _readOnlyModels  保存只读模型契约映射
└─ _services        保存服务
```

### 调用关系

```text
View
├─ GetReadOnlyModel<T>()
└─ GetService<T>()

Service
├─ GetModel<T>()
└─ GetService<T>()

Architecture<T>
├─ RegisterModel(...)
├─ RegisterService(...)
├─ GetModel<T>()
├─ GetReadOnlyModel<T>()
└─ GetService<T>()
```

## 设计约束

- `Model` 和 `Service` 只能在初始化阶段注册
- `View` 默认应只使用只读架构接口
- 已销毁架构不允许重新 `Init()`，必须通过 `Interface` 重新获取实例
- 获取模块时必须处于 `Initializing` 或 `Initialized`
- 只读模型契约依赖接口映射，未实现 `IReadOnlyModel` 的接口不会进入只读容器

## 扩展方式

### 新增架构

1. 继承 `Architecture<T>`
2. 实现 `InitModules()`
3. 在其中注册模型和服务
4. 业务启动时调用 `YourApp.Interface.Init()`

### 新增模型

1. 继承 `AbstractModel`
2. 如需给 `View` 暴露只读能力，实现一个继承 `IReadOnlyModel` 的接口
3. 在 `InitModules()` 中注册

### 新增服务

1. 继承 `AbstractService`
2. 在内部通过 `GetModel<T>() / GetService<T>()` 协作
3. 在 `InitModules()` 中注册

### 新增视图

1. 继承 `StellarView`
2. 返回所属 `IReadOnlyArchitecture`
3. 在 `OnBind()` 中建立绑定
4. 在 `OnUnbind()` 中清理绑定

## 常见误用

- 在运行中动态调用 `RegisterModel` 或 `RegisterService`
- 视图直接依赖可变模型类型
- 在 `Disposed` 状态下继续复用旧实例
- 在未初始化前读取模型或服务
- 只读接口未继承 `IReadOnlyModel`，导致 `GetReadOnlyModel<T>()` 拿不到实例

## 测试与验证入口

当前架构行为主要通过以下方式间接验证：

- `ArchitectureDemo`
  验证 `Architecture / Model / Service / View / UI` 的基础协作链路
- `Samples`
  验证模块在架构容器中的接线方式
- 依赖架构的各 Kit 测试
  间接覆盖生命周期、查询和绑定调用链

如果后续补充专项测试，建议至少覆盖：

- 生命周期状态流转
- 重复注册保护
- 只读模型契约映射
- `Disposed` 后重新取 `Interface`
- 视图绑定/解绑时机
