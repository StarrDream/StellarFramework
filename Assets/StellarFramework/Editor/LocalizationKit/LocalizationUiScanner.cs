using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Localization.Editor
{
    public enum LocalizationUiScanStatus
    {
        New = 0,
        Synced = 1,
        SourceChanged = 2,
        DuplicateBindingId = 3,
        DynamicCandidate = 4,
        Conflict = 5
    }

    public sealed class LocalizationUiScanCandidate
    {
        internal int[] TransformIndexPath;
        internal int TextComponentIndex;

        public string HierarchyPath { get; internal set; }
        public string SourceText { get; internal set; }
        public string BindingId { get; internal set; }
        public string Key { get; internal set; }
        public long TargetLocalFileId { get; internal set; }
        public LocalizationUiScanStatus Status { get; internal set; }
        public string Message { get; internal set; }
        public bool Selected { get; set; }
    }

    public sealed class LocalizationUiScanResult
    {
        public string PrefabPath { get; internal set; }
        public string PrefabGuid { get; internal set; }
        public string SourceLocale { get; internal set; }
        public bool IsVariant { get; internal set; }
        public IReadOnlyList<LocalizationUiScanCandidate> Candidates { get; internal set; }
    }

    /// <summary>
    /// Editor-only UGUI scanner. Hierarchy/index data is used only to locate objects between Preview and Apply;
    /// persistent localization identity is always BindingId + Key.
    /// </summary>
    public static class LocalizationUiScanner
    {
        public static bool TryScanPrefab(
            GameObject prefabAsset,
            string sourceLocale,
            LocalizationSourceRegistry registry,
            out LocalizationUiScanResult result,
            out string error)
        {
            result = null;
            if (prefabAsset == null)
            {
                error = "Prefab asset is not assigned.";
                return false;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrWhiteSpace(prefabPath) ||
                PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.NotAPrefab)
            {
                error = "Selected object is not a prefab asset.";
                return false;
            }

            if (!LocaleId.TryCreate(sourceLocale, out LocaleId locale, out error))
            {
                return false;
            }

            registry = registry != null ? registry : LocalizationSourceRegistry.LoadOrCreate();
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            bool isVariant = PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.Variant;
            var seenBindingIds = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<LocalizationUiScanCandidate>();

            Text[] texts = prefabAsset.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text target = texts[i];
                string sourceText = target.text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    continue;
                }

                LocalizedTextView view = target.GetComponent<LocalizedTextView>();
                string bindingId = view == null ? string.Empty : view.BindingId;
                string key = view == null ? string.Empty : view.Key;
                LocalizationUiScanStatus status = LocalizationUiScanStatus.Synced;
                string message = string.Empty;

                bool hasStableBinding = !string.IsNullOrWhiteSpace(bindingId);
                if (hasStableBinding && !seenBindingIds.Add(bindingId))
                {
                    bindingId = CreateBindingId();
                    key = BuildKey(prefabAsset.name, target, bindingId);
                    status = LocalizationUiScanStatus.DuplicateBindingId;
                    message = "Prefab 内检测到重复 BindingId；Apply 时会给该复制对象 Fork 新身份。";
                }
                else if (hasStableBinding && registry.TryGet(bindingId, out LocalizationSourceRecord record))
                {
                    bool ownedByAnotherPrefab =
                        !string.Equals(record.PrefabGuid, prefabGuid, StringComparison.Ordinal);
                    if (ownedByAnotherPrefab && !isVariant)
                    {
                        bindingId = CreateBindingId();
                        key = BuildKey(prefabAsset.name, target, bindingId);
                        status = LocalizationUiScanStatus.DuplicateBindingId;
                        message = "该 BindingId 属于另一个独立 Prefab；Apply 时会 Fork 新身份。";
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            key = record.Key;
                        }

                        string currentHash = LocalizationSourceRecord.ComputeSourceHash(sourceText);
                        if (!string.Equals(currentHash, record.SourceHash, StringComparison.Ordinal))
                        {
                            if (ownedByAnotherPrefab && isVariant)
                            {
                                status = LocalizationUiScanStatus.Conflict;
                                message = "Prefab Variant 修改了继承文本。为避免共享 Key 被静默改义，请先显式 Fork Localization Binding。";
                            }
                            else
                            {
                                status = LocalizationUiScanStatus.SourceChanged;
                                message = "源文本发生变化；Key/BindingId 保持不变，已有翻译应标记 NeedsReview。";
                            }
                        }
                    }
                }
                else if (!hasStableBinding)
                {
                    bindingId = CreateBindingId();
                    key = BuildKey(prefabAsset.name, target, bindingId);
                    status = LooksDynamic(sourceText)
                        ? LocalizationUiScanStatus.DynamicCandidate
                        : LocalizationUiScanStatus.New;
                    message = status == LocalizationUiScanStatus.DynamicCandidate
                        ? "文本包含明显动态数据特征，默认不自动 Apply，请确认是否应改为模板文本。"
                        : "新文本；Apply 后会写入稳定 BindingId、Key、Registry 与源语言表。";
                    seenBindingIds.Add(bindingId);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        key = BuildKey(prefabAsset.name, target, bindingId);
                    }
                    status = LocalizationUiScanStatus.New;
                    message = "已有 BindingId 但 Registry 未登记；Apply 后补齐 Registry/源语言表。";
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = BuildKey(prefabAsset.name, target, bindingId);
                }

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out _, out long localFileId);
                candidates.Add(new LocalizationUiScanCandidate
                {
                    TransformIndexPath = BuildTransformIndexPath(prefabAsset.transform, target.transform),
                    TextComponentIndex = GetTextComponentIndex(target),
                    HierarchyPath = BuildHierarchyPath(prefabAsset.transform, target.transform),
                    SourceText = sourceText,
                    BindingId = bindingId,
                    Key = key,
                    TargetLocalFileId = localFileId,
                    Status = status,
                    Message = message,
                    Selected = status != LocalizationUiScanStatus.DynamicCandidate &&
                               status != LocalizationUiScanStatus.Conflict
                });
            }

            result = new LocalizationUiScanResult
            {
                PrefabPath = prefabPath,
                PrefabGuid = prefabGuid,
                SourceLocale = locale.Value,
                IsVariant = isVariant,
                Candidates = candidates
            };
            error = null;
            return true;
        }

        public static bool TryApply(
            LocalizationUiScanResult result,
            LocalizationTableAsset sourceTable,
            LocalizationSourceRegistry registry,
            out string error)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.PrefabPath))
            {
                error = "Scan result is missing.";
                return false;
            }
            if (sourceTable == null)
            {
                error = "Source LocalizationTableAsset is not assigned.";
                return false;
            }
            if (!string.Equals(sourceTable.Locale, result.SourceLocale, StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Source table locale '{sourceTable.Locale}' does not match scan locale '{result.SourceLocale}'.";
                return false;
            }

            LocalizationUiScanCandidate[] selected = result.Candidates
                .Where(candidate => candidate.Selected && candidate.Status != LocalizationUiScanStatus.Conflict)
                .ToArray();
            if (selected.Length == 0)
            {
                error = "No scan candidates are selected.";
                return false;
            }

            registry = registry != null ? registry : LocalizationSourceRegistry.LoadOrCreate();
            GameObject root = PrefabUtility.LoadPrefabContents(result.PrefabPath);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(root, "Apply Localization Scan");
                foreach (LocalizationUiScanCandidate candidate in selected)
                {
                    Transform transform = ResolveTransform(root.transform, candidate.TransformIndexPath);
                    if (transform == null)
                    {
                        error = "Prefab changed after scan; cannot resolve " + candidate.HierarchyPath + ". Please rescan.";
                        return false;
                    }

                    Text[] textComponents = transform.GetComponents<Text>();
                    if (candidate.TextComponentIndex < 0 || candidate.TextComponentIndex >= textComponents.Length)
                    {
                        error = "Prefab changed after scan; Text component no longer matches " +
                                candidate.HierarchyPath + ". Please rescan.";
                        return false;
                    }

                    Text target = textComponents[candidate.TextComponentIndex];
                    LocalizedTextView view = target.GetComponent<LocalizedTextView>();
                    if (view == null)
                    {
                        view = Undo.AddComponent<LocalizedTextView>(target.gameObject);
                    }
                    else
                    {
                        Undo.RecordObject(view, "Update Localization Binding");
                    }

                    view.ConfigureBinding(candidate.BindingId, target, candidate.Key, view.Context);
                    EditorUtility.SetDirty(view);
                }

                PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            UpdateSourceTable(sourceTable, selected, result.SourceLocale);
            UpdateRegistry(registry, result, selected);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(result.PrefabPath, ImportAssetOptions.ForceUpdate);
            error = null;
            return true;
        }

        private static void UpdateSourceTable(
            LocalizationTableAsset sourceTable,
            IEnumerable<LocalizationUiScanCandidate> candidates,
            string sourceLocale)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LocalizationAuthoringEntry entry in sourceTable.Entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                {
                    entries[entry.Key] = entry.Value;
                }
            }

            foreach (LocalizationUiScanCandidate candidate in candidates)
            {
                entries[candidate.Key] = candidate.SourceText;
            }

            Undo.RecordObject(sourceTable, "Update Localization Source Table");
            LocalizationAuthoringEntry[] authoringEntries = entries
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new LocalizationAuthoringEntry(pair.Key, pair.Value))
                .ToArray();
            sourceTable.Configure(sourceLocale, authoringEntries);
            EditorUtility.SetDirty(sourceTable);
        }

        private static void UpdateRegistry(
            LocalizationSourceRegistry registry,
            LocalizationUiScanResult result,
            IEnumerable<LocalizationUiScanCandidate> candidates)
        {
            Undo.RecordObject(registry, "Update Localization Source Registry");
            var activeBindings = new HashSet<string>(StringComparer.Ordinal);

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            if (prefabAsset != null)
            {
                foreach (LocalizedTextView view in prefabAsset.GetComponentsInChildren<LocalizedTextView>(true))
                {
                    if (!string.IsNullOrWhiteSpace(view.BindingId))
                    {
                        activeBindings.Add(view.BindingId);
                    }
                }
            }

            foreach (LocalizationUiScanCandidate candidate in candidates)
            {
                activeBindings.Add(candidate.BindingId);
                LocalizationSourceStatus registryStatus =
                    candidate.Status == LocalizationUiScanStatus.SourceChanged
                        ? LocalizationSourceStatus.NeedsReview
                        : LocalizationSourceStatus.Synced;
                registry.Upsert(
                    candidate.BindingId,
                    candidate.Key,
                    result.PrefabGuid,
                    candidate.TargetLocalFileId,
                    candidate.HierarchyPath,
                    typeof(Text).FullName,
                    result.SourceLocale,
                    candidate.SourceText,
                    registryStatus);
            }

            registry.MarkMissingPrefabBindingsAsOrphan(result.PrefabGuid, activeBindings);
            EditorUtility.SetDirty(registry);
        }

        public static string BuildKey(string prefabName, Text target, string bindingId)
        {
            string prefabToken = ToSnakeToken(prefabName);
            string semanticName = target == null ? "text" : target.gameObject.name;
            if (IsGenericTextName(semanticName) && target != null && target.transform.parent != null)
            {
                semanticName = target.transform.parent.name;
            }

            return BuildKey(prefabName, semanticName, bindingId);
        }

        public static string BuildKey(string prefabName, string semanticName, string bindingId)
        {
            string prefabToken = ToSnakeToken(prefabName);
            string semanticToken = ToSnakeToken(semanticName);
            string shortId = string.IsNullOrWhiteSpace(bindingId)
                ? "missing"
                : bindingId.Replace("-", string.Empty).Substring(0, Math.Min(8, bindingId.Replace("-", string.Empty).Length));
            return $"ui.{prefabToken}.{semanticToken}.{shortId}";
        }

        public static string CreateBindingId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static bool LooksDynamic(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            bool hasDigit = text.Any(char.IsDigit);
            bool hasTemplateBrace = text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0;
            return hasDigit || hasTemplateBrace;
        }

        private static bool IsGenericTextName(string value)
        {
            return string.Equals(value, "Text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Label", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "TextLabel", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToSnakeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            var builder = new StringBuilder(value.Length + 8);
            char previous = '\0';
            foreach (char raw in value.Trim())
            {
                char current = char.ToLowerInvariant(raw);
                if (char.IsLetterOrDigit(current))
                {
                    if (builder.Length > 0 &&
                        char.IsUpper(raw) &&
                        char.IsLetterOrDigit(previous) &&
                        char.IsLower(previous) &&
                        builder[builder.Length - 1] != '_')
                    {
                        builder.Append('_');
                    }
                    builder.Append(current);
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                {
                    builder.Append('_');
                }
                previous = raw;
            }

            return builder.ToString().Trim('_');
        }

        private static int[] BuildTransformIndexPath(Transform root, Transform target)
        {
            var indices = new List<int>();
            Transform current = target;
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            indices.Reverse();
            return indices.ToArray();
        }

        private static Transform ResolveTransform(Transform root, IReadOnlyList<int> indexPath)
        {
            Transform current = root;
            for (int i = 0; i < indexPath.Count; i++)
            {
                int childIndex = indexPath[i];
                if (childIndex < 0 || childIndex >= current.childCount)
                {
                    return null;
                }
                current = current.GetChild(childIndex);
            }
            return current;
        }

        private static string BuildHierarchyPath(Transform root, Transform target)
        {
            var names = new List<string>();
            Transform current = target;
            while (current != null)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static int GetTextComponentIndex(Text target)
        {
            Text[] components = target.GetComponents<Text>();
            for (int i = 0; i < components.Length; i++)
            {
                if (ReferenceEquals(components[i], target))
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
