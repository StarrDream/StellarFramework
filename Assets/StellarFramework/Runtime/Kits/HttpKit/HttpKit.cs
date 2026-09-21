using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework
{
    /// <summary>
    /// HttpKit 的统一响应结果。
    /// </summary>
    /// <remarks>
    /// 网络失败、HTTP 协议错误和数据处理错误都会令 <see cref="isSuccess"/> 为 false。
    /// responseText 保留原始响应体，便于上层按业务协议自行解析。
    /// </remarks>
    [Serializable]
    public class HttpResponse
    {
        /// <summary>请求是否成功完成。</summary>
        public bool isSuccess;
        /// <summary>HTTP 状态码；未收到服务器响应时通常为 0。</summary>
        public long responseCode;
        /// <summary>原始文本响应体。</summary>
        public string responseText;
        /// <summary>失败原因；成功时通常为空。</summary>
        public string error;
        /// <summary>响应头快照。</summary>
        public Dictionary<string, string> headers;

        /// <summary>创建空响应并初始化 Headers 容器。</summary>
        public HttpResponse()
        {
            headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// 使用 Newtonsoft.Json 将成功响应反序列化为 T。
        /// </summary>
        /// <returns>反序列化失败或请求失败时返回 default。</returns>
        public T Deserialize<T>()
        {
            if (!isSuccess || string.IsNullOrEmpty(responseText))
            {
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            catch (Exception ex)
            {
                LogKit.LogError(
                    $"[HttpKit] JSON反序列化失败 | Type={typeof(T).Name} | Exception={ex.Message}\nResponse={responseText}");
                return default;
            }
        }

        /// <summary>
        /// 尝试将成功响应反序列化为 T。
        /// </summary>
        /// <returns>请求成功且 JSON 可解析为非 null T 时返回 true。</returns>
        public bool TryDeserialize<T>(out T result)
        {
            result = default;

            if (!isSuccess || string.IsNullOrEmpty(responseText))
            {
                return false;
            }

            try
            {
                result = JsonConvert.DeserializeObject<T>(responseText);
                return result != null;
            }
            catch (Exception ex)
            {
                LogKit.LogError(
                    $"[HttpKit] TryDeserialize 失败 | Type={typeof(T).Name} | Exception={ex.Message}\nResponse={responseText}");
                return false;
            }
        }
    }

    /// <summary>
    /// 单次 HTTP 请求的可选配置。
    /// </summary>
    public class RequestConfig
    {
        /// <summary>存在全局 Auth Token 时是否自动注入 Authorization Header。</summary>
        public bool autoInjectToken = true;
        /// <summary>额外请求头；显式 Authorization 会覆盖自动注入。</summary>
        public Dictionary<string, string> headers;
        /// <summary>上传/下载进度回调，范围通常为 0~1。</summary>
        public Action<float> onProgress;
        /// <summary>是否阻止 Method + URL + Body 相同的并发请求。</summary>
        public bool preventDuplicate;
        /// <summary>UnityWebRequest timeout，单位秒。</summary>
        public int timeout = 30;

        /// <summary>创建默认请求配置。</summary>
        public RequestConfig()
        {
            headers = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// 基于 UnityWebRequest + UniTask 的轻量 HTTP 门面。
    /// </summary>
    /// <remarks>
    /// 首次使用时会创建 DontDestroyOnLoad 的运行时宿主。
    /// 请求取消与重复请求检测按 Method + URL + BodyHash 维度管理。
    /// JSON 适配当前使用 Newtonsoft.Json。
    /// </remarks>
    public class HttpKit : MonoBehaviour
    {
        private static HttpKit _instance;
        private static bool _isQuitting;

        // Key: Method::URL::BodyHash
        private readonly Dictionary<string, HashSet<CancellationTokenSource>> _activeCTS =
            new Dictionary<string, HashSet<CancellationTokenSource>>();

        private readonly object _requestLock = new object();

        private string _authToken;
        private string _tokenType = "Bearer";

        /// <summary>
        /// 获取运行时 HttpKit 实例；应用退出阶段可能返回 null。
        /// </summary>
        public static HttpKit Instance
        {
            get
            {
                return GetOrCreateInstance();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitRuntimeState()
        {
            _instance = null;
            _isQuitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            _isQuitting = true;
        }

        private static HttpKit GetOrCreateInstance()
        {
            if (_isQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
                GameObject go = new GameObject("[HttpKit]");
                _instance = go.AddComponent<HttpKit>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }

        private static bool TryGetInstance(out HttpKit instance)
        {
            instance = GetOrCreateInstance();
            if (instance != null)
            {
                return true;
            }

            LogKit.LogWarning("[HttpKit] 当前处于退出或销毁阶段，本次调用已忽略");
            return false;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _isQuitting |= !Application.isPlaying;
            }

            CancelAllRequests();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region Token 管理

        /// <summary>
        /// 设置全局认证 Token。
        /// 默认以 "Bearer {token}" 形式自动注入请求。
        /// </summary>
        public static void SetAuthToken(string token, string tokenType = "Bearer")
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return;
            }

            instance._authToken = token;
            instance._tokenType = tokenType;
        }

        /// <summary>返回当前全局认证 Token。</summary>
        public static string GetAuthToken()
        {
            return TryGetInstance(out HttpKit instance) ? instance._authToken : null;
        }

        /// <summary>清除全局认证 Token，并恢复 Bearer 类型。</summary>
        public static void ClearAuthToken()
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return;
            }

            instance._authToken = null;
            instance._tokenType = "Bearer";
        }

        /// <summary>当前是否配置了非空认证 Token。</summary>
        public static bool HasAuthToken()
        {
            return TryGetInstance(out HttpKit instance) && !string.IsNullOrEmpty(instance._authToken);
        }

        #endregion

        #region 文件下载

        /// <summary>
        /// 将远端文件直接下载到指定磁盘路径。
        /// </summary>
        /// <remarks>会自动创建父目录。任一网络/协议/IO/取消失败都会返回 false。</remarks>
        public static async UniTask<bool> DownloadFileAsync(string url, string savePath,
            Action<float> onProgress = null, int timeout = 60)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(savePath))
            {
                LogKit.LogError($"[HttpKit] 下载参数非法 | URL={url} | SavePath={savePath}");
                return false;
            }

            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[HttpKit] 创建下载目录失败 | Dir={dir} | Exception={ex.Message}");
                    return false;
                }
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(savePath);
            request.timeout = timeout;

            IProgress<float> progress = onProgress != null ? Progress.Create<float>(onProgress) : null;

            try
            {
                await request.SendWebRequest().ToUniTask(progress: progress);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogKit.LogError($"[HttpKit] 文件下载失败 | URL={url} | Error={request.error}");
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                LogKit.LogWarning($"[HttpKit] 文件下载被取消 | URL={url}");
                return false;
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[HttpKit] 文件下载异常 | URL={url} | Exception={ex.Message}");
                return false;
            }
        }

        #endregion

        #region 核心异步逻辑

        private async UniTask<HttpResponse> SendRequestAsync(string method, string url, string body,
            RequestConfig config)
        {
            if (string.IsNullOrEmpty(url))
            {
                LogKit.LogError($"[HttpKit] 请求失败: URL 不能为空 | Method={method}");
                return new HttpResponse
                {
                    isSuccess = false,
                    error = "URL is null or empty"
                };
            }

            if (config == null)
            {
                LogKit.LogError($"[HttpKit] 请求失败: config 为空 | Method={method} | URL={url}");
                return new HttpResponse
                {
                    isSuccess = false,
                    error = "RequestConfig is null"
                };
            }

            string requestKey = GenerateRequestKey(method, url, body);

            if (config.preventDuplicate && HasPendingRequest(requestKey))
            {
                LogKit.LogWarning($"[HttpKit] 拦截重复请求 | Key={requestKey} | Method={method} | URL={url}");
                return new HttpResponse
                {
                    isSuccess = false,
                    error = "Request is pending (Duplicate)",
                    responseCode = 0
                };
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            AddPendingRequest(requestKey, cts);

            HttpResponse response = new HttpResponse();
            UnityWebRequest request = null;

            try
            {
                request = CreateWebRequest(method, url, body, config);

                IProgress<float> progress = config.onProgress != null
                    ? Progress.Create<float>(config.onProgress)
                    : null;

                await request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cts.Token);
                response = ProcessResponse(request);

                if (!response.isSuccess)
                {
                    LogKit.LogWarning(
                        $"[HttpKit] 请求失败 | Method={method} | URL={url} | Code={response.responseCode} | Error={response.error}");
                }
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.error = "Request Cancelled";
                LogKit.Log($"[HttpKit] 请求已取消 | Method={method} | URL={url}");
            }
            catch (UnityWebRequestException uwrEx)
            {
                response = ProcessResponse(uwrEx.UnityWebRequest);
                LogKit.LogError($"[HttpKit] 网络异常 | Method={method} | URL={url} | Exception={uwrEx.Message}");
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.error = ex.Message;
                LogKit.LogError($"[HttpKit] 未知异常 | Method={method} | URL={url} | Exception={ex.Message}");
            }
            finally
            {
                RemovePendingRequest(requestKey, cts);
                cts.Dispose();
                request?.Dispose();
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
                foreach (KeyValuePair<string, string> header in config.headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            request.timeout = config.timeout;
            return request;
        }

        private HttpResponse ProcessResponse(UnityWebRequest request)
        {
            if (request == null)
            {
                return new HttpResponse
                {
                    isSuccess = false,
                    error = "Request object is null"
                };
            }

            HttpResponse response = new HttpResponse
            {
                responseCode = request.responseCode,
                responseText = request.downloadHandler?.text ?? string.Empty
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

            Dictionary<string, string> responseHeaders = request.GetResponseHeaders();
            if (responseHeaders != null)
            {
                foreach (KeyValuePair<string, string> kvp in responseHeaders)
                {
                    response.headers[kvp.Key] = kvp.Value;
                }
            }

            return response;
        }

        private string GenerateRequestKey(string method, string url, string body)
        {
            return $"{method}::{url}::{ComputeStableBodyHash(body)}";
        }

        #endregion

        #region 取消控制

        /// <summary>
        /// 取消与指定 Method + URL + Body 完全匹配的所有活动请求。
        /// </summary>
        public void CancelRequest(string url, string method = "GET", string body = null)
        {
            string key = GenerateRequestKey(method, url, body);

            CancellationTokenSource[] ctsList;
            lock (_requestLock)
            {
                if (!_activeCTS.TryGetValue(key, out HashSet<CancellationTokenSource> group) || group.Count == 0)
                {
                    return;
                }

                ctsList = group.ToArray();
            }

            foreach (CancellationTokenSource cts in ctsList)
            {
                cts.Cancel();
            }

            LogKit.Log($"[HttpKit] 触发取消 | Method={method} | URL={url} | Count={ctsList.Length}");
        }

        /// <summary>取消当前实例跟踪的全部活动请求。</summary>
        public void CancelAllRequests()
        {
            CancellationTokenSource[] ctsList;
            lock (_requestLock)
            {
                var snapshot = new List<CancellationTokenSource>(_activeCTS.Count * 2);
                foreach (HashSet<CancellationTokenSource> group in _activeCTS.Values)
                {
                    snapshot.AddRange(group);
                }

                _activeCTS.Clear();
                ctsList = snapshot.ToArray();
            }

            foreach (CancellationTokenSource cts in ctsList)
            {
                cts.Cancel();
            }
        }

        #endregion

        #region Public API - Async

        /// <summary>发送 GET 请求。</summary>
        public static async UniTask<HttpResponse> GetAsync(string url, Dictionary<string, string> headers = null,
            int timeout = 30)
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return CreateUnavailableResponse();
            }

            RequestConfig config = new RequestConfig
            {
                headers = headers,
                timeout = timeout
            };

            return await instance.SendRequestAsync("GET", url, null, config);
        }

        /// <summary>
        /// 发送 GET 请求并尝试将成功响应反序列化为 T。
        /// 原始 HttpResponse 始终随结果返回。
        /// </summary>
        public static async UniTask<(T data, HttpResponse response)> GetJsonAsync<T>(string url,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            HttpResponse response = await GetAsync(url, headers, timeout);
            T data = default;
            if (response.isSuccess)
            {
                response.TryDeserialize(out data);
            }

            return (data, response);
        }

        /// <summary>发送 JSON POST 请求。</summary>
        public static async UniTask<HttpResponse> PostAsync(string url, string jsonBody,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return CreateUnavailableResponse();
            }

            RequestConfig config = new RequestConfig
            {
                headers = headers,
                timeout = timeout
            };

            return await instance.SendRequestAsync("POST", url, jsonBody, config);
        }

        /// <summary>将 dataObject 序列化为 JSON 后发送 POST。</summary>
        public static async UniTask<HttpResponse> PostAsync<T>(string url, T dataObject,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            string json = JsonConvert.SerializeObject(dataObject);
            return await PostAsync(url, json, headers, timeout);
        }

        /// <summary>
        /// 序列化请求对象发送 POST，并尝试反序列化成功响应。
        /// </summary>
        public static async UniTask<(TResponse data, HttpResponse response)> PostJsonAsync<TRequest, TResponse>(
            string url,
            TRequest requestData,
            Dictionary<string, string> headers = null,
            int timeout = 30)
        {
            HttpResponse response = await PostAsync(url, requestData, headers, timeout);
            TResponse data = default;
            if (response.isSuccess)
            {
                response.TryDeserialize(out data);
            }

            return (data, response);
        }

        /// <summary>发送 JSON PUT 请求。</summary>
        public static async UniTask<HttpResponse> PutAsync(string url, string jsonBody,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return CreateUnavailableResponse();
            }

            RequestConfig config = new RequestConfig
            {
                headers = headers,
                timeout = timeout
            };

            return await instance.SendRequestAsync("PUT", url, jsonBody, config);
        }

        /// <summary>发送 DELETE 请求。</summary>
        public static async UniTask<HttpResponse> DeleteAsync(string url, Dictionary<string, string> headers = null,
            int timeout = 30)
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return CreateUnavailableResponse();
            }

            RequestConfig config = new RequestConfig
            {
                headers = headers,
                timeout = timeout
            };

            return await instance.SendRequestAsync("DELETE", url, null, config);
        }

        #endregion

        #region Public API - Fire & Forget

        /// <summary>GET 的回调式兼容入口，内部仍使用 UniTask。</summary>
        public static void Get(string url, Action<HttpResponse> onComplete, Dictionary<string, string> headers = null)
        {
            GetAsync(url, headers).ContinueWith(response => onComplete?.Invoke(response)).Forget();
        }

        /// <summary>POST 的回调式兼容入口。</summary>
        public static void Post(string url, string jsonBody, Action<HttpResponse> onComplete,
            Dictionary<string, string> headers = null)
        {
            PostAsync(url, jsonBody, headers).ContinueWith(response => onComplete?.Invoke(response)).Forget();
        }

        /// <summary>GET + JSON 反序列化的回调式兼容入口。</summary>
        public static void GetJson<T>(string url, Action<T, HttpResponse> onComplete)
        {
            GetJsonAsync<T>(url).ContinueWith(result => onComplete?.Invoke(result.data, result.response)).Forget();
        }

        /// <summary>POST + JSON 请求/响应的回调式兼容入口。</summary>
        public static void PostJson<TRequest, TResponse>(string url, TRequest requestData,
            Action<TResponse, HttpResponse> onComplete)
        {
            PostJsonAsync<TRequest, TResponse>(url, requestData)
                .ContinueWith(result => onComplete?.Invoke(result.data, result.response))
                .Forget();
        }

        #endregion

        private void AddPendingRequest(string requestKey, CancellationTokenSource cts)
        {
            lock (_requestLock)
            {
                if (!_activeCTS.TryGetValue(requestKey, out HashSet<CancellationTokenSource> group))
                {
                    group = new HashSet<CancellationTokenSource>();
                    _activeCTS[requestKey] = group;
                }

                group.Add(cts);
            }
        }

        private bool HasPendingRequest(string requestKey)
        {
            lock (_requestLock)
            {
                return _activeCTS.TryGetValue(requestKey, out HashSet<CancellationTokenSource> group) &&
                       group.Count > 0;
            }
        }

        private void RemovePendingRequest(string requestKey, CancellationTokenSource cts)
        {
            lock (_requestLock)
            {
                if (!_activeCTS.TryGetValue(requestKey, out HashSet<CancellationTokenSource> group))
                {
                    return;
                }

                group.Remove(cts);
                if (group.Count == 0)
                {
                    _activeCTS.Remove(requestKey);
                }
            }
        }

        private string ComputeStableBodyHash(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return "0";
            }

            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static HttpResponse CreateUnavailableResponse()
        {
            return new HttpResponse
            {
                isSuccess = false,
                error = "HttpKit is unavailable during shutdown or disposal"
            };
        }
    }
}
