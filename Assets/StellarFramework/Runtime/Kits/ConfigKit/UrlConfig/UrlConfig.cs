using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StellarFramework
{
    public enum UrlEnvironment
    {
        Dev,
        Release
    }

    /// <summary>
    /// 高性能参数结构体，避免装箱拆箱
    /// </summary>
    [Serializable]
    public readonly struct UrlParam : IEquatable<UrlParam>
    {
        public readonly string Key;
        public readonly string Value;

        public UrlParam(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public static implicit operator UrlParam((string key, string value) tuple) => new UrlParam(tuple.key, tuple.value);
        public static implicit operator UrlParam((string key, int value) tuple) => new UrlParam(tuple.key, tuple.value.ToString());
        public static implicit operator UrlParam((string key, float value) tuple) => new UrlParam(tuple.key, tuple.value.ToString());
        public static implicit operator UrlParam((string key, long value) tuple) => new UrlParam(tuple.key, tuple.value.ToString());

        public bool Equals(UrlParam other) => Key == other.Key && Value == other.Value;
        public override bool Equals(object obj) => obj is UrlParam other && Equals(other);
        public override int GetHashCode() => (Key, Value).GetHashCode();
        public override string ToString() => $"{Key}={Value}";
    }

    /// <summary>
    /// URL 配置管理类
    /// 职责: 解析URL配置，支持环境切换，高性能参数拼接
    /// </summary>
    public static class UrlConfig
    {
        public const string CONFIG_NAME = "urlConfig.json";
        private static bool _initialized;

        // 运行时缓存
        private static readonly Dictionary<string, string> _serviceRoots = new Dictionary<string, string>(16);
        private static readonly Dictionary<string, EndpointData> _endpoints = new Dictionary<string, EndpointData>(64);

        // 线程静态 StringBuilder，复用内存
        [ThreadStatic] private static StringBuilder _sharedBuilder;

        private class EndpointData
        {
            public string Service;
            public string Path;
            public bool HasParams;
        }

#if UNITY_EDITOR
        public static UrlEnvironment EditorLoadMode
        {
            get => (UrlEnvironment)UnityEditor.EditorPrefs.GetInt("UrlConfig_Env_Key", (int)UrlEnvironment.Dev);
            set => UnityEditor.EditorPrefs.SetInt("UrlConfig_Env_Key", (int)value);
        }

        public static bool EnableEditorOverride = true;
#endif

        #region 初始化

        public static IEnumerator Init(Action<bool> onComplete = null)
        {
            if (_initialized)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            // 调用核心层加载
            yield return ConfigCore.LoadConfigProcess(CONFIG_NAME, (data, isHotUpdate) =>
            {
                if (data != null)
                {
                    ParseConfig(data);
                    _initialized = true;
                    LogKit.Log($"[UrlConfig] 初始化完成. 源: {(isHotUpdate ? "热更配置" : "内置配置")}, 节点数: {_endpoints.Count}");
                    onComplete?.Invoke(true);
                }
                else
                {
                    LogKit.LogError("[UrlConfig] 初始化失败，配置无效");
                    onComplete?.Invoke(false);
                }
            });
        }

        private static void ParseConfig(JObject rootJson)
        {
            _serviceRoots.Clear();
            _endpoints.Clear();

            string profile = "Release";
            if (rootJson.TryGetValue("ActiveProfile", out var token)) profile = token.ToString();

#if UNITY_EDITOR
            if (EnableEditorOverride)
            {
                profile = EditorLoadMode.ToString();
                LogKit.Log($"[UrlConfig] Editor 环境强制覆盖: {profile}");
            }
#endif

            if (rootJson["Environments"]?[profile] is JObject envObj)
            {
                foreach (var property in envObj.Properties())
                {
                    string rootUrl = property.Value.ToString();
                    if (rootUrl.EndsWith("/")) rootUrl = rootUrl.Substring(0, rootUrl.Length - 1);
                    _serviceRoots[property.Name] = rootUrl;
                }
            }
            else
            {
                LogKit.LogError($"[UrlConfig] 找不到环境配置 '{profile}'");
                // 即使环境没找到，也继续解析 Endpoints，方便调试
            }

            if (rootJson["Endpoints"] is JObject endpointsObj)
            {
                foreach (var property in endpointsObj.Properties())
                {
                    if (property.Value is JObject valObj)
                    {
                        string path = valObj["Path"]?.ToString() ?? "";
                        _endpoints[property.Name] = new EndpointData
                        {
                            Service = valObj["Service"]?.ToString(),
                            Path = path,
                            HasParams = path.IndexOf('{') >= 0
                        };
                    }
                }
            }
        }

        #endregion

        #region 核心 API (GetUrl)

        public static string GetUrl(string key) => GetUrlCore(key, ReadOnlySpan<UrlParam>.Empty);
        public static string GetUrl(string key, UrlParam p1) => GetUrlCore(key, new[] { p1 });
        public static string GetUrl(string key, UrlParam p1, UrlParam p2) => GetUrlCore(key, new[] { p1, p2 });
        public static string GetUrl(string key, params UrlParam[] parameters) => GetUrlCore(key, parameters.AsSpan());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetUrlCore(string key, ReadOnlySpan<UrlParam> parameters)
        {
            if (!_initialized)
            {
                LogKit.LogError($"[UrlConfig] 未初始化! Key: {key}");
                return string.Empty;
            }

            if (!_endpoints.TryGetValue(key, out var endpoint))
            {
                LogKit.LogError($"[UrlConfig] Key 不存在: '{key}'");
                return $"ERROR_KEY_NOT_FOUND_{key}";
            }

            string root = "";
            if (!string.IsNullOrEmpty(endpoint.Service))
            {
                if (!_serviceRoots.TryGetValue(endpoint.Service, out root))
                {
                    LogKit.LogError($"[UrlConfig] Service '{endpoint.Service}' 未定义 (Key: {key})");
                }
            }

            if (!endpoint.HasParams)
            {
                return CombineSimple(root, endpoint.Path);
            }

            return BuildUrlWithSpan(root, endpoint.Path, parameters);
        }

        private static string CombineSimple(string root, string path)
        {
            if (string.IsNullOrEmpty(root)) return path;
            if (string.IsNullOrEmpty(path)) return root;

            var sb = GetBuilder(root.Length + path.Length + 1);
            sb.Append(root);
            if (!path.StartsWith("/")) sb.Append('/');
            sb.Append(path);
            return sb.ToString();
        }

        // 高性能拼接逻辑 (保留原样)
        private static string BuildUrlWithSpan(string root, string path, ReadOnlySpan<UrlParam> parameters)
        {
            var sb = GetBuilder(root.Length + path.Length + 64);

            if (!string.IsNullOrEmpty(root))
            {
                sb.Append(root);
                if (!path.StartsWith("/")) sb.Append('/');
            }

            ReadOnlySpan<char> pathSpan = path.AsSpan();
            int len = pathSpan.Length;
            int lastIndex = 0;

            for (int i = 0; i < len; i++)
            {
                if (pathSpan[i] == '{')
                {
                    if (i > lastIndex) sb.Append(pathSpan.Slice(lastIndex, i - lastIndex));

                    int closeIndex = -1;
                    for (int j = i + 1; j < len; j++)
                    {
                        if (pathSpan[j] == '}')
                        {
                            closeIndex = j;
                            break;
                        }
                    }

                    if (closeIndex > -1)
                    {
                        ReadOnlySpan<char> contentSpan = pathSpan.Slice(i + 1, closeIndex - i - 1);
                        ReadOnlySpan<char> paramNameSpan = contentSpan;

                        // 处理 {id:int}，忽略类型
                        int colonIndex = -1;
                        for (int k = 0; k < contentSpan.Length; k++)
                        {
                            if (contentSpan[k] == ':')
                            {
                                colonIndex = k;
                                break;
                            }
                        }

                        if (colonIndex > -1) paramNameSpan = contentSpan.Slice(0, colonIndex);

                        bool found = false;
                        foreach (var param in parameters)
                        {
                            if (param.Key.AsSpan().SequenceEqual(paramNameSpan))
                            {
                                sb.Append(param.Value);
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            LogKit.LogError($"[UrlConfig] 参数缺失: '{paramNameSpan.ToString()}' (Path: {path})");
                            sb.Append('{').Append(contentSpan).Append('}');
                        }

                        i = closeIndex;
                        lastIndex = closeIndex + 1;
                    }
                }
            }

            if (lastIndex < len)
            {
                sb.Append(pathSpan.Slice(lastIndex, len - lastIndex));
            }

            return sb.ToString();
        }

        private static StringBuilder GetBuilder(int capacity)
        {
            if (_sharedBuilder == null)
                _sharedBuilder = new StringBuilder(Math.Max(capacity, 512));
            else
            {
                _sharedBuilder.Clear();
                if (_sharedBuilder.Capacity < capacity) _sharedBuilder.EnsureCapacity(capacity);
            }

            return _sharedBuilder;
        }

        #endregion
    }
}