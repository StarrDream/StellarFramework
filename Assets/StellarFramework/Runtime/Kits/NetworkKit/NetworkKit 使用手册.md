# NetworkKit 使用手册

## 1. UnityHttpUtils (HTTP 请求)

基于 `UnityWebRequest` 和 `UniTask` 的封装，支持 RESTful API。

### 特性
*   **自动 Token 注入**：登录后设置一次 Token，后续请求自动带上 Authorization 头。
*   **防重复请求**：同一 URL + Body 的请求在未返回前会被拦截。
*   **强类型响应**：支持泛型直接反序列化 JSON。

### 使用示例
```csharp
// 1. GET 请求
var response = await UnityHttpUtils.GetAsync("https://api.com/data");
if (response.isSuccess) {
    Debug.Log(response.responseText);
}

// 2. POST JSON (自动序列化)
var reqData = new LoginReq { User = "admin", Pass = "123" };
var (resData, rawRes) = await UnityHttpUtils.PostJsonAsync<LoginReq, LoginRes>(url, reqData);

if (rawRes.isSuccess) {
    Debug.Log($"登录成功, Token: {resData.Token}");
    // 设置全局 Token
    UnityHttpUtils.SetAuthToken(resData.Token);
}
```

---

## 2. NetworkImageDownload (图片加载)

专为 UI 图片加载设计，带有内存缓存。

### 使用示例
```csharp
public RawImage avatarImg;

async void ShowAvatar(string url)
{
    // 自动绑定 RawImage 生命周期
    // 如果图片还没下载完，avatarImg 就被销毁了，下载任务会自动取消
    await NetworkImageDownload.DownloadToRawImageAsync(url, avatarImg);
}
```

### 缓存策略
*   **内存缓存**：下载过的图片会缓存在 `Dictionary<string, Texture2D>` 中。
*   **清理**：在低内存警告或切场景时，调用 `NetworkImageDownload.ClearCache()` 释放内存。

