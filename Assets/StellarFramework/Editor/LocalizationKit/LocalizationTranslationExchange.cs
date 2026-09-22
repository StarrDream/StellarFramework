using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Localization.Editor
{
    public enum LocalizationTranslationImportStatus
    {
        Applied = 0,
        SkippedEmpty = 1,
        MissingKey = 2,
        MissingTargetTable = 3,
        StaleSource = 4,
        InvalidLocale = 5
    }

    public sealed class LocalizationTranslationImportIssue
    {
        public string Key;
        public string Locale;
        public LocalizationTranslationImportStatus Status;
        public string Message;
    }

    public sealed class LocalizationTranslationImportReport
    {
        public int AppliedCount;
        public int SkippedCount;
        public readonly List<LocalizationTranslationImportIssue> Issues =
            new List<LocalizationTranslationImportIssue>();

        public bool HasBlockingIssues => Issues.Any(issue =>
            issue.Status == LocalizationTranslationImportStatus.StaleSource ||
            issue.Status == LocalizationTranslationImportStatus.MissingTargetTable ||
            issue.Status == LocalizationTranslationImportStatus.InvalidLocale);
    }

    [Serializable]
    internal sealed class LocalizationExchangeDocument
    {
        public int schemaVersion = 1;
        public string sourceLocale;
        public string[] languages;
        public LocalizationExchangeEntry[] entries;
    }

    [Serializable]
    internal sealed class LocalizationExchangeEntry
    {
        public string key;
        public string bindingId;
        public string source;
        public string sourceHash;
        public LocalizationExchangeContext context;
        public LocalizationExchangeTranslation[] translations;
    }

    [Serializable]
    internal sealed class LocalizationExchangeContext
    {
        public string prefabGuid;
        public string hierarchy;
        public string componentType;
    }

    [Serializable]
    internal sealed class LocalizationExchangeTranslation
    {
        public string locale;
        public string value;
    }

    /// <summary>
    /// External translation exchange. Unity exports structured files; translation may happen manually or through any external AI.
    /// Import never calls remote services and rejects stale source hashes by default.
    /// </summary>
    public static class LocalizationTranslationExchange
    {
        public const int SchemaVersion = 1;

        public static bool TryExportJson(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            string filePath,
            out string error)
        {
            if (!TryBuildDocument(workspace, registry, out LocalizationExchangeDocument document, out error))
            {
                return false;
            }

            EnsureParentDirectory(filePath);
            File.WriteAllText(
                filePath,
                JsonUtility.ToJson(document, true),
                new UTF8Encoding(false));
            error = null;
            return true;
        }

        public static bool TryExportCsv(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            string filePath,
            out string error)
        {
            if (!TryBuildDocument(workspace, registry, out LocalizationExchangeDocument document, out error))
            {
                return false;
            }

            var builder = new StringBuilder(4096);
            var header = new List<string>
            {
                "key",
                "bindingId",
                "source",
                "sourceHash",
                "prefabGuid",
                "hierarchy",
                "componentType"
            };
            header.AddRange(document.languages.Where(locale =>
                !string.Equals(locale, document.sourceLocale, StringComparison.OrdinalIgnoreCase)));
            AppendCsvRow(builder, header);

            foreach (LocalizationExchangeEntry entry in document.entries)
            {
                var row = new List<string>
                {
                    entry.key,
                    entry.bindingId,
                    entry.source,
                    entry.sourceHash,
                    entry.context?.prefabGuid ?? string.Empty,
                    entry.context?.hierarchy ?? string.Empty,
                    entry.context?.componentType ?? string.Empty
                };

                foreach (string locale in document.languages.Where(locale =>
                             !string.Equals(locale, document.sourceLocale, StringComparison.OrdinalIgnoreCase)))
                {
                    LocalizationExchangeTranslation translation = entry.translations?
                        .FirstOrDefault(item =>
                            string.Equals(item.locale, locale, StringComparison.OrdinalIgnoreCase));
                    row.Add(translation?.value ?? string.Empty);
                }
                AppendCsvRow(builder, row);
            }

            EnsureParentDirectory(filePath);
            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(true));
            error = null;
            return true;
        }

        public static bool TryImportJson(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            string filePath,
            out LocalizationTranslationImportReport report,
            out string error)
        {
            report = null;
            if (!File.Exists(filePath))
            {
                error = "Translation JSON does not exist: " + filePath;
                return false;
            }

            LocalizationExchangeDocument document;
            try
            {
                document = JsonUtility.FromJson<LocalizationExchangeDocument>(
                    File.ReadAllText(filePath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                error = "Failed to parse translation JSON: " + exception.Message;
                return false;
            }

            if (!ValidateDocument(document, out error))
            {
                return false;
            }

            report = ApplyDocument(workspace, registry, document);
            return true;
        }

        public static bool TryImportCsv(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            string filePath,
            out LocalizationTranslationImportReport report,
            out string error)
        {
            report = null;
            if (!File.Exists(filePath))
            {
                error = "Translation CSV does not exist: " + filePath;
                return false;
            }

            List<string[]> rows;
            try
            {
                rows = ParseCsv(File.ReadAllText(filePath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                error = "Failed to parse translation CSV: " + exception.Message;
                return false;
            }

            if (rows.Count == 0)
            {
                error = "Translation CSV is empty.";
                return false;
            }

            string[] header = rows[0];
            var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(header[i]))
                {
                    indexByName[header[i].Trim()] = i;
                }
            }

            string[] requiredColumns =
            {
                "key", "bindingId", "source", "sourceHash", "prefabGuid", "hierarchy", "componentType"
            };
            if (requiredColumns.Any(column => !indexByName.ContainsKey(column)))
            {
                error = "Translation CSV is missing one or more required metadata columns.";
                return false;
            }

            string sourceLocale = workspace == null ? string.Empty : workspace.SourceLocale;
            string[] languageColumns = header
                .Where(column =>
                    !string.IsNullOrWhiteSpace(column) &&
                    !requiredColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            var entries = new List<LocalizationExchangeEntry>();
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                if (row.Length == 0 || string.IsNullOrWhiteSpace(GetCell(row, indexByName["key"])))
                {
                    continue;
                }

                var translations = new List<LocalizationExchangeTranslation>();
                foreach (string locale in languageColumns)
                {
                    int index = indexByName[locale];
                    translations.Add(new LocalizationExchangeTranslation
                    {
                        locale = locale,
                        value = GetCell(row, index)
                    });
                }

                entries.Add(new LocalizationExchangeEntry
                {
                    key = GetCell(row, indexByName["key"]),
                    bindingId = GetCell(row, indexByName["bindingId"]),
                    source = GetCell(row, indexByName["source"]),
                    sourceHash = GetCell(row, indexByName["sourceHash"]),
                    context = new LocalizationExchangeContext
                    {
                        prefabGuid = GetCell(row, indexByName["prefabGuid"]),
                        hierarchy = GetCell(row, indexByName["hierarchy"]),
                        componentType = GetCell(row, indexByName["componentType"])
                    },
                    translations = translations.ToArray()
                });
            }

            var document = new LocalizationExchangeDocument
            {
                schemaVersion = SchemaVersion,
                sourceLocale = sourceLocale,
                languages = new[] { sourceLocale }.Concat(languageColumns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                entries = entries.ToArray()
            };
            if (!ValidateDocument(document, out error))
            {
                return false;
            }

            report = ApplyDocument(workspace, registry, document);
            return true;
        }

        private static bool TryBuildDocument(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            out LocalizationExchangeDocument document,
            out string error)
        {
            document = null;
            if (workspace == null)
            {
                error = "LocalizationWorkspaceAsset is not assigned.";
                return false;
            }
            if (!workspace.TryGetSourceTable(out LocalizationTableAsset sourceTable, out error))
            {
                return false;
            }

            registry = registry != null ? registry : LocalizationSourceRegistry.LoadOrCreate();
            LocalizationWorkspaceLanguage[] languages = workspace.GetEnabledLanguages();
            if (languages.Length == 0)
            {
                error = "Workspace has no enabled languages.";
                return false;
            }

            var tablesByLocale = new Dictionary<string, LocalizationTableAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationWorkspaceLanguage language in languages)
            {
                if (!LocaleId.TryCreate(language.Locale, out _, out error))
                {
                    return false;
                }
                if (language.Table != null)
                {
                    tablesByLocale[language.Locale] = language.Table;
                }
            }
            tablesByLocale[workspace.SourceLocale] = sourceTable;

            var valuesByLocale = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, LocalizationTableAsset> pair in tablesByLocale)
            {
                valuesByLocale[pair.Key] = pair.Value.Entries
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                    .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
            }

            var entries = new List<LocalizationExchangeEntry>();
            foreach (LocalizationAuthoringEntry sourceEntry in sourceTable.Entries
                         .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                         .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                LocalizationSourceRecord record = registry.Records.FirstOrDefault(item =>
                    string.Equals(item.Key, sourceEntry.Key, StringComparison.Ordinal));
                string sourceHash = LocalizationSourceRecord.ComputeSourceHash(sourceEntry.Value);

                var translations = new List<LocalizationExchangeTranslation>();
                foreach (LocalizationWorkspaceLanguage language in languages)
                {
                    if (string.Equals(language.Locale, workspace.SourceLocale, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string value = string.Empty;
                    if (valuesByLocale.TryGetValue(language.Locale, out Dictionary<string, string> tableValues))
                    {
                        tableValues.TryGetValue(sourceEntry.Key, out value);
                    }
                    translations.Add(new LocalizationExchangeTranslation
                    {
                        locale = language.Locale,
                        value = value ?? string.Empty
                    });
                }

                entries.Add(new LocalizationExchangeEntry
                {
                    key = sourceEntry.Key,
                    bindingId = record?.BindingId ?? string.Empty,
                    source = sourceEntry.Value,
                    sourceHash = sourceHash,
                    context = new LocalizationExchangeContext
                    {
                        prefabGuid = record?.PrefabGuid ?? string.Empty,
                        hierarchy = record?.CurrentHierarchy ?? string.Empty,
                        componentType = record?.ComponentType ?? string.Empty
                    },
                    translations = translations.ToArray()
                });
            }

            document = new LocalizationExchangeDocument
            {
                schemaVersion = SchemaVersion,
                sourceLocale = workspace.SourceLocale,
                languages = languages
                    .Select(language => language.Locale)
                    .Append(workspace.SourceLocale)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                entries = entries.ToArray()
            };
            error = null;
            return true;
        }

        private static LocalizationTranslationImportReport ApplyDocument(
            LocalizationWorkspaceAsset workspace,
            LocalizationSourceRegistry registry,
            LocalizationExchangeDocument document)
        {
            var report = new LocalizationTranslationImportReport();
            if (workspace == null)
            {
                report.Issues.Add(new LocalizationTranslationImportIssue
                {
                    Status = LocalizationTranslationImportStatus.MissingTargetTable,
                    Message = "LocalizationWorkspaceAsset is not assigned."
                });
                return report;
            }

            registry = registry != null ? registry : LocalizationSourceRegistry.LoadOrCreate();
            if (!workspace.TryGetSourceTable(out LocalizationTableAsset sourceTable, out string sourceError))
            {
                report.Issues.Add(new LocalizationTranslationImportIssue
                {
                    Status = LocalizationTranslationImportStatus.MissingTargetTable,
                    Message = sourceError
                });
                return report;
            }

            Dictionary<string, string> currentSources = sourceTable.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);

            var pendingByTable = new Dictionary<LocalizationTableAsset, Dictionary<string, string>>();
            foreach (LocalizationExchangeEntry entry in document.entries ?? Array.Empty<LocalizationExchangeEntry>())
            {
                if (!currentSources.TryGetValue(entry.key ?? string.Empty, out string currentSource))
                {
                    report.SkippedCount++;
                    report.Issues.Add(new LocalizationTranslationImportIssue
                    {
                        Key = entry.key,
                        Status = LocalizationTranslationImportStatus.MissingKey,
                        Message = "Key no longer exists in the source table."
                    });
                    continue;
                }

                string currentHash = LocalizationSourceRecord.ComputeSourceHash(currentSource);
                if (!string.Equals(currentHash, entry.sourceHash, StringComparison.Ordinal))
                {
                    report.SkippedCount++;
                    report.Issues.Add(new LocalizationTranslationImportIssue
                    {
                        Key = entry.key,
                        Status = LocalizationTranslationImportStatus.StaleSource,
                        Message = $"Source changed from exported '{entry.source}' to current '{currentSource}'."
                    });
                    if (!string.IsNullOrWhiteSpace(entry.bindingId) &&
                        registry.TryGet(entry.bindingId, out LocalizationSourceRecord record))
                    {
                        record.SetStatus(LocalizationSourceStatus.NeedsReview);
                        EditorUtility.SetDirty(registry);
                    }
                    continue;
                }

                foreach (LocalizationExchangeTranslation translation in
                         entry.translations ?? Array.Empty<LocalizationExchangeTranslation>())
                {
                    if (translation == null || string.IsNullOrWhiteSpace(translation.locale))
                    {
                        continue;
                    }
                    if (!LocaleId.TryCreate(translation.locale, out _, out string localeError))
                    {
                        report.SkippedCount++;
                        report.Issues.Add(new LocalizationTranslationImportIssue
                        {
                            Key = entry.key,
                            Locale = translation.locale,
                            Status = LocalizationTranslationImportStatus.InvalidLocale,
                            Message = localeError
                        });
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(translation.value))
                    {
                        report.SkippedCount++;
                        continue;
                    }
                    if (!workspace.TryGetLanguage(translation.locale, out LocalizationWorkspaceLanguage language) ||
                        language.Table == null)
                    {
                        report.SkippedCount++;
                        report.Issues.Add(new LocalizationTranslationImportIssue
                        {
                            Key = entry.key,
                            Locale = translation.locale,
                            Status = LocalizationTranslationImportStatus.MissingTargetTable,
                            Message = "Workspace has no table for this locale."
                        });
                        continue;
                    }

                    if (!pendingByTable.TryGetValue(language.Table, out Dictionary<string, string> pending))
                    {
                        pending = language.Table.Entries
                            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Key))
                            .GroupBy(item => item.Key, StringComparer.Ordinal)
                            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
                        pendingByTable.Add(language.Table, pending);
                    }
                    pending[entry.key] = translation.value;
                    report.AppliedCount++;
                }
            }

            foreach (KeyValuePair<LocalizationTableAsset, Dictionary<string, string>> pair in pendingByTable)
            {
                Undo.RecordObject(pair.Key, "Import Localization Translations");
                pair.Key.Configure(
                    pair.Key.Locale,
                    pair.Value
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .Select(item => new LocalizationAuthoringEntry(item.Key, item.Value))
                        .ToArray());
                EditorUtility.SetDirty(pair.Key);
            }
            AssetDatabase.SaveAssets();
            return report;
        }

        private static bool ValidateDocument(LocalizationExchangeDocument document, out string error)
        {
            if (document == null)
            {
                error = "Translation document is null.";
                return false;
            }
            if (document.schemaVersion != SchemaVersion)
            {
                error = $"Unsupported translation schema version {document.schemaVersion}.";
                return false;
            }
            if (!LocaleId.TryCreate(document.sourceLocale, out _, out error))
            {
                return false;
            }
            document.languages = document.languages ?? Array.Empty<string>();
            document.entries = document.entries ?? Array.Empty<LocalizationExchangeEntry>();
            error = null;
            return true;
        }

        private static void EnsureParentDirectory(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void AppendCsvRow(StringBuilder builder, IEnumerable<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                builder.Append(EscapeCsv(value));
            }
            builder.Append("\r\n");
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!quote)
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<string[]> ParseCsv(string content)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Any(value => !string.IsNullOrEmpty(value)))
                    {
                        rows.Add(row.ToArray());
                    }
                    row.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            if (quoted)
            {
                throw new FormatException("CSV contains an unterminated quoted field.");
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }

        private static string GetCell(string[] row, int index)
        {
            return index >= 0 && index < row.Length ? row[index] : string.Empty;
        }
    }
}
