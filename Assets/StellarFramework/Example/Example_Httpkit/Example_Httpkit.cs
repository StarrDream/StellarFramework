using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Examples
{
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

    /// <summary>
    /// HttpKit 综合使用示例
    /// </summary>
    public class Example_Httpkit : MonoBehaviour
    {
        [Header("UI 引用")] public Image userAvatarImage;

        public RawImage bannerRawImage;

        [Header("配置")] public string apiBaseUrl = "https://api.example.com/v1";

        private bool _isDisposed;

        private void Start()
        {
            ExecuteLoginFlowAsync().Forget();
        }

        private void OnDestroy()
        {
            _isDisposed = true;
        }

        #region 1. 核心业务流：POST 登录 -> 设置 Token -> GET 获取信息

        private async UniTaskVoid ExecuteLoginFlowAsync()
        {
            LogKit.Log("[Example_Httpkit] 开始执行登录流程...");

            LoginRequest loginReq = new LoginRequest
            {
                username = "admin",
                password = "123"
            };

            string loginUrl = $"{apiBaseUrl}/login";
            (LoginResponse loginRes, HttpResponse rawResponse) =
                await HttpKit.PostJsonAsync<LoginRequest, LoginResponse>(loginUrl, loginReq);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (!rawResponse.isSuccess || loginRes == null)
            {
                LogKit.LogError(
                    $"[Example_Httpkit] 登录失败 | HTTP状态码={rawResponse.responseCode} | Error={rawResponse.error}");
                return;
            }

            if (loginRes.code != 200)
            {
                LogKit.LogError($"[Example_Httpkit] 业务登录失败 | Code={loginRes.code} | Message={loginRes.message}");
                return;
            }

            LogKit.Log($"[Example_Httpkit] 登录成功，获取到 Token: {loginRes.token}");
            HttpKit.SetAuthToken(loginRes.token);

            string profileUrl = $"{apiBaseUrl}/user/profile";
            (UserProfile profile, HttpResponse profileResponse) = await HttpKit.GetJsonAsync<UserProfile>(profileUrl);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (!profileResponse.isSuccess || profile == null)
            {
                LogKit.LogError(
                    $"[Example_Httpkit] 获取用户信息失败 | HTTP状态码={profileResponse.responseCode} | Error={profileResponse.error}");
                return;
            }

            LogKit.Log($"[Example_Httpkit] 获取用户信息成功 | 昵称={profile.nickname}");
            RefreshUI(profile);
        }

        #endregion

        #region 2. UI 表现层：安全加载网络图片

        private void RefreshUI(UserProfile profile)
        {
            if (profile == null)
            {
                LogKit.LogError("[Example_Httpkit] RefreshUI 失败: profile 为空");
                return;
            }

            if (userAvatarImage != null && !string.IsNullOrEmpty(profile.avatarUrl))
            {
                HttpImageDownload.DownloadToImageAsync(userAvatarImage, profile.avatarUrl).Forget();
            }

            string bannerUrl = "https://example.com/banner.png";
            if (bannerRawImage != null)
            {
                HttpImageDownload.DownloadToRawImageAsync(bannerRawImage, bannerUrl).Forget();
            }
        }

        #endregion

        #region 3. 大文件下载与防 OOM 机制

        public async UniTaskVoid DownloadGamePatchAsync(string patchUrl, string savePath)
        {
            if (string.IsNullOrEmpty(patchUrl) || string.IsNullOrEmpty(savePath))
            {
                LogKit.LogError(
                    $"[Example_Httpkit] DownloadGamePatchAsync 失败: patchUrl 或 savePath 为空, PatchUrl={patchUrl}, SavePath={savePath}");
                return;
            }

            LogKit.Log($"[Example_Httpkit] 开始下载补丁: {patchUrl}");

            bool success = await HttpKit.DownloadFileAsync(
                url: patchUrl,
                savePath: savePath,
                onProgress: progress => { LogKit.Log($"[Example_Httpkit] 下载进度: {progress * 100f:F1}%"); },
                timeout: 120);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (success)
            {
                LogKit.Log($"[Example_Httpkit] 补丁下载完成，已保存至: {savePath}");
            }
            else
            {
                LogKit.LogError("[Example_Httpkit] 补丁下载失败，请检查网络或存储空间");
            }
        }

        #endregion

        #region 4. 内存管理与请求控制

        public void OnReceiveLowMemoryWarn()
        {
            HttpImageDownload.ClearCache();
        }

        public void CancelSpecificRequest()
        {
            string targetUrl = $"{apiBaseUrl}/user/profile";
            HttpKit.Instance.CancelRequest(targetUrl, "GET");
        }

        #endregion
    }
}