using UnityEngine;

namespace StellarFramework.Settings
{
    /// <summary>
    /// 基于 Unity PlayerPrefs 的默认字符串 Settings Storage。
    /// </summary>
    /// <remarks>
    /// 适合本机轻量用户偏好，不适合敏感数据、大体积数据或需要事务/云同步的设置。
    /// </remarks>
    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        /// <summary>默认 PlayerPrefs Key 前缀。</summary>
        public const string DefaultPrefix = "Stellar.Settings.";

        private readonly string _prefix;

        /// <summary>创建 PlayerPrefs Storage。</summary>
        public PlayerPrefsSettingsStorage(string prefix = DefaultPrefix)
        {
            _prefix = string.IsNullOrEmpty(prefix) ? DefaultPrefix : prefix;
        }

        /// <inheritdoc />
        public bool TryLoad(string key, out string rawValue)
        {
            string prefKey = BuildKey(key);
            if (!PlayerPrefs.HasKey(prefKey))
            {
                rawValue = null;
                return false;
            }

            rawValue = PlayerPrefs.GetString(prefKey, string.Empty);
            return true;
        }

        /// <inheritdoc />
        public void Save(string key, string rawValue)
        {
            PlayerPrefs.SetString(BuildKey(key), rawValue ?? string.Empty);
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(BuildKey(key));
        }

        /// <inheritdoc />
        public void Flush()
        {
            PlayerPrefs.Save();
        }

        private string BuildKey(string key)
        {
            return _prefix + key;
        }
    }
}
