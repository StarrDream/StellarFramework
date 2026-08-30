# Runtime Tools / 说明文档

`Runtime Tools` 存放可独立使用的运行时工具，不承载架构、业务流程或 Kit 间依赖。

当前包含：

- `CoroutineRunner`：为非 `MonoBehaviour` 代码提供常驻协程宿主。

`CoroutineRunner` 首次访问 `Instance` 或调用 `Run` 时自动创建，不依赖 SingletonKit、LogKit、UniTask 或其他 Kit。需要直接启动协程时，可使用：

```csharp
CoroutineRunner.Run(LoadRoutine());
```

它只用于提供协程承载点；新的异步业务链路仍可按项目需要使用 UniTask 或 ActionKit。
