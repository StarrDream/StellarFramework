using UnityEngine;

namespace StellarFramework.Settings
{
    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        public const string DefaultPrefix = "Stellar.Settings.";

        private readonly string _prefix;

        public PlayerPrefsSettingsStorage(string prefix = DefaultPrefix)
        {
            _prefix = string.IsNullOrEmpty(prefix) ? DefaultPrefix : prefix;
        }

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

        public void Save(string key, string rawValue)
        {
            PlayerPrefs.SetString(BuildKey(key), rawValue ?? string.Empty);
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(BuildKey(key));
        }

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
