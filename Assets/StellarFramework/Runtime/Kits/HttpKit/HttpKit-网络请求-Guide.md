# HttpKit / 网络请求

## 定位

`HttpKit` 是基于 `UnityWebRequest` 和 `UniTask` 的轻量请求入口，负责统一请求、取消、Token 注入和 JSON 解析。

`HttpImageDownload` 是配套的 UI 图片下载工具，负责远端图片加载、缓存与生命周期解绑。

## HttpKit

### 核心特性

- 自动 Token 注入
- 并发重复请求拦截
- 强类型 JSON 反序列化
- 大文件下载
- 请求取消

### 基础请求

```csharp
var response = await HttpKit.GetAsync("https://api.example.com/data");
if (response.isSuccess)
{
    Debug.Log(response.responseText);
}

var reqData = new LoginReq { User = "admin", Pass = "123" };
var (resData, rawRes) = await HttpKit.PostJsonAsync<LoginReq, LoginRes>(url, reqData);

if (rawRes.isSuccess && resData != null)
{
    HttpKit.SetAuthToken(resData.Token);
}
```

### 大文件下载

```csharp
bool success = await HttpKit.DownloadFileAsync(
    url: "https://example.com/patch.zip",
    savePath: "C:/Game/patch.zip",
    onProgress: progress => Debug.Log($"进度: {progress * 100}%"));
```

### 请求取消

```csharp
var http = HttpKit.Instance;
if (http != null)
{
    http.CancelRequest("https://api.example.com/data", "GET");
    http.CancelAllRequests();
}
```

### 去重语义

`preventDuplicate` 的语义是“拦截同 key 的新请求”，不是“自动复用第一个请求结果”。

适合：

- 防按钮连点
- 防重复提交

不适合：

- 多调用方共享同一个结果
- fan-out 请求汇聚

## HttpImageDownload

### UI 生命周期绑定

```csharp
public Image userAvatar;
public RawImage bannerImg;

async void ShowImages(string avatarUrl, string bannerUrl)
{
    await HttpImageDownload.DownloadToImageAsync(avatarUrl, userAvatar);
    await HttpImageDownload.DownloadToRawImageAsync(bannerUrl, bannerImg);
}
```

### 缓存清理

```csharp
HttpImageDownload.ClearCache();
HttpImageDownload.ClearCache("https://cdn.example.com/avatar.png");
```

### ClearCache 语义

- `ClearCache()`：清空当前图片缓存，并尝试中断仍在进行中的下载
- `ClearCache(url)`：只清理指定 URL 的缓存和在途任务

适合触发时机：

- 重度场景切换
- 低内存告警
- 大量远端图片页面退出

## 常见问题

### Q1: Token 会怎么注入？
默认使用 `Authorization: Bearer <token>`。如需改类型，可调用 `SetAuthToken(token, tokenType)`。

### Q2: 为什么取消了页面，图片下载还可能短时间继续？
图片下载底层有共享任务去重。调用方取消时会先解绑当前等待者；当没有等待者时，底层才会尝试停止共享下载。

### Q3: HttpKit 是不是完整网络层？
不是。它更像统一请求入口，不包含协议治理、重试策略、幂等控制或业务错误码体系。

## 对应示例

- 代码示例：[Example_Httpkit.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_Httpkit/Example_Httpkit.cs:1>)
  示例入口类为 `Example_HttpKit`。
