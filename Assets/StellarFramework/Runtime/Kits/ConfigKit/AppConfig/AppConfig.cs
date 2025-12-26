// ========== AppConfig.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/ConfigKit/AppConfig/AppConfig.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StellarFramework
{
    public static class AppConfig
    {
        public const string CONFIG_NAME = "appConfig.json";

        private static JObject _data;
        private static bool _initialized;
        private static bool _isLoading;
        private static bool _isSaving;

        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        public static void Init(Action<bool> onComplete = null)
        {
            if (_initialized)
            {
                onComplete?.Invoke(true);
                return;
            }

            if (_isLoading)
            {
                LogKit.LogWarning("[AppConfig] 正在初始化中，请勿重复调用");
                return;
            }

            if (CoroutineRunner.Instance == null)
            {
                LogKit.LogError("[AppConfig] 场景缺少 CoroutineRunner，无法启动协程");
                return;
            }

            _isLoading = true;

            CoroutineRunner.Instance.StartCoroutine(ConfigCore.LoadConfigProcess(CONFIG_NAME, (data, isUserSave) =>
            {
                _isLoading = false;
                if (data != null)
                {
                    _data = data;
                    _initialized = true;
                    _cache.Clear();
                    LogKit.Log($"[AppConfig] 初始化成功 (来源: {(isUserSave ? "用户存档" : "默认配置")})");
                    onComplete?.Invoke(true);
                }
                else
                {
                    LogKit.LogError("[AppConfig] 初始化失败，配置为空");
                    _data = new JObject();
                    onComplete?.Invoke(false);
                }
            }));
        }

        public static string GetString(string key, string defaultValue = "") => GetVal(key, defaultValue);
        public static int GetInt(string key, int defaultValue = 0) => GetVal(key, defaultValue);
        public static float GetFloat(string key, float defaultValue = 0f) => GetVal(key, defaultValue);
        public static bool GetBool(string key, bool defaultValue = false) => GetVal(key, defaultValue);

        public static T GetVal<T>(string key, T defaultValue = default)
        {
            if (!_initialized || _data == null) return defaultValue;

            if (_cache.TryGetValue(key, out var cachedValue))
            {
                if (cachedValue is T castedValue) return castedValue;
            }

            try
            {
                JToken token = _data.SelectToken(key);
                if (token != null)
                {
                    T result = token.ToObject<T>();
                    _cache[key] = result;
                    return result;
                }
            }
            catch (Exception e)
            {
                LogKit.LogWarning($"[AppConfig] Key '{key}' 解析失败: {e.Message}");
            }

            return defaultValue;
        }

        public static bool HasKey(string key) => _data != null && _data.SelectToken(key) != null;

        public static void Set(string key, object value)
        {
            if (_data == null) _data = new JObject();
            _data[key] = value != null ? JToken.FromObject(value) : null;
            _cache[key] = value;
        }

        public static void Save()
        {
            if (_data == null) return;
            if (_isSaving) return;

            _isSaving = true;

            // 在主线程完成序列化，确保 JObject 线程安全
            string jsonString;
            try
            {
                jsonString = _data.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[AppConfig] 序列化失败: {ex.Message}");
                _isSaving = false;
                return;
            }

            string path = ConfigCore.GetPersistentPath(CONFIG_NAME);

            // 后台线程只负责 IO 写入
            Task.Run(async () =>
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(jsonString);
                    }
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[AppConfig] 保存失败: {ex.Message}");
                }
                finally
                {
                    _isSaving = false;
                }
            });
        }
    }
}