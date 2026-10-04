using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Classifies the workspace before build stages and blocks ordinary patches on Base App changes.</summary>
    public sealed class HotUpdateChangeClassificationStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly HotUpdateChangeClassifier _classifier;

        public HotUpdateChangeClassificationStageHandler(HotUpdateChangeClassifier classifier)
        {
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        }

        public HotUpdatePublishStage Stage => HotUpdatePublishStage.ClassifyChanges;

        public Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();

            HotUpdateChangeClassificationResult classification = _classifier.AnalyzeWorkspace();
            if (classification == null)
                throw new InvalidOperationException("HotUpdate change classifier returned no classification result.");

            context.ChangeClassification = classification;
            if (!context.IsBaseAppRelease && !classification.CanHotPatch)
                return Task.FromResult(HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.ReleaseGatePolicyRejected,
                    "RED changes or Base App → HotUpdate dependency violations block an ordinary Hot Patch."));

            if (!context.IsBaseAppRelease && !classification.HasPlayerPayload)
                return Task.FromResult(HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.StageFailed,
                    "The workspace has no HotUpdate or remote-content payload to publish."));

            if (classification.RequiresFullGate)
                return Task.FromResult(HotUpdatePublishStepResult.Succeeded(
                    "YELLOW changes were found; the Release Gate policy will require Full Gate."));

            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }
    }

    /// <summary>Concrete Editor composition of all Publisher stages; GUI code supplies project-specific inputs.</summary>
    public sealed class HotUpdatePublisherWorkflow
    {
        internal HotUpdatePublisherWorkflow(
            HotUpdatePublishPipeline pipeline,
            HotUpdatePublishDryRun dryRun,
            IReadOnlyList<IHotUpdatePublishStageHandler> handlers,
            IReadOnlyList<HotUpdatePublishStage> fullStages,
            IReadOnlyList<HotUpdatePublishStage> dryRunStages)
        {
            Pipeline = pipeline;
            DryRun = dryRun;
            StageHandlers = handlers;
            FullStages = fullStages;
            DryRunStages = dryRunStages;
        }

        public HotUpdatePublishPipeline Pipeline { get; }
        public HotUpdatePublishDryRun DryRun { get; }
        public IReadOnlyList<IHotUpdatePublishStageHandler> StageHandlers { get; }
        public IReadOnlyList<HotUpdatePublishStage> FullStages { get; }
        public IReadOnlyList<HotUpdatePublishStage> DryRunStages { get; }
    }

    /// <summary>
    /// Assembles the existing pure stage adapters into one full pipeline and a read-only Dry Run.
    /// It does not invent project settings, BaseRelease records, collectors, credentials or server targets.
    /// </summary>
    public static class HotUpdatePublisherWorkflowAssembly
    {
        private static readonly HotUpdatePublishStage[] FullStageOrder =
        {
            HotUpdatePublishStage.Preflight,
            HotUpdatePublishStage.ClassifyChanges,
            HotUpdatePublishStage.CompileHotUpdate,
            HotUpdatePublishStage.ExportHybridCLRAssets,
            HotUpdatePublishStage.BuildYooAsset,
            HotUpdatePublishStage.ValidateArtifacts,
            HotUpdatePublishStage.RunReleaseGate,
            HotUpdatePublishStage.PrepareUpload,
            HotUpdatePublishStage.UploadFiles,
            HotUpdatePublishStage.VerifyRemote,
            HotUpdatePublishStage.PublishVersion,
            HotUpdatePublishStage.Finalize
        };

        private static readonly HotUpdatePublishStage[] DryRunStageOrder =
        {
            HotUpdatePublishStage.Preflight,
            HotUpdatePublishStage.ClassifyChanges,
            HotUpdatePublishStage.CompileHotUpdate,
            HotUpdatePublishStage.ExportHybridCLRAssets,
            HotUpdatePublishStage.BuildYooAsset,
            HotUpdatePublishStage.ValidateArtifacts,
            HotUpdatePublishStage.RunReleaseGate,
            HotUpdatePublishStage.PrepareUpload
        };

        /// <summary>Builds stage handlers and both execution paths from explicit project-specific adapters.</summary>
        public static HotUpdatePublisherWorkflow Create(
            IHotUpdateGitSnapshotProvider gitSnapshotProvider,
            HotUpdatePublishContext context,
            HotUpdateChangeClassifier changeClassifier,
            IHotUpdateBuildAdapter buildAdapter,
            HotUpdateArtifactValidator artifactValidator,
            IHotUpdateFastReleaseGateRunner fastGateRunner,
            IHotUpdateFullReleaseGateRunner fullGateRunner,
            IHotUpdatePublishTarget publishTarget,
            IHotUpdatePrePublishRemoteVerifier remoteVerifier,
            HotUpdateReleaseHistoryRepository historyRepository)
        {
            if (gitSnapshotProvider == null) throw new ArgumentNullException(nameof(gitSnapshotProvider));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (changeClassifier == null) throw new ArgumentNullException(nameof(changeClassifier));
            if (buildAdapter == null) throw new ArgumentNullException(nameof(buildAdapter));
            if (artifactValidator == null) throw new ArgumentNullException(nameof(artifactValidator));
            if (fastGateRunner == null) throw new ArgumentNullException(nameof(fastGateRunner));
            if (publishTarget == null) throw new ArgumentNullException(nameof(publishTarget));
            if (remoteVerifier == null) throw new ArgumentNullException(nameof(remoteVerifier));
            if (historyRepository == null) throw new ArgumentNullException(nameof(historyRepository));
            if (string.IsNullOrWhiteSpace(context.PublishTarget))
                throw new ArgumentException("Context must identify the configured publish target.", nameof(context));

            context.PublishTargetAdapter = publishTarget;
            var handlers = new List<IHotUpdatePublishStageHandler>(FullStageOrder.Length)
            {
                new HotUpdateGitPreflightStageHandler(gitSnapshotProvider),
                new HotUpdateChangeClassificationStageHandler(changeClassifier),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.CompileHotUpdate, buildAdapter),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.ExportHybridCLRAssets, buildAdapter),
                new HotUpdateBuildStageHandler(HotUpdatePublishStage.BuildYooAsset, buildAdapter),
                new HotUpdateArtifactValidationStageHandler(artifactValidator),
                new HotUpdateReleaseGateStageHandler(fastGateRunner, fullGateRunner),
                new HotUpdatePrepareUploadStageHandler(),
                new HotUpdateUploadFilesStageHandler(),
                new HotUpdateRemoteVerificationStageHandler(remoteVerifier),
                new HotUpdatePublishVersionStageHandler(),
                new HotUpdateReleaseHistoryFinalizeStageHandler(historyRepository)
            };

            var dryRunHandlers = new List<IHotUpdatePublishStageHandler>(DryRunStageOrder.Length);
            for (int index = 0; index < DryRunStageOrder.Length; index++)
                dryRunHandlers.Add(handlers[index]);

            return new HotUpdatePublisherWorkflow(
                new HotUpdatePublishPipeline(handlers),
                new HotUpdatePublishDryRun(dryRunHandlers),
                handlers.AsReadOnly(),
                Array.AsReadOnly((HotUpdatePublishStage[])FullStageOrder.Clone()),
                Array.AsReadOnly((HotUpdatePublishStage[])DryRunStageOrder.Clone()));
        }
    }
}
