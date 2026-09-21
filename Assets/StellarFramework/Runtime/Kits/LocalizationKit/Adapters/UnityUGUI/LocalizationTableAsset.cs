using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Localization.UnityUGUI
{
    /// <summary>
    /// 单个 Locale 的 Unity Authoring 资产。
    /// 运行时通过 <see cref="TryBuild"/> 转换成不可变 <see cref="LocalizationTable"/>。
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationTable",
        menuName = "Stellar Framework/Localization/Table")]
    public sealed class LocalizationTableAsset : ScriptableObject
    {
        [SerializeField] private string _locale = "zh-CN";
        [SerializeField] private LocalizationAuthoringEntry[] _entries =
            Array.Empty<LocalizationAuthoringEntry>();

        /// <summary>Authoring Locale 字符串。</summary>
        public string Locale => _locale ?? string.Empty;
        /// <summary>Authoring 条目只读视图。</summary>
        public IReadOnlyList<LocalizationAuthoringEntry> Entries => _entries;

        /// <summary>
        /// 代码式配置 Authoring 数据，主要用于测试、生成器和工具链。
        /// </summary>
        public void Configure(string locale, LocalizationAuthoringEntry[] entries)
        {
            _locale = locale;
            _entries = entries ?? Array.Empty<LocalizationAuthoringEntry>();
        }

        /// <summary>
        /// 校验 Authoring 数据并构建纯 C# LocalizationTable。
        /// </summary>
        /// <returns>Locale、Key、重复项均合法时返回 true。</returns>
        public bool TryBuild(out LocalizationTable table, out string error)
        {
            table = null;
            if (!LocaleId.TryCreate(_locale, out LocaleId locale, out error))
                return false;

            var entries = new LocalizationEntry[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
            {
                LocalizationAuthoringEntry source = _entries[i];
                if (source == null)
                {
                    error = "Localization entry at index " + i + " is null.";
                    return false;
                }
                if (!LocalizationKey.TryCreate(source.Key, out LocalizationKey key, out error))
                {
                    error = "Entry " + i + ": " + error;
                    return false;
                }
                entries[i] = new LocalizationEntry(key, source.Value);
            }

            try
            {
                table = new LocalizationTable(locale, entries);
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
