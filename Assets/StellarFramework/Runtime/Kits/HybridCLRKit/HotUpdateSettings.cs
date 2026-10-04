using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.HybridCLR
{
    public sealed class HotUpdateSettingsValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Warnings.Add(message);
            }
        }
    }

    [CreateAssetMenu(fileName = "HotUpdateSettings", menuName = "StellarFramework/HybridCLR/Hot Update Settings")]
    public sealed class HotUpdateSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "HotUpdateSettings";

        [Header("HybridCLR Code Update")]
        [SerializeField] private string resourceLoaderKey = "YooAsset";
        [SerializeField] private string hotUpdateManifestKey = "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json";
        [SerializeField] private string hotUpdateAssemblyKey = "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes";
        [SerializeField] private string hotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        [SerializeField] private string hotUpdateEntryMethod = "Main";
        [SerializeField]
        private string[] aotMetadataKeys =
        {
            "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes",
            "Assets/GameHotUpdate/Metadata/System.dll.bytes",
            "Assets/GameHotUpdate/Metadata/System.Core.dll.bytes",
            "Assets/GameHotUpdate/Metadata/UnityEngine.CoreModule.dll.bytes"
        };

        public string ResourceLoaderKey => resourceLoaderKey;
        public string HotUpdateManifestKey => hotUpdateManifestKey;
        public string HotUpdateAssemblyKey => hotUpdateAssemblyKey;
        public string HotUpdateEntryClass => hotUpdateEntryClass;
        public string HotUpdateEntryMethod => hotUpdateEntryMethod;
        public IReadOnlyList<string> AotMetadataKeys => aotMetadataKeys;

        public static HotUpdateSettings LoadOrCreateDefault(string resourcesPath = DefaultResourcesPath)
        {
            HotUpdateSettings settings = null;
            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                settings = Resources.Load<HotUpdateSettings>(resourcesPath);
            }

            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<HotUpdateSettings>();
            settings.name = "HotUpdateSettings_RuntimeDefault";
            return settings;
        }

        public HotUpdateSettingsValidationReport Validate()
        {
            return Validate(HybridCLRRuntimePolicy.IsStrictProductionRuntime);
        }

        public HotUpdateSettingsValidationReport Validate(bool strictProduction)
        {
            HotUpdateSettingsValidationReport report = new HotUpdateSettingsValidationReport();

            if (string.IsNullOrWhiteSpace(resourceLoaderKey))
            {
                report.AddError("ResourceLoaderKey is empty. Configure the ResKit backend used to read HybridCLR dll.bytes assets.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateManifestKey))
            {
                report.AddError("HotUpdateManifestKey is empty. Configure the ResKit asset key for HotUpdateManifest.json.");
            }
            else if (!hotUpdateManifestKey.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("HotUpdateManifestKey should usually point to HotUpdateManifest.json.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                report.AddError("HotUpdateAssemblyKey is empty.");
            }
            else if (!hotUpdateAssemblyKey.Trim().EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("HotUpdateAssemblyKey should usually point to a .dll.bytes TextAsset address.");
            }

            if (ToDistinctStringList(aotMetadataKeys).Count == 0)
            {
                report.AddError("AOT metadata keys are empty.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryClass))
            {
                report.AddError("HotUpdateEntryClass is empty.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryMethod))
            {
                report.AddError("HotUpdateEntryMethod is empty.");
            }

            return report;
        }

        internal static List<string> ToDistinctStringList(IEnumerable<string> values)
        {
            var result = new List<string>();
            if (values == null)
            {
                return result;
            }

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string normalized = value.Trim();
                if (!result.Contains(normalized)) result.Add(normalized);
            }

            return result;
        }
    }
}
