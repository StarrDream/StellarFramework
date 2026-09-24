using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateReleaseGateTests
    {
        [Test]
        public void GreenHotPatchRequiresFastGate()
        {
            Assert.That(HotUpdateReleaseGatePolicy.Resolve(Context(GreenFacts())), Is.EqualTo(HotUpdateReleaseGateLevel.Fast));
        }

        [Test]
        public void YellowChangeRequiresFullGate()
        {
            var facts = new HotUpdateChangeFacts("Assets/_Project/Content/Shader.shader",
                HotUpdateChangeAssetKind.Shader, HotUpdateProjectLayer.RemoteContent);
            Assert.That(HotUpdateReleaseGatePolicy.Resolve(Context(facts)), Is.EqualTo(HotUpdateReleaseGateLevel.Full));
        }

        [Test]
        public void MajorHotPatchAndBaseAppReleaseRequireFullGate()
        {
            HotUpdatePublishContext major = Context(GreenFacts());
            major.IsMajorHotPatch = true;
            Assert.That(HotUpdateReleaseGatePolicy.Resolve(major), Is.EqualTo(HotUpdateReleaseGateLevel.Full));

            HotUpdatePublishContext baseRelease = Context(
                new HotUpdateChangeFacts("ProjectSettings/ProjectSettings.asset",
                    HotUpdateChangeAssetKind.ProjectConfiguration, HotUpdateProjectLayer.Unknown));
            baseRelease.IsBaseAppRelease = true;
            Assert.That(HotUpdateReleaseGatePolicy.Resolve(baseRelease), Is.EqualTo(HotUpdateReleaseGateLevel.Full));
        }

        [Test]
        public void RedOrdinaryHotPatchIsRejectedBeforeFastGate()
        {
            HotUpdatePublishContext context = Context(
                new HotUpdateChangeFacts("Assets/_Project/Base/Bootstrap.cs",
                    HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.BaseApp));
            var fast = new StubRunner(true);
            var stage = new HotUpdateReleaseGateStageHandler(fast, new StubRunner(true));

            HotUpdatePublishStepResult result = stage.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.ReleaseGatePolicyRejected));
            Assert.That(fast.CallCount, Is.Zero);
        }

        [Test]
        public void FullGateRunsOnlyAfterFastGatePasses()
        {
            HotUpdatePublishContext context = Context(
                new HotUpdateChangeFacts("Assets/_Project/Content/Shader.shader",
                    HotUpdateChangeAssetKind.Shader, HotUpdateProjectLayer.RemoteContent));
            var order = new List<string>();
            var fast = new StubRunner(true, "Fast", order);
            var full = new StubRunner(true, "Full", order);

            HotUpdatePublishStepResult result = new HotUpdateReleaseGateStageHandler(fast, full)
                .ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True);
            CollectionAssert.AreEqual(new[] { "Fast", "Full" }, order);
            Assert.That(context.ReleaseGateReport.Passed, Is.True);
            Assert.That(context.ReleaseGateReport.RequiredLevel, Is.EqualTo(HotUpdateReleaseGateLevel.Full));
        }

        [Test]
        public void FastFailureDoesNotStartFullGate()
        {
            HotUpdatePublishContext context = Context(
                new HotUpdateChangeFacts("Assets/_Project/Content/Shader.shader",
                    HotUpdateChangeAssetKind.Shader, HotUpdateProjectLayer.RemoteContent));
            var order = new List<string>();
            var fast = new StubRunner(false, "Fast", order);
            var full = new StubRunner(true, "Full", order);

            HotUpdatePublishStepResult result = new HotUpdateReleaseGateStageHandler(fast, full)
                .ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.ReleaseGateFailed));
            CollectionAssert.AreEqual(new[] { "Fast" }, order);
            Assert.That(context.ReleaseGateReport.Passed, Is.False);
        }

        [Test]
        public void RequiredFullGateWithoutConfiguredRunnerFailsClosed()
        {
            HotUpdatePublishContext context = Context(
                new HotUpdateChangeFacts("Assets/_Project/Content/Shader.shader",
                    HotUpdateChangeAssetKind.Shader, HotUpdateProjectLayer.RemoteContent));
            var fast = new StubRunner(true);

            HotUpdatePublishStepResult result = new HotUpdateReleaseGateStageHandler(fast)
                .ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.FullReleaseGateUnavailable));
            Assert.That(fast.CallCount, Is.EqualTo(1));
        }

        private static HotUpdatePublishContext Context(params HotUpdateChangeFacts[] facts)
        {
            return new HotUpdatePublishContext
            {
                ReleaseId = "gate-test",
                ChangeClassification = HotUpdateChangeClassifier.Classify(facts)
            };
        }

        private static HotUpdateChangeFacts GreenFacts()
        {
            return new HotUpdateChangeFacts("Assets/_Project/HotUpdate/Gameplay/Player.cs",
                HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.HotUpdate);
        }

        private sealed class StubRunner : IHotUpdateFastReleaseGateRunner, IHotUpdateFullReleaseGateRunner
        {
            private readonly bool _pass;
            private readonly string _name;
            private readonly List<string> _order;

            public StubRunner(bool pass, string name = "Gate", List<string> order = null)
            {
                _pass = pass;
                _name = name;
                _order = order;
            }

            public int CallCount { get; private set; }

            public Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            {
                CallCount++;
                _order?.Add(_name);
                return Task.FromResult(new HotUpdateReleaseGateRun
                {
                    Passed = _pass,
                    EvidencePath = _name + ".json",
                    Diagnostic = _pass ? string.Empty : _name + " failed."
                });
            }
        }
    }
}
