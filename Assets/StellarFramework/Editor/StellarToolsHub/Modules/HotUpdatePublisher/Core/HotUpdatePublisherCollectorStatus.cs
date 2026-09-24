using System;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Result of inspecting the selected YooAsset business package.</summary>
    public readonly struct HotUpdatePublisherCollectorStatus
    {
        public readonly bool IsReady;
        public readonly string Message;

        public HotUpdatePublisherCollectorStatus(bool isReady, string message)
        {
            IsReady = isReady;
            Message = message ?? string.Empty;
        }

        public static HotUpdatePublisherCollectorStatus Check(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    "缺少 YooAsset 业务 Package 名称。请先在 Overview 填写 Package；Verification package 不能用于发布。");
            }

            if (packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    $"Package '{packageName}' 是 Verification 用途，不能作为 Production Collector。");
            }

            if (Provider == null)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    "无法检查 YooAsset Collector：YooAsset Editor Adapter 尚未加载。请确认 YooAsset Editor assembly 已完成编译。");
            }

            return Provider(packageName);
        }

        /// <summary>Registered by the optional YooAsset Editor assembly to keep Core SDK independent.</summary>
        public static Func<string, HotUpdatePublisherCollectorStatus> Provider { get; set; }
    }
}
