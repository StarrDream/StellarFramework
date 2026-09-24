using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>平台或第三方 SDK 隔离后的 HotUpdate/YooAsset 构建入口。</summary>
    public interface IHotUpdateBuildAdapter
    {
        Task<HotUpdatePublishStepResult> CompileHotUpdateAsync(HotUpdatePublishContext context, CancellationToken cancellationToken);
        Task<HotUpdatePublishStepResult> ExportHybridCLRAssetsAsync(HotUpdatePublishContext context, CancellationToken cancellationToken);
        Task<HotUpdatePublishStepResult> BuildYooAssetAsync(HotUpdatePublishContext context, CancellationToken cancellationToken);
    }

    /// <summary>将一个构建适配器阶段绑定到 Publisher 有序流水线。</summary>
    public sealed class HotUpdateBuildStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly IHotUpdateBuildAdapter _adapter;
        public HotUpdatePublishStage Stage { get; }

        public HotUpdateBuildStageHandler(HotUpdatePublishStage stage, IHotUpdateBuildAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (stage != HotUpdatePublishStage.CompileHotUpdate &&
                stage != HotUpdatePublishStage.ExportHybridCLRAssets &&
                stage != HotUpdatePublishStage.BuildYooAsset)
                throw new ArgumentOutOfRangeException(nameof(stage), "The stage is not a build adapter stage.");
            Stage = stage;
            _adapter = adapter;
        }

        public Task<HotUpdatePublishStepResult> ExecuteAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            switch (Stage)
            {
                case HotUpdatePublishStage.CompileHotUpdate: return _adapter.CompileHotUpdateAsync(context, cancellationToken);
                case HotUpdatePublishStage.ExportHybridCLRAssets: return _adapter.ExportHybridCLRAssetsAsync(context, cancellationToken);
                case HotUpdatePublishStage.BuildYooAsset: return _adapter.BuildYooAssetAsync(context, cancellationToken);
                default: throw new InvalidOperationException($"Unsupported build stage '{Stage}'.");
            }
        }
    }

    /// <summary>将各 SDK 专属 Adapter 按职责组合，避免一个实现伪装处理另一 SDK 的阶段。</summary>
    public sealed class CompositeHotUpdateBuildAdapter : IHotUpdateBuildAdapter
    {
        private readonly IHotUpdateBuildAdapter _hybridClr;
        private readonly IHotUpdateBuildAdapter _yooAsset;

        public CompositeHotUpdateBuildAdapter(IHotUpdateBuildAdapter hybridClr, IHotUpdateBuildAdapter yooAsset)
        {
            _hybridClr = hybridClr ?? throw new ArgumentNullException(nameof(hybridClr));
            _yooAsset = yooAsset ?? throw new ArgumentNullException(nameof(yooAsset));
        }

        public Task<HotUpdatePublishStepResult> CompileHotUpdateAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            => _hybridClr.CompileHotUpdateAsync(context, cancellationToken);

        public Task<HotUpdatePublishStepResult> ExportHybridCLRAssetsAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            => _hybridClr.ExportHybridCLRAssetsAsync(context, cancellationToken);

        public Task<HotUpdatePublishStepResult> BuildYooAssetAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            => _yooAsset.BuildYooAssetAsync(context, cancellationToken);
    }

    /// <summary>已复制到 YooAsset 收集路径的 HybridCLR 产物摘要。</summary>
    public sealed class HybridCLRBuildOutput
    {
        public string ManifestAssetPath { get; set; }
        public string ManifestJson { get; set; }
        public string HotUpdateDllAssetPath { get; set; }
        public string HotUpdateDllSha256 { get; set; }
        public string[] AotMetadataAssetPaths { get; set; } = Array.Empty<string>();
    }

    /// <summary>YooAsset 构建产物摘要，不暴露 YooAsset Editor SDK 类型到 Core。</summary>
    public sealed class YooAssetBuildOutput
    {
        public string PackageVersion { get; set; }
        public string OutputDirectory { get; set; }
        public int BundleCount { get; set; }
        public long TotalBytes { get; set; }
        public string[] ManifestFiles { get; set; } = Array.Empty<string>();
    }
}
