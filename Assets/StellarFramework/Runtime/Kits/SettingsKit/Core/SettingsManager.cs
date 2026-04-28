using System;
using System.Collections.Generic;
using System.Linq;

namespace StellarFramework.Settings
{
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public sealed class SettingsManager : Singleton<SettingsManager>
    {
        private readonly SettingsRegistry _registry = new SettingsRegistry();
        private readonly Dictionary<string, SettingEntry> _entries = new Dictionary<string, SettingEntry>();
        private readonly List<ISettingsPageProvider> _providers = new List<ISettingsPageProvider>();

        private ISettingsStorage _storage;
        private bool _isInitialized;
        private bool _defaultProvidersInstalled;

        public event Action<SettingEntry> SettingChanged;

        public bool IsInitialized => _isInitialized;
        public bool HasDirtySettings => _entries.Values.Any(entry => entry.IsDirty);

        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            _storage = new PlayerPrefsSettingsStorage();
        }

        public void Configure(ISettingsStorage storage)
        {
            if (_isInitialized)
            {
                LogKit.LogWarning("[SettingsManager] Configure ignored because the manager has already initialized.");
                return;
            }

            _storage = storage ?? new PlayerPrefsSettingsStorage();
        }

        public void RegisterProvider(ISettingsPageProvider provider)
        {
            if (provider == null)
            {
                LogKit.LogError("[SettingsManager] RegisterProvider failed because provider is null.");
                return;
            }

            if (_providers.Contains(provider))
            {
                return;
            }

            _providers.Add(provider);
            provider.Register(_registry);

            if (_isInitialized)
            {
                EnsureEntriesForDefinitions();
                ApplyAllCurrentValues();
            }
        }

        public void InstallDefaultProviders(DefaultSettingsInstallOptions options)
        {
            if (_defaultProvidersInstalled)
            {
                return;
            }

            DefaultSettingsInstaller.Install(this, options ?? new DefaultSettingsInstallOptions());
            _defaultProvidersInstalled = true;
        }

        public void Init()
        {
            if (_isInitialized)
            {
                return;
            }

            _storage ??= new PlayerPrefsSettingsStorage();

            EnsureEntriesForDefinitions();
            ApplyAllCurrentValues();
            _isInitialized = true;

            LogKit.Log(
                $"[SettingsManager] Initialized. Providers={_providers.Count}, Entries={_entries.Count}");
        }

        public IReadOnlyList<SettingsPageDefinition> GetPages()
        {
            return _registry.GetSortedPages();
        }

        public IReadOnlyList<SettingEntry> GetEntriesForPage(string pageId)
        {
            IReadOnlyList<SettingDefinition> definitions = _registry.GetSortedSettingsForPage(pageId);
            var entries = new List<SettingEntry>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                if (_entries.TryGetValue(definitions[i].Key, out SettingEntry entry))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public bool TryGetEntry(string key, out SettingEntry entry)
        {
            EnsureInitializedForUsage();
            return _entries.TryGetValue(key, out entry);
        }

        public T GetValue<T>(string key, T fallback = default)
        {
            EnsureInitializedForUsage();
            if (!_entries.TryGetValue(key, out SettingEntry entry))
            {
                return fallback;
            }

            return entry.CurrentValue is T typedValue ? typedValue : fallback;
        }

        public bool TrySetValue(string key, object rawValue, out string error)
        {
            EnsureInitializedForUsage();

            if (!_entries.TryGetValue(key, out SettingEntry entry))
            {
                error = $"[SettingsManager] Setting not found. Key={key}";
                return false;
            }

            if (!entry.Definition.TryNormalize(rawValue, out object normalizedValue))
            {
                error = $"[SettingsManager] Invalid setting value. Key={key}, RawValue={rawValue}";
                entry.SetError(error);
                return false;
            }

            if (Equals(entry.CurrentValue, normalizedValue))
            {
                error = null;
                return true;
            }

            if (entry.Definition.ApplyImmediately &&
                !TryApplyValue(entry.Definition, normalizedValue, out error))
            {
                entry.SetError(error);
                return false;
            }

            entry.SetCurrentValue(normalizedValue);
            entry.SetError(null);
            SettingChanged?.Invoke(entry);
            error = null;
            return true;
        }

        public bool ApplyPending(out string error)
        {
            EnsureInitializedForUsage();

            foreach (SettingEntry entry in _entries.Values)
            {
                if (!entry.IsDirty || entry.Definition.ApplyImmediately)
                {
                    continue;
                }

                if (!TryApplyValue(entry.Definition, entry.CurrentValue, out error))
                {
                    entry.SetError(error);
                    return false;
                }

                entry.SetError(null);
            }

            error = null;
            return true;
        }

        public bool Save(out string error)
        {
            EnsureInitializedForUsage();
            if (!ApplyPending(out error))
            {
                return false;
            }

            foreach (KeyValuePair<string, SettingEntry> pair in _entries)
            {
                string rawValue = pair.Value.Definition.Serialize(pair.Value.CurrentValue);
                _storage.Save(pair.Key, rawValue);
                pair.Value.MarkSaved();
            }

            _storage.Flush();
            error = null;
            return true;
        }

        public bool RevertPending(out string error)
        {
            EnsureInitializedForUsage();

            foreach (SettingEntry entry in _entries.Values)
            {
                if (!entry.IsDirty)
                {
                    continue;
                }

                if (!TryApplyValue(entry.Definition, entry.SavedValue, out error))
                {
                    entry.SetError(error);
                    return false;
                }

                entry.SetCurrentValue(entry.SavedValue);
                entry.SetError(null);
                SettingChanged?.Invoke(entry);
            }

            error = null;
            return true;
        }

        public void ResetPage(string pageId)
        {
            EnsureInitializedForUsage();
            IReadOnlyList<SettingEntry> entries = GetEntriesForPage(pageId);

            for (int i = 0; i < entries.Count; i++)
            {
                SettingEntry entry = entries[i];
                if (!TrySetValue(entry.Definition.Key, entry.Definition.DefaultValue, out string error))
                {
                    LogKit.LogError(error);
                }
            }
        }

        public void ResetAll()
        {
            EnsureInitializedForUsage();

            foreach (SettingEntry entry in _entries.Values)
            {
                if (!TrySetValue(entry.Definition.Key, entry.Definition.DefaultValue, out string error))
                {
                    LogKit.LogError(error);
                }
            }
        }

        private void EnsureEntriesForDefinitions()
        {
            foreach (SettingDefinition definition in _registry.Settings)
            {
                if (_entries.ContainsKey(definition.Key))
                {
                    continue;
                }

                object value = definition.DefaultValue;
                if (_storage.TryLoad(definition.Key, out string rawValue) &&
                    definition.TryDeserialize(rawValue, out object loadedValue))
                {
                    value = loadedValue;
                }

                _entries[definition.Key] = new SettingEntry(definition, value);
            }
        }

        private void ApplyAllCurrentValues()
        {
            foreach (SettingEntry entry in _entries.Values)
            {
                if (TryApplyValue(entry.Definition, entry.CurrentValue, out string error))
                {
                    entry.SetError(null);
                    continue;
                }

                LogKit.LogWarning(
                    $"[SettingsManager] Failed to apply setting. Falling back to default. Key={entry.Definition.Key}, Error={error}");

                object fallbackValue = entry.Definition.DefaultValue;
                if (!TryApplyValue(entry.Definition, fallbackValue, out string fallbackError))
                {
                    entry.SetError(fallbackError);
                    LogKit.LogError(
                        $"[SettingsManager] Failed to apply fallback value. Key={entry.Definition.Key}, Error={fallbackError}");
                    continue;
                }

                entry.SetSavedValue(fallbackValue);
            }
        }

        private static bool TryApplyValue(SettingDefinition definition, object value, out string error)
        {
            try
            {
                return definition.ApplyStrategy.TryApply(definition, value, out error);
            }
            catch (Exception ex)
            {
                error =
                    $"[SettingsManager] Apply strategy threw an exception. Key={definition.Key}, Strategy={definition.ApplyStrategy.StrategyName}, Message={ex.Message}";
                return false;
            }
        }

        private void EnsureInitializedForUsage()
        {
            if (!_isInitialized)
            {
                Init();
            }
        }
    }
}
