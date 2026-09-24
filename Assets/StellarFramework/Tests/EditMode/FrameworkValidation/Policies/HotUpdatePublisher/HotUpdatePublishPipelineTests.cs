using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdatePublishPipelineTests
    {
        private static readonly HotUpdatePublishStage[] ExpectedStages =
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

        [Test]
        public void PipelineRunsEveryConfiguredStageInOrderAndReturnsReleaseRecord()
        {
            var executed = new List<HotUpdatePublishStage>();
            var handlers = new List<IHotUpdatePublishStageHandler>();
            for (int index = 0; index < ExpectedStages.Length; index++)
            {
                handlers.Add(new StubHandler(ExpectedStages[index], executed));
            }

            var releaseRecord = new HotUpdateReleaseRecord
            {
                ReleaseId = "release-001",
                PackageVersion = "1.0.1"
            };
            var context = new HotUpdatePublishContext { ReleaseRecord = releaseRecord };
            var pipeline = new HotUpdatePublishPipeline(handlers);

            HotUpdatePublishResult result = pipeline.RunAsync(context).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(ExpectedStages, executed);
            Assert.That(pipeline.CurrentStage, Is.EqualTo(HotUpdatePublishStage.Completed));
            Assert.That(result.Success, Is.True);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.None));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.None));
            Assert.That(result.ReleaseRecord, Is.SameAs(releaseRecord));
            CollectionAssert.AreEqual(new[] { "Preflight completed." }, result.Warnings);
        }

        [Test]
        public void MissingStageHandlerFailsAtFirstUnconfiguredStage()
        {
            var pipeline = new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[0]);

            HotUpdatePublishResult result = pipeline.RunAsync(new HotUpdatePublishContext()).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.Preflight));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.MissingStageHandler));
            Assert.That(result.Error, Does.Contain("Preflight"));
            Assert.That(pipeline.CurrentStage, Is.EqualTo(HotUpdatePublishStage.Failed));
        }

        [Test]
        public void FailedStageStopsPipelineAndPreservesStructuredErrorAndWarnings()
        {
            var executed = new List<HotUpdatePublishStage>();
            var pipeline = new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[]
            {
                new StubHandler(
                    HotUpdatePublishStage.Preflight,
                    executed,
                    HotUpdatePublishStepResult.Failed(
                        HotUpdatePublishErrorCode.StageFailed,
                        "Git status could not be read.",
                        "Workspace state is unavailable.")),
                new StubHandler(HotUpdatePublishStage.ClassifyChanges, executed)
            });

            HotUpdatePublishResult result = pipeline.RunAsync(new HotUpdatePublishContext()).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { HotUpdatePublishStage.Preflight }, executed);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.Preflight));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.StageFailed));
            Assert.That(result.Error, Is.EqualTo("Git status could not be read."));
            CollectionAssert.AreEqual(new[] { "Workspace state is unavailable." }, result.Warnings);
        }

        [Test]
        public void UnhandledExceptionIsReturnedWithOriginalExceptionAndStage()
        {
            var expectedException = new InvalidOperationException("HybridCLR compile failed.");
            var executed = new List<HotUpdatePublishStage>();
            var pipeline = new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[]
            {
                new StubHandler(HotUpdatePublishStage.Preflight, executed),
                new StubHandler(HotUpdatePublishStage.ClassifyChanges, executed),
                new StubHandler(
                    HotUpdatePublishStage.CompileHotUpdate,
                    executed,
                    exception: expectedException)
            });

            HotUpdatePublishResult result = pipeline.RunAsync(new HotUpdatePublishContext()).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.CompileHotUpdate));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.StageException));
            Assert.That(result.Exception, Is.SameAs(expectedException));
            Assert.That(result.Error, Does.Contain("HybridCLR compile failed."));
        }

        [Test]
        public void CancellationBeforePreflightReturnsCancelledWithoutRunningHandlers()
        {
            var executed = new List<HotUpdatePublishStage>();
            var pipeline = new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[]
            {
                new StubHandler(HotUpdatePublishStage.Preflight, executed)
            });
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            HotUpdatePublishResult result = pipeline.RunAsync(
                new HotUpdatePublishContext(), cancellation.Token).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.Preflight));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.Cancelled));
            Assert.That(pipeline.CurrentStage, Is.EqualTo(HotUpdatePublishStage.Cancelled));
            Assert.That(executed, Is.Empty);
        }

        [Test]
        public void NullStageResultFailsInsteadOfAdvancing()
        {
            var executed = new List<HotUpdatePublishStage>();
            var pipeline = new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[]
            {
                new StubHandler(HotUpdatePublishStage.Preflight, executed, returnNull: true),
                new StubHandler(HotUpdatePublishStage.ClassifyChanges, executed)
            });

            HotUpdatePublishResult result = pipeline.RunAsync(new HotUpdatePublishContext()).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.Preflight));
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.InvalidStageResult));
            CollectionAssert.AreEqual(new[] { HotUpdatePublishStage.Preflight }, executed);
        }

        [Test]
        public void DuplicateStageHandlersAreRejectedAtConstruction()
        {
            var executed = new List<HotUpdatePublishStage>();
            Assert.Throws<ArgumentException>(() => new HotUpdatePublishPipeline(new IHotUpdatePublishStageHandler[]
            {
                new StubHandler(HotUpdatePublishStage.Preflight, executed),
                new StubHandler(HotUpdatePublishStage.Preflight, executed)
            }));
        }

        private sealed class StubHandler : IHotUpdatePublishStageHandler
        {
            private readonly List<HotUpdatePublishStage> _executed;
            private readonly HotUpdatePublishStepResult _result;
            private readonly Exception _exception;
            private readonly bool _returnNull;

            public StubHandler(
                HotUpdatePublishStage stage,
                List<HotUpdatePublishStage> executed,
                HotUpdatePublishStepResult result = null,
                Exception exception = null,
                bool returnNull = false)
            {
                Stage = stage;
                _executed = executed;
                _result = result ?? HotUpdatePublishStepResult.Succeeded(
                    stage == HotUpdatePublishStage.Preflight ? "Preflight completed." : null);
                _exception = exception;
                _returnNull = returnNull;
            }

            public HotUpdatePublishStage Stage { get; }

            public Task<HotUpdatePublishStepResult> ExecuteAsync(
                HotUpdatePublishContext context,
                CancellationToken cancellationToken)
            {
                _executed.Add(Stage);
                if (_exception != null)
                {
                    throw _exception;
                }

                return Task.FromResult(_returnNull ? null : _result);
            }
        }
    }
}
