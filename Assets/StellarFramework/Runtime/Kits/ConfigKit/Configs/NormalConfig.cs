using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 普通配置实例
    /// 职责: 提供基于实例的键值对读取、缓存与本地存档覆盖能力
    /// </summary>
    public class NormalConfig
    {
        public string ConfigName { get; private set; }
        public string RelativePath { get; private set; }
        public bool IsUserSave { get; private set; }

        private JObject _data;
        private readonly Dictionary<string, object> _cache;
        private bool _isSaving;

        public NormalConfig(string configName, string relativePath, JObject data, bool isUserSave)
        {
            ConfigName = configName;
            RelativePath = relativePath;
            _data = data ?? new JObject();
            IsUserSave = isUserSave;
            _cache = new Dictionary<string, object>(32);
        }

        public string GetString(string key, string defaultValue = "") => GetVal(key, defaultValue);
        public int GetInt(string key, int defaultValue = 0) => GetVal(key, defaultValue);
        public float GetFloat(string key, float defaultValue = 0f) => GetVal(key, defaultValue);
        public bool GetBool(string key, bool defaultValue = false) => GetVal(key, defaultValue);

        public T GetVal<T>(string key, T defaultValue = default)
        {
            if (_data == null) return defaultValue;

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
                LogKit.LogWarning($"[NormalConfig] 配置 '{ConfigName}' Key '{key}' 解析失败: {e.Message}");
            }

            return defaultValue;
        }

        public bool HasKey(string key)
        {
            return _data != null && _data.SelectToken(key) != null;
        }

        public void Set(string key, object value)
        {
            if (_data == null) _data = new JObject();
            _data[key] = value != null ? JToken.FromObject(value) : null;
            _cache[key] = value;
        }

        public void Save()
        {
            if (_data == null) return;
            if (_isSaving) return;

            _isSaving = true;
            string jsonString;

            try
            {
                jsonString = _data.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[NormalConfig] 配置 '{ConfigName}' 序列化失败: {ex.Message}");
                _isSaving = false;
                return;
            }

            string path = ConfigCore.GetPersistentPath(RelativePath);
            string directory = Path.GetDirectoryName(path);

            Task.Run(async () =>
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(jsonString);
                    }

                    // 状态流转标记
                    IsUserSave = true;

                    // 核心修复：输出带有高亮颜色的绝对路径，彻底消除路径疑虑
                    LogKit.Log(
                        $"<color=#00FF00>[ConfigKit]</color> 配置 <b>{ConfigName}</b> 已成功异步保存至沙盒(持久化)目录!\n绝对路径: <color=#00FFFF>{path}</color>");
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[NormalConfig] 配置 '{ConfigName}' 保存失败: {ex.Message}");
                }
                finally
                {
                    _isSaving = false;
                }
            });
        }
    }
}