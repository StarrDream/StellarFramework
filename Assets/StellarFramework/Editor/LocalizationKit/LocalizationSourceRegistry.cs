using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Localization.Editor
{
    public enum LocalizationSourceStatus
    {
        Synced = 0,
        New = 1,
        SourceChanged = 2,
        NeedsReview = 3,
        Orphan = 4,
        Conflict = 5
    }

    [Serializable]
    public sealed class LocalizationSourceRecord
    {
        [SerializeField] private string _bindingId;
        [SerializeField] private string _key;
        [SerializeField] private string _prefabGuid;
        [SerializeField] private long _localFileId;
        [SerializeField] private string _currentHierarchy;
        [SerializeField] private string _componentType;
        [SerializeField] private string _sourceLocale;
        [SerializeField, TextArea(1, 4)] private string _sourceText;
        [SerializeField] private string _sourceHash;
        [SerializeField] private LocalizationSourceStatus _status;

        public string BindingId => _bindingId ?? string.Empty;
        public string Key => _key ?? string.Empty;
        public string PrefabGuid => _prefabGuid ?? string.Empty;
        public long LocalFileId => _localFileId;
        public string CurrentHierarchy => _currentHierarchy ?? string.Empty;
        public string ComponentType => _componentType ?? string.Empty;
        public string SourceLocale => _sourceLocale ?? string.Empty;
        public string SourceText => _sourceText ?? string.Empty;
        public string SourceHash => _sourceHash ?? string.Empty;
        public LocalizationSourceStatus Status => _status;

        internal void Update(
            string bindingId,
            string key,
            string prefabGuid,
            long localFileId,
            string hierarchy,
            string componentType,
            string sourceLocale,
            string sourceText,
            LocalizationSourceStatus status)
        {
            _bindingId = bindingId ?? string.Empty;
            _key = key ?? string.Empty;
            _prefabGuid = prefabGuid ?? string.Empty;
            _localFileId = localFileId;
            _currentHierarchy = hierarchy ?? string.Empty;
            _componentType = componentType ?? string.Empty;
            _sourceLocale = sourceLocale ?? string.Empty;
            _sourceText = sourceText ?? string.Empty;
            _sourceHash = ComputeSourceHash(sourceText);
            _status = status;
        }

        internal void SetStatus(LocalizationSourceStatus status)
        {
            _status = status;
        }

        public static string ComputeSourceHash(string sourceText)
        {
            return Hash128.Compute(sourceText ?? string.Empty).ToString();
        }
    }

    /// <summary>
    /// Editor-only localization source identity registry.
    /// Runtime lookup never depends on this asset; it exists only to preserve BindingId/Key ownership across rescans.
    /// </summary>
    public sealed class LocalizationSourceRegistry : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Editor/Localization/LocalizationSourceRegistry.asset";

        [SerializeField] private List<LocalizationSourceRecord> _records =
            new List<LocalizationSourceRecord>();

        public IReadOnlyList<LocalizationSourceRecord> Records => _records;

        public bool TryGet(string bindingId, out LocalizationSourceRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                return false;
            }

            record = _records.FirstOrDefault(item =>
                string.Equals(item.BindingId, bindingId, StringComparison.Ordinal));
            return record != null;
        }

        public LocalizationSourceRecord Upsert(
            string bindingId,
            string key,
            string prefabGuid,
            long localFileId,
            string hierarchy,
            string componentType,
            string sourceLocale,
            string sourceText,
            LocalizationSourceStatus status)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                throw new ArgumentException("BindingId cannot be empty.", nameof(bindingId));
            }

            LocalizationSourceRecord record = _records.FirstOrDefault(item =>
                string.Equals(item.BindingId, bindingId, StringComparison.Ordinal));
            if (record == null)
            {
                record = new LocalizationSourceRecord();
                _records.Add(record);
            }

            record.Update(
                bindingId,
                key,
                prefabGuid,
                localFileId,
                hierarchy,
                componentType,
                sourceLocale,
                sourceText,
                status);
            return record;
        }

        public void MarkMissingPrefabBindingsAsOrphan(string prefabGuid, ISet<string> activeBindingIds)
        {
            foreach (LocalizationSourceRecord record in _records)
            {
                if (!string.Equals(record.PrefabGuid, prefabGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                if (activeBindingIds == null || !activeBindingIds.Contains(record.BindingId))
                {
                    record.SetStatus(LocalizationSourceStatus.Orphan);
                }
            }
        }

        public int RemoveOrphans()
        {
            return _records.RemoveAll(record => record.Status == LocalizationSourceStatus.Orphan);
        }

        public static LocalizationSourceRegistry LoadOrCreate()
        {
            LocalizationSourceRegistry registry =
                AssetDatabase.LoadAssetAtPath<LocalizationSourceRegistry>(DefaultAssetPath);
            if (registry != null)
            {
                return registry;
            }

            string directory = Path.GetDirectoryName(DefaultAssetPath)?.Replace('\\', '/');
            EnsureAssetFolder(directory);

            registry = CreateInstance<LocalizationSourceRegistry>();
            registry.name = "LocalizationSourceRegistry";
            AssetDatabase.CreateAsset(registry, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return registry;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
