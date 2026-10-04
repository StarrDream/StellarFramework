using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.HybridCLR
{
    /// <summary>
    /// HybridCLR code-update manifest.
    /// The manifest is a normal ResKit asset and must be versioned together with the dll.bytes
    /// and AOT metadata assets supplied by the selected content backend.
    /// </summary>
    [Serializable]
    public sealed class HotUpdateManifest
    {
        public int version = 1;
        public string buildTarget;
        public string hotUpdateAssemblyKey;
        public string hotUpdateAssemblySha256;
        public string hotUpdateEntryClass;
        public string hotUpdateEntryMethod;
        public List<string> aotMetadataKeys = new List<string>();

        public static HotUpdateManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                HotUpdateManifest manifest = JsonUtility.FromJson<HotUpdateManifest>(json.TrimStart('\uFEFF'));
                if (manifest != null && manifest.aotMetadataKeys == null)
                {
                    manifest.aotMetadataKeys = new List<string>();
                }

                return manifest;
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[HybridCLRManifest] JSON parse failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates an authoring fallback manifest from settings. Runtime startup does not use this
        /// as a network/file fallback; production startup always loads the manifest through ResKit.
        /// </summary>
        public static HotUpdateManifest FromRuntimeSettings(HotUpdateSettings settings)
        {
            if (settings == null) return null;

            return new HotUpdateManifest
            {
                version = 1,
                buildTarget = Application.platform.ToString(),
                hotUpdateAssemblyKey = settings.HotUpdateAssemblyKey,
                hotUpdateAssemblySha256 = string.Empty,
                hotUpdateEntryClass = settings.HotUpdateEntryClass,
                hotUpdateEntryMethod = settings.HotUpdateEntryMethod,
                aotMetadataKeys = HotUpdateSettings.ToDistinctStringList(settings.AotMetadataKeys)
            };
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public HotUpdateManifestValidationReport Validate(bool strictAssemblyIntegrity = false)
        {
            var report = new HotUpdateManifestValidationReport();

            if (string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                report.AddError("hotUpdateAssemblyKey is empty.");
            }
            else if (!hotUpdateAssemblyKey.Trim().EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("hotUpdateAssemblyKey should point to a .dll.bytes TextAsset.");
            }

            string normalizedSha256 = NormalizeSha256(hotUpdateAssemblySha256);
            if (strictAssemblyIntegrity && string.IsNullOrEmpty(normalizedSha256))
            {
                report.AddError(
                    "Production HybridCLR startup requires hotUpdateAssemblySha256. Re-export the code-update assets.");
            }

            if (!string.IsNullOrEmpty(normalizedSha256) && normalizedSha256.Length != 64)
            {
                report.AddError("hotUpdateAssemblySha256 must be a 64-character SHA256 hex string.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryClass))
            {
                report.AddError("hotUpdateEntryClass is empty.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryMethod))
            {
                report.AddError("hotUpdateEntryMethod is empty.");
            }

            if (HotUpdateSettings.ToDistinctStringList(aotMetadataKeys).Count == 0)
            {
                report.AddError("aotMetadataKeys are empty.");
            }

            return report;
        }

        public static string NormalizeSha256(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("-", string.Empty);
        }
    }

    public sealed class HotUpdateManifestValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) Warnings.Add(message);
        }
    }
}
