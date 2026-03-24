# ResKit Addressables (AA) 实战与热更指南
**版本**: v2.0 (HotUpdate Ready)  
**适用**: StellarFramework ResKit 模块

## 1. 环境配置 (Setup)
1.  通过 Package Manager 安装 **Addressables** 插件。
2.  在 `Project Settings -> Player -> Scripting Define Symbols` 中添加宏：`UNITY_ADDRESSABLES`。
3.  在 Addressables Groups 窗口中配置资源的 Address Name。

---

## 2. 基础加载工作流
使用 `AddressableLoader` 进行资源加载。

```csharp
public class AATest : MonoBehaviour
{
    private ResLoader _loader;

    void Start()
    {
        _loader = ResKit.Allocate<AddressableLoader>();
        LoadAsset();
    }

    async void LoadAsset()
    {
        // 传入 Address Name
        var prefab = await _loader.LoadAsync<GameObject>("Hero_Address");
        if (prefab != null) Instantiate(prefab);
    }

    void OnDestroy()
    {
        ResKit.Recycle(_loader);
    }
}
```

---

## 3. 商业级热更工作流 (Hot Update)
框架内置了 `AddressableHotUpdateManager`，提供了完整的 Catalog 更新与资源预下载工作流。

### 3.1 检查更新与获取体积
在游戏启动（如 Login 界面前）调用，用于判断是否需要弹窗提示玩家下载。

```csharp
async UniTask CheckUpdate()
{
    // 传入 null 默认检查所有 Catalog，或者传入特定的 Label (如 "default")
    UpdateCheckResult result = await AddressableHotUpdateManager.Instance.CheckUpdateAsync(new[] { "default" });

    if (result.HasUpdate)
    {
        float sizeMB = result.TotalDownloadSize / 1048576f;
        Debug.Log($"发现新版本，需要下载: {sizeMB:F2} MB");
        // 此时可以弹出 UI 询问玩家是否下载
    }
    else
    {
        Debug.Log("当前已是最新版本，直接进入游戏。");
    }
}
```

### 3.2 执行下载与进度回调
玩家点击确认后，执行下载并更新进度条。

```csharp
using System.Threading;

private CancellationTokenSource _downloadCts;

async UniTask StartDownload()
{
    _downloadCts = new CancellationTokenSource();

    bool success = await AddressableHotUpdateManager.Instance.DownloadUpdateAsync(
        keys: new[] { "default" },
        onProgress: (progress) => 
        {
            Debug.Log($"下载进度: {progress * 100:F1}%");
            // 更新 UI 进度条...
        },
        cancellationToken: _downloadCts.Token
    );

    if (success)
    {
        Debug.Log("下载完成，进入游戏！");
    }
    else
    {
        Debug.LogError("下载失败或被取消，请重试。");
    }
}

public void CancelDownload()
{
    _downloadCts?.Cancel();
}
```

---

## 4. 常见问题 (FAQ)
### Q1: 为什么真机上没有触发热更？
*   **检查 1**: 确保 Addressables Groups 中的 `Build Path` 和 `Load Path` 设置为 Remote。
*   **检查 2**: 确保在 Addressables Settings 中勾选了 `Build Remote Catalog`。
*   **检查 3**: 确保你的远端服务器 (如 OSS/CDN) 上的 `catalog.json` 和 `.hash` 文件已更新。

### Q2: 报错 "InvalidKeyException"
*   **原因**: 代码里传的 path 和 Groups 窗口里的 Address Name 不一致。
*   **解决**: 检查拼写。注意 AA 的 Address Name 是自定义字符串，不要带不必要的后缀。