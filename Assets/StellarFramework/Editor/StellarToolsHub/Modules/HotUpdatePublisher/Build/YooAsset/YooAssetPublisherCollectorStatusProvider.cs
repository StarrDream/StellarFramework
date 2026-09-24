using System;
using System.Linq;
using UnityEditor;
using YooAsset.Editor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Inspects the configured business package without pulling YooAsset.Editor into Publisher Core.</summary>
    [InitializeOnLoad]
    internal static class YooAssetPublisherCollectorStatusProvider
    {
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
                        $"缺少 Package '{packageName}' 的 YooAsset Collector。点击“配置 / 创建推荐 YooAsset Collector”会新增独立业务 Package，并保留 Verification 配置。");
                }

                int groupCount = package.Groups?.Count ?? 0;
                int collectorCount = package.Groups?.Where(group => group != null)
                    .Sum(group => group.Collectors?.Count ?? 0) ?? 0;
                if (groupCount == 0 || collectorCount == 0)
                {
                    return new HotUpdatePublisherCollectorStatus(false,
                        $"Package '{packageName}' 已存在，但缺少有效 Group 或 Collector。请打开 YooAsset Collector 补全业务资源和 Publisher 输出收集项。");
                }

                return new HotUpdatePublisherCollectorStatus(true,
                    $"已找到业务 Collector '{packageName}'（{groupCount} 个 Group，{collectorCount} 个 Collector）。正式构建仍会校验每个收集路径和 Android 产物。");
            }
            catch (Exception exception)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    $"读取 YooAsset Collector 失败：{exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
