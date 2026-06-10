# SingletonKit / 单例系统说明文档

## 模块定位

`SingletonKit` 提供：

- 纯 C# 单例
- `MonoBehaviour` 单例
- 单例元数据注册与构建期生成

适合：

- 框架服务
- 管理器
- 少量稳定的全局对象

## 模块组成

- `Singleton<T>`
- `MonoSingleton<T>`
- `SingletonFactory`
- `SingletonAttribute`
- `SingletonLifeCycle`
- `SingletonMetadata`
- `Generated/SingletonRegister`

## 选择单例类型

### 纯 C# 单例

适合不需要 Unity 组件生命周期的服务，例如存档、配置、计算和状态管理。

- 继承 `Singleton<T>`
- 必须标记 `[Singleton]`
- 必须有公开无参构造，未显式声明构造函数即可
- 首次访问 `Instance` 时由生成注册表注入的创建器创建

```csharp
using StellarFramework;

[Singleton(lifeCycle: SingletonLifeCycle.Global)]
public sealed class SaveService : Singleton<SaveService>
{
    public void Save() {}
}
```

使用：

```csharp
SaveService.Instance.Save();
```

### MonoBehaviour 单例

适合需要 `Awake`、`OnDestroy`、协程、`Update`、组件引用或场景对象的管理器。

- 继承 `MonoSingleton<T>`
- 必须标记 `[Singleton]`
- `Awake()` 自动注册，`OnDestroy()` 自动反注册
- 可以是 `Global` 或 `Scene`

```csharp
using StellarFramework;

[Singleton(lifeCycle: SingletonLifeCycle.Global)]
public sealed class GameAudioRoot : MonoSingleton<GameAudioRoot>
{
}
```

### Global Mono prefab 样板

```csharp
using StellarFramework;

[Singleton(resourcePath: nameof(AudioRoot), lifeCycle: SingletonLifeCycle.Global, useContainer: false)]
public sealed class AudioRoot : MonoSingleton<AudioRoot>
{
}
```

Prefab 放置路径：`Assets/Resources/AudioRoot.prefab`。
`ResourcePath` 不包含 `Resources` 前缀，也不包含 `.prefab` 后缀；如果 prefab 放在子目录，则填写相对 `Resources` 的路径，例如 `Managers/AudioRoot`。

### Scene Mono 样板

```csharp
using StellarFramework;

[Singleton(lifeCycle: SingletonLifeCycle.Scene)]
public sealed class LevelDirector : MonoSingleton<LevelDirector>
{
    public void StartLevel()
    {
        // Start level flow here.
    }
}
```

`Scene` 单例必须预先挂在当前场景中的启用对象上。推荐在 `Start()` 或更晚阶段访问，避免访问顺序早于该对象的 `Awake()` 注册。

## 生命周期和加载方式

### `Global`

`Global` 单例会在首次访问 `Instance` 时自动创建，并跨场景保留。

当前 `Global` 加载链路是固定规则，不提供策略模式：

- 纯 C# 单例通过 `PureSingletonCreator` 创建。
- `MonoBehaviour` 且 `ResourcePath` 为空时，创建空 `GameObject` 并 `AddComponent`。
- `MonoBehaviour` 且 `ResourcePath` 非空时，通过 `Resources.Load<GameObject>(ResourcePath)` 加载 prefab 并实例化。
- `UseContainer=true` 时挂到 `[SingletonContainer]` 下。
- `UseContainer=false` 时单例对象自身 `DontDestroyOnLoad`。

设计取舍：单例对象通常很小，只承担管理器或服务入口职责，因此框架不额外引入 Addressables、AssetBundle 或自定义加载策略。若单例需要管理大型资源，应在单例初始化后通过 `ResKit`、Addressables 或 AssetBundle 加载业务资源。

### `Scene`

`Scene` 单例不会自动创建，必须预先放在当前场景中。场景对象执行 `Awake()` 时注册，切换场景时随场景销毁。未注册就访问 `Instance` 会报错并返回 `null`，框架不会执行 `FindObjectOfType`。

## 开发流程

1. 选择 `Singleton<T>` 或 `MonoSingleton<T>`。
2. 在类上添加 `[Singleton]` 特性。
3. 按需要配置 `lifeCycle`、`resourcePath`、`useContainer`。
4. 初始化逻辑优先写在 `OnSingletonInit()` 中。
5. `MonoSingleton` 子类重写 `Awake()` 或 `OnDestroy()` 时必须调用基类方法。
6. 新增或修改单例后，通过 Tools Hub 生成 `SingletonRegister`。
7. 构建 Player 前也会自动生成一次注册表。
8. 如果单例在新的 asmdef 中，确保生成注册表所在 asmdef 能引用该程序集。

## 生成注册表

运行时不通过反射读取 `[Singleton]`，也不通过反射创建纯 C# 单例。编辑器生成器会把有效 `[Singleton]` 类型写入 `Assets/StellarFramework/Generated/SingletonRegister/SingletonRegister.cs`。

手动生成入口：`StellarFramework -> Tools Hub -> SingletonKit 注册表 -> 立即生成 SingletonRegister`。

生成内容包括：`SingletonFactory.RegisterMetadata(...)` 和 `SingletonFactory.RegisterPureSingletonCreator(...)`。

## 运行规则

- 统一通过 `SingletonFactory` 获取和管理实例
- `MonoSingleton` 会在 `Awake / OnDestroy` 中自动注册和反注册
- 纯 C# 单例依赖静态创建器和元数据

## 使用约束

- 不手动 `new` 单例类型
- 统一通过 `Instance` 或 `SingletonFactory` 访问
- 场景单例必须保证场景里已有实例
- 生成相关文件必须参与编译
- 不在后台线程访问 `Instance`
- 不在应用退出阶段假设 `Instance` 一定非空
- 纯 C# 单例不要把带参数构造函数作为唯一构造函数
- `MonoSingleton` 子类重写 `Awake()` 或 `OnDestroy()` 时必须调用基类方法

## 常见问题

### `Instance` 返回 `null`

检查类上是否添加 `[Singleton]`，是否重新生成 `Generated/SingletonRegister`，目标类型所在 asmdef 是否被 `StellarFramework.Generated.SingletonRegister.asmdef` 引用。如果是 `Scene` 单例，还要检查场景中是否存在启用对象，且是否已经执行 `Awake()`。

### 构建后找不到单例

检查 `Generated/SingletonRegister/SingletonRegister.cs` 是否参与编译，生成表是否包含目标类型，目标类型是否在可被生成表引用的程序集里。

### Resources prefab 加载失败

检查 prefab 是否放在 `Assets/Resources` 下，`resourcePath` 是否省略了 `Resources` 前缀和 `.prefab` 后缀。Prefab 上未挂载目标组件时，框架会尝试自动 `AddComponent`。

### 场景里出现重复单例

`SingletonFactory.Register` 会保留已注册实例，并销毁后注册的重复 `MonoBehaviour` 对象。应检查场景、Prefab 和自动创建路径，避免同时放置多个同类型单例。

### 手动 new 了单例

应统一通过 `Instance` 获取。纯 C# 单例需要由生成注册表注入创建器，手动 `new` 会绕过 `SingletonFactory`、`OnSingletonInit()` 和重复实例保护。

## 相关文档

- [SingletonKit 源码文档](SingletonKit-单例系统-源码文档-Guide.md)
