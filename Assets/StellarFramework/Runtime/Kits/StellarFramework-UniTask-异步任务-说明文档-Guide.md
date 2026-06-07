# StellarFramework UniTask / 异步任务说明文档

`UniTask` 是框架统一推荐的异步任务方案，用来替代大部分基于 `Coroutine` 的异步流程。

## 使用原因

- 更少 GC
- `async / await` 结构更清晰
- 支持 `UniTask<T>` 返回值
- 可和 `CancellationToken` 结合，处理对象销毁后的异步取消

## 常见写法

### 等待一帧

```csharp
await UniTask.Yield();
```

### 等待时间

```csharp
await UniTask.Delay(1000);
```

### 使用销毁令牌

```csharp
await UniTask.Delay(1000, cancellationToken: destroyCancellationToken);
```

## 与框架模块的关系

- `ResKit`
  异步资源加载
- `UIKit`
  异步打开面板
- `HttpKit`
  网络请求
- `HotUpdateKit`
  热更新流程
- `ActionKit`
  等待动作链和插值流程
- `ConfigKit`
  异步配置加载

## 使用建议

- 事件回调里用 `UniTaskVoid + Forget()`
- 普通业务流程优先用 `UniTask` 或 `UniTask<T>`
- 注意对象销毁时使用 `CancellationToken`

## 相关文档

- [UniTask 源码文档](StellarFramework-UniTask-异步任务-源码文档-Guide.md)
