using System;
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

        public static implicit operator UrlParam((string key, string value) tuple) =>
            new UrlParam(tuple.key, tuple.value);

        public static implicit operator UrlParam((string key, int value) tuple) =>
            new UrlParam(tuple.key, tuple.value.ToString());

        public static implicit operator UrlParam((string key, float value) tuple) =>
            new UrlParam(tuple.key, tuple.value.ToString());

        public static implicit operator UrlParam((string key, long value) tuple) =>
            new UrlParam(tuple.key, tuple.value.ToString());

        public bool Equals(UrlParam other) => Key == other.Key && Value == other.Value;
        public override bool Equals(object obj) => obj is UrlParam other && Equals(other);
        public override int GetHashCode() => (Key, Value).GetHashCode();
        public override string ToString() => $"{Key}={Value}";
    }

    /// <summary>
    /// 网络配置实例
    /// 职责: 维护单一业务域的网络环境与路由，提供零GC的URL拼接
    /// </summary>
    public class NetConfig
    {
        public string ConfigName { get; private set; }

        private readonly Dictionary<string, string> _serviceRoots;
        private readonly Dictionary<string, EndpointData> _endpoints;

        [ThreadStatic] private static StringBuilder _sharedBuilder;

        private class EndpointData
        {
            public string Service;
            public string Path;
            public bool HasParams;
        }

        public NetConfig(string configName, JObject rootJson)
        {
            ConfigName = configName;
            _serviceRoots = new Dictionary<string, string>(16);
            _endpoints = new Dictionary<string, EndpointData>(64);
            ParseConfig(rootJson);
        }

        private void ParseConfig(JObject rootJson)
        {
            if (rootJson == null) return;

            string profile = "Release";
            if (rootJson.TryGetValue("ActiveProfile", out var token))
            {
                profile = token.ToString();
            }

#if UNITY_EDITOR
            // 读取 EditorPrefs 中存储的全局环境配置
            int envInt = UnityEditor.EditorPrefs.GetInt("ConfigKit_Global_Env", (int)UrlEnvironment.Dev);
            profile = ((UrlEnvironment)envInt).ToString();
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
                LogKit.LogError($"[NetConfig] 配置 '{ConfigName}' 找不到环境配置 '{profile}'");
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

        public string GetUrl(string key) => GetUrlCore(key, ReadOnlySpan<UrlParam>.Empty);
        public string GetUrl(string key, UrlParam p1) => GetUrlCore(key, new[] { p1 });
        public string GetUrl(string key, UrlParam p1, UrlParam p2) => GetUrlCore(key, new[] { p1, p2 });
        public string GetUrl(string key, params UrlParam[] parameters) => GetUrlCore(key, parameters.AsSpan());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetUrlCore(string key, ReadOnlySpan<UrlParam> parameters)
        {
            if (!_endpoints.TryGetValue(key, out var endpoint))
            {
                LogKit.LogError($"[NetConfig] 配置 '{ConfigName}' Key 不存在: '{key}'");
                return string.Empty;
            }

            string root = "";
            if (!string.IsNullOrEmpty(endpoint.Service))
            {
                if (!_serviceRoots.TryGetValue(endpoint.Service, out root))
                {
                    LogKit.LogError($"[NetConfig] 配置 '{ConfigName}' Service '{endpoint.Service}' 未定义 (Key: {key})");
                }
            }

            if (!endpoint.HasParams)
            {
                return CombineSimple(root, endpoint.Path);
            }

            return BuildUrlWithSpan(root, endpoint.Path, parameters);
        }

        private string CombineSimple(string root, string path)
        {
            if (string.IsNullOrEmpty(root)) return path;
            if (string.IsNullOrEmpty(path)) return root;

            var sb = GetBuilder(root.Length + path.Length + 1);
            sb.Append(root);
            if (!path.StartsWith("/")) sb.Append('/');
            sb.Append(path);
            return sb.ToString();
        }

        private string BuildUrlWithSpan(string root, string path, ReadOnlySpan<UrlParam> parameters)
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
                            LogKit.LogError(
                                $"[NetConfig] 配置 '{ConfigName}' 参数缺失: '{paramNameSpan.ToString()}' (Path: {path})");
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
            {
                _sharedBuilder = new StringBuilder(Math.Max(capacity, 512));
            }
            else
            {
                _sharedBuilder.Clear();
                if (_sharedBuilder.Capacity < capacity) _sharedBuilder.EnsureCapacity(capacity);
            }

            return _sharedBuilder;
        }
    }
}