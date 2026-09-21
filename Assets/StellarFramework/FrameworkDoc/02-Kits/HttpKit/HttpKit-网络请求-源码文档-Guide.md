# HttpKit / 网络请求源码文档

## 模块职责

`HttpKit` 提供统一的 HTTP 请求入口和图片下载工具。

主要负责：

- GET / POST / PUT / DELETE 请求
- JSON 序列化与反序列化
- 请求去重与取消控制
- Token 注入
- 文件下载
- 纹理和 Sprite 下载缓存

## 源码文件

- `Runtime/Kits/HttpKit/HttpKit.cs`
- `Runtime/Kits/HttpKit/HttpImageDownload.cs`

## 总体结构

```text
HttpKit
├─ HttpResponse
├─ RequestConfig
├─ _activeCTS
└─ SendRequestAsync(...)

HttpImageDownload
├─ TextureCache
├─ SpriteCache
└─ OngoingTasks
```

## 类型详解

## `HttpResponse`

### 作用

封装一次 HTTP 请求的结果。

### 字段

- `isSuccess`
- `responseCode`
- `responseText`
- `error`
- `headers`

### 方法

- `Deserialize<T>()`
- `TryDeserialize<T>(out T result)`

## `RequestConfig`

### 作用

请求配置对象。

### 字段

- `autoInjectToken`
- `headers`
- `onProgress`
- `preventDuplicate`
- `timeout`

## `HttpKit`

### 作用

HTTP 系统核心单例实现。

### 关键字段

- `_instance`
- `_isQuitting`
- `_activeCTS`
- `_requestLock`
- `_authToken`
- `_tokenType`

### 关键方法

#### `GetOrCreateInstance()`

创建或获取运行时单例。

#### `SetAuthToken / GetAuthToken / ClearAuthToken / HasAuthToken`

维护鉴权 Token。

#### `SendRequestAsync(...)`

统一请求主链路。

职责：

- 校验 URL 和配置
- 生成请求唯一 Key
- 按配置拦截重复请求
- 创建 `CancellationTokenSource`
- 构建 `UnityWebRequest`
- 发起请求并处理结果
- 请求结束后清理 pending 表

#### `CreateWebRequest(...)`

创建底层 `UnityWebRequest`，注入：

- 请求方法
- Body
- 默认请求头
- Token
- 超时设置

#### `ProcessResponse(...)`

把 `UnityWebRequest` 转成 `HttpResponse`。

#### `GenerateRequestKey(...)`

基于 `Method + Url + BodyHash` 生成去重键。

#### `CancelRequest(...) / CancelAllRequests()`

取消指定请求或全部请求。

### 对外 API

- `GetAsync(...)`
- `GetJsonAsync<T>(...)`
- `PostAsync(...)`
- `PostJsonAsync<TRequest, TResponse>(...)`
- `PutAsync(...)`
- `DeleteAsync(...)`
- Fire-and-forget 包装版本

## `HttpImageDownload`

### 作用

负责图片资源下载与缓存。

### 内部类型

#### `CacheEntry<T>`

字段：

- `Url`
- `Asset`
- `LastAccessTick`

#### `OngoingDownload`

字段：

- `SharedCts`
- `CompletionSource`
- `WaiterCount`
- `IsCompleted`

### 核心缓存

- `TextureCache`
- `SpriteCache`
- `OngoingTasks`

### 关键方法

- `DownloadTextureAsync(...)`
- `DownloadSpriteAsync(...)`
- `DownloadToImageAsync(...)`
- `DownloadToRawImageAsync(...)`
- `ClearCache()`
- `ClearCache(string imageUrl)`

### 内部方法

- `DownloadTextureInternalAsync(...)`
- `TryGetCachedTexture(...)`
- `TryGetCachedSprite(...)`
- `AddTextureCache(...)`
- `AddSpriteCache(...)`
- `TrimTextureCacheIfNeeded()`
- `TrimSpriteCacheIfNeeded()`
- `RunOngoingDownloadAsync(...)`

## 设计约束

- `HttpKit` 在退出阶段不可用
- 重复请求拦截依赖 `requestKey`
- 图片下载缓存使用简单 LRU 剪枝
- 同 URL 并发图片下载会复用同一个进行中任务

## 常见误用

- 退出阶段继续发请求
- 开启 `preventDuplicate` 但 body 不稳定
- 图片下载后不清理长生命周期缓存
