# HttpKit / 网络请求源码文档

## 源码位置

- `Runtime/Kits/HttpKit/HttpKit.cs`
- `Runtime/Kits/HttpKit/HttpImageDownload.cs`

## 核心类型

- `HttpResponse`：封装状态码、响应文本、错误、Header 等结果。
- `RequestConfig`：请求配置。
- `HttpKit`：MonoBehaviour 单例式请求入口，提供静态 async 和回调接口。
- `HttpImageDownload`：图片下载和缓存工具。

## 关键方法

- `HttpKit.Instance`：确保场景中存在请求承载对象。
- `SetAuthToken` / `GetAuthToken` / `ClearAuthToken`：维护全局认证头。
- `GetAsync`、`PostAsync`、`PutAsync`、`DeleteAsync`：构建 UnityWebRequest 并返回 `HttpResponse`。
- `GetJsonAsync<T>`、`PostJsonAsync<TRequest,TResponse>`：请求后进行 JSON 反序列化。
- `DownloadFileAsync`：下载文件并写入本地路径。
- `HttpImageDownload.DownloadTextureAsync`：下载 Texture2D 并缓存。

## 数据流

业务调用静态入口，HttpKit 创建 UnityWebRequest，合并 headers 和认证 token，发送请求，等待完成后把 UnityWebRequest 状态转换成 `HttpResponse`。JSON 接口在成功后反序列化响应体。图片工具下载 Texture 后缓存，并可应用到 Image 或 RawImage。

## 依赖关系

- 依赖 UnityWebRequest。
- 依赖 UniTask。
- JSON 接口依赖 Newtonsoft.Json。
- 图片接口依赖 Unity UI。

## 扩展点

- 新增默认 Header：在请求构建阶段统一合并。
- 新增重试策略：包裹发送逻辑，避免业务层重复写循环。
- 新增认证方式：扩展 tokenType 或 header 生成逻辑。
- 新增缓存策略：在 `HttpImageDownload` 中扩展 key 和失效策略。

## 测试入口

- 修改请求构建时，使用本地或测试 HTTP 服务验证 GET/POST/错误码。
- 修改图片下载时，验证 Image、RawImage、Sprite、Texture 缓存清理。
