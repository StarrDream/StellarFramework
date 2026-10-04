using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Runs the local build and artifact-validation stages without invoking gates or a publish target.</summary>
    public static class HotUpdatePublisherBuildOnly
    {
        private static readonly HotUpdatePublishStage[] Stages =
        {
            HotUpdatePublishStage.Preflight,
            HotUpdatePublishStage.ClassifyChanges,
            HotUpdatePublishStage.CompileHotUpdate,
            HotUpdatePublishStage.ExportHybridCLRAssets,
            HotUpdatePublishStage.BuildYooAsset,
            HotUpdatePublishStage.ValidateArtifacts
        };

        public static async Task<HotUpdatePublishResult> RunAsync(
            HotUpdatePublishContext context,
            IHotUpdateGitSnapshotProvider gitSnapshotProvider,
            HotUpdateChangeClassifier changeClassifier,
            IHotUpdateBuildAdapter buildAdapter,
            HotUpdateArtifactValidator artifactValidator,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (gitSnapshotProvider == null) throw new ArgumentNullException(nameof(gitSnapshotProvider));
            if (changeClassifier == null) throw new ArgumentNullException(nameof(changeClassifier));
            if (buildAdapter == null) throw new ArgumentNullException(nameof(buildAdapter));
            if (artifactValidator == null) throw new ArgumentNullException(nameof(artifactValidator));

            var handlers = new IHotUpdatePublishStageHandler[]
            {
                new HotUpdateGitPreflightStageHandler(gitSnapshotProvider),
                new HotUpdateChangeClassificationStageHandler(changeClassifier),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.CompileHotUpdate, buildAdapter),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.ExportHybridCLRAssets, buildAdapter),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.BuildYooAsset, buildAdapter),
                new HotUpdateArtifactValidationStageHandler(artifactValidator)
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            var warnings = new List<string>();
            for (int index = 0; index < Stages.Length; index++)
            {
                HotUpdatePublishStage stage = Stages[index];
                if (cancellationToken.IsCancellationRequested)
                    return Finish(false, stage, HotUpdatePublishErrorCode.Cancelled, "Build was cancelled.", null, stopwatch, warnings);

                HotUpdatePublishStepResult result;
                try
                {
                    result = await handlers[index].ExecuteAsync(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Finish(false, stage, HotUpdatePublishErrorCode.Cancelled, "Build was cancelled.", null, stopwatch, warnings);
                }
                catch (Exception exception)
                {
                    return Finish(false, stage, HotUpdatePublishErrorCode.StageException,
                        $"Build stage '{stage}' threw an exception: {exception.Message}", exception, stopwatch, warnings);
                }

                if (result == null)
                    return Finish(false, stage, HotUpdatePublishErrorCode.InvalidStageResult,
                        $"Build stage '{stage}' returned no result.", null, stopwatch, warnings);
                for (int warningIndex = 0; warningIndex < result.Warnings.Count; warningIndex++)
                    if (!string.IsNullOrWhiteSpace(result.Warnings[warningIndex])) warnings.Add(result.Warnings[warningIndex]);
                if (!result.Success)
                    return Finish(false, stage, result.ErrorCode, result.Error, null, stopwatch, warnings);
            }

            return Finish(true, HotUpdatePublishStage.None, HotUpdatePublishErrorCode.None,
                string.Empty, null, stopwatch, warnings);
        }

        private static HotUpdatePublishResult Finish(
            bool success,
            HotUpdatePublishStage failedStage,
            HotUpdatePublishErrorCode errorCode,
            string error,
            Exception exception,
            Stopwatch stopwatch,
            IReadOnlyList<string> warnings)
        {
            stopwatch.Stop();
            return new HotUpdatePublishResult(success, failedStage, errorCode, error, exception,
                stopwatch.Elapsed, warnings.ToArray(), null);
        }
    }
}
