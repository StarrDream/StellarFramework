using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Res
{
    public sealed class ResKitRuntimeSettingsValidationReport
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

    /// <summary>
    /// Runtime settings for base ResKit loading only.
    /// Put an asset under any Resources folder, or ResKit will use these built-in defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "ResKitRuntimeSettings", menuName = "StellarFramework/ResKit Runtime Settings")]
    public class ResKitRuntimeSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "ResKitRuntimeSettings";

        [Header("Default Loading")]
        [SerializeField] private ResLoadBackend defaultLoadBackend = ResLoadBackend.Resources;
        [SerializeField] private ResLoadBackend defaultUILoadBackend = ResLoadBackend.Resources;
        [SerializeField] private string defaultCustomLoaderKey = string.Empty;
        [SerializeField] private string defaultUICustomLoaderKey = string.Empty;
        [SerializeField] private string resourcesRootPath = string.Empty;
        [SerializeField] private string assetBundleRootPath = string.Empty;

        [Header("UIKit")]
        [SerializeField] private string uiRootPath = "UIPanel/UIRoot";
        [SerializeField] private string uiPanelPathFormat = "UIPanel/{0}";

        [Header("AssetBundle")]
        [SerializeField] private AssetBundleUnloadMode assetBundleUnloadMode =
            AssetBundleUnloadMode.PreserveLoadedAssets;

        public ResLoadBackend DefaultLoadBackend => defaultLoadBackend;
        public ResLoadBackend DefaultUILoadBackend => defaultUILoadBackend;
        public string DefaultCustomLoaderKey => NormalizeCustomKey(defaultCustomLoaderKey);
        public string DefaultUICustomLoaderKey => NormalizeCustomKey(defaultUICustomLoaderKey);
        public string ResourcesRootPath => resourcesRootPath;
        public string AssetBundleRootPath => assetBundleRootPath;
        public string UIRootPath => uiRootPath;
        public string UIPanelPathFormat => uiPanelPathFormat;
        public AssetBundleUnloadMode AssetBundleUnloadMode => assetBundleUnloadMode;

        public static ResKitRuntimeSettings LoadOrCreateDefault(string resourcesPath = DefaultResourcesPath)
        {
            ResKitRuntimeSettings settings = null;
            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                settings = Resources.Load<ResKitRuntimeSettings>(resourcesPath);
            }

            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<ResKitRuntimeSettings>();
            settings.name = "ResKitRuntimeSettings_RuntimeDefault";
            return settings;
        }

        public ResKitRuntimeSettingsValidationReport Validate(bool includeHybridCLR = true)
        {
            ResKitRuntimeSettingsValidationReport report = new ResKitRuntimeSettingsValidationReport();

            if (defaultLoadBackend == ResLoadBackend.Custom)
            {
                if (string.IsNullOrWhiteSpace(DefaultCustomLoaderKey))
                {
                    report.AddError("DefaultCustomLoaderKey is required when DefaultLoadBackend is Custom.");
                }
                else
                {
                    report.AddWarning("DefaultLoadBackend is Custom. Make sure a custom loader factory is registered before runtime allocation.");
                }
            }

            if (defaultUILoadBackend == ResLoadBackend.Custom)
            {
                if (string.IsNullOrWhiteSpace(DefaultUICustomLoaderKey))
                {
                    report.AddError("DefaultUICustomLoaderKey is required when DefaultUILoadBackend is Custom.");
                }
                else
                {
                    report.AddWarning("DefaultUILoadBackend is Custom. Make sure a custom UI loader factory is registered before UIKit allocation.");
                }
            }

            if (string.IsNullOrWhiteSpace(uiRootPath))
            {
                report.AddWarning("UIRootPath is empty. UIKit default strategy will not be able to load UIRoot.");
            }

            if (string.IsNullOrWhiteSpace(uiPanelPathFormat) || !uiPanelPathFormat.Contains("{0}"))
            {
                report.AddWarning("UIPanelPathFormat should contain {0}, for example UIPanel/{0}.");
            }

            return report;
        }

        public static List<object> ToObjectKeyList(IEnumerable<string> keys)
        {
            List<object> result = new List<object>();
            if (keys == null)
            {
                return result;
            }

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result.Add(key.Trim());
            }

            return result;
        }

        public static List<string> ToDistinctStringList(IEnumerable<string> keys)
        {
            List<string> result = new List<string>();
            if (keys == null)
            {
                return result;
            }

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string trimmed = key.Trim();
                if (!ContainsOrdinal(result, trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
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

        private static string NormalizeCustomKey(string customKey)
        {
            return string.IsNullOrWhiteSpace(customKey) ? string.Empty : customKey.Trim();
        }
    }
}
