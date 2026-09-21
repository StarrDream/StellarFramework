using System;
using System.Collections.Generic;

namespace StellarFramework.Localization
{
    /// <summary>
    /// 一次文本查询的结果状态。
    /// </summary>
    public enum LocalizationLookupStatus
    {
        None = 0,
        Success = 1,
        MissingLocale = 2,
        MissingKey = 3
    }

    /// <summary>
    /// 本地化查询结果，包含请求 Locale、最终解析 Locale 与是否使用回退。
    /// </summary>
    public readonly struct LocalizationLookupResult
    {
        /// <summary>查询状态。</summary>
        public LocalizationLookupStatus Status { get; }
        /// <summary>调用方请求的 Locale。</summary>
        public LocaleId RequestedLocale { get; }
        /// <summary>实际找到文本的 Locale；失败时可能无效。</summary>
        public LocaleId ResolvedLocale { get; }
        /// <summary>查询 Key。</summary>
        public LocalizationKey Key { get; }
        /// <summary>查询到的文本；失败时为空字符串。</summary>
        public string Value { get; }
        /// <summary>是否通过 FallbackPolicy 找到结果。</summary>
        public bool UsedFallback { get; }
        /// <summary>是否查询成功。</summary>
        public bool Success => Status == LocalizationLookupStatus.Success;

        internal LocalizationLookupResult(
            LocalizationLookupStatus status,
            LocaleId requestedLocale,
            LocaleId resolvedLocale,
            LocalizationKey key,
            string value,
            bool usedFallback)
        {
            Status = status;
            RequestedLocale = requestedLocale;
            ResolvedLocale = resolvedLocale;
            Key = key;
            Value = value ?? string.Empty;
            UsedFallback = usedFallback;
        }
    }

    /// <summary>
    /// LocaleChanged 事件参数。
    /// </summary>
    public sealed class LocalizationChangedEventArgs : EventArgs
    {
        /// <summary>切换前 Locale。</summary>
        public LocaleId PreviousLocale { get; }
        /// <summary>切换后 Locale。</summary>
        public LocaleId CurrentLocale { get; }

        /// <summary>创建语言切换事件参数。</summary>
        public LocalizationChangedEventArgs(LocaleId previousLocale, LocaleId currentLocale)
        {
            PreviousLocale = previousLocale;
            CurrentLocale = currentLocale;
        }
    }

    /// <summary>
    /// LocalizationKit Core 的运行时查询服务。
    /// </summary>
    /// <remarks>
    /// Service 不依赖 UnityEngine。当前 Locale 查询时会先查当前表，再按 FallbackPolicy 顺序查找。
    /// 显式指定 Locale 的 Lookup(locale,key) 不使用 fallback，适合内容验证与工具场景。
    /// </remarks>
    public sealed class LocalizationService
    {
        private readonly LocalizationCatalog _catalog;
        private readonly LocalizationFallbackPolicy _fallbackPolicy;
        private LocalizationTable _currentTable;

        /// <summary>语言切换成功后同步触发。</summary>
        public event EventHandler<LocalizationChangedEventArgs> LocaleChanged;

        /// <summary>服务使用的不可变 Catalog。</summary>
        public LocalizationCatalog Catalog => _catalog;
        /// <summary>当前 Locale。</summary>
        public LocaleId CurrentLocale => _currentTable.Locale;
        /// <summary>当前回退策略。</summary>
        public LocalizationFallbackPolicy FallbackPolicy => _fallbackPolicy;

        /// <summary>
        /// 创建本地化服务。
        /// initialLocale 与所有 fallback Locale 都必须存在于 Catalog。
        /// </summary>
        public LocalizationService(
            LocalizationCatalog catalog,
            LocaleId initialLocale,
            LocalizationFallbackPolicy fallbackPolicy = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!initialLocale.IsValid)
                throw new ArgumentException("Initial locale must be valid.", nameof(initialLocale));
            if (!_catalog.TryGetTable(initialLocale, out _currentTable))
                throw new ArgumentException(
                    "Initial locale '" + initialLocale + "' does not exist in the catalog.",
                    nameof(initialLocale));

            _fallbackPolicy = fallbackPolicy ?? LocalizationFallbackPolicy.None;
            for (int i = 0; i < _fallbackPolicy.Count; i++)
            {
                LocaleId fallback = _fallbackPolicy.GetLocale(i);
                if (!_catalog.Contains(fallback))
                    throw new ArgumentException(
                        "Fallback locale '" + fallback + "' does not exist in the catalog.",
                        nameof(fallbackPolicy));
            }
        }

        /// <summary>
        /// 切换当前 Locale。
        /// 切换到当前 Locale 视为成功，但不会重复触发 LocaleChanged。
        /// </summary>
        public bool SetLocale(LocaleId locale, out string error)
        {
            error = null;
            if (!locale.IsValid)
            {
                error = "Locale must be valid.";
                return false;
            }
            if (!_catalog.TryGetTable(locale, out LocalizationTable next))
            {
                error = "Locale '" + locale + "' does not exist in the catalog.";
                return false;
            }
            if (next.Locale == _currentTable.Locale) return true;

            LocaleId previous = _currentTable.Locale;
            _currentTable = next;
            LocaleChanged?.Invoke(
                this,
                new LocalizationChangedEventArgs(previous, _currentTable.Locale));
            return true;
        }

        /// <summary>在当前 Locale 及回退策略中查找 Key。</summary>
        public LocalizationLookupResult Lookup(LocalizationKey key)
        {
            if (!key.IsValid)
                throw new ArgumentException("Localization key must be valid.", nameof(key));

            LocaleId requested = _currentTable.Locale;
            if (_currentTable.TryGet(key, out string value))
                return Success(requested, requested, key, value, false);

            for (int i = 0; i < _fallbackPolicy.Count; i++)
            {
                LocaleId fallback = _fallbackPolicy.GetLocale(i);
                if (fallback == requested) continue;
                if (_catalog.TryGetTable(fallback, out LocalizationTable table) &&
                    table.TryGet(key, out value))
                    return Success(requested, fallback, key, value, true);
            }

            return new LocalizationLookupResult(
                LocalizationLookupStatus.MissingKey,
                requested,
                default(LocaleId),
                key,
                string.Empty,
                false);
        }

        /// <summary>只在指定 Locale 中查找 Key，不应用 FallbackPolicy。</summary>
        public LocalizationLookupResult Lookup(LocaleId locale, LocalizationKey key)
        {
            if (!locale.IsValid)
                throw new ArgumentException("Locale must be valid.", nameof(locale));
            if (!key.IsValid)
                throw new ArgumentException("Localization key must be valid.", nameof(key));

            if (!_catalog.TryGetTable(locale, out LocalizationTable table))
                return new LocalizationLookupResult(
                    LocalizationLookupStatus.MissingLocale, locale, default(LocaleId), key, string.Empty, false);
            if (!table.TryGet(key, out string value))
                return new LocalizationLookupResult(
                    LocalizationLookupStatus.MissingKey, locale, default(LocaleId), key, string.Empty, false);

            return Success(locale, locale, key, value, false);
        }

        /// <summary>在当前 Locale/Fallback 中尝试取得文本。</summary>
        public bool TryGet(LocalizationKey key, out string value)
        {
            LocalizationLookupResult result = Lookup(key);
            value = result.Success ? result.Value : null;
            return result.Success;
        }

        /// <summary>
        /// 获取必需文本；当前 Locale 与全部 fallback 都缺失时抛出 <see cref="KeyNotFoundException"/>。
        /// </summary>
        public string GetRequired(LocalizationKey key)
        {
            LocalizationLookupResult result = Lookup(key);
            if (!result.Success)
                throw new KeyNotFoundException(
                    "Localization key '" + key + "' was not found for locale '" +
                    CurrentLocale + "' or its configured fallbacks.");
            return result.Value;
        }

        /// <summary>
        /// 查找当前文本模板并使用命名参数格式化。
        /// </summary>
        public bool TryFormat(
            LocalizationKey key,
            ReadOnlySpan<LocalizationFormatArgument> arguments,
            out string value,
            out string error)
        {
            LocalizationLookupResult lookup = Lookup(key);
            if (!lookup.Success)
            {
                value = null;
                error = "Localization lookup failed with status " + lookup.Status +
                    " for key '" + key + "'.";
                return false;
            }

            return LocalizationTemplateFormatter.TryFormat(
                lookup.Value, arguments, out value, out error);
        }

        private static LocalizationLookupResult Success(
            LocaleId requested,
            LocaleId resolved,
            LocalizationKey key,
            string value,
            bool usedFallback)
        {
            return new LocalizationLookupResult(
                LocalizationLookupStatus.Success,
                requested,
                resolved,
                key,
                value,
                usedFallback);
        }
    }
}
