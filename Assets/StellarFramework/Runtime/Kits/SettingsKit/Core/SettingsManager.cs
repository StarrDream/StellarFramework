using System;
using System.Collections.Generic;
using System.Linq;
using LogKit = StellarFramework.Settings.SettingsKitDiagnostics;

namespace StellarFramework.Settings
{
    /// <summary>
    /// SettingsKit 的运行时状态与持久化服务。
    /// </summary>
    /// <remarks>
    /// 初始化时会从 Storage 恢复值并把有效值应用到真实系统。
    /// ApplyImmediately=true 的设置在 TrySetValue 时立即产生副作用；
    /// 其他设置只修改 CurrentValue，直到 ApplyPending/Save。
    /// Provider 支持初始化后追加；此时只创建并应用本次新增 Entry，不重放已有设置。
    /// </remarks>
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public sealed class SettingsManager : Singleton<SettingsManager>
    {
        private readonly SettingsRegistry _registry = new SettingsRegistry();
        private readonly Dictionary<string, SettingEntry> _entries = new Dictionary<string, SettingEntry>();
        private readonly List<ISettingsPageProvider> _providers = new List<ISettingsPageProvider>();

        private ISettingsStorage _storage;
        private bool _isInitialized;
        private bool _defaultProvidersInstalled;

        /// <summary>设置 CurrentValue 发生成功变化时触发。</summary>
        public event Action<SettingEntry> SettingChanged;

        /// <summary>是否已完成首次初始化。</summary>
        public bool IsInitialized => _isInitialized;
        /// <summary>是否存在 CurrentValue != SavedValue 的 Entry。</summary>
        public bool HasDirtySettings => _entries.Values.Any(entry => entry.IsDirty);

        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            _storage = new PlayerPrefsSettingsStorage();
        }

        /// <summary>
        /// 初始化前替换持久化后端。初始化后调用会被忽略。
        /// null 恢复默认 PlayerPrefs Storage。
        /// </summary>
        public void Configure(ISettingsStorage storage)
        {
            if (_isInitialized)
            {
                LogKit.LogWarning("[SettingsManager] Configure ignored because the manager has already initialized.");
                return;
            }

            _storage = storage ?? new PlayerPrefsSettingsStorage();
        }

        /// <summary>
        /// 注册一个设置 Provider。
        /// 初始化后注册时只加载和应用新出现的 Definition。
        /// </summary>
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
                List<SettingEntry> addedEntries = EnsureEntriesForDefinitions();
                ApplyEntries(addedEntries);
            }
        }

        /// <summary>
        /// 安装框架内置页面 Provider。
        /// 每个 Manager 生命周期只安装一次；具体页面由 Options/Adapter 决定。
        /// </summary>
        public void InstallDefaultProviders(DefaultSettingsInstallOptions options)
        {
            if (_defaultProvidersInstalled)
            {
                return;
            }

            DefaultSettingsInstaller.Install(this, options ?? new DefaultSettingsInstallOptions());
            _defaultProvidersInstalled = true;
        }

        /// <summary>
        /// 创建 Entry、从 Storage 恢复值并应用到运行时。
        /// 重复调用为幂等操作。
        /// </summary>
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

        /// <summary>取得按显示顺序排列的设置页。</summary>
        public IReadOnlyList<SettingsPageDefinition> GetPages()
        {
            return _registry.GetSortedPages();
        }

        /// <summary>取得指定页面已经创建的运行时 Entry。</summary>
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

        /// <summary>
        /// 查询 Entry；尚未初始化时会先自动 Init。
        /// </summary>
        public bool TryGetEntry(string key, out SettingEntry entry)
        {
            EnsureInitializedForUsage();
            return _entries.TryGetValue(key, out entry);
        }

        /// <summary>
        /// 读取当前值。Key 不存在或类型不匹配时返回 fallback。
        /// </summary>
        public T GetValue<T>(string key, T fallback = default)
        {
            EnsureInitializedForUsage();
            if (!_entries.TryGetValue(key, out SettingEntry entry))
            {
                return fallback;
            }

            return entry.CurrentValue is T typedValue ? typedValue : fallback;
        }

        /// <summary>
        /// 验证/归一化 rawValue 并更新 CurrentValue。
        /// Immediate 设置会先成功 Apply 再提交 CurrentValue；Apply 失败时保持旧值。
        /// </summary>
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

        /// <summary>
        /// 把所有 Dirty 且非 Immediate 的 CurrentValue 应用到运行时。
        /// </summary>
        /// <remarks>
        /// 成功 Apply 不代表已经持久化，因此不会清除 Dirty；
        /// 需要持久化并清除 Dirty 请调用 Save。
        /// </remarks>
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

        /// <summary>
        /// 先 ApplyPending，再把所有 CurrentValue 序列化写入 Storage，并更新 SavedValue。
        /// </summary>
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

        /// <summary>
        /// 将所有 Dirty Entry 的 SavedValue 重新应用到真实系统并恢复 CurrentValue。
        /// </summary>
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

        /// <summary>
        /// 把指定页的 CurrentValue 重置为 Definition.DefaultValue。
        /// 是否立即产生副作用取决于各 Definition 的 ApplyImmediately。
        /// </summary>
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

        /// <summary>
        /// 把全部 CurrentValue 重置为 Definition.DefaultValue。
        /// 该操作本身不自动 Save。
        /// </summary>
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

        private List<SettingEntry> EnsureEntriesForDefinitions()
        {
            var addedEntries = new List<SettingEntry>();

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

                var entry = new SettingEntry(definition, value);
                _entries[definition.Key] = entry;
                addedEntries.Add(entry);
            }

            return addedEntries;
        }

        private void ApplyAllCurrentValues()
        {
            ApplyEntries(_entries.Values);
        }

        private static void ApplyEntries(IEnumerable<SettingEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (SettingEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

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
