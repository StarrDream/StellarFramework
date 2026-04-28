using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Examples
{
    [Serializable]
    public class DemoLoginRequest
    {
        public string username;
        public string password;
        public int expiresInMins = 30;
    }

    [Serializable]
    public class DemoLoginResponse
    {
        public int id;
        public string username;
        public string email;
        public string firstName;
        public string lastName;
        public string image;
        public string accessToken;
        public string refreshToken;
    }

    [Serializable]
    public class DemoUserProfile
    {
        public int id;
        public string username;
        public string firstName;
        public string lastName;
        public string email;
        public string image;
    }

    /// <summary>
    /// HttpKit example scene entry.
    /// </summary>
    public class Example_HttpKit : MonoBehaviour
    {
        private const string DefaultBannerUrl =
            "https://dummyjson.com/image/640x240/0f172a/e2e8f0?text=StellarFramework+HttpKit";

        [Header("UI References")]
        public Image userAvatarImage;

        public RawImage bannerRawImage;

        [Header("Config")]
        public string apiBaseUrl = "https://dummyjson.com";

        private bool _isDisposed;
        private Texture2D _avatarFallbackTexture;
        private Texture2D _bannerFallbackTexture;
        private Sprite _avatarFallbackSprite;

        private void Start()
        {
            ApplyOfflinePreview();
            ExecuteLoginFlowAsync().Forget();
        }

        private void OnDestroy()
        {
            _isDisposed = true;
            HttpKit.ClearAuthToken();
            CancelSpecificRequest();
            ReleaseFallbackPreview();
        }

        private async UniTaskVoid ExecuteLoginFlowAsync()
        {
            LogKit.Log("[Example_HttpKit] Starting login flow.");

            DemoLoginRequest loginReq = new DemoLoginRequest
            {
                username = "emilys",
                password = "emilyspass"
            };

            string loginUrl = $"{apiBaseUrl}/auth/login";
            (DemoLoginResponse loginRes, HttpResponse rawResponse) =
                await HttpKit.PostJsonAsync<DemoLoginRequest, DemoLoginResponse>(loginUrl, loginReq);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (!rawResponse.isSuccess || loginRes == null)
            {
                LogKit.LogError(
                    $"[Example_HttpKit] Login failed | Code={rawResponse.responseCode} | Error={rawResponse.error}");
                ApplyOfflineFallback("login request failed");
                return;
            }

            if (string.IsNullOrEmpty(loginRes.accessToken))
            {
                LogKit.LogError("[Example_HttpKit] Login failed: accessToken is empty.");
                ApplyOfflineFallback("login response missing token");
                return;
            }

            LogKit.Log($"[Example_HttpKit] Login success: {loginRes.username}");
            HttpKit.SetAuthToken(loginRes.accessToken);

            string profileUrl = $"{apiBaseUrl}/auth/me";
            (DemoUserProfile profile, HttpResponse profileResponse) =
                await HttpKit.GetJsonAsync<DemoUserProfile>(profileUrl);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (!profileResponse.isSuccess || profile == null)
            {
                LogKit.LogError(
                    $"[Example_HttpKit] Profile fetch failed | Code={profileResponse.responseCode} | Error={profileResponse.error}");
                ApplyOfflineFallback("profile request failed");
                return;
            }

            LogKit.Log($"[Example_HttpKit] Profile loaded: {profile.firstName} {profile.lastName}");
            RefreshUI(profile);
        }

        private void RefreshUI(DemoUserProfile profile)
        {
            if (profile == null)
            {
                LogKit.LogError("[Example_HttpKit] RefreshUI failed: profile is null.");
                return;
            }

            ApplyOfflinePreview();

            if (userAvatarImage != null && !string.IsNullOrEmpty(profile.image))
            {
                HttpImageDownload.DownloadToImageAsync(userAvatarImage, profile.image).Forget();
            }

            if (bannerRawImage != null)
            {
                HttpImageDownload.DownloadToRawImageAsync(bannerRawImage, DefaultBannerUrl).Forget();
            }
        }

        public async UniTaskVoid DownloadGamePatchAsync(string patchUrl, string savePath)
        {
            if (string.IsNullOrEmpty(patchUrl) || string.IsNullOrEmpty(savePath))
            {
                LogKit.LogError(
                    $"[Example_HttpKit] DownloadGamePatchAsync failed: PatchUrl={patchUrl}, SavePath={savePath}");
                return;
            }

            LogKit.Log($"[Example_HttpKit] Start downloading patch: {patchUrl}");

            bool success = await HttpKit.DownloadFileAsync(
                url: patchUrl,
                savePath: savePath,
                onProgress: progress => { LogKit.Log($"[Example_HttpKit] Download progress: {progress * 100f:F1}%"); },
                timeout: 120);

            if (_isDisposed || this == null)
            {
                return;
            }

            if (success)
            {
                LogKit.Log($"[Example_HttpKit] Patch download completed: {savePath}");
            }
            else
            {
                LogKit.LogError("[Example_HttpKit] Patch download failed.");
            }
        }

        public void OnReceiveLowMemoryWarn()
        {
            HttpImageDownload.ClearCache();
        }

        public void CancelSpecificRequest()
        {
            string targetUrl = $"{apiBaseUrl}/auth/me";
            HttpKit http = HttpKit.Instance;
            if (http != null)
            {
                http.CancelRequest(targetUrl, "GET");
            }
        }

        private void ApplyOfflineFallback(string reason)
        {
            LogKit.LogWarning($"[Example_HttpKit] Using offline preview because {reason}.");

            RefreshUI(new DemoUserProfile
            {
                id = -1,
                username = "offline.demo",
                firstName = "Offline",
                lastName = "Preview",
                email = "offline@example.local",
                image = string.Empty
            });
        }

        private void ApplyOfflinePreview()
        {
            if (userAvatarImage != null)
            {
                _avatarFallbackTexture ??= CreateAvatarTexture();
                _avatarFallbackSprite ??= Sprite.Create(
                    _avatarFallbackTexture,
                    new Rect(0f, 0f, _avatarFallbackTexture.width, _avatarFallbackTexture.height),
                    new Vector2(0.5f, 0.5f));
                userAvatarImage.sprite = _avatarFallbackSprite;
                userAvatarImage.color = Color.white;
            }

            if (bannerRawImage != null)
            {
                _bannerFallbackTexture ??= CreateBannerTexture();
                bannerRawImage.texture = _bannerFallbackTexture;
                bannerRawImage.color = Color.white;
            }
        }

        private void ReleaseFallbackPreview()
        {
            if (_avatarFallbackSprite != null)
            {
                Destroy(_avatarFallbackSprite);
                _avatarFallbackSprite = null;
            }

            if (_avatarFallbackTexture != null)
            {
                Destroy(_avatarFallbackTexture);
                _avatarFallbackTexture = null;
            }

            if (_bannerFallbackTexture != null)
            {
                Destroy(_bannerFallbackTexture);
                _bannerFallbackTexture = null;
            }
        }

        private static Texture2D CreateAvatarTexture()
        {
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color background = new Color(0.15f, 0.23f, 0.39f, 1f);
            Color accent = new Color(0.87f, 0.92f, 0.98f, 1f);
            Vector2 center = new Vector2(64f, 82f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color pixel = background;
                    float dx = x - center.x;
                    float dy = y - center.y;
                    if ((dx * dx) + (dy * dy) <= 22f * 22f)
                    {
                        pixel = accent;
                    }
                    else if (Mathf.Abs(x - 64f) <= 30f && y >= 18 && y <= 56)
                    {
                        pixel = accent;
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateBannerTexture()
        {
            Texture2D texture = new Texture2D(640, 240, TextureFormat.RGBA32, false);
            Color left = new Color(0.07f, 0.10f, 0.17f, 1f);
            Color right = new Color(0.23f, 0.45f, 0.76f, 1f);
            Color stripe = new Color(0.90f, 0.95f, 1f, 1f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float t = x / (float)(texture.width - 1);
                    Color pixel = Color.Lerp(left, right, t);
                    if ((x + y) % 97 < 6)
                    {
                        pixel = Color.Lerp(pixel, stripe, 0.55f);
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
