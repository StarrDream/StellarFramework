using System;
using System.Collections.Generic;
using System.Linq;

namespace StellarFramework.Settings
{
    public sealed class SettingsRegistry
    {
        private readonly Dictionary<string, SettingsPageDefinition> _pages =
            new Dictionary<string, SettingsPageDefinition>();

        private readonly Dictionary<string, SettingDefinition> _settings =
            new Dictionary<string, SettingDefinition>();

        public IReadOnlyCollection<SettingsPageDefinition> Pages => _pages.Values;
        public IReadOnlyCollection<SettingDefinition> Settings => _settings.Values;

        public void RegisterPage(SettingsPageDefinition page)
        {
            if (page == null)
            {
                LogKit.LogError("[SettingsRegistry] RegisterPage failed because page is null.");
                return;
            }

            if (string.IsNullOrEmpty(page.Id))
            {
                LogKit.LogError("[SettingsRegistry] RegisterPage failed because page.Id is empty.");
                return;
            }

            _pages[page.Id] = page;
        }

        public void RegisterSetting(SettingDefinition definition)
        {
            if (definition == null)
            {
                LogKit.LogError("[SettingsRegistry] RegisterSetting failed because definition is null.");
                return;
            }

            if (string.IsNullOrEmpty(definition.Key))
            {
                LogKit.LogError("[SettingsRegistry] RegisterSetting failed because definition.Key is empty.");
                return;
            }

            if (string.IsNullOrEmpty(definition.PageId))
            {
                LogKit.LogError(
                    $"[SettingsRegistry] RegisterSetting failed because definition.PageId is empty. Key={definition.Key}");
                return;
            }

            if (!_pages.ContainsKey(definition.PageId))
            {
                LogKit.LogWarning(
                    $"[SettingsRegistry] Auto-created missing page definition. PageId={definition.PageId}, TriggerSetting={definition.Key}");
                RegisterPage(new SettingsPageDefinition(definition.PageId, definition.PageId, string.Empty));
            }

            if (_settings.ContainsKey(definition.Key))
            {
                LogKit.LogWarning($"[SettingsRegistry] Duplicate setting key replaced. Key={definition.Key}");
            }

            _settings[definition.Key] = definition;
        }

        public bool TryGetSetting(string key, out SettingDefinition definition)
        {
            return _settings.TryGetValue(key, out definition);
        }

        public bool TryGetPage(string pageId, out SettingsPageDefinition page)
        {
            return _pages.TryGetValue(pageId, out page);
        }

        public IReadOnlyList<SettingsPageDefinition> GetSortedPages()
        {
            return _pages.Values
                .OrderBy(page => page.Order)
                .ThenBy(page => page.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<SettingDefinition> GetSortedSettingsForPage(string pageId)
        {
            return _settings.Values
                .Where(setting => string.Equals(setting.PageId, pageId, StringComparison.Ordinal))
                .OrderBy(setting => setting.Order)
                .ThenBy(setting => setting.DisplayName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
