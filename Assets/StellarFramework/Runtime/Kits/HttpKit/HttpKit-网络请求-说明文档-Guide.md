# HttpKit / 网络请求说明文档

HttpKit 封装 UnityWebRequest，提供 async/await 和回调两套接口，适合普通 GET/POST/PUT/DELETE、JSON 请求和图片下载。

## 入口 API

- `HttpKit.SetAuthToken(token, tokenType)`：设置全局认证头。
- `HttpKit.ClearAuthToken()`：清理认证。
- `HttpKit.GetAsync(url, headers)`：GET 请求。
- `HttpKit.GetJsonAsync<T>(url, headers)`：GET 并反序列化 JSON。
- `HttpKit.PostAsync(url, jsonBody, headers)`：POST JSON。
- `HttpKit.PostJsonAsync<TRequest,TResponse>(url, data, headers)`：POST 并反序列化。
- `HttpKit.PutAsync(...)`、`DeleteAsync(...)`
- `HttpKit.DownloadFileAsync(url, savePath, progress)`
- `HttpImageDownload.DownloadTextureAsync(...)`、`DownloadSpriteAsync(...)`

## 使用模板

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Http;

public sealed class LoginApi
{
    public async UniTask LoginAsync()
    {
        HttpKit.SetAuthToken("token");
        (UserDto data, HttpResponse response) =
            await HttpKit.GetJsonAsync<UserDto>("https://example.com/user");
    }
}
```

## 常见问题

- 请求没有认证：确认先调用 `SetAuthToken`。
- JSON 解析失败：确认返回体和泛型类型匹配。
- 图片下载重复：`HttpImageDownload` 有缓存，必要时调用 `ClearCache`。

## 源码阅读

见 [HttpKit 源码文档](HttpKit-网络请求-源码文档-Guide.md)。
