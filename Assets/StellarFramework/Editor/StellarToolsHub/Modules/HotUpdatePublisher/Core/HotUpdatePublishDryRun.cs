using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Remote disposition of one immutable artifact in a dry-run upload plan.</summary>
    public enum HotUpdateDryRunFileDisposition
    {
        New = 0,
        Reuse = 1
    }

    /// <summary>Read-only upload-plan line with the source integrity snapshot.</summary>
    public sealed class HotUpdateDryRunFilePlan
    {
        public HotUpdateDryRunFilePlan(HotUpdatePublishFile source, HotUpdateDryRunFileDisposition disposition)
        {
            RelativePath = source.RelativePath;
            Length = source.Length;
            Sha256 = source.Sha256;
            Disposition = disposition;
        }

        public string RelativePath { get; }
        public long Length { get; }
        public string Sha256 { get; }
        public HotUpdateDryRunFileDisposition Disposition { get; }
    }

    /// <summary>Machine-readable outcome of a HotUpdate dry run; it never uploads or activates a version.</summary>
    public sealed class HotUpdateDryRunResult
    {
        internal HotUpdateDryRunResult(HotUpdatePublishContext context, bool success, HotUpdatePublishStage failedStage, string error,
            TimeSpan duration, IReadOnlyList<HotUpdatePublishStage> executedStages,
            IReadOnlyList<HotUpdateDryRunFilePlan> files, string changeSafety)
        {
            ReleaseId = context.ReleaseId ?? string.Empty;
            PackageVersion = context.PackageVersion ?? string.Empty;
            BaseAppVersion = context.BaseAppVersion ?? string.Empty;
            Environment = context.Environment ?? string.Empty;
            Platform = context.Platform;
            PublishTarget = context.PublishTarget ?? string.Empty;
            Success = success;
            FailedStage = failedStage;
            Error = error ?? string.Empty;
            Duration = duration;
            ExecutedStages = executedStages;
            Files = files;
            ChangeSafety = changeSafety ?? string.Empty;
            NewCount = Count(files, HotUpdateDryRunFileDisposition.New);
            ReuseCount = Count(files, HotUpdateDryRunFileDisposition.Reuse);
            TotalBytes = Sum(files, false);
            UploadBytes = Sum(files, true);
            NoRemoteMutation = true;
        }

        public bool Success { get; }
        public string ReleaseId { get; }
        public string PackageVersion { get; }
        public string BaseAppVersion { get; }
        public string Environment { get; }
        public UnityEditor.BuildTarget Platform { get; }
        public string PublishTarget { get; }
        public HotUpdatePublishStage FailedStage { get; }
        public string Error { get; }
        public TimeSpan Duration { get; }
        public IReadOnlyList<HotUpdatePublishStage> ExecutedStages { get; }
        public IReadOnlyList<HotUpdateDryRunFilePlan> Files { get; }
        public string ChangeSafety { get; }
        public int NewCount { get; }
        public int ReuseCount { get; }
        public long TotalBytes { get; }
        public long UploadBytes { get; }
        public bool NoRemoteMutation { get; }

        private static int Count(IReadOnlyList<HotUpdateDryRunFilePlan> files, HotUpdateDryRunFileDisposition disposition)
        {
            int count = 0;
            for (int index = 0; index < files.Count; index++)
                if (files[index].Disposition == disposition) count++;
            return count;
        }

        private static long Sum(IReadOnlyList<HotUpdateDryRunFilePlan> files, bool uploadOnly)
        {
            long total = 0;
            for (int index = 0; index < files.Count; index++)
                if (!uploadOnly || files[index].Disposition == HotUpdateDryRunFileDisposition.New)
                    total = checked(total + files[index].Length);
            return total;
        }
    }

    /// <summary>
    /// Executes Preflight through Release Gate and PrepareUpload, then queries remote metadata to build a plan.
    /// It deliberately has no code path to UploadFiles, VerifyRemote or PublishVersion.
    /// </summary>
    public sealed class HotUpdatePublishDryRun
    {
        private static readonly HotUpdatePublishStage[] Stages =
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

        private readonly Dictionary<HotUpdatePublishStage, IHotUpdatePublishStageHandler> _handlers;

        public HotUpdatePublishDryRun(IEnumerable<IHotUpdatePublishStageHandler> handlers)
        {
            _handlers = new Dictionary<HotUpdatePublishStage, IHotUpdatePublishStageHandler>();
            foreach (IHotUpdatePublishStageHandler handler in handlers ?? Array.Empty<IHotUpdatePublishStageHandler>())
            {
                if (handler == null) throw new ArgumentException("Dry-run handlers cannot contain null entries.", nameof(handlers));
                if (Array.IndexOf(Stages, handler.Stage) < 0)
                    throw new ArgumentException($"'{handler.Stage}' is not part of the dry-run stage sequence.", nameof(handlers));
                if (_handlers.ContainsKey(handler.Stage))
                    throw new ArgumentException($"More than one dry-run handler was registered for '{handler.Stage}'.", nameof(handlers));
                _handlers.Add(handler.Stage, handler);
            }
        }

        public async Task<HotUpdateDryRunResult> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var watch = Stopwatch.StartNew();
            var executed = new List<HotUpdatePublishStage>(Stages.Length);
            var emptyPlan = new List<HotUpdateDryRunFilePlan>();

            for (int index = 0; index < Stages.Length; index++)
            {
                HotUpdatePublishStage stage = Stages[index];
                if (cancellationToken.IsCancellationRequested)
                    return Failed(stage, "Dry Run was cancelled.", watch, executed, emptyPlan, context);
                if (!_handlers.TryGetValue(stage, out IHotUpdatePublishStageHandler handler))
                    return Failed(stage, $"Dry Run requires the '{stage}' stage handler.", watch, executed, emptyPlan, context);

                HotUpdatePublishStepResult result;
                try
                {
                    result = await handler.ExecuteAsync(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Failed(stage, "Dry Run was cancelled.", watch, executed, emptyPlan, context);
                }
                catch (Exception exception)
                {
                    return Failed(stage, $"Dry Run stage '{stage}' threw: {exception.Message}", watch, executed, emptyPlan, context);
                }

                executed.Add(stage);
                if (result == null || !result.Success)
                    return Failed(stage, result?.Error ?? $"Dry Run stage '{stage}' returned no result.", watch, executed, emptyPlan, context);
                if (cancellationToken.IsCancellationRequested)
                    return Failed(stage, "Dry Run was cancelled.", watch, executed, emptyPlan, context);
            }

            if (context.ReleaseGateReport == null || !context.ReleaseGateReport.Passed)
                return Failed(HotUpdatePublishStage.RunReleaseGate, "Dry Run requires passing machine-readable Release Gate evidence.", watch, executed, emptyPlan, context);
            if (context.ChangeClassification == null)
                return Failed(HotUpdatePublishStage.ClassifyChanges, "Dry Run requires completed change classification.", watch, executed, emptyPlan, context);
            if (context.PublishTargetAdapter == null)
                return Failed(HotUpdatePublishStage.PrepareUpload, "Dry Run requires a configured publish target for read-only remote inspection.", watch, executed, emptyPlan, context);

            var files = new List<HotUpdateDryRunFilePlan>(context.PublishFiles.Count);
            try
            {
                for (int index = 0; index < context.PublishFiles.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    HotUpdatePublishFile file = context.PublishFiles[index];
                    bool exists = await context.PublishTargetAdapter.ExistsAsync(file.RelativePath, cancellationToken);
                    HotUpdateDryRunFileDisposition disposition = HotUpdateDryRunFileDisposition.New;
                    if (exists)
                    {
                        HotUpdatePublishTargetFileInfo remote = await context.PublishTargetAdapter.GetInfoAsync(file.RelativePath, cancellationToken);
                        if (remote == null)
                            return Failed(HotUpdatePublishStage.PrepareUpload,
                                $"Remote target reported '{file.RelativePath}' exists but returned no file metadata.", watch, executed, files, context);
                        if (remote.Length != file.Length || !string.Equals(remote.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                            return Failed(HotUpdatePublishStage.PrepareUpload,
                                $"Immutable remote path '{file.RelativePath}' already contains different bytes; dry-run cannot classify it as reusable.", watch, executed, files, context);
                        disposition = HotUpdateDryRunFileDisposition.Reuse;
                    }
                    files.Add(new HotUpdateDryRunFilePlan(file, disposition));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failed(HotUpdatePublishStage.PrepareUpload, "Dry Run was cancelled.", watch, executed, files, context);
            }
            catch (Exception exception)
            {
                return Failed(HotUpdatePublishStage.PrepareUpload, $"Remote dry-run inspection failed: {exception.Message}", watch, executed, files, context);
            }

            watch.Stop();
            return new HotUpdateDryRunResult(context, true, HotUpdatePublishStage.None, string.Empty, watch.Elapsed,
                executed.ToArray(), files.AsReadOnly(), ResolveChangeSafety(context.ChangeClassification));
        }

        private static HotUpdateDryRunResult Failed(HotUpdatePublishStage stage, string error, Stopwatch watch,
            List<HotUpdatePublishStage> executed, List<HotUpdateDryRunFilePlan> files, HotUpdatePublishContext context)
        {
            watch.Stop();
            return new HotUpdateDryRunResult(context, false, stage, error, watch.Elapsed, executed.ToArray(),
                files.AsReadOnly(), context.ChangeClassification == null ? string.Empty : ResolveChangeSafety(context.ChangeClassification));
        }

        private static string ResolveChangeSafety(HotUpdateChangeClassificationResult classification)
        {
            if (classification.RedCount > 0 || classification.DependencyViolations.Count > 0) return "RED";
            return classification.YellowCount > 0 ? "YELLOW" : "GREEN";
        }
    }
}
