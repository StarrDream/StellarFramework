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

## 两类单例

### 纯 C# 单例

```csharp
public sealed class SaveService : Singleton<SaveService>
{
    public void Save() {}
}

SaveService.Instance.Save();
```

### MonoBehaviour 单例

```csharp
public sealed class GameAudioRoot : MonoSingleton<GameAudioRoot>
{
}
```

## 生命周期模式

- `Global`
  自动创建、跨场景保留
- `Scene`
  依赖场景中已有实例，不自动创建

## 运行规则

- 统一通过 `SingletonFactory` 获取和管理实例
- `MonoSingleton` 会在 `Awake / OnDestroy` 中自动注册和反注册
- 纯 C# 单例依赖静态创建器和元数据

## 使用约束

- 不手动 `new` 单例类型
- 统一通过 `Instance` 或 `SingletonFactory` 访问
- 场景单例必须保证场景里已有实例
- 生成相关文件必须参与编译

## 常见问题

- 场景里重复实例
  检查生命周期模式和场景对象配置。
- 构建后找不到单例
  检查 `Generated/SingletonRegister` 是否生成并编译通过。
- 手动 new 了单例
  应统一通过 `Instance` 获取。

## 相关文档

- [SingletonKit 源码文档](SingletonKit-单例系统-源码文档-Guide.md)
