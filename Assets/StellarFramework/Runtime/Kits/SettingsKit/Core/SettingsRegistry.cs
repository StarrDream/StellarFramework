using System;
using System.Collections.Generic;
using System.Linq;
using LogKit = StellarFramework.Settings.SettingsKitDiagnostics;

namespace StellarFramework.Settings
{
    /// <summary>
    /// SettingsKit 的定义注册表。
    /// 只保存 Page/Definition 元数据，不保存当前设置值。
    /// </summary>
    public sealed class SettingsRegistry
    {
        private readonly Dictionary<string, SettingsPageDefinition> _pages =
            new Dictionary<string, SettingsPageDefinition>();

        private readonly Dictionary<string, SettingDefinition> _settings =
            new Dictionary<string, SettingDefinition>();

        /// <summary>已注册页面的只读集合视图。</summary>
        public IReadOnlyCollection<SettingsPageDefinition> Pages => _pages.Values;
        /// <summary>已注册设置定义的只读集合视图。</summary>
        public IReadOnlyCollection<SettingDefinition> Settings => _settings.Values;

        /// <summary>
        /// 注册或替换同 ID 页面定义。
        /// </summary>
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

        /// <summary>
        /// 注册设置定义。
        /// 若 Page 尚不存在会自动创建占位 Page；重复 Key 会以新 Definition 替换 Registry 中的旧定义。
        /// </summary>
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

        /// <summary>尝试查询设置定义。</summary>
        public bool TryGetSetting(string key, out SettingDefinition definition)
        {
            return _settings.TryGetValue(key, out definition);
        }

        /// <summary>尝试查询页面定义。</summary>
        public bool TryGetPage(string pageId, out SettingsPageDefinition page)
        {
            return _pages.TryGetValue(pageId, out page);
        }

        /// <summary>
        /// 按 Order、DisplayName 生成新的页面排序列表。
        /// </summary>
        public IReadOnlyList<SettingsPageDefinition> GetSortedPages()
        {
            return _pages.Values
                .OrderBy(page => page.Order)
                .ThenBy(page => page.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 取得指定页的设置，并按 Order、DisplayName 排序。
        /// </summary>
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
