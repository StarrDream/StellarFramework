# SingletonKit / 单例系统源码文档

## 源码位置

- `Runtime/Kits/SingletonKit/ISingleton.cs`
- `Runtime/Kits/SingletonKit/Singleton.cs`
- `Runtime/Kits/SingletonKit/MonoSingleton.cs`
- `Runtime/Kits/SingletonKit/SingletonFactory.cs`
- `Runtime/Kits/SingletonKit/SingletonAttribute.cs`
- `Runtime/Kits/SingletonKit/SingletonMetadata.cs`
- `Runtime/Kits/SingletonKit/Editor/SingletonGenerator.cs`
- `Generated/SingletonRegister/SingletonRegister.cs`

## 核心类型

- `ISingleton`：单例标记接口。
- `Singleton<T>`：纯 C# 单例基类。
- `MonoSingleton<T>`：MonoBehaviour 单例基类。
- `SingletonFactory`：统一创建、注册、查询和清理单例。
- `SingletonAttribute`：声明单例生命周期和生成信息。
- `SingletonLifeCycle`：单例生命周期枚举。
- `SingletonMetadata`：生成后的元数据。
- `SingletonGenerator`：构建前扫描并生成注册代码。

## 关键方法

- `Singleton<T>.Instance` / `MonoSingleton<T>.Instance`：转发到 `SingletonFactory.GetSingleton<T>()`。
- `SingletonFactory.GetSingleton<T>`：优先返回已注册实例，否则按元数据或默认规则创建。
- `RegisterMetadata`：写入生成元数据。
- `RegisterPureSingletonCreator`：注册纯 C# 创建器。
- `Register` / `Unregister`：手动管理实例。
- `ClearAll`：清理所有单例。
- `SingletonGenerator.Generate`：扫描特性并重写生成文件。

## 数据流

运行时访问 `Instance`，请求进入 `SingletonFactory`。Factory 查询已注册实例；如果不存在，则根据生成元数据或默认构造创建。MonoSingleton 会创建或查找 GameObject 组件。构建前，`SingletonGenerator` 扫描带特性的类型并生成 `SingletonRegister`，减少运行时反射。

## 依赖关系

- MonoSingleton 依赖 Unity GameObject 和 MonoBehaviour。
- 生成器依赖 UnityEditor 构建前处理器。
- Generated 目录中的 SingletonRegister 依赖 SingletonFactory。
- AudioManager、ResMgr、SettingsManager 等多个 Kit 使用 SingletonKit。

## 扩展点

- 新增生命周期策略：扩展 `SingletonLifeCycle`、元数据和 Factory 处理逻辑。
- 新增生成字段：同步更新 `SingletonMetadata`、`SingletonGenerator` 和 Generated 文档。
- 新增单例类型：继承 `Singleton<T>` 或 `MonoSingleton<T>`，必要时加 `SingletonAttribute`。

## 测试入口

- 验证纯 C# 单例、MonoSingleton、手动注册、反注册、ClearAll。
- 修改生成器后检查 `Generated/SingletonRegister/SingletonRegister.cs` 编译通过。
