using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StellarFramework.Res.CodeUpdate.HybridCLR;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Inspects the configured business package without pulling YooAsset.Editor into Publisher Core.</summary>
    [InitializeOnLoad]
    internal static class YooAssetPublisherCollectorStatusProvider
    {
        private const string GeneratedRoot = "Assets/HotUpdate/Generated";
        private const string PrefsPrefix = "StellarFramework.HotUpdatePublisher.";

        static YooAssetPublisherCollectorStatusProvider()
        {
            HotUpdatePublisherCollectorStatus.Provider = Check;
        }

        private static HotUpdatePublisherCollectorStatus Check(string packageName)
        {
            try
            {
                AssetBundleCollectorSetting setting = AssetBundleCollectorSettingData.Setting;
                AssetBundleCollectorPackage package = setting?.Packages?.FirstOrDefault(item =>
                    item != null && string.Equals(item.PackageName, packageName, StringComparison.Ordinal));
                if (package == null)
                {
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"还没有为“{packageName}”创建 YooAsset 资源收集配置。点击“创建或打开 YooAsset 资源收集配置”添加业务资源包；现有验证配置会保留。");
                }

                if (!package.EnableAddressable)
                {
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"业务 Package '{packageName}' 未启用 Addressable。Publisher 的运行时按 AddressByFileName 地址加载资源；请在 YooAsset Collector 中启用该选项，然后重新检查。未启用时构建产物不会包含可用资源地址。");
                }

                int groupCount = package.Groups?.Count ?? 0;
                int collectorCount = package.Groups?.Where(group => group != null)
                    .Sum(group => group.Collectors?.Count ?? 0) ?? 0;
                if (groupCount == 0 || collectorCount == 0)
                {
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"Package '{packageName}' 已存在，但缺少有效 Group 或 Collector。请打开 YooAsset Collector 补全业务资源和 Publisher 输出收集项。");
                }

                string outputRoot = ReadHotUpdateAssetOutputRoot();
                if (!IsSafeAssetRoot(outputRoot))
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"HotUpdate Assets Root '{outputRoot}' 无效。请设置 Assets/ 下的安全目录后再检查 Collector。");

                string[] metadataPaths = HotUpdateAotMetadataSelection.GetGeneratedAssetPaths(
                    outputRoot, HotUpdateSettings.LoadOrCreateDefault().AotMetadataKeys);
                string[] requiredPaths = new[]
                {
                    outputRoot + "/Manifest/HotUpdateManifest.json",
                    outputRoot + "/Code/HotUpdate.dll.bytes"
                }.Concat(metadataPaths).ToArray();
                var configuredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (AssetBundleCollectorGroup group in package.Groups)
                {
                    if (group?.Collectors == null) continue;
                    foreach (AssetBundleCollector collector in group.Collectors)
                    {
                        if (!string.IsNullOrWhiteSpace(collector?.CollectPath))
                            configuredPaths.Add(collector.CollectPath.Replace('\\', '/').TrimEnd('/'));
                    }
                }

                string[] missingPaths = requiredPaths.Where(path => !configuredPaths.Contains(path)).ToArray();
                if (missingPaths.Length > 0)
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"Package '{packageName}' 缺少 Publisher 产物收集路径：{string.Join(", ", missingPaths)}。打开 YooAsset Collector 并添加这些路径；已有 Package 配置会保留，不会自动覆盖。");

                return new HotUpdatePublisherCollectorStatus(true,
                    $"业务 Collector '{packageName}' 已包含全部 Publisher 产物路径（{groupCount} 个 Group，{collectorCount} 个 Collector）。正式构建仍会校验 Android 产物。");
            }
            catch (Exception exception)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    $"读取 YooAsset Collector 失败：{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static string ReadHotUpdateAssetOutputRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            string suffix = parent?.FullName.Replace('\\', '/') ?? string.Empty;
            return EditorPrefs.GetString(PrefsPrefix + suffix + ".assetOutputRoot", GeneratedRoot)
                .Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsSafeAssetRoot(string assetRoot)
        {
            if (string.IsNullOrWhiteSpace(assetRoot) || !assetRoot.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetRoot.Contains(":") || assetRoot.Contains("%")) return false;
            string[] segments = assetRoot.Split('/');
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."))
                return false;

            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent == null) return false;
            string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(parent.FullName,
                assetRoot.Replace('/', Path.DirectorySeparatorChar))).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
