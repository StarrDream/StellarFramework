using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>由具体构建、校验或发布 Adapter 实现的单个有序流水线阶段。</summary>
    public interface IHotUpdatePublishStageHandler
    {
        /// <summary>该处理器负责的唯一阶段。</summary>
        HotUpdatePublishStage Stage { get; }

        /// <summary>执行阶段并返回显式成功、失败代码和警告。</summary>
        Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// 顺序执行 HotUpdate 发布阶段。缺少处理器、失败结果或异常都会停止流水线并保留阶段；
    /// 任何阶段都不会因为尚未实现而被静默跳过。
    /// </summary>
    public sealed class HotUpdatePublishPipeline
    {
        private static readonly HotUpdatePublishStage[] OrderedStages =
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

        private readonly Dictionary<HotUpdatePublishStage, IHotUpdatePublishStageHandler> _handlers;
        private int _isRunning;

        /// <summary>创建可通过阶段 Adapter 扩展的流水线。</summary>
        public HotUpdatePublishPipeline(IEnumerable<IHotUpdatePublishStageHandler> handlers)
        {
            _handlers = new Dictionary<HotUpdatePublishStage, IHotUpdatePublishStageHandler>();
            foreach (IHotUpdatePublishStageHandler handler in handlers ?? Array.Empty<IHotUpdatePublishStageHandler>())
            {
                if (handler == null)
                {
                    throw new ArgumentException("Pipeline stage handlers cannot contain null entries.", nameof(handlers));
                }

                if (Array.IndexOf(OrderedStages, handler.Stage) < 0)
                {
                    throw new ArgumentException(
                        $"'{handler.Stage}' is not an executable publish stage.", nameof(handlers));
                }

                if (_handlers.ContainsKey(handler.Stage))
                {
                    throw new ArgumentException(
                        $"More than one handler was registered for '{handler.Stage}'.", nameof(handlers));
                }

                _handlers.Add(handler.Stage, handler);
            }
        }

        /// <summary>当前执行阶段；流水线结束后为 Completed、Failed 或 Cancelled。</summary>
        public HotUpdatePublishStage CurrentStage { get; private set; }

        /// <summary>按固定顺序运行阶段，并在首个失败、异常或取消点停止。</summary>
        public async Task<HotUpdatePublishResult> RunAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                throw new InvalidOperationException("This HotUpdate publish pipeline is already running.");
            }

            var stopwatch = Stopwatch.StartNew();
            var warnings = new List<string>();
            CurrentStage = HotUpdatePublishStage.None;
            try
            {
                for (int index = 0; index < OrderedStages.Length; index++)
                {
                    HotUpdatePublishStage stage = OrderedStages[index];
                    CurrentStage = stage;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Cancelled(stage, stopwatch, warnings);
                    }

                    if (!_handlers.TryGetValue(stage, out IHotUpdatePublishStageHandler handler))
                    {
                        return Failed(
                            stage,
                            HotUpdatePublishErrorCode.MissingStageHandler,
                            $"No stage handler is configured for '{stage}'.",
                            null,
                            stopwatch,
                            warnings);
                    }

                    HotUpdatePublishStepResult stepResult;
                    try
                    {
                        stepResult = await handler.ExecuteAsync(context, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return Cancelled(stage, stopwatch, warnings);
                    }
                    catch (Exception exception)
                    {
                        return Failed(
                            stage,
                            HotUpdatePublishErrorCode.StageException,
                            $"Stage '{stage}' threw an exception: {exception.Message}",
                            exception,
                            stopwatch,
                            warnings);
                    }

                    if (stepResult == null)
                    {
                        return Failed(
                            stage,
                            HotUpdatePublishErrorCode.InvalidStageResult,
                            $"Stage '{stage}' returned no result.",
                            null,
                            stopwatch,
                            warnings);
                    }

                    AppendWarnings(warnings, stepResult.Warnings);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Cancelled(stage, stopwatch, warnings);
                    }

                    if (!stepResult.Success)
                    {
                        return Failed(
                            stage,
                            stepResult.ErrorCode,
                            stepResult.Error,
                            null,
                            stopwatch,
                            warnings);
                    }
                }

                CurrentStage = HotUpdatePublishStage.Completed;
                return new HotUpdatePublishResult(
                    true,
                    HotUpdatePublishStage.None,
                    HotUpdatePublishErrorCode.None,
                    string.Empty,
                    null,
                    stopwatch.Elapsed,
                    warnings.ToArray(),
                    context.ReleaseRecord);
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private HotUpdatePublishResult Cancelled(
            HotUpdatePublishStage stage,
            Stopwatch stopwatch,
            IReadOnlyList<string> warnings)
        {
            CurrentStage = HotUpdatePublishStage.Cancelled;
            return new HotUpdatePublishResult(
                false,
                stage,
                HotUpdatePublishErrorCode.Cancelled,
                $"Publish was cancelled during '{stage}'.",
                null,
                stopwatch.Elapsed,
                CopyWarnings(warnings),
                null);
        }

        private void AppendWarnings(List<string> destination, IReadOnlyList<string> source)
        {
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(source[index]))
                {
                    destination.Add(source[index]);
                }
            }
        }

        private HotUpdatePublishResult Failed(
            HotUpdatePublishStage stage,
            HotUpdatePublishErrorCode errorCode,
            string error,
            Exception exception,
            Stopwatch stopwatch,
            IReadOnlyList<string> warnings)
        {
            CurrentStage = HotUpdatePublishStage.Failed;
            return new HotUpdatePublishResult(
                false,
                stage,
                errorCode,
                error,
                exception,
                stopwatch.Elapsed,
                CopyWarnings(warnings),
                null);
        }

        private static string[] CopyWarnings(IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[warnings.Count];
            for (int index = 0; index < warnings.Count; index++)
            {
                copy[index] = warnings[index];
            }

            return copy;
        }
    }
}
