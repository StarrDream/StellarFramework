# HttpKit 使用手册

## 1. HttpKit (核心 HTTP 请求)
基于 `UnityWebRequest` 和 `UniTask` 的封装，支持 RESTful API。

### 核心特性
*   **自动 Token 注入**：设置全局 Token 后，后续请求可自动在 Header 注入 `Authorization`。
*   **防重复请求**：基于 `Method::URL::BodyHash` 生成标识，可拦截并发重复请求。
*   **强类型响应**：支持泛型直接反序列化 JSON。
*   **大文件下载**：支持直接将数据流写入磁盘，减少内存占用。
*   **生命周期管理**：结合 `CancellationTokenSource`，支持取消请求。

### 使用示例

#### 基础 API 请求
```csharp
// 1. GET 请求
var response = await HttpKit.GetAsync("https://api.example.com/data");
if (response.isSuccess) 
{
    Debug.Log(response.responseText);
}

// 2. POST JSON (自动序列化与反序列化)
var reqData = new LoginReq { User = "admin", Pass = "123" };
var (resData, rawRes) = await HttpKit.PostJsonAsync<LoginReq, LoginRes>(url, reqData);

if (rawRes.isSuccess && resData != null) 
{
    Debug.Log($"登录成功, Token: {resData.Token}");
    // 设置全局 Token
    HttpKit.SetAuthToken(resData.Token);
}
```

#### 大文件下载
```csharp
// 直接写入磁盘
bool success = await HttpKit.DownloadFileAsync(
    url: "https://example.com/patch.zip",
    savePath: "C:/Game/patch.zip",
    onProgress: (progress) => Debug.Log($"进度: {progress * 100}%")
);
```

#### 请求控制
```csharp
// 取消特定请求
HttpKit.Instance.CancelRequest("https://api.example.com/data", "GET");

// 取消所有活跃请求 (通常在切场景或退出时调用)
HttpKit.Instance.CancelAllRequests();
```

---

## 2. HttpImageDownload (UI 图片加载)
专为 UI 图片加载设计，内置 `Texture2D` 与 `Sprite` 缓存。

### 使用示例

#### 绑定 UI 生命周期
```csharp
public Image userAvatar;
public RawImage bannerImg;

async void ShowImages(string avatarUrl, string bannerUrl)
{
    // 内部绑定了组件的 GetCancellationTokenOnDestroy()
    await HttpImageDownload.DownloadToImageAsync(avatarUrl, userAvatar);
    
    await HttpImageDownload.DownloadToRawImageAsync(bannerUrl, bannerImg);
}
```

### 缓存与显存管理策略
*   **内存缓存**：下载过的图片会缓存在 `Dictionary<string, Texture2D>` 与 `Dictionary<string, Sprite>` 中。
*   **显存释放规范**：在收到系统低内存警告或进行重度场景切换时，建议调用 `HttpImageDownload.ClearCache()`。该方法会显式调用 `UnityEngine.Object.Destroy` 释放底层非托管显存。

```csharp
// 清理图片缓存并释放显存
HttpImageDownload.ClearCache();
```