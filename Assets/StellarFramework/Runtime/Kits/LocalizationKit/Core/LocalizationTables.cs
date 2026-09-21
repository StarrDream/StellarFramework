using System;
using System.Collections.Generic;

namespace StellarFramework.Localization
{
    /// <summary>
    /// 一条不可变的本地化文本记录。
    /// </summary>
    public readonly struct LocalizationEntry
    {
        /// <summary>文本键。</summary>
        public LocalizationKey Key { get; }
        /// <summary>文本模板；null 会规范化为空字符串。</summary>
        public string Value { get; }

        /// <summary>创建文本记录。</summary>
        public LocalizationEntry(LocalizationKey key, string value)
        {
            if (!key.IsValid)
                throw new ArgumentException("Localization key must be valid.", nameof(key));
            Key = key;
            Value = value ?? string.Empty;
        }
    }

    /// <summary>
    /// 单个 Locale 的不可变文本表。
    /// </summary>
    /// <remarks>
    /// 构造时复制输入、按 Key 排序并建立 Dictionary 查询索引。
    /// 重复 Key 会在构造阶段直接报错。
    /// </remarks>
    public sealed class LocalizationTable
    {
        private sealed class EntryComparer : IComparer<LocalizationEntry>
        {
            internal static readonly EntryComparer Instance = new EntryComparer();
            public int Compare(LocalizationEntry x, LocalizationEntry y) => x.Key.CompareTo(y.Key);
        }

        private readonly LocalizationEntry[] _entries;
        private readonly Dictionary<LocalizationKey, string> _values;

        /// <summary>该表所属 Locale。</summary>
        public LocaleId Locale { get; }
        /// <summary>条目数量。</summary>
        public int Count => _entries.Length;

        /// <summary>从条目 Span 构建独立表副本。</summary>
        public LocalizationTable(LocaleId locale, ReadOnlySpan<LocalizationEntry> entries)
        {
            if (!locale.IsValid)
                throw new ArgumentException("Locale must be valid.", nameof(locale));

            Locale = locale;
            _entries = new LocalizationEntry[entries.Length];
            entries.CopyTo(_entries);
            Array.Sort(_entries, EntryComparer.Instance);
            _values = new Dictionary<LocalizationKey, string>(_entries.Length);

            for (int i = 0; i < _entries.Length; i++)
            {
                LocalizationEntry entry = _entries[i];
                if (_values.ContainsKey(entry.Key))
                    throw new ArgumentException(
                        "Duplicate localization key '" + entry.Key + "' for locale '" + locale + "'.",
                        nameof(entries));
                _values.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>尝试按 Key 查询文本。</summary>
        public bool TryGet(LocalizationKey key, out string value)
        {
            if (!key.IsValid)
            {
                value = null;
                return false;
            }
            return _values.TryGetValue(key, out value);
        }

        /// <summary>
        /// 获取必需文本；不存在时抛出 <see cref="KeyNotFoundException"/>。
        /// </summary>
        public string GetRequired(LocalizationKey key)
        {
            if (!TryGet(key, out string value))
                throw new KeyNotFoundException(
                    "Localization key '" + key + "' does not exist for locale '" + Locale + "'.");
            return value;
        }

        /// <summary>按稳定 Key 排序后的索引取得条目。</summary>
        public LocalizationEntry GetEntry(int index)
        {
            if ((uint)index >= (uint)_entries.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _entries[index];
        }
    }

    /// <summary>
    /// 多语言表集合。
    /// </summary>
    /// <remarks>
    /// 构造时复制并按 Locale 排序；同一个 Locale 只能出现一次。
    /// </remarks>
    public sealed class LocalizationCatalog
    {
        private sealed class TableComparer : IComparer<LocalizationTable>
        {
            internal static readonly TableComparer Instance = new TableComparer();
            public int Compare(LocalizationTable x, LocalizationTable y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return x.Locale.CompareTo(y.Locale);
            }
        }

        private readonly LocalizationTable[] _tables;
        private readonly Dictionary<LocaleId, LocalizationTable> _lookup;

        /// <summary>语言表数量。</summary>
        public int Count => _tables.Length;

        /// <summary>构建不可变 Catalog。</summary>
        public LocalizationCatalog(ReadOnlySpan<LocalizationTable> tables)
        {
            _tables = new LocalizationTable[tables.Length];
            tables.CopyTo(_tables);
            Array.Sort(_tables, TableComparer.Instance);
            _lookup = new Dictionary<LocaleId, LocalizationTable>(_tables.Length);

            for (int i = 0; i < _tables.Length; i++)
            {
                LocalizationTable table = _tables[i];
                if (table == null)
                    throw new ArgumentException("Localization catalog cannot contain null tables.", nameof(tables));
                if (_lookup.ContainsKey(table.Locale))
                    throw new ArgumentException(
                        "Duplicate localization locale '" + table.Locale + "'.", nameof(tables));
                _lookup.Add(table.Locale, table);
            }
        }

        /// <summary>判断是否包含指定 Locale。</summary>
        public bool Contains(LocaleId locale) => locale.IsValid && _lookup.ContainsKey(locale);

        /// <summary>尝试取得指定 Locale 的语言表。</summary>
        public bool TryGetTable(LocaleId locale, out LocalizationTable table)
        {
            if (!locale.IsValid)
            {
                table = null;
                return false;
            }
            return _lookup.TryGetValue(locale, out table);
        }

        /// <summary>按 Locale 排序后的索引取得语言表。</summary>
        public LocalizationTable GetTable(int index)
        {
            if ((uint)index >= (uint)_tables.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _tables[index];
        }
    }

    /// <summary>
    /// 当前语言缺 Key 时使用的有序回退语言列表。
    /// </summary>
    /// <remarks>
    /// 顺序即查找优先级。LocalizationService 构造时会验证所有回退 Locale 都存在于 Catalog。
    /// </remarks>
    public sealed class LocalizationFallbackPolicy
    {
        private static readonly LocalizationFallbackPolicy Empty =
            new LocalizationFallbackPolicy(Array.Empty<LocaleId>());
        private readonly LocaleId[] _locales;

        /// <summary>不执行任何回退的共享空策略。</summary>
        public static LocalizationFallbackPolicy None => Empty;
        /// <summary>回退 Locale 数量。</summary>
        public int Count => _locales.Length;

        /// <summary>创建有序回退策略。无效或重复 Locale 会直接抛错。</summary>
        public LocalizationFallbackPolicy(ReadOnlySpan<LocaleId> locales)
        {
            _locales = new LocaleId[locales.Length];
            locales.CopyTo(_locales);
            for (int i = 0; i < _locales.Length; i++)
            {
                if (!_locales[i].IsValid)
                    throw new ArgumentException(
                        "Fallback locale at index " + i + " is invalid.", nameof(locales));
                for (int j = 0; j < i; j++)
                {
                    if (_locales[j] == _locales[i])
                        throw new ArgumentException(
                            "Fallback locale '" + _locales[i] + "' is duplicated.", nameof(locales));
                }
            }
        }

        /// <summary>按配置顺序取得回退 Locale。</summary>
        public LocaleId GetLocale(int index)
        {
            if ((uint)index >= (uint)_locales.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _locales[index];
        }
    }
}
