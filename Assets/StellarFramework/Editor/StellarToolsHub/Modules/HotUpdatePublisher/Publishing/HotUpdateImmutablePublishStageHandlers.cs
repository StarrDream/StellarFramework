using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>检查候选输出并创建不可变文件快照；可变 PackageVersion 指针不进入上传清单。</summary>
    public sealed class HotUpdatePrepareUploadStageHandler : IHotUpdatePublishStageHandler
    {
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.PrepareUpload;

        public Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            YooAssetBuildOutput output = context.YooAssetBuildOutput;
            if (output == null || string.IsNullOrWhiteSpace(output.OutputDirectory) || !Directory.Exists(output.OutputDirectory))
                return Task.FromResult(Failed("A completed YooAsset output directory is required before preparing publish files."));
            if (string.IsNullOrWhiteSpace(context.PackageVersion) ||
                !string.Equals(output.PackageVersion, context.PackageVersion, StringComparison.Ordinal))
                return Task.FromResult(Failed("YooAsset output and publish context package versions must match."));
            if (output.ManifestFiles == null || output.ManifestFiles.Length < 4)
                return Task.FromResult(Failed("YooAsset output must identify its package version pointer file."));

            string pointerFileName = output.ManifestFiles[3];
            if (string.IsNullOrWhiteSpace(pointerFileName) ||
                !string.Equals(pointerFileName, Path.GetFileName(pointerFileName), StringComparison.Ordinal) ||
                !File.Exists(Path.Combine(output.OutputDirectory, pointerFileName)))
                return Task.FromResult(Failed("YooAsset package version pointer must be a present file name inside the output directory."));

            string outputRoot = Path.GetFullPath(output.OutputDirectory);
            string rootPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string[] allFiles = Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories);
            var publishFiles = new List<HotUpdatePublishFile>(allFiles.Length - 1);
            for (int index = 0; index < allFiles.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(allFiles[index]);
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(Failed($"YooAsset output file escaped the package root: '{allFiles[index]}'."));

                string relativePath = fullPath.Substring(rootPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
                if (string.Equals(relativePath, pointerFileName, StringComparison.OrdinalIgnoreCase)) continue;
                publishFiles.Add(HotUpdatePublishFile.FromFile(relativePath, fullPath));
            }

            if (publishFiles.Count == 0)
                return Task.FromResult(Failed("YooAsset output contains no immutable files to publish."));

            context.SetPublishFiles(publishFiles.AsReadOnly());
            context.VersionPublishRequest = new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = pointerFileName,
                PackageVersion = context.PackageVersion,
                ExpectedCurrentPackageVersion = context.ExpectedCurrentPackageVersion ?? string.Empty
            };
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        private static HotUpdatePublishStepResult Failed(string error)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, error);
        }
    }

    /// <summary>对目标逐个上传不可变文件；任意失败立即中断，不能进入远端验证或版本切换。</summary>
    public sealed class HotUpdateUploadFilesStageHandler : IHotUpdatePublishStageHandler
    {
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.UploadFiles;

        public async Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.PublishTargetAdapter == null) return Failed("A concrete publish target is required before upload.");
            if (context.PublishFiles == null || context.PublishFiles.Count == 0) return Failed("No immutable publish files have been prepared.");

            for (int index = 0; index < context.PublishFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await context.PublishTargetAdapter.UploadAsync(context.PublishFiles[index], cancellationToken);
            }

            context.MarkImmutableUploadCompleted();
            return HotUpdatePublishStepResult.Succeeded();
        }

        private static HotUpdatePublishStepResult Failed(string error)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, error);
        }
    }

    /// <summary>发布前远端 HTTP/Range 探测边界；P9 实现负责真实 URL、GET、Range 和回退 Host 校验。</summary>
    public interface IHotUpdatePrePublishRemoteVerifier
    {
        Task VerifyAsync(
            HotUpdatePublishContext context,
            IReadOnlyList<HotUpdatePublishFile> immutableFiles,
            CancellationToken cancellationToken);
    }

    /// <summary>Checks the historical manifest and bundle after rollback switches the version pointer.</summary>
    public interface IHotUpdateHistoricalReleaseRemoteVerifier
    {
        Task VerifyRollbackAsync(HotUpdateReleaseRecord release, CancellationToken cancellationToken);
    }

    /// <summary>先校验目标端每个文件的长度/SHA256，再要求远端 GET/Range 等探测通过。</summary>
    public sealed class HotUpdateRemoteVerificationStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly IHotUpdatePrePublishRemoteVerifier _remoteVerifier;
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.VerifyRemote;

        public HotUpdateRemoteVerificationStageHandler(IHotUpdatePrePublishRemoteVerifier remoteVerifier)
        {
            _remoteVerifier = remoteVerifier ?? throw new ArgumentNullException(nameof(remoteVerifier));
        }

        public async Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!context.ImmutableUploadCompleted) return Failed("Immutable files must all upload before remote verification.");
            if (context.PublishTargetAdapter == null) return Failed("A concrete publish target is required for remote verification.");

            await context.PublishTargetAdapter.VerifyAsync(context.PublishFiles, cancellationToken);
            await _remoteVerifier.VerifyAsync(context, context.PublishFiles, cancellationToken);
            context.MarkRemotePublishVerificationCompleted();
            return HotUpdatePublishStepResult.Succeeded();
        }

        private static HotUpdatePublishStepResult Failed(string error)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, error);
        }
    }

    /// <summary>仅在所有不可变文件和发布前远端验证通过后，最后切换 PackageVersion。</summary>
    public sealed class HotUpdatePublishVersionStageHandler : IHotUpdatePublishStageHandler
    {
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.PublishVersion;

        public async Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!context.RemotePublishVerificationCompleted)
                return Failed("PackageVersion cannot be published before all immutable and remote checks pass.");
            if (context.PublishTargetAdapter == null || context.VersionPublishRequest == null)
                return Failed("Publish target and version pointer request are required.");
            if (!string.Equals(context.VersionPublishRequest.PackageVersion, context.PackageVersion, StringComparison.Ordinal))
                return Failed("Version pointer package version does not match the validated publish context.");

            await context.PublishTargetAdapter.PublishVersionAsync(context.VersionPublishRequest, cancellationToken);
            return HotUpdatePublishStepResult.Succeeded();
        }

        private static HotUpdatePublishStepResult Failed(string error)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, error);
        }
    }
}
