using System;
using System.Collections.Generic;
using System.Linq;
using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Localization.Editor
{
    public enum LocalizationValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public readonly struct LocalizationValidationIssue
    {
        public LocalizationValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string Locale { get; }
        public string Key { get; }

        public LocalizationValidationIssue(
            LocalizationValidationSeverity severity,
            string code,
            string message,
            string locale = "",
            string key = "")
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Locale = locale ?? string.Empty;
            Key = key ?? string.Empty;
        }
    }

    public sealed class LocalizationCoverageReport
    {
        private readonly LocalizationValidationIssue[] _issues;

        public IReadOnlyList<LocalizationValidationIssue> Issues => _issues;
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public int RequiredLocaleCount { get; }
        public int UnionKeyCount { get; }
        public int RequiredCellCount { get; }
        public int PresentCellCount { get; }
        public double CoveragePercent =>
            RequiredCellCount == 0 ? 100.0 : PresentCellCount * 100.0 / RequiredCellCount;
        public bool IsValid => ErrorCount == 0;

        internal LocalizationCoverageReport(
            List<LocalizationValidationIssue> issues,
            int requiredLocaleCount,
            int unionKeyCount,
            int requiredCellCount,
            int presentCellCount)
        {
            _issues = issues.ToArray();
            RequiredLocaleCount = requiredLocaleCount;
            UnionKeyCount = unionKeyCount;
            RequiredCellCount = requiredCellCount;
            PresentCellCount = presentCellCount;

            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < _issues.Length; i++)
            {
                if (_issues[i].Severity == LocalizationValidationSeverity.Error) errors++;
                else warnings++;
            }
            ErrorCount = errors;
            WarningCount = warnings;
        }
    }

    public static class LocalizationCatalogValidator
    {
        private static readonly string[] DefaultRequiredLocales = { "zh-CN", "en-US" };

        public static LocalizationCoverageReport Validate(
            LocalizationCatalogAsset catalog,
            IReadOnlyList<string> requiredLocales = null)
        {
            var issues = new List<LocalizationValidationIssue>();
            if (catalog == null)
            {
                issues.Add(Error("CatalogMissing", "LocalizationCatalogAsset is null."));
                return new LocalizationCoverageReport(issues, 0, 0, 0, 0);
            }

            IReadOnlyList<string> required =
                requiredLocales == null || requiredLocales.Count == 0
                    ? DefaultRequiredLocales
                    : requiredLocales;

            var localeKeys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var localeNonEmptyKeys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var localeValues = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var allLocales = new HashSet<string>(StringComparer.Ordinal);
            var unionKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int tableIndex = 0; tableIndex < catalog.Tables.Count; tableIndex++)
            {
                LocalizationTableAsset table = catalog.Tables[tableIndex];
                if (table == null)
                {
                    issues.Add(Error("NullTable", "Localization table at index " + tableIndex + " is null."));
                    continue;
                }

                if (!LocaleId.TryCreate(table.Locale, out LocaleId localeId, out string localeError))
                {
                    issues.Add(Error("InvalidLocale", localeError, table.Locale));
                    continue;
                }
                string locale = localeId.Value;
                if (!allLocales.Add(locale))
                {
                    issues.Add(Error("DuplicateLocale", "Locale '" + locale + "' is duplicated.", locale));
                    continue;
                }

                var keys = new HashSet<string>(StringComparer.Ordinal);
                var nonEmpty = new HashSet<string>(StringComparer.Ordinal);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int entryIndex = 0; entryIndex < table.Entries.Count; entryIndex++)
                {
                    LocalizationAuthoringEntry entry = table.Entries[entryIndex];
                    if (entry == null)
                    {
                        issues.Add(Error(
                            "NullEntry",
                            "Entry at index " + entryIndex + " is null.",
                            locale));
                        continue;
                    }
                    if (!LocalizationKey.TryCreate(entry.Key, out LocalizationKey key, out string keyError))
                    {
                        issues.Add(Error("InvalidKey", keyError, locale, entry.Key));
                        continue;
                    }
                    if (!keys.Add(key.Value))
                    {
                        issues.Add(Error(
                            "DuplicateKey",
                            "Key '" + key.Value + "' is duplicated in locale '" + locale + "'.",
                            locale,
                            key.Value));
                        continue;
                    }
                    unionKeys.Add(key.Value);
                    if (string.IsNullOrEmpty(entry.Value))
                    {
                        issues.Add(Error(
                            "EmptyValue",
                            "Key '" + key.Value + "' has an empty value.",
                            locale,
                            key.Value));
                    }
                    else
                    {
                        nonEmpty.Add(key.Value);
                        values.Add(key.Value, entry.Value);
                    }
                }

                localeKeys.Add(locale, keys);
                localeNonEmptyKeys.Add(locale, nonEmpty);
                localeValues.Add(locale, values);
            }

            ValidateInitialAndFallback(catalog, allLocales, issues);

            int presentCells = 0;
            var canonicalRequired = new string[required.Count];
            for (int i = 0; i < required.Count; i++)
            {
                if (!LocaleId.TryCreate(required[i], out LocaleId requiredId, out string error))
                {
                    issues.Add(Error("InvalidRequiredLocale", error, required[i]));
                    canonicalRequired[i] = required[i] ?? string.Empty;
                    continue;
                }

                string locale = requiredId.Value;
                canonicalRequired[i] = locale;
                if (!allLocales.Contains(locale))
                {
                    issues.Add(Error(
                        "MissingRequiredLocale",
                        "Required locale '" + locale + "' is missing.",
                        locale));
                    continue;
                }

                HashSet<string> nonEmpty = localeNonEmptyKeys[locale];
                presentCells += nonEmpty.Count;
            }

            foreach (string key in unionKeys.OrderBy(value => value, StringComparer.Ordinal))
            {
                for (int i = 0; i < canonicalRequired.Length; i++)
                {
                    string locale = canonicalRequired[i];
                    if (!localeKeys.TryGetValue(locale, out HashSet<string> keys) || !keys.Contains(key))
                    {
                        issues.Add(Error(
                            "MissingKey",
                            "Required locale '" + locale + "' is missing key '" + key + "'.",
                            locale,
                            key));
                    }
                }
            }

            ValidateTemplateContracts(
                unionKeys,
                canonicalRequired,
                localeValues,
                issues);

            int requiredCells = unionKeys.Count * required.Count;
            return new LocalizationCoverageReport(
                issues,
                required.Count,
                unionKeys.Count,
                requiredCells,
                Math.Min(presentCells, requiredCells));
        }

        public static void ValidateSelectedCatalogAndShowReport()
        {
            ValidateAndShowReport(Selection.activeObject as LocalizationCatalogAsset);
        }

        public static void ValidateAndShowReport(LocalizationCatalogAsset catalog)
        {
            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "Localization Validator",
                    "请选择一个 LocalizationCatalogAsset。",
                    "OK");
                return;
            }

            LocalizationCoverageReport report = Validate(catalog);
            string summary =
                "Coverage: " + report.CoveragePercent.ToString("F2") + "%\n" +
                "Errors: " + report.ErrorCount + "\n" +
                "Warnings: " + report.WarningCount + "\n" +
                "Keys: " + report.UnionKeyCount;

            if (report.IsValid)
            {
                Debug.Log("[LocalizationValidator] " + catalog.name + "\n" + summary, catalog);
                EditorUtility.DisplayDialog("Localization Validator", summary, "OK");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                LocalizationValidationIssue issue = report.Issues[i];
                if (issue.Severity == LocalizationValidationSeverity.Error)
                    Debug.LogError("[LocalizationValidator][" + issue.Code + "] " + issue.Message, catalog);
                else
                    Debug.LogWarning("[LocalizationValidator][" + issue.Code + "] " + issue.Message, catalog);
            }
            EditorUtility.DisplayDialog("Localization Validator", summary, "OK");
        }

        private static void ValidateTemplateContracts(
            HashSet<string> unionKeys,
            string[] requiredLocales,
            Dictionary<string, Dictionary<string, string>> localeValues,
            List<LocalizationValidationIssue> issues)
        {
            var referenceNames = new HashSet<string>(StringComparer.Ordinal);
            var currentNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (string key in unionKeys.OrderBy(value => value, StringComparer.Ordinal))
            {
                bool hasReference = false;
                string referenceLocale = string.Empty;
                referenceNames.Clear();

                for (int i = 0; i < requiredLocales.Length; i++)
                {
                    string locale = requiredLocales[i];
                    if (!localeValues.TryGetValue(locale, out Dictionary<string, string> values) ||
                        !values.TryGetValue(key, out string template))
                        continue;

                    currentNames.Clear();
                    if (!LocalizationTemplateFormatter.TryGetArgumentNames(
                        template, currentNames, out string error))
                    {
                        issues.Add(Error(
                            "InvalidTemplate",
                            "Key '" + key + "' has an invalid template: " + error,
                            locale,
                            key));
                        continue;
                    }

                    if (!hasReference)
                    {
                        referenceNames.UnionWith(currentNames);
                        referenceLocale = locale;
                        hasReference = true;
                        continue;
                    }

                    if (!referenceNames.SetEquals(currentNames))
                    {
                        issues.Add(Error(
                            "PlaceholderMismatch",
                            "Key '" + key + "' placeholder set for locale '" + locale +
                            "' does not match reference locale '" + referenceLocale + "'.",
                            locale,
                            key));
                    }
                }
            }
        }

        private static void ValidateInitialAndFallback(
            LocalizationCatalogAsset catalog,
            HashSet<string> locales,
            List<LocalizationValidationIssue> issues)
        {
            if (!LocaleId.TryCreate(catalog.InitialLocale, out LocaleId initial, out string initialError))
            {
                issues.Add(Error("InvalidInitialLocale", initialError, catalog.InitialLocale));
            }
            else if (!locales.Contains(initial.Value))
            {
                issues.Add(Error(
                    "MissingInitialLocale",
                    "Initial locale '" + initial.Value + "' does not exist in the catalog.",
                    initial.Value));
            }

            var seenFallback = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.FallbackLocales.Count; i++)
            {
                string raw = catalog.FallbackLocales[i];
                if (!LocaleId.TryCreate(raw, out LocaleId fallback, out string fallbackError))
                {
                    issues.Add(Error("InvalidFallbackLocale", fallbackError, raw));
                    continue;
                }
                if (!seenFallback.Add(fallback.Value))
                {
                    issues.Add(Error(
                        "DuplicateFallbackLocale",
                        "Fallback locale '" + fallback.Value + "' is duplicated.",
                        fallback.Value));
                    continue;
                }
                if (!locales.Contains(fallback.Value))
                {
                    issues.Add(Error(
                        "MissingFallbackLocale",
                        "Fallback locale '" + fallback.Value + "' does not exist in the catalog.",
                        fallback.Value));
                }
            }
        }

        private static LocalizationValidationIssue Error(
            string code,
            string message,
            string locale = "",
            string key = "") =>
            new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error, code, message, locale, key);
    }
}
