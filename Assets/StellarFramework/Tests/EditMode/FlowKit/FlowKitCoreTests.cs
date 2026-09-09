using NUnit.Framework;

namespace StellarFramework.FlowKit.Tests
{
    public sealed class FlowKitCoreTests
    {
        [Test]
        public void CompilerProducesStablePlanHashAndRejectsImmediateCycle()
        {
            FlowGraphData graph = CreateGraph();
            FlowCompileResult first = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            FlowCompileResult second = FlowCompiler.Compile(CreateGraph(), FlowBuiltInNodes.CreateRegistry());
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Plan.PlanHash, Is.EqualTo(second.Plan.PlanHash));

            graph.Edges.Add(new FlowEdgeData { FromNodeId = "complete", FromPortId = "completed", ToNodeId = "pass", ToPortId = "in" });
            FlowCompileResult cycle = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(cycle.HasErrors, Is.True);
            bool hasCycle = false;
            for (int i = 0; i < cycle.Issues.Count; i++) hasCycle |= cycle.Issues[i].Code == FlowValidationErrorCode.ImmediateCycle;
            Assert.That(hasCycle, Is.True);
        }

        [Test]
        public void RunnerCompletesSimpleGraphWithoutCurrentNodeScan()
        {
            FlowCompileResult compile = FlowCompiler.Compile(CreateGraph(), FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
        }

        [Test]
        public void SignalIsQueuedAndRunLocal()
        {
            var router = new FlowSignalRouter();
            int received = 0;
            FlowRunId runId = FlowRunId.Create();
            router.Subscribe("ready", FlowSignalScope.RunLocal, runId, _ => received++);
            router.Publish("ready", FlowSignalScope.RunLocal, runId);
            Assert.That(received, Is.EqualTo(0));
            router.Drain(1);
            Assert.That(received, Is.EqualTo(1));
            router.Publish("ready", FlowSignalScope.RunLocal, FlowRunId.Create());
            router.Drain(1);
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void SignalSourceFilterDoesNotCrossBindings()
        {
            var router = new FlowSignalRouter();
            int received = 0;
            router.Subscribe("button.clicked", FlowSignalScope.Host, default(FlowRunId), "confirm", _ => received++);
            router.Publish("button.clicked", FlowSignalScope.Host, payload: FlowValue.None, sourceKey: "cancel");
            router.Publish("button.clicked", FlowSignalScope.Host, payload: FlowValue.None, sourceKey: "confirm");
            router.Drain(10);
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void SignalBacklogAgeStartsAtNewlyPublishedTail()
        {
            var router = new FlowSignalRouter();
            var services = new FlowRuntimeServices(signals: router);
            var runner = new FlowRunner(services, new FlowRunnerOptions { MaxSignalNotificationsPerTick = 1 });
            router.Subscribe("first", FlowSignalScope.Host, default(FlowRunId), _ =>
                router.Publish("second", FlowSignalScope.Host));
            router.Publish("first", FlowSignalScope.Host);
            runner.Tick(new FlowTimeSnapshot(0d, 10d, 0d));
            Assert.That(router.PendingNotificationCount, Is.EqualTo(1));
            Assert.That(router.OldestNotificationAge, Is.EqualTo(0d));
        }

        [Test]
        public void CompletionArbiterUsesPriorityThenSchedulerSequence()
        {
            var candidates = new[]
            {
                new FlowCompletionCandidate("timeout", 0, "timeout", schedulerSequence: 20),
                new FlowCompletionCandidate("user", 10, "confirmed", schedulerSequence: 30),
                new FlowCompletionCandidate("same-priority-earlier", 10, "confirmed", schedulerSequence: 10)
            };
            Assert.That(FlowCompletionArbiter.TrySelect(candidates, out FlowCompletionCandidate winner), Is.True);
            Assert.That(winner.Reason, Is.EqualTo("same-priority-earlier"));
        }

        [Test]
        public void StateRevisionOnlyChangesOnActualChangeAndSnapshotIsAtomic()
        {
            var store = new FlowStateStore();
            FlowStateKey key = new FlowStateKey("player.ready");
            int changes = 0;
            FlowStateObservation observation = store.SubscribeAndSnapshot(key, _ => changes++);
            Assert.That(observation.Snapshot.Exists, Is.False);
            Assert.That(store.Set(key, FlowValue.FromBool(true)), Is.True);
            Assert.That(store.Set(key, FlowValue.FromBool(true)), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(store.TryGet(key, out FlowStateSnapshot snapshot), Is.True);
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            observation.Subscription.Dispose();
        }

        [Test]
        public void TimerUsesDueTimeAndGenerationHandle()
        {
            var scheduler = new FlowTimerScheduler();
            int fired = 0;
            FlowTimerHandle handle = scheduler.Schedule(FlowTimeDomain.FlowTime, 2d, _ => fired++);
            Assert.That(scheduler.Advance(new FlowTimeSnapshot(0d, 0d, 1d), 10), Is.EqualTo(0));
            Assert.That(scheduler.Advance(new FlowTimeSnapshot(0d, 0d, 2d), 10), Is.EqualTo(1));
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(scheduler.Cancel(handle), Is.False);
        }

        [Test]
        public void TimerDomainsAdvanceIndependently()
        {
            var scheduler = new FlowTimerScheduler();
            int unscaledFired = 0;
            int scaledFired = 0;
            scheduler.Schedule(FlowTimeDomain.Scaled, 100d, _ => scaledFired++);
            scheduler.Schedule(FlowTimeDomain.Unscaled, 1d, _ => unscaledFired++);

            int callbacks = scheduler.Advance(new FlowTimeSnapshot(0d, 2d, 0d), 10);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(unscaledFired, Is.EqualTo(1));
            Assert.That(scaledFired, Is.EqualTo(0));
        }

        [Test]
        public void RelativeTimerUsesLatestObservedDomainTime()
        {
            var scheduler = new FlowTimerScheduler();
            scheduler.Advance(new FlowTimeSnapshot(3d, 7d, 11d), 10);
            int fired = 0;
            scheduler.Schedule(FlowDuration.FromSeconds(2d), FlowTimeDomain.Unscaled, () => fired++);
            scheduler.Advance(new FlowTimeSnapshot(3d, 8d, 11d), 10);
            Assert.That(fired, Is.EqualTo(0));
            scheduler.Advance(new FlowTimeSnapshot(3d, 9d, 11d), 10);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void BlackboardBatchSharesRevision()
        {
            var board = new FlowBlackboard();
            long revision = 0;
            board.Subscribe("a", change => revision = change.Revision);
            board.Subscribe("b", change => revision = change.Revision);
            using (FlowBlackboardBatch batch = board.BeginBatch())
            {
                batch.Set("a", FlowValue.FromInt(1), FlowBlackboardPersistence.Persistent);
                batch.Set("b", FlowValue.FromInt(2), FlowBlackboardPersistence.Persistent);
                batch.Commit();
            }

            Assert.That(revision, Is.EqualTo(1));
            Assert.That(board.CapturePersistentEntries(), Has.Length.EqualTo(2));
        }

        [Test]
        public void ParallelAllBranchesJoinOnce()
        {
            var graph = new FlowGraphData { FlowId = "tests.parallel", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "parallel", TypeId = FlowBuiltInNodes.ParallelTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "branchA", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "branchB", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "join", TypeId = FlowBuiltInNodes.JoinTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "parallel", "in");
            AddEdge(graph, "parallel", "branch", "branchA", "in");
            AddEdge(graph, "parallel", "branch", "branchB", "in");
            AddEdge(graph, "branchA", "next", "join", "in");
            AddEdge(graph, "branchB", "next", "join", "in");
            AddEdge(graph, "join", "joined", "complete", "in");
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
        }

        [Test]
        public void SnapshotRequiresQuiescentRunAndValidatesPlanHash()
        {
            FlowCompileResult compile = FlowCompiler.Compile(CreateGraph(), FlowBuiltInNodes.CreateRegistry());
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            FlowSnapshotResult before = FlowSnapshotService.Capture(run);
            Assert.That(before.Code, Is.EqualTo(FlowSnapshotResultCode.NotQuiescent));
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            FlowSnapshotResult after = FlowSnapshotService.Capture(run);
            Assert.That(after.Succeeded, Is.True);
            Assert.That(FlowSnapshotService.Validate(after.Snapshot, compile.Plan).Succeeded, Is.True);
        }

        [Test]
        public void PollingOnlyInvokesRegisteredSourcesWithinBudget()
        {
            var polling = new FlowPollingScheduler();
            int calls = 0;
            polling.Subscribe(new AlwaysPollingSource(), _ => calls++);
            polling.Subscribe(new AlwaysPollingSource(), _ => calls++);
            polling.Poll(new FlowTimeSnapshot(0d, 0d, 0d), 1);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void PollingFrequencyUsesUnscaledCadence()
        {
            var polling = new FlowPollingScheduler();
            int calls = 0;
            polling.Subscribe(new AlwaysPollingSource(), _ => calls++, FlowPollingFrequency.Hertz10);
            polling.Poll(new FlowTimeSnapshot(0d, 0d, 0d), 10);
            polling.Poll(new FlowTimeSnapshot(0d, 0.05d, 0d), 10);
            Assert.That(calls, Is.EqualTo(1));
            polling.Poll(new FlowTimeSnapshot(0d, 0.1d, 0d), 10);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void CompilerTreatsNullParameterEntriesAsEmpty()
        {
            var graph = new FlowGraphData { FlowId = "tests.null-parameters", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData
            {
                Id = "entry",
                TypeId = "flow.entry",
                Parameters = new FlowPropertyBag { Entries = null }
            });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "complete", "in");

            FlowCompileResult result = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void RunnerFailsFastWhenCompletionHasNoRoute()
        {
            var graph = new FlowGraphData { FlowId = "tests.unrouted", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);

            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Failed));
            Assert.That(run.LastError.Code, Is.EqualTo(FlowRuntimeErrorCode.UnroutedCompletion));
        }

        [Test]
        public void RunnerStopsCompletionBoundaryLoopAtTotalActivationBudget()
        {
            var graph = new FlowGraphData { FlowId = "tests.activation-budget", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "pass", TypeId = "flow.pass" });
            var delay = new FlowNodeData { Id = "delay", TypeId = "flow.delay" };
            delay.Parameters.Set("seconds", FlowValue.FromDouble(0d));
            graph.Nodes.Add(delay);
            AddEdge(graph, "entry", "next", "pass", "in");
            AddEdge(graph, "pass", "next", "delay", "in");
            AddEdge(graph, "delay", "completed", "pass", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner(options: new FlowRunnerOptions { MaxTotalActivationsPerRun = 4 });
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Failed));
            Assert.That(run.LastError.Code, Is.EqualTo(FlowRuntimeErrorCode.ActivationBudgetExceeded));
        }

        [Test]
        public void SnapshotCapturesPersistentStateMetadata()
        {
            var states = new FlowStateStore();
            var services = new FlowRuntimeServices(states: states);
            FlowCompileResult compile = FlowCompiler.Compile(CreateGraph(), FlowBuiltInNodes.CreateRegistry());
            var runner = new FlowRunner(services);
            FlowRun run = runner.Start(compile.Plan);
            states.Set(new FlowStateKey("training.ready"), FlowValue.FromBool(true), FlowStateLifetime.Persistent);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            FlowSnapshotResult capture = FlowSnapshotService.Capture(run, "checkpoint-1", "save-1");
            Assert.That(capture.Succeeded, Is.True);
            Assert.That(capture.Snapshot.PersistentRunId, Is.EqualTo("save-1"));
            Assert.That(capture.Snapshot.CheckpointId, Is.EqualTo("checkpoint-1"));
            Assert.That(capture.Snapshot.PersistentStates, Has.Length.EqualTo(1));

            var restored = new FlowStateStore();
            FlowSnapshotService.RestorePersistentStates(capture.Snapshot, restored, compile.Plan);
            Assert.That(restored.TryGet(new FlowStateKey("training.ready"), out FlowStateSnapshot state), Is.True);
            Assert.That(state.Value.BoolValue, Is.True);
        }

        private sealed class AlwaysPollingSource : IFlowPollingSource
        {
            public bool TryPoll(in FlowTimeSnapshot time, out FlowValue value)
            {
                value = FlowValue.FromBool(true);
                return true;
            }
        }

        private static FlowGraphData CreateGraph()
        {
            var graph = new FlowGraphData { FlowId = "tests.simple", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "pass", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            graph.Edges.Add(new FlowEdgeData { FromNodeId = "entry", FromPortId = "next", ToNodeId = "pass", ToPortId = "in" });
            graph.Edges.Add(new FlowEdgeData { FromNodeId = "pass", FromPortId = "next", ToNodeId = "complete", ToPortId = "in" });
            return graph;
        }

        private static void AddEdge(FlowGraphData graph, string fromNode, string fromPort, string toNode, string toPort)
        {
            graph.Edges.Add(new FlowEdgeData
            {
                FromNodeId = fromNode,
                FromPortId = fromPort,
                ToNodeId = toNode,
                ToPortId = toPort
            });
        }
    }
}
