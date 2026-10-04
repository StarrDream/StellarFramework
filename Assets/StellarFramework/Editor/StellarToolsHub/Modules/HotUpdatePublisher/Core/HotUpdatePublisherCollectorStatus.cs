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
                    "请先在“概览”填写 YooAsset 业务资源包名。验证专用资源包不能用于发布。");
            }

            if (packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    $"资源包“{packageName}”是验证用途，不能用于正式发布。");
            }

            if (Provider == null)
            {
                return new HotUpdatePublisherCollectorStatus(false,
                    "无法检查 YooAsset 资源收集配置：YooAsset 编辑器扩展尚未加载。请等待 Unity 完成脚本编译。");
            }

            return Provider(packageName);
        }

        /// <summary>Registered by the optional YooAsset Editor assembly to keep Core SDK independent.</summary>
        public static Func<string, HotUpdatePublisherCollectorStatus> Provider { get; set; }
    }
}
