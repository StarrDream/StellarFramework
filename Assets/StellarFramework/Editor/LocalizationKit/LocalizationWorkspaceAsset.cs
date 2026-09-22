using System;
using System.Collections.Generic;
using System.Linq;
using StellarFramework.Localization.UnityUGUI;
using UnityEngine;

namespace StellarFramework.Localization.Editor
{
    [Serializable]
    public sealed class LocalizationWorkspaceLanguage
    {
        [SerializeField] private string _locale;
        [SerializeField] private string _displayName;
        [SerializeField] private bool _enabled = true;
        [SerializeField] private LocalizationTableAsset _table;

        public string Locale => _locale ?? string.Empty;
        public string DisplayName => _displayName ?? string.Empty;
        public bool Enabled => _enabled;
        public LocalizationTableAsset Table => _table;

        public void Configure(
            string locale,
            string displayName,
            LocalizationTableAsset table,
            bool enabled = true)
        {
            _locale = locale == null ? string.Empty : locale.Trim();
            _displayName = displayName == null ? string.Empty : displayName.Trim();
            _table = table;
            _enabled = enabled;
        }
    }

    /// <summary>
    /// Editor-only localization production workspace.
    /// It describes which catalog/source/target locales participate in scan and translation exchange.
    /// Runtime Core never references this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationWorkspace",
        menuName = "Stellar Framework/Localization/Workspace")]
    public sealed class LocalizationWorkspaceAsset : ScriptableObject
    {
        [SerializeField] private LocalizationCatalogAsset _catalog;
        [SerializeField] private string _sourceLocale = "zh-CN";
        [SerializeField] private List<LocalizationWorkspaceLanguage> _languages =
            new List<LocalizationWorkspaceLanguage>();

        public LocalizationCatalogAsset Catalog => _catalog;
        public string SourceLocale => _sourceLocale ?? string.Empty;
        public IReadOnlyList<LocalizationWorkspaceLanguage> Languages => _languages;

        public void Configure(
            LocalizationCatalogAsset catalog,
            string sourceLocale,
            IEnumerable<LocalizationWorkspaceLanguage> languages)
        {
            _catalog = catalog;
            _sourceLocale = sourceLocale == null ? string.Empty : sourceLocale.Trim();
            _languages = languages == null
                ? new List<LocalizationWorkspaceLanguage>()
                : new List<LocalizationWorkspaceLanguage>(languages.Where(language => language != null));
        }

        public bool TryGetLanguage(string locale, out LocalizationWorkspaceLanguage language)
        {
            language = _languages.FirstOrDefault(item =>
                item != null &&
                string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase));
            return language != null;
        }

        public bool TryGetSourceTable(out LocalizationTableAsset table, out string error)
        {
            table = null;
            if (!LocaleId.TryCreate(SourceLocale, out _, out error))
            {
                return false;
            }

            if (TryGetLanguage(SourceLocale, out LocalizationWorkspaceLanguage sourceLanguage) &&
                sourceLanguage.Table != null)
            {
                table = sourceLanguage.Table;
                error = null;
                return true;
            }

            if (_catalog != null)
            {
                foreach (LocalizationTableAsset candidate in _catalog.Tables)
                {
                    if (candidate != null &&
                        string.Equals(candidate.Locale, SourceLocale, StringComparison.OrdinalIgnoreCase))
                    {
                        table = candidate;
                        error = null;
                        return true;
                    }
                }
            }

            error = $"Source table for locale '{SourceLocale}' is not configured.";
            return false;
        }

        public LocalizationWorkspaceLanguage[] GetEnabledLanguages()
        {
            return _languages
                .Where(language =>
                    language != null &&
                    language.Enabled &&
                    !string.IsNullOrWhiteSpace(language.Locale))
                .OrderBy(language => language.Locale, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
