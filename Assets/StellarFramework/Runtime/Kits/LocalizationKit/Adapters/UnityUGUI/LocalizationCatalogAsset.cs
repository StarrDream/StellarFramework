using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Localization.UnityUGUI
{
    /// <summary>
    /// Unity Authoring Catalog。
    /// 聚合多语言 Table、初始语言与有序 Fallback，并负责构建纯 C# LocalizationService。
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationCatalog",
        menuName = "Stellar Framework/Localization/Catalog")]
    public sealed class LocalizationCatalogAsset : ScriptableObject
    {
        [SerializeField] private LocalizationTableAsset[] _tables =
            Array.Empty<LocalizationTableAsset>();
        [SerializeField] private string _initialLocale = "zh-CN";
        [SerializeField] private string[] _fallbackLocales = Array.Empty<string>();

        /// <summary>配置的语言表。</summary>
        public IReadOnlyList<LocalizationTableAsset> Tables => _tables;
        /// <summary>启动时使用的 Locale。</summary>
        public string InitialLocale => _initialLocale ?? string.Empty;
        /// <summary>按顺序应用的 Fallback Locale。</summary>
        public IReadOnlyList<string> FallbackLocales => _fallbackLocales;

        /// <summary>
        /// 代码式配置 Catalog Authoring 数据。
        /// </summary>
        public void Configure(
            LocalizationTableAsset[] tables,
            string initialLocale,
            string[] fallbackLocales = null)
        {
            _tables = tables ?? Array.Empty<LocalizationTableAsset>();
            _initialLocale = initialLocale;
            _fallbackLocales = fallbackLocales ?? Array.Empty<string>();
        }

        /// <summary>
        /// 校验全部 Authoring 资产并构建 LocalizationService。
        /// </summary>
        /// <returns>
        /// 任一 Table、Locale、Fallback 或重复配置非法时返回 false，并提供 error。
        /// </returns>
        public bool TryBuildService(out LocalizationService service, out string error)
        {
            service = null;
            var tables = new LocalizationTable[_tables.Length];
            for (int i = 0; i < _tables.Length; i++)
            {
                LocalizationTableAsset tableAsset = _tables[i];
                if (tableAsset == null)
                {
                    error = "Localization table asset at index " + i + " is null.";
                    return false;
                }
                if (!tableAsset.TryBuild(out tables[i], out error))
                {
                    error = tableAsset.name + ": " + error;
                    return false;
                }
            }

            if (!LocaleId.TryCreate(_initialLocale, out LocaleId initialLocale, out error))
                return false;

            var fallbackLocales = new LocaleId[_fallbackLocales.Length];
            for (int i = 0; i < _fallbackLocales.Length; i++)
            {
                if (!LocaleId.TryCreate(_fallbackLocales[i], out fallbackLocales[i], out error))
                {
                    error = "Fallback " + i + ": " + error;
                    return false;
                }
            }

            try
            {
                var catalog = new LocalizationCatalog(tables);
                var fallback = new LocalizationFallbackPolicy(fallbackLocales);
                service = new LocalizationService(catalog, initialLocale, fallback);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
