# SingletonKit / 单例系统源码文档

## 模块职责

`SingletonKit` 负责统一管理纯 C# 单例和 `MonoBehaviour` 单例。

核心目标：

- 统一 `Instance` 获取入口
- 区分 `Global / Scene` 生命周期
- 用静态元数据替代运行时反射读取特性
- 限制主线程访问 Unity 相关单例

## 源码文件

- `Runtime/Kits/SingletonKit/ISingleton.cs`
- `Runtime/Kits/SingletonKit/Singleton.cs`
- `Runtime/Kits/SingletonKit/MonoSingleton.cs`
- `Runtime/Kits/SingletonKit/SingletonFactory.cs`
- `Runtime/Kits/SingletonKit/SingletonAttribute.cs`
- `Runtime/Kits/SingletonKit/SingletonMetadata.cs`

## 总体结构

```text
ISingleton
├─ Singleton<T>
└─ MonoSingleton<T>

SingletonFactory
├─ Instances
├─ MetadataCache
├─ PureSingletonCreators
└─ GlobalContainer
```

## 调用链

### 获取纯 C# 单例

1. 业务调用 `YourSingleton.Instance`
2. `Singleton<T>.Instance` 转发到 `SingletonFactory.GetSingleton<T>()`
3. 工厂读取 `SingletonMetadata`
4. 用 `PureSingletonCreators` 创建实例
5. 注册到 `Instances`
6. 调用 `OnSingletonInit()`

### 获取 Mono 单例

1. 业务调用 `YourMonoSingleton.Instance`
2. `MonoSingleton<T>.Instance` 转发到 `SingletonFactory.GetSingleton<T>()`
3. 工厂读取元数据
4. 按 `ResourcePath` 实例化预制体或创建空对象
5. 挂载组件并注册
6. `DontDestroyOnLoad`

### 场景单例注册

1. 场景对象 `Awake()`
2. `MonoSingleton<T>.Awake()` 调用 `SingletonFactory.Register(...)`
3. 注册成功后调用 `OnSingletonInit()`
4. 对象销毁时 `OnDestroy()` 反注册

## 类型详解

## `ISingleton`

### 作用

定义单例初始化回调契约。

### 方法

- `OnSingletonInit()`

## `Singleton<T>`

### 作用

纯 C# 单例基类。

### 成员

- `Instance`
  转发到 `SingletonFactory.GetSingleton<T>()`
- `OnSingletonInit()`
  首次注册成功后调用一次

## `MonoSingleton<T>`

### 作用

`MonoBehaviour` 单例基类。

### 字段 / 属性

- `IsInitialized`
  标记当前实例是否完成单例初始化

### 方法

- `Awake()`
  向 `SingletonFactory` 注册
- `OnDestroy()`
  反注册并清理初始化标记
- `OnSingletonInit()`
  设置 `IsInitialized = true`

## `SingletonLifeCycle`

### 枚举值

- `Global`
  自动创建，跨场景保留
- `Scene`
  不能自动创建，必须由场景对象主动注册

## `SingletonAttribute`

### 作用

声明单例配置。

### 字段

- `ResourcePath`
- `LifeCycle`
- `UseContainer`

运行时不依赖反射读取它，最终会转成 `SingletonMetadata`。

## `SingletonMetadata`

### 作用

运行时静态元数据载体。

### 字段

- `ResourcePath`
- `LifeCycle`
- `UseContainer`

## `SingletonFactory`

### 作用

单例系统核心工厂。

### 核心字段

- `Instances`
  已注册单例实例
- `MetadataCache`
  类型到元数据映射
- `PureSingletonCreators`
  纯 C# 单例创建器
- `Locker`
  线程锁
- `_globalContainer`
  全局容器对象
- `_isQuitting`
  应用退出标记
- `_mainThreadId`
  主线程 ID

### 关键方法

#### `RegisterMetadata(...)`

注册静态元数据。

#### `RegisterPureSingletonCreator(...)`

注册纯 C# 单例创建器。

#### `GetSingleton<T>()`

统一实例获取入口。

职责：

- 退出阶段返回 `null`
- 校验主线程访问
- 清理已失效 Unity 对象引用
- 读取元数据
- 自动创建全局单例或返回场景单例

#### `Register(...)`

注册实例并调用 `OnSingletonInit()`。

会拦截重复单例：

- `MonoBehaviour` 重复时保留旧实例并销毁新对象
- 纯 C# 重复时直接报错

#### `Unregister(...)`

在实例销毁时反注册。

#### `TryGetRegisteredSingleton<T>(out T instance)`

仅查询，不触发创建。

#### `CreateGlobalInstance<T>(...)`

全局单例自动创建逻辑。

行为：

- 纯 C# 走 `PureSingletonCreators`
- `MonoBehaviour` 走预制体实例化或空对象创建
- 可选择挂到 `[SingletonContainer]`

## 设计约束

- Unity 相关单例只能在主线程访问
- 场景单例不会自动创建
- 纯 C# 单例禁止运行时反射实例化
- 缺少静态元数据时在开发期直接断言

## 常见误用

- 场景单例未放进场景就访问 `Instance`
- 没有注册 `PureSingletonCreator`
- 后台线程访问 Unity 单例

## 测试建议

- 全局单例自动创建
- 场景单例未注册报错
- 重复单例注册拦截
- 主线程限制
- 应用退出阶段访问行为
