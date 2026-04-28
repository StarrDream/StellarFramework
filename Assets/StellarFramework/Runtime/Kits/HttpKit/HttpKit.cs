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
    [Serializable]
    public class HttpResponse
    {
        public bool isSuccess;
        public long responseCode;
        public string responseText;
        public string error;
        public Dictionary<string, string> headers;

        public HttpResponse()
        {
            headers = new Dictionary<string, string>();
        }

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

    public class RequestConfig
    {
        public bool autoInjectToken = true;
        public Dictionary<string, string> headers;
        public Action<float> onProgress;
        public bool preventDuplicate;
        public int timeout = 30;

        public RequestConfig()
        {
            headers = new Dictionary<string, string>();
        }
    }

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

        public static void SetAuthToken(string token, string tokenType = "Bearer")
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return;
            }

            instance._authToken = token;
            instance._tokenType = tokenType;
        }

        public static string GetAuthToken()
        {
            return TryGetInstance(out HttpKit instance) ? instance._authToken : null;
        }

        public static void ClearAuthToken()
        {
            if (!TryGetInstance(out HttpKit instance))
            {
                return;
            }

            instance._authToken = null;
            instance._tokenType = "Bearer";
        }

        public static bool HasAuthToken()
        {
            return TryGetInstance(out HttpKit instance) && !string.IsNullOrEmpty(instance._authToken);
        }

        #endregion

        #region 文件下载

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

        public static async UniTask<HttpResponse> PostAsync<T>(string url, T dataObject,
            Dictionary<string, string> headers = null, int timeout = 30)
        {
            string json = JsonConvert.SerializeObject(dataObject);
            return await PostAsync(url, json, headers, timeout);
        }

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

        public static void Get(string url, Action<HttpResponse> onComplete, Dictionary<string, string> headers = null)
        {
            GetAsync(url, headers).ContinueWith(response => onComplete?.Invoke(response)).Forget();
        }

        public static void Post(string url, string jsonBody, Action<HttpResponse> onComplete,
            Dictionary<string, string> headers = null)
        {
            PostAsync(url, jsonBody, headers).ContinueWith(response => onComplete?.Invoke(response)).Forget();
        }

        public static void GetJson<T>(string url, Action<T, HttpResponse> onComplete)
        {
            GetJsonAsync<T>(url).ContinueWith(result => onComplete?.Invoke(result.data, result.response)).Forget();
        }

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
