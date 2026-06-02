# SingletonKit / 单例系统说明文档

SingletonKit 提供纯 C# 单例、MonoBehaviour 单例和构建前生成注册表。它适合框架服务、管理器和少量全局对象。

## 入口 API

- `Singleton<T>.Instance`：纯 C# 单例。
- `MonoSingleton<T>.Instance`：MonoBehaviour 单例。
- `SingletonFactory.GetSingleton<T>()`：统一获取入口。
- `SingletonFactory.Register(type, instance)`：注册实例。
- `SingletonFactory.Unregister(type, instance)`：反注册。
- `SingletonAttribute`：声明生命周期和创建策略。
- `SingletonLifeCycle`：单例生命周期。

## 使用模板

```csharp
public sealed class SaveService : Singleton<SaveService>
{
    public void Save() {}
}

SaveService.Instance.Save();
```

MonoBehaviour：

```csharp
public sealed class GameAudioRoot : MonoSingleton<GameAudioRoot>
{
}
```

## 常见问题

- 场景里重复实例：确认 MonoSingleton 生命周期和场景对象策略。
- 构建后单例找不到：确认 `Generated/SingletonRegister` 已生成并编译。
- 手动 new 了单例：优先从 `Instance` 获取。
