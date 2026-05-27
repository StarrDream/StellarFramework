// ==================================================================================
// ResData - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：资源缓存数据实体。
// 改造说明：
// 1. 引入 Owners 追踪集合（仅在开发期生效），精确记录当前是哪些 Loader 持有了该资源。
// 2. 配合 ResMgr 实现资源泄漏的精准定位。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public class ResData
    {
        public string Path;
        public Object Asset;
        public int RefCount;
        public string LoaderName;
        public object Data;
        public Action<ResData> UnloadAction;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 审计追踪：记录所有持有该资源的 LoaderId
        // 仅在开发环境开启，Release 包 0 开销
        private HashSet<string> _owners;

        public HashSet<string> Owners
        {
            get
            {
                if (_owners == null) _owners = new HashSet<string>();
                return _owners;
            }
        }

        public void AddOwner(string ownerId)
        {
            Owners.Add(ownerId);
        }

        public void RemoveOwner(string ownerId)
        {
            Owners.Remove(ownerId);
        }
#endif
    }

    public enum AssetBundleUnloadMode
    {
        PreserveLoadedAssets = 0,
        DestroyLoadedAssets = 1
    }

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
    /// Runtime settings shared by ResKit, Addressables hot update, and HybridCLR startup hot update.
    /// Put an asset under any Resources folder, or ResKit will use these built-in defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "ResKitRuntimeSettings", menuName = "StellarFramework/ResKit Runtime Settings")]
    public class ResKitRuntimeSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "ResKitRuntimeSettings";

        [Header("Default Loading")]
        [SerializeField] private ResLoadBackend defaultLoadBackend = ResLoadBackend.Resources;
        [SerializeField] private ResLoadBackend defaultUILoadBackend = ResLoadBackend.Resources;
        [SerializeField] private string resourcesRootPath = string.Empty;
        [SerializeField] private string addressablesAssetPathPrefix = "Assets/";
        [SerializeField] private string assetBundleRootPath = string.Empty;

        [Header("UIKit")]
        [SerializeField] private string uiRootPath = "UIPanel/UIRoot";
        [SerializeField] private string uiPanelPathFormat = "UIPanel/{0}";

        [Header("AssetBundle")]
        [SerializeField] private AssetBundleUnloadMode assetBundleUnloadMode =
            AssetBundleUnloadMode.PreserveLoadedAssets;

        [Header("Addressables")]
        [SerializeField] private bool addressablesUpdateCatalogsOnCheck = true;
        [SerializeField] private string[] addressablesDefaultHotUpdateLabels = { "hotupdate" };
        [SerializeField] private string[] addressablesDefaultUpdateKeys = { "hotupdate" };

        [Header("HybridCLR Code Update")]
        [SerializeField] private string hotUpdateAssemblyKey = "HotUpdate.dll.bytes";
        [SerializeField] private string hotUpdateAssemblySha256 = string.Empty;
        [SerializeField] private string hotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        [SerializeField] private string hotUpdateEntryMethod = "Main";
        [SerializeField]
        private string[] aotMetadataKeys =
        {
            "mscorlib.dll.bytes",
            "System.dll.bytes",
            "System.Core.dll.bytes"
        };

        public ResLoadBackend DefaultLoadBackend => defaultLoadBackend;
        public ResLoadBackend DefaultUILoadBackend => defaultUILoadBackend;
        public string ResourcesRootPath => resourcesRootPath;
        public string AddressablesAssetPathPrefix => addressablesAssetPathPrefix;
        public string AssetBundleRootPath => assetBundleRootPath;
        public string UIRootPath => uiRootPath;
        public string UIPanelPathFormat => uiPanelPathFormat;
        public AssetBundleUnloadMode AssetBundleUnloadMode => assetBundleUnloadMode;
        public bool AddressablesUpdateCatalogsOnCheck => addressablesUpdateCatalogsOnCheck;
        public IReadOnlyList<string> AddressablesDefaultHotUpdateLabels => addressablesDefaultHotUpdateLabels;
        public IReadOnlyList<string> AddressablesDefaultUpdateKeys => addressablesDefaultUpdateKeys;
        public string HotUpdateAssemblyKey => hotUpdateAssemblyKey;
        public string HotUpdateAssemblySha256 => hotUpdateAssemblySha256;
        public string HotUpdateEntryClass => hotUpdateEntryClass;
        public string HotUpdateEntryMethod => hotUpdateEntryMethod;
        public IReadOnlyList<string> AotMetadataKeys => aotMetadataKeys;

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

        public List<object> BuildAddressablesDefaultUpdateKeys()
        {
            List<string> keys = ToDistinctStringList(addressablesDefaultUpdateKeys);
            if (keys.Count == 0)
            {
                keys = ToDistinctStringList(addressablesDefaultHotUpdateLabels);
            }

            return ToObjectKeyList(keys);
        }

        public List<object> BuildHotUpdateDownloadKeys()
        {
            List<string> stringKeys = ToDistinctStringList(aotMetadataKeys);
            if (!string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                string assemblyKey = hotUpdateAssemblyKey.Trim();
                if (!ContainsOrdinal(stringKeys, assemblyKey))
                {
                    stringKeys.Add(assemblyKey);
                }
            }

            return ToObjectKeyList(stringKeys);
        }

        public ResKitRuntimeSettingsValidationReport Validate(bool includeHybridCLR = true)
        {
            ResKitRuntimeSettingsValidationReport report = new ResKitRuntimeSettingsValidationReport();

            if (defaultLoadBackend == ResLoadBackend.Custom)
            {
                report.AddWarning("DefaultLoadBackend is Custom. Make sure a custom loader factory is registered before runtime allocation.");
            }

            if (defaultUILoadBackend == ResLoadBackend.Custom)
            {
                report.AddWarning("DefaultUILoadBackend is Custom. Configure UIKit with an explicit custom loader strategy.");
            }

            if (string.IsNullOrWhiteSpace(uiRootPath))
            {
                report.AddWarning("UIRootPath is empty. UIKit default strategy will not be able to load UIRoot.");
            }

            if (string.IsNullOrWhiteSpace(uiPanelPathFormat) || !uiPanelPathFormat.Contains("{0}"))
            {
                report.AddWarning("UIPanelPathFormat should contain {0}, for example UIPanel/{0}.");
            }

            if (ToDistinctStringList(addressablesDefaultHotUpdateLabels).Count == 0)
            {
                report.AddWarning("Addressables default labels are empty. Download checks need explicit keys.");
            }

            if (BuildAddressablesDefaultUpdateKeys().Count == 0)
            {
                report.AddWarning("Addressables default update keys are empty. Runtime checks will report no downloadable content unless keys are passed explicitly.");
            }

            ValidateAssetsPathKeys(addressablesDefaultUpdateKeys, "Addressables default update key", report, false);

            if (!includeHybridCLR)
            {
                return report;
            }

            if (string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                report.AddError("HotUpdateAssemblyKey is empty.");
            }
            else if (!hotUpdateAssemblyKey.Trim().EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("HotUpdateAssemblyKey should usually point to a .dll.bytes TextAsset address.");
            }

            if (!string.IsNullOrWhiteSpace(hotUpdateAssemblySha256) &&
                hotUpdateAssemblySha256.Trim().Length != 64)
            {
                report.AddError("HotUpdateAssemblySha256 must be a 64-character SHA256 hex string when provided.");
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

        private static void ValidateAssetsPathKeys(IEnumerable<string> keys, string label,
            ResKitRuntimeSettingsValidationReport report, bool requireAssetsPath)
        {
            List<string> normalizedKeys = ToDistinctStringList(keys);
            for (int i = 0; i < normalizedKeys.Count; i++)
            {
                string key = normalizedKeys[i];
                if (requireAssetsPath && !key.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    report.AddWarning($"{label} should use an Assets/... address for AB/AA path compatibility: {key}");
                }
            }
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
