using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks; // 核心依赖：UniTask
using Newtonsoft.Json; // 核心依赖：Newtonsoft.Json
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework
{
    /// <summary>
    /// 标准 HTTP 响应结构
    /// 封装了状态码、错误信息、响应头和原始文本
    /// </summary>
    [Serializable]
    public class HttpResponse
    {
        /// <summary>
        /// 请求是否成功 (HTTP 200-299 且无网络错误)
        /// </summary>
        public bool isSuccess;

        /// <summary>
        /// HTTP 状态码 (如 200, 404, 500)
        /// </summary>
        public long responseCode;

        /// <summary>
        /// 原始响应文本
        /// </summary>
        public string responseText;

        /// <summary>
        /// 错误信息 (仅在 isSuccess 为 false 时有效)
        /// </summary>
        public string error;

        /// <summary>
        /// 响应头集合
        /// </summary>
        public Dictionary<string, string> headers;

        public HttpResponse()
        {
            headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// 将响应文本反序列化为对象 (可能会抛出异常)
        /// </summary>
        public T Deserialize<T>()
        {
            if (!isSuccess || string.IsNullOrEmpty(responseText))
                return default;

            try
            {
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[UnityHttpUtils] JSON反序列化失败: {ex.Message}\nContent: {responseText}");
                return default;
            }
        }

        /// <summary>
        /// 尝试将响应文本反序列化为对象 (安全方法，不抛出异常)
        /// </summary>
        public bool TryDeserialize<T>(out T result)
        {
            result = default;
            if (!isSuccess || string.IsNullOrEmpty(responseText))
                return false;

            try
            {
                result = JsonConvert.DeserializeObject<T>(responseText);
                return result != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 请求配置参数
    /// </summary>
    public class RequestConfig
    {
        /// <summary>
        /// 是否自动注入 Authorization Token (默认 true)
        /// </summary>
        public bool autoInjectToken = true;

        /// <summary>
        /// 自定义请求头
        /// </summary>
        public Dictionary<string, string> headers;

        /// <summary>
        /// 进度回调 (0.0 ~ 1.0)
        /// </summary>
        public Action<float> onProgress;

        /// <summary>
        /// 是否阻止重复请求 (基于 URL+Method+Body 计算哈希)
        /// </summary>
        public bool preventDuplicate;

        /// <summary>
        /// 超时时间 (秒)
        /// </summary>
        public int timeout = 30;

        public RequestConfig()
        {
            headers = new Dictionary<string, string>();
        }
    }

    /// <summary>
    ///     Unity HTTP 请求管理工具类 (UniTask 商业版)
    ///     <para>特性：</para>
    ///     <list type="bullet">
    ///         <item>基于 UniTask 实现，零 GC，高性能异步</item>
    ///         <item>支持 GET/POST/PUT/DELETE</item>
    ///         <item>内置 Token 管理与自动注入</item>
    ///         <item>支持防重复请求机制</item>
    ///         <item>支持标准 CancellationToken 取消机制</item>
    ///     </list>
    /// </summary>
    public class NetworkApiKit : MonoBehaviour
    {
        private static NetworkApiKit _instance;

        // 活跃请求的取消令牌集合 (Key: RequestHash)
        private readonly Dictionary<int, CancellationTokenSource> _activeCTS = new Dictionary<int, CancellationTokenSource>();

        // Token 缓存
        private string _authToken;
        private string _tokenType = "Bearer";

        /// <summary>
        /// 单例访问入口
        /// </summary>
        public static NetworkApiKit Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[UnityHttpUtils]");
                    _instance = go.AddComponent<NetworkApiKit>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            CancelAllRequests();
        }

        #region Token 管理

        public static void SetAuthToken(string token, string tokenType = "Bearer")
        {
            Instance._authToken = token;
            Instance._tokenType = tokenType;
        }

        public static string GetAuthToken() => Instance._authToken;

        public static void ClearAuthToken()
        {
            Instance._authToken = null;
            Instance._tokenType = "Bearer";
        }

        public static bool HasAuthToken() => !string.IsNullOrEmpty(Instance._authToken);

        #endregion

        #region 文件下载 (Fix OOM)

        /// <summary>
        ///  大文件下载 (直接写入磁盘，防止内存溢出)
        /// </summary>
        /// <param name="url">下载地址</param>
        /// <param name="savePath">保存路径 (包含文件名)</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="timeout">超时时间</param>
        public static async UniTask<bool> DownloadFileAsync(string url, string savePath, Action<float> onProgress = null, int timeout = 60)
        {
            //  自动创建目录
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[Download] 创建目录失败: {dir}\n{ex}");
                    return false;
                }
            }

            using (var request = UnityWebRequest.Get(url))
            {
                //  使用 DownloadHandlerFile 直接写入磁盘，不占用堆内存
                request.downloadHandler = new DownloadHandlerFile(savePath);
                request.timeout = timeout;

                var progress = onProgress != null ? Progress.Create<float>(onProgress) : null;

                try
                {
                    await request.SendWebRequest().ToUniTask(progress: progress);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        LogKit.LogError($"[Download] 下载失败: {url}\n{request.error}");
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[Download] 下载异常: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 核心异步逻辑 (API 请求)

        /// <summary>
        /// 底层通用请求方法 (仅限 API 数据请求)
        /// </summary>
        private async UniTask<HttpResponse> SendRequestAsync(string method, string url, string body, RequestConfig config)
        {
            int requestHash = GenerateRequestHash(method, url, body);

            // 1. 防重复检查
            if (config.preventDuplicate && _activeCTS.ContainsKey(requestHash))
            {
                LogKit.LogWarning($"[UnityHttpUtils] 拦截重复请求: [{method}] {url}");
                return new HttpResponse { isSuccess = false, error = "Request is pending (Duplicate)", responseCode = 0 };
            }

            // 2. 创建取消令牌
            var cts = new CancellationTokenSource();
            _activeCTS[requestHash] = cts;

            UnityWebRequest request = CreateWebRequest(method, url, body, config);
            HttpResponse response = new HttpResponse();

            try
            {
                // 3. 发送请求
                var progress = config.onProgress != null ? Progress.Create<float>(config.onProgress) : null;
                await request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cts.Token);

                // 4. 处理响应结果
                response = ProcessResponse(request);

                if (!response.isSuccess)
                {
                    LogKit.LogWarning($"[UnityHttpUtils] 请求失败: [{method}] {url}\nCode: {response.responseCode}\nError: {response.error}");
                }
            }
            catch (OperationCanceledException)
            {
                LogKit.Log($"[UnityHttpUtils] 请求已取消: [{method}] {url}");
                response.isSuccess = false;
                response.error = "Request Cancelled";
            }
            catch (UnityWebRequestException uwrEx)
            {
                response = ProcessResponse(uwrEx.UnityWebRequest);
                LogKit.LogError($"[UnityHttpUtils] 网络异常: [{method}] {url}\n{uwrEx.Message}");
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.error = ex.Message;
                LogKit.LogError($"[UnityHttpUtils] 未知异常: [{method}] {url}\n{ex}");
            }
            finally
            {
                // 5. 清理资源
                if (_activeCTS.ContainsKey(requestHash))
                {
                    _activeCTS.Remove(requestHash);
                }

                cts.Dispose();
                request.Dispose();
            }

            return response;
        }

        private UnityWebRequest CreateWebRequest(string method, string url, string body, RequestConfig config)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);

            if (!string.IsNullOrEmpty(body))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            // 注意：这里使用 Buffer 是因为 API 响应通常较小
            // 如果是下载文件，请务必使用 DownloadFileAsync
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            if (config.autoInjectToken && !string.IsNullOrEmpty(_authToken))
            {
                bool hasAuthHeader = config.headers != null && config.headers.ContainsKey("Authorization");
                if (!hasAuthHeader)
                {
                    request.SetRequestHeader("Authorization", $"{_tokenType} {_authToken}");
                }
            }

            if (config.headers != null)
            {
                foreach (var header in config.headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            request.timeout = config.timeout;
            return request;
        }

        private HttpResponse ProcessResponse(UnityWebRequest request)
        {
            var response = new HttpResponse
            {
                responseCode = request.responseCode,
                responseText = request.downloadHandler?.text ?? ""
            };

            bool isNetworkError = request.result == UnityWebRequest.Result.ConnectionError;
            bool isProtocolError = request.result == UnityWebRequest.Result.ProtocolError;
            bool isDataError = request.result == UnityWebRequest.Result.DataProcessingError;

            if (isNetworkError || isProtocolError || isDataError)
            {
                response.isSuccess = false;
                response.error = request.error;
            }
            else
            {
                response.isSuccess = true;
            }

            var headers = request.GetResponseHeaders();
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    response.headers[kvp.Key] = kvp.Value;
                }
            }

            return response;
        }

        private int GenerateRequestHash(string method, string url, string body)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + method.GetHashCode();
                hash = hash * 31 + url.GetHashCode();
                hash = hash * 31 + (body ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        #endregion

        #region 取消控制

        public void CancelRequest(string url, string method = "GET", string body = null)
        {
            int hash = GenerateRequestHash(method, url, body);
            if (_activeCTS.TryGetValue(hash, out var cts))
            {
                cts.Cancel();
                LogKit.Log($"[UnityHttpUtils] 触发取消: {url}");
            }
        }

        public void CancelAllRequests()
        {
            foreach (var cts in _activeCTS.Values)
            {
                cts.Cancel();
            }
        }

        #endregion

        #region Public API - Async (UniTask 推荐)

        public static async UniTask<HttpResponse> GetAsync(string url, Dictionary<string, string> headers = null, int timeout = 30)
        {
            var config = new RequestConfig { headers = headers, timeout = timeout };
            return await Instance.SendRequestAsync("GET", url, null, config);
        }

        public static async UniTask<(T data, HttpResponse response)> GetJsonAsync<T>(string url, Dictionary<string, string> headers = null, int timeout = 30)
        {
            var response = await GetAsync(url, headers, timeout);
            T data = default;
            if (response.isSuccess) response.TryDeserialize(out data);
            return (data, response);
        }

        public static async UniTask<HttpResponse> PostAsync(string url, string jsonBody, Dictionary<string, string> headers = null, int timeout = 30)
        {
            var config = new RequestConfig { headers = headers, timeout = timeout };
            return await Instance.SendRequestAsync("POST", url, jsonBody, config);
        }

        public static async UniTask<HttpResponse> PostAsync<T>(string url, T dataObject, Dictionary<string, string> headers = null, int timeout = 30)
        {
            string json = JsonConvert.SerializeObject(dataObject);
            return await PostAsync(url, json, headers, timeout);
        }

        public static async UniTask<(TResponse data, HttpResponse response)> PostJsonAsync<TRequest, TResponse>(string url, TRequest requestData,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            var response = await PostAsync(url, requestData, headers, timeout);
            TResponse data = default;
            if (response.isSuccess) response.TryDeserialize(out data);
            return (data, response);
        }

        public static async UniTask<HttpResponse> PutAsync(string url, string jsonBody, Dictionary<string, string> headers = null, int timeout = 30)
        {
            var config = new RequestConfig { headers = headers, timeout = timeout };
            return await Instance.SendRequestAsync("PUT", url, jsonBody, config);
        }

        public static async UniTask<HttpResponse> DeleteAsync(string url, Dictionary<string, string> headers = null, int timeout = 30)
        {
            var config = new RequestConfig { headers = headers, timeout = timeout };
            return await Instance.SendRequestAsync("DELETE", url, null, config);
        }

        #endregion

        #region Public API - Fire & Forget (回调式兼容)

        public static void Get(string url, Action<HttpResponse> onComplete, Dictionary<string, string> headers = null)
        {
            GetAsync(url, headers).ContinueWith(onComplete).Forget();
        }

        public static void Post(string url, string jsonBody, Action<HttpResponse> onComplete, Dictionary<string, string> headers = null)
        {
            PostAsync(url, jsonBody, headers).ContinueWith(onComplete).Forget();
        }

        public static void GetJson<T>(string url, Action<T, HttpResponse> onComplete)
        {
            GetJsonAsync<T>(url).ContinueWith(result => onComplete?.Invoke(result.data, result.response)).Forget();
        }

        public static void PostJson<TRequest, TResponse>(string url, TRequest requestData, Action<TResponse, HttpResponse> onComplete)
        {
            PostJsonAsync<TRequest, TResponse>(url, requestData).ContinueWith(result => onComplete?.Invoke(result.data, result.response)).Forget();
        }

        #endregion
    }
}