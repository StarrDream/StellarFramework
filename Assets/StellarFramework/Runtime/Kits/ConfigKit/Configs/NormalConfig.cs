using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
        private bool _hasPendingSaveRequest;

        public NormalConfig(string configName, string relativePath, JObject data, bool isUserSave)
        {
            ConfigName = configName;
            RelativePath = relativePath;
            _data = data ?? new JObject();
            IsUserSave = isUserSave;
            _cache = new Dictionary<string, object>(32);
        }

        public string GetString(string key, string defaultValue = "")
        {
            return GetVal(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            return GetVal(key, defaultValue);
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            return GetVal(key, defaultValue);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return GetVal(key, defaultValue);
        }

        public T GetVal<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError(
                    $"[NormalConfig] GetVal 失败: key 为空, Config={ConfigName}, RelativePath={RelativePath}, ValueType={typeof(T).Name}");
                return defaultValue;
            }

            if (_data == null)
            {
                return defaultValue;
            }

            if (_cache.TryGetValue(key, out object cachedValue))
            {
                if (cachedValue is T castedValue)
                {
                    return castedValue;
                }
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
                LogKit.LogWarning(
                    $"[NormalConfig] 配置解析失败: Config={ConfigName}, Key={key}, ValueType={typeof(T).Name}, Exception={e.Message}");
            }

            return defaultValue;
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return _data != null && _data.SelectToken(key) != null;
        }

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError(
                    $"[NormalConfig] Set 失败: key 为空, Config={ConfigName}, RelativePath={RelativePath}, Value={(value == null ? "null" : value.ToString())}");
                return;
            }

            if (_data == null)
            {
                _data = new JObject();
            }

            _data[key] = value != null ? JToken.FromObject(value) : null;
            _cache[key] = value;
        }

        public void Save()
        {
            if (_data == null)
            {
                LogKit.LogError($"[NormalConfig] Save 失败: _data 为空, Config={ConfigName}, RelativePath={RelativePath}");
                return;
            }

            if (string.IsNullOrEmpty(RelativePath))
            {
                LogKit.LogError($"[NormalConfig] Save 失败: RelativePath 为空, Config={ConfigName}");
                return;
            }

            if (_isSaving)
            {
                _hasPendingSaveRequest = true;
                LogKit.LogWarning(
                    $"[NormalConfig] Save 已排队: 当前正在保存中, Config={ConfigName}, RelativePath={RelativePath}");
                return;
            }

            SaveSnapshotAsync().Forget();
        }

        private async UniTaskVoid SaveSnapshotAsync()
        {
            _isSaving = true;
            _hasPendingSaveRequest = false;

            string configNameSnapshot = ConfigName;
            string relativePathSnapshot = RelativePath;
            string path = ConfigCore.GetPersistentPath(relativePathSnapshot);
            string directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
            {
                LogKit.LogError(
                    $"[NormalConfig] SaveSnapshotAsync 失败: 路径非法, Config={configNameSnapshot}, RelativePath={relativePathSnapshot}, FullPath={path}");
                _isSaving = false;
                return;
            }

            string jsonSnapshot;
            try
            {
                // 我在主线程先固化不可变快照，后台线程只负责纯 IO 写入。
                jsonSnapshot = _data.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                LogKit.LogError(
                    $"[NormalConfig] SaveSnapshotAsync 失败: JSON 序列化异常, Config={configNameSnapshot}, RelativePath={relativePathSnapshot}, Exception={ex.Message}");
                _isSaving = false;
                return;
            }

            try
            {
                await Task.Run(async () =>
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
                    await writer.WriteAsync(jsonSnapshot);
                });

                IsUserSave = true;
                LogKit.Log(
                    $"<color=#00FF00>[ConfigKit]</color> 配置 <b>{configNameSnapshot}</b> 已成功异步保存至沙盒目录\n绝对路径: <color=#00FFFF>{path}</color>");
            }
            catch (Exception ex)
            {
                LogKit.LogError(
                    $"[NormalConfig] 保存失败: Config={configNameSnapshot}, RelativePath={relativePathSnapshot}, FullPath={path}, Exception={ex.Message}");
            }
            finally
            {
                _isSaving = false;
            }

            if (_hasPendingSaveRequest)
            {
                SaveSnapshotAsync().Forget();
            }
        }
    }
}