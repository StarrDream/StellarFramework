using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Examples
{
    // 模拟业务数据结构
    [Serializable]
    public class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class LoginResponse
    {
        public int code;
        public string token;
        public string message;
    }

    [Serializable]
    public class UserProfile
    {
        public string uid;
        public string nickname;
        public string avatarUrl;
    }

    public class Example_Httpkit : MonoBehaviour
    {
        [Header("UI 引用")] public Image userAvatarImage;
        public RawImage bannerRawImage;

        [Header("配置")] public string apiBaseUrl = "https://api.example.com/v1";

        private void Start()
        {
            // 业务入口：模拟登录并获取用户信息
            ExecuteLoginFlowAsync().Forget();
        }

        private void OnDestroy()
        {
            // 业务层面的兜底清理，防止组件销毁后仍在下载
            HttpKit.Instance.CancelAllRequests();
        }

        #region 1. 核心业务流：POST 登录 -> 设置 Token -> GET 获取信息

        private async UniTaskVoid ExecuteLoginFlowAsync()
        {
            LogKit.Log("[HttpKitExample] 开始执行登录流程...");

            // 1. 发送 POST 请求进行登录
            var loginReq = new LoginRequest { username = "admin", password = "123" };
            string loginUrl = $"{apiBaseUrl}/login";

            var (loginRes, rawResponse) = await HttpKit.PostJsonAsync<LoginRequest, LoginResponse>(loginUrl, loginReq);

            if (!rawResponse.isSuccess || loginRes == null)
            {
                LogKit.LogError(
                    $"[HttpKitExample] 登录失败 | HTTP状态码: {rawResponse.responseCode} | 错误: {rawResponse.error}");
                return;
            }

            if (loginRes.code != 200)
            {
                LogKit.LogError($"[HttpKitExample] 业务登录失败 | 错误码: {loginRes.code} | 信息: {loginRes.message}");
                return;
            }

            LogKit.Log($"[HttpKitExample] 登录成功，获取到 Token: {loginRes.token}");

            // 2. 注册全局 Token，后续请求将自动在 Header 中注入 Authorization: Bearer {token}
            HttpKit.SetAuthToken(loginRes.token);

            // 3. 发送 GET 请求获取用户信息 (此时已自动携带 Token)
            string profileUrl = $"{apiBaseUrl}/user/profile";
            var (profile, profileResponse) = await HttpKit.GetJsonAsync<UserProfile>(profileUrl);

            if (!profileResponse.isSuccess || profile == null)
            {
                LogKit.LogError("[HttpKitExample] 获取用户信息失败");
                return;
            }

            LogKit.Log($"[HttpKitExample] 获取用户信息成功 | 昵称: {profile.nickname}");

            // 4. 驱动 UI 刷新
            RefreshUI(profile);
        }

        #endregion

        #region 2. UI 表现层：安全加载网络图片

        private void RefreshUI(UserProfile profile)
        {
            // 使用 HttpImageDownload 加载图片
            // 内部已通过 GetCancellationTokenOnDestroy 绑定了 UI 组件的生命周期
            // 若在下载过程中 userAvatarImage 被销毁，下载任务会自动安全取消，不会引发空指针

            if (userAvatarImage != null && !string.IsNullOrEmpty(profile.avatarUrl))
            {
                HttpImageDownload.DownloadToImageAsync(profile.avatarUrl, userAvatarImage).Forget();
            }

            string bannerUrl = "https://example.com/banner.png";
            if (bannerRawImage != null)
            {
                HttpImageDownload.DownloadToRawImageAsync(bannerUrl, bannerRawImage).Forget();
            }
        }

        #endregion

        #region 3. 大文件下载与防 OOM 机制

        public async UniTaskVoid DownloadGamePatchAsync(string patchUrl, string savePath)
        {
            LogKit.Log($"[HttpKitExample] 开始下载补丁: {patchUrl}");

            // 使用 DownloadFileAsync 直接将数据流写入磁盘，避免大文件撑爆堆内存
            bool success = await HttpKit.DownloadFileAsync(
                url: patchUrl,
                savePath: savePath,
                onProgress: (progress) =>
                {
                    // 进度回调 0.0 ~ 1.0
                    LogKit.Log($"[HttpKitExample] 下载进度: {progress * 100:F1}%");
                },
                timeout: 120
            );

            if (success)
            {
                LogKit.Log($"[HttpKitExample] 补丁下载完成，已保存至: {savePath}");
            }
            else
            {
                LogKit.LogError("[HttpKitExample] 补丁下载失败，请检查网络或存储空间");
            }
        }

        #endregion

        #region 4. 内存管理与请求控制

        public void OnReceiveLowMemoryWarn()
        {
            // 当系统触发低内存警告，或进行重度场景切换时，主动清理图片缓存
            // 内部会调用 Destroy 彻底释放 Texture2D 和 Sprite 占用的显存
            HttpImageDownload.ClearCache();
        }

        public void CancelSpecificRequest()
        {
            // 通过 URL 和 Method 精准取消某个正在进行的请求
            string targetUrl = $"{apiBaseUrl}/user/profile";
            HttpKit.Instance.CancelRequest(targetUrl, "GET");
        }

        #endregion
    }
}