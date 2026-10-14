# HttpKit / 网络请求说明文档

HttpKit 封装 UnityWebRequest，提供 async/await 和回调两套接口，适合普通 GET/POST/PUT/DELETE、JSON 请求和图片下载。

按需导出中，`HttpKit` 包含请求、纹理和 Sprite 下载；`HttpKit.UnityUGUIAdapter` 另行补齐 UGUI 依赖，启用 Image / RawImage 绑定接口。基础 HttpKit 可以在未安装 UGUI 的项目中使用。

## 入口 API

- `HttpKit.SetAuthToken(token, tokenType)`：设置全局认证头。
- `HttpKit.SetAuthContext(scope, token)`：同时切换账号作用域与 Token，登录和换账号优先使用。
- `HttpKit.ClearAuthToken()`：清理认证。
- `HttpKit.SetRequestScope(scope)`：切换账号或会话作用域，默认取消旧会话的活动请求。
- `HttpKit.SendAsync(method, url, body, config)`：使用完整配置发送请求，包括去重和单次请求作用域。
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
using StellarFramework;

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

## 换账号与去重

去重默认关闭。启用 `preventDuplicate` 后，只有作用域、HTTP 方法、地址、请求体和最终请求头都相同的活动请求才会被拦截。不同 Token、Cookie 或语言请求头不会共用去重状态；请求头名称忽略大小写，排列顺序不影响匹配。认证值参与哈希，不会出现在去重键中。

登录或切换账号时，用 `SetAuthContext` 同时设置作用域与 Token，避免取消回调中启动的新请求拿到旧 Token。退出登录时将游客作用域与空 Token 一起设置。上下文切换默认取消旧会话的活动请求；这些请求返回 `isSuccess = false`、`error = "Request Cancelled"`。业务层应检查成功状态后再更新账号数据。

```csharp
HttpKit.SetAuthContext("account:" + userId, accessToken);

HttpResponse response = await HttpKit.SendAsync("GET", apiUrl, config: new RequestConfig
{
    preventDuplicate = true
});

// 退出账号
HttpKit.SetAuthContext("guest", null);
```

多个账号同时请求时，可在每次请求的 `RequestConfig.requestScope` 中显式指定账号，并在 `headers` 中传入各自的认证头。不要为这种场景反复切换全局 Token。显式 `Authorization`（包括小写 `authorization`）会覆盖自动注入。

使用 Cookie 或无 Token 的认证方式时可用 `SetRequestScope`，作用域改变时会清除旧的全局 Token。单独更新 `SetAuthToken` 用于同账号刷新 Token；去重会使用新认证头，但该兼容入口不自动取消旧请求。

`CancelRequest` 也按同一作用域和请求头匹配。发送时用了自定义配置，取消时就传入同一份配置；修改共享配置不会改变已经发送的请求。

图片下载的共享任务和缓存同样按作用域、URL 与有效请求头隔离。默认图片接口保持不注入认证头；私有图片可调用接受 `RequestConfig` 的重载。切换全局作用域默认也会取消共享图片下载并清理图片缓存。

## 常见问题

- 请求没有认证：确认先调用 `SetAuthToken`。
- JSON 解析失败：确认返回体和泛型类型匹配。
- 图片下载重复：`HttpImageDownload` 有缓存，必要时调用 `ClearCache`。

## 源码阅读

见 [HttpKit 源码文档](HttpKit-网络请求-源码文档-Guide.md)。
