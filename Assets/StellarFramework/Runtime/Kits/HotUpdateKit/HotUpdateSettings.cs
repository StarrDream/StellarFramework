using System;
using System.Collections.Generic;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.HotUpdate
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

    [CreateAssetMenu(fileName = "HotUpdateSettings", menuName = "StellarFramework/HotUpdate Settings")]
    public sealed class HotUpdateSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "HotUpdateSettings";

        [Header("Addressables")]
        [SerializeField] private bool addressablesUpdateCatalogsOnCheck = true;
        [SerializeField] private string[] addressablesDefaultHotUpdateLabels = { "hotupdate" };
        [SerializeField] private string[] addressablesDefaultUpdateKeys = { "hotupdate" };

        [Header("Hot Update Manifest")]
        [SerializeField] private string hotUpdateManifestPathOrUrl = string.Empty;
        [SerializeField] private bool hotUpdateManifestFallbackToStreamingAssets = true;
        [SerializeField] private bool hotUpdateManifestFallbackToResources = true;
        [SerializeField] private int hotUpdateManifestHttpTimeoutSeconds = 30;

        [Header("HybridCLR Code Update")]
        [SerializeField] private string hotUpdateAssemblyKey = "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes";
        [SerializeField] private string hotUpdateAssemblySha256 = string.Empty;
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

        public bool AddressablesUpdateCatalogsOnCheck => addressablesUpdateCatalogsOnCheck;
        public IReadOnlyList<string> AddressablesDefaultHotUpdateLabels => addressablesDefaultHotUpdateLabels;
        public IReadOnlyList<string> AddressablesDefaultUpdateKeys => addressablesDefaultUpdateKeys;
        public string HotUpdateManifestPathOrUrl => hotUpdateManifestPathOrUrl;
        public bool HotUpdateManifestFallbackToStreamingAssets => hotUpdateManifestFallbackToStreamingAssets;
        public bool HotUpdateManifestFallbackToResources => hotUpdateManifestFallbackToResources;
        public int HotUpdateManifestHttpTimeoutSeconds => hotUpdateManifestHttpTimeoutSeconds;
        public string HotUpdateAssemblyKey => hotUpdateAssemblyKey;
        public string HotUpdateAssemblySha256 => hotUpdateAssemblySha256;
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

        public List<object> BuildAddressablesDefaultUpdateKeys()
        {
            List<string> keys = ResKitRuntimeSettings.ToDistinctStringList(addressablesDefaultUpdateKeys);
            if (keys.Count == 0)
            {
                keys = ResKitRuntimeSettings.ToDistinctStringList(addressablesDefaultHotUpdateLabels);
            }

            return ResKitRuntimeSettings.ToObjectKeyList(keys);
        }

        public List<object> BuildHotUpdateDownloadKeys()
        {
            List<string> stringKeys = ResKitRuntimeSettings.ToDistinctStringList(aotMetadataKeys);
            if (!string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                string assemblyKey = hotUpdateAssemblyKey.Trim();
                if (!ContainsOrdinal(stringKeys, assemblyKey))
                {
                    stringKeys.Add(assemblyKey);
                }
            }

            return ResKitRuntimeSettings.ToObjectKeyList(stringKeys);
        }

        public HotUpdateSettingsValidationReport Validate()
        {
            return Validate(HotUpdateRuntimePolicy.IsStrictProductionRuntime);
        }

        public HotUpdateSettingsValidationReport Validate(bool strictProduction)
        {
            HotUpdateSettingsValidationReport report = new HotUpdateSettingsValidationReport();

            if (ResKitRuntimeSettings.ToDistinctStringList(addressablesDefaultHotUpdateLabels).Count == 0)
            {
                report.AddWarning("Addressables default labels are empty. Download checks need explicit keys.");
            }

            if (BuildAddressablesDefaultUpdateKeys().Count == 0)
            {
                report.AddWarning("Addressables default update keys are empty. Runtime checks will report no downloadable content unless keys are passed explicitly.");
            }

            if (hotUpdateManifestHttpTimeoutSeconds <= 0)
            {
                report.AddWarning("HotUpdateManifestHttpTimeoutSeconds should be greater than 0.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                report.AddError("HotUpdateAssemblyKey is empty.");
            }
            else if (!hotUpdateAssemblyKey.Trim().EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("HotUpdateAssemblyKey should usually point to a .dll.bytes TextAsset address.");
            }

            string normalizedSha256 = string.IsNullOrWhiteSpace(hotUpdateAssemblySha256)
                ? string.Empty
                : hotUpdateAssemblySha256.Trim().Replace("-", string.Empty);

            if (strictProduction)
            {
                if (string.IsNullOrWhiteSpace(normalizedSha256))
                {
                    report.AddError(
                        "Production hot update requires HotUpdateAssemblySha256. Re-export dll.bytes so the framework can verify the hot update DLL.");
                }

                if (string.IsNullOrWhiteSpace(hotUpdateManifestPathOrUrl) &&
                    !hotUpdateManifestFallbackToStreamingAssets)
                {
                    report.AddError(
                        "Production hot update requires HotUpdateManifestPathOrUrl or StreamingAssets fallback. Resources-only fallback is not allowed.");
                }
            }
            else if (string.IsNullOrWhiteSpace(normalizedSha256))
            {
                report.AddWarning(
                    "HotUpdateAssemblySha256 is empty. Editor and development builds can still self-check flow, but release builds must use exporter-generated SHA256.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedSha256) &&
                normalizedSha256.Length != 64)
            {
                report.AddError("HotUpdateAssemblySha256 must be a 64-character SHA256 hex string when provided.");
            }

            if (ResKitRuntimeSettings.ToDistinctStringList(aotMetadataKeys).Count == 0)
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

        private static bool ContainsOrdinal(IReadOnlyList<string> list, string value)
        {
            if (list == null || value == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
