using System;
using System.Collections.Generic;
using System.Linq;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.TMP;
using StellarFramework.Localization.UnityUGUI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Localization.TMP.Editor
{
    public sealed class LocalizationTmpScanCandidate
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

    public sealed class LocalizationTmpScanResult
    {
        public string PrefabPath { get; internal set; }
        public string PrefabGuid { get; internal set; }
        public string SourceLocale { get; internal set; }
        public bool IsVariant { get; internal set; }
        public IReadOnlyList<LocalizationTmpScanCandidate> Candidates { get; internal set; }
    }

    public static class LocalizationTmpUiScanner
    {
        public static bool TryScanPrefab(
            GameObject prefabAsset,
            string sourceLocale,
            LocalizationSourceRegistry registry,
            out LocalizationTmpScanResult result,
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

            registry ??= LocalizationSourceRegistry.LoadOrCreate();
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            bool isVariant = PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.Variant;
            var seenBindingIds = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<LocalizationTmpScanCandidate>();

            TMP_Text[] texts = prefabAsset.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text target = texts[i];
                string sourceText = target.text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    continue;
                }

                LocalizedTMPTextView view = target.GetComponent<LocalizedTMPTextView>();
                string bindingId = view == null ? string.Empty : view.BindingId;
                string key = view == null ? string.Empty : view.Key;
                LocalizationUiScanStatus status = LocalizationUiScanStatus.Synced;
                string message = string.Empty;

                bool hasStableBinding = !string.IsNullOrWhiteSpace(bindingId);
                if (hasStableBinding && !seenBindingIds.Add(bindingId))
                {
                    ForkIdentity(prefabAsset, target, out bindingId, out key);
                    status = LocalizationUiScanStatus.DuplicateBindingId;
                    message = "Prefab 内检测到重复 BindingId；Apply 时会给该 TMP 复制对象 Fork 新身份。";
                }
                else if (hasStableBinding &&
                         registry.TryGet(bindingId, out LocalizationSourceRecord record))
                {
                    bool ownedByAnotherPrefab =
                        !string.Equals(record.PrefabGuid, prefabGuid, StringComparison.Ordinal);
                    if (ownedByAnotherPrefab && !isVariant)
                    {
                        ForkIdentity(prefabAsset, target, out bindingId, out key);
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
                                message = "Prefab Variant 修改了继承 TMP 文本，请先显式 Fork Localization Binding。";
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
                    bindingId = LocalizationUiScanner.CreateBindingId();
                    key = BuildKey(prefabAsset.name, target, bindingId);
                    status = LooksDynamic(sourceText)
                        ? LocalizationUiScanStatus.DynamicCandidate
                        : LocalizationUiScanStatus.New;
                    message = status == LocalizationUiScanStatus.DynamicCandidate
                        ? "文本包含明显动态数据特征，默认不自动 Apply。"
                        : "新 TMP 文本；Apply 后会写入稳定 BindingId、Key、Registry 与源语言表。";
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
                candidates.Add(new LocalizationTmpScanCandidate
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

            result = new LocalizationTmpScanResult
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
            LocalizationTmpScanResult result,
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

            LocalizationTmpScanCandidate[] selected = result.Candidates
                .Where(candidate => candidate.Selected && candidate.Status != LocalizationUiScanStatus.Conflict)
                .ToArray();
            if (selected.Length == 0)
            {
                error = "No TMP scan candidates are selected.";
                return false;
            }

            registry ??= LocalizationSourceRegistry.LoadOrCreate();
            GameObject root = PrefabUtility.LoadPrefabContents(result.PrefabPath);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(root, "Apply TMP Localization Scan");
                foreach (LocalizationTmpScanCandidate candidate in selected)
                {
                    Transform transform = ResolveTransform(root.transform, candidate.TransformIndexPath);
                    if (transform == null)
                    {
                        error = "Prefab changed after scan; please rescan " + candidate.HierarchyPath + ".";
                        return false;
                    }

                    TMP_Text[] components = transform.GetComponents<TMP_Text>();
                    if (candidate.TextComponentIndex < 0 ||
                        candidate.TextComponentIndex >= components.Length)
                    {
                        error = "TMP component changed after scan; please rescan " + candidate.HierarchyPath + ".";
                        return false;
                    }

                    TMP_Text target = components[candidate.TextComponentIndex];
                    LocalizedTMPTextView view = target.GetComponent<LocalizedTMPTextView>();
                    if (view == null)
                    {
                        view = Undo.AddComponent<LocalizedTMPTextView>(target.gameObject);
                    }
                    else
                    {
                        Undo.RecordObject(view, "Update TMP Localization Binding");
                    }

                    view.ConfigureBinding(candidate.BindingId, target, candidate.Key, view.ContextProvider);
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

        private static void ForkIdentity(
            GameObject prefab,
            TMP_Text target,
            out string bindingId,
            out string key)
        {
            bindingId = LocalizationUiScanner.CreateBindingId();
            key = BuildKey(prefab.name, target, bindingId);
        }

        private static string BuildKey(string prefabName, TMP_Text target, string bindingId)
        {
            string semanticName = target.gameObject.name;
            if (IsGenericTextName(semanticName) && target.transform.parent != null)
            {
                semanticName = target.transform.parent.name;
            }
            return LocalizationUiScanner.BuildKey(prefabName, semanticName, bindingId);
        }

        private static bool LooksDynamic(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.Any(char.IsDigit) || text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0);
        }

        private static bool IsGenericTextName(string value)
        {
            return string.Equals(value, "Text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Label", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Text (TMP)", StringComparison.OrdinalIgnoreCase);
        }

        private static void UpdateSourceTable(
            LocalizationTableAsset sourceTable,
            IEnumerable<LocalizationTmpScanCandidate> candidates,
            string sourceLocale)
        {
            var entries = sourceTable.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
            foreach (LocalizationTmpScanCandidate candidate in candidates)
            {
                entries[candidate.Key] = candidate.SourceText;
            }

            Undo.RecordObject(sourceTable, "Update TMP Localization Source Table");
            sourceTable.Configure(
                sourceLocale,
                entries
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new LocalizationAuthoringEntry(pair.Key, pair.Value))
                    .ToArray());
            EditorUtility.SetDirty(sourceTable);
        }

        private static void UpdateRegistry(
            LocalizationSourceRegistry registry,
            LocalizationTmpScanResult result,
            IEnumerable<LocalizationTmpScanCandidate> candidates)
        {
            Undo.RecordObject(registry, "Update TMP Localization Registry");
            var activeBindings = new HashSet<string>(StringComparer.Ordinal);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            if (prefab != null)
            {
                foreach (LocalizedTMPTextView view in prefab.GetComponentsInChildren<LocalizedTMPTextView>(true))
                {
                    if (!string.IsNullOrWhiteSpace(view.BindingId))
                    {
                        activeBindings.Add(view.BindingId);
                    }
                }
            }

            foreach (LocalizationTmpScanCandidate candidate in candidates)
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
                    typeof(TMP_Text).FullName,
                    result.SourceLocale,
                    candidate.SourceText,
                    registryStatus);
            }

            registry.MarkMissingPrefabBindingsAsOrphan(result.PrefabGuid, activeBindings);
            EditorUtility.SetDirty(registry);
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

        private static Transform ResolveTransform(Transform root, IReadOnlyList<int> path)
        {
            Transform current = root;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] < 0 || path[i] >= current.childCount)
                {
                    return null;
                }
                current = current.GetChild(path[i]);
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

        private static int GetTextComponentIndex(TMP_Text target)
        {
            TMP_Text[] components = target.GetComponents<TMP_Text>();
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
