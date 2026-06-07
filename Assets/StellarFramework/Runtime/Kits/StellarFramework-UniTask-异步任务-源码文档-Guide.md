# StellarFramework UniTask / 异步任务源码文档

## 模块职责

这份文档不是描述某个独立运行时代码包的内部实现，而是描述 `UniTask` 在框架源码中的接入位置和统一使用约定。

## 源码文件

`UniTask` 本身不是当前仓库自研模块，但框架内的主要接入点包括：

- `ResKit`
- `UIKit`
- `HttpKit`
- `HotUpdateKit`
- `ActionKit`
- `ConfigKit`

## 总体结构

```text
UniTask Usage
├─ 资源异步加载
├─ 面板异步打开
├─ 网络请求
├─ 热更新流程
└─ 动作链与等待
```

## 主要接入点

- `ResKit`
  `LoadAsync<T>`、`PreloadAsync(...)`
- `UIKit`
  `InitAsync()`、`OpenAsync<T>()`、`PreloadAsync<T>()`
- `HttpKit`
  `GetAsync / PostAsync / DownloadFileAsync`
- `HotUpdateKit`
  `InitializeAsync / CheckResourceUpdatesAsync / DownloadResourceUpdatesAsync / RunStartupHotUpdateAsync`
- `ActionKit`
  动作链等待与异步动作执行
- `ConfigKit`
  `LoadConfigAsync(...)`

## 使用约定

- 生命周期入口优先使用 `UniTask` / `UniTask<T>`
- 事件型回调用 `UniTaskVoid`
- 与 `GameObject` 绑定的流程优先传入 `CancellationToken`
- 不推荐继续扩展基于协程的主异步链路

## 常见模式

### 异步资源加载

```csharp
GameObject prefab = await loader.LoadAsync<GameObject>(path, destroyCancellationToken);
```

### 面板异步打开

```csharp
await UIKit.OpenAsync<LoginPanel>(data);
```

### 网络请求

```csharp
HttpResponse response = await HttpKit.GetAsync(url);
```

## 设计约束

- 框架的主要异步 API 都以 `UniTask` 为标准
- 取消令牌应尽量和对象生命周期绑定
- 不把 `UniTask` 再包回复杂的协程桥接层
