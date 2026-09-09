using System;
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
        public void NestedParallelInnerJoinRestoresOuterLineage()
        {
            var graph = new FlowGraphData { FlowId = "tests.parallel.nested", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "outerParallel", TypeId = FlowBuiltInNodes.ParallelTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "outerA", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "outerB", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "innerParallel", TypeId = FlowBuiltInNodes.ParallelTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "innerA", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "innerB", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "innerJoin", TypeId = FlowBuiltInNodes.JoinTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "outerJoin", TypeId = FlowBuiltInNodes.JoinTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });

            AddEdge(graph, "entry", "next", "outerParallel", "in");
            AddEdge(graph, "outerParallel", "branch", "outerA", "in");
            AddEdge(graph, "outerParallel", "branch", "outerB", "in");
            AddEdge(graph, "outerA", "next", "innerParallel", "in");
            AddEdge(graph, "outerB", "next", "outerJoin", "in");
            AddEdge(graph, "innerParallel", "branch", "innerA", "in");
            AddEdge(graph, "innerParallel", "branch", "innerB", "in");
            AddEdge(graph, "innerA", "next", "innerJoin", "in");
            AddEdge(graph, "innerB", "next", "innerJoin", "in");
            AddEdge(graph, "innerJoin", "joined", "outerJoin", "in");
            AddEdge(graph, "outerJoin", "joined", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
        }

        [Test]
        public void DuplicateBranchArrivalDoesNotReleaseAllJoinEarly()
        {
            var graph = new FlowGraphData { FlowId = "tests.parallel.duplicate", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "parallel", TypeId = FlowBuiltInNodes.ParallelTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "branchA", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "branchA1", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "branchA2", TypeId = "flow.pass" });
            var delayedBranch = new FlowNodeData { Id = "branchB", TypeId = "flow.delay" };
            delayedBranch.Parameters.Set("seconds", FlowValue.FromDouble(1d));
            graph.Nodes.Add(delayedBranch);
            graph.Nodes.Add(new FlowNodeData { Id = "join", TypeId = FlowBuiltInNodes.JoinTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });

            AddEdge(graph, "entry", "next", "parallel", "in");
            AddEdge(graph, "parallel", "branch", "branchA", "in");
            AddEdge(graph, "parallel", "branch", "branchB", "in");
            AddEdge(graph, "branchA", "next", "branchA1", "in");
            AddEdge(graph, "branchA", "next", "branchA2", "in");
            AddEdge(graph, "branchA1", "next", "join", "in");
            AddEdge(graph, "branchA2", "next", "join", "in");
            AddEdge(graph, "branchB", "completed", "join", "in");
            AddEdge(graph, "join", "joined", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);

            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Running));

            runner.Tick(new FlowTimeSnapshot(1d, 1d, 1d));
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
            for (int i = 0; i < 8 && !run.IsTerminal; i++)
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
            Assert.That(capture.Snapshot.PersistentStates.Count, Is.EqualTo(1));

            var restored = new FlowStateStore();
            FlowSnapshotService.RestorePersistentStates(capture.Snapshot, restored, compile.Plan);
            Assert.That(restored.TryGet(new FlowStateKey("training.ready"), out FlowStateSnapshot state), Is.True);
            Assert.That(state.Value.BoolValue, Is.True);
        }

        [Test]
        public void OperationReceivesCompiledRequestArguments()
        {
            var graph = new FlowGraphData { FlowId = "tests.operation.request", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            var operationNode = new FlowNodeData { Id = "operation", TypeId = "flow.operation" };
            operationNode.Parameters.Set("operation", FlowValue.FromString("test.echo"));
            operationNode.Parameters.Set("message", FlowValue.FromString("hello"));
            graph.Nodes.Add(operationNode);
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "operation", "in");
            AddEdge(graph, "operation", "succeeded", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var adapter = new CapturingOperationAdapter(FlowOperationResult.Success());
            var operations = new FlowOperationRegistry().Register("test.echo", adapter);
            var runner = new FlowRunner(new FlowRuntimeServices(operations: operations));
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
            Assert.That(adapter.StartCount, Is.EqualTo(1));
            Assert.That(adapter.LastRequest.OperationId, Is.EqualTo("test.echo"));
            Assert.That(adapter.LastRequest.TryGetArgument("message", out FlowValue message), Is.True);
            Assert.That(message.StringValue, Is.EqualTo("hello"));
        }

        [Test]
        public void OperationCancelledUsesCancelledOutput()
        {
            var graph = new FlowGraphData { FlowId = "tests.operation.cancelled", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            var operationNode = new FlowNodeData { Id = "operation", TypeId = "flow.operation" };
            operationNode.Parameters.Set("operation", FlowValue.FromString("test.cancel"));
            graph.Nodes.Add(operationNode);
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "operation", "in");
            AddEdge(graph, "operation", "cancelled", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            var operations = new FlowOperationRegistry().Register(
                "test.cancel", new CapturingOperationAdapter(FlowOperationResult.Cancelled("cancelled")));
            var runner = new FlowRunner(new FlowRuntimeServices(operations: operations));
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
        }

        [Test]
        public void BindingReferenceResolvesStableIdAndRejectsOldHandle()
        {
            var bindings = new FlowBindingRegistry();
            var first = new object();
            FlowBindingHandle oldHandle = bindings.Bind("training.target", first);
            Assert.That(bindings.TryResolve(new FlowBindingReference("training.target"), out object resolved), Is.True);
            Assert.That(resolved, Is.SameAs(first));
            Assert.That(bindings.Unbind(oldHandle), Is.True);

            var second = new object();
            FlowBindingHandle newHandle = bindings.Bind("training.target", second);
            Assert.That(newHandle.Generation, Is.Not.EqualTo(oldHandle.Generation));
            Assert.That(bindings.TryResolve(oldHandle, out _), Is.False);
            Assert.That(bindings.TryResolve(new FlowBindingReference("training.target"), out resolved), Is.True);
            Assert.That(resolved, Is.SameAs(second));
        }

        [Test]
        public void CompilerRejectsUnknownBuiltInEnumValue()
        {
            var graph = new FlowGraphData { FlowId = "tests.invalid.enum", EntryNodeId = "delay" };
            var delay = new FlowNodeData { Id = "delay", TypeId = "flow.delay" };
            delay.Parameters.Set("seconds", FlowValue.FromDouble(1d));
            delay.Parameters.Set("timeDomain", FlowValue.FromString("Unscaleed"));
            graph.Nodes.Add(delay);
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.False);
            Assert.That(compile.HasErrors, Is.True);
        }

        [Test]
        public void IncrementBlackboardPreservesTypeAndPersistence()
        {
            var graph = new FlowGraphData { FlowId = "tests.increment", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            var increment = new FlowNodeData { Id = "increment", TypeId = "flow.increment.blackboard" };
            increment.Parameters.Set("key", FlowValue.FromString("score"));
            increment.Parameters.Set("amount", FlowValue.FromFloat(0.5f));
            graph.Nodes.Add(increment);
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "increment", "in");
            AddEdge(graph, "increment", "next", "complete", "in");

            var board = new FlowBlackboard();
            board.Set("score", FlowValue.FromFloat(2f), FlowBlackboardPersistence.Persistent);
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan, board);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
            Assert.That(board.TryGetEntry("score", out FlowBlackboardEntry entry), Is.True);
            Assert.That(entry.Value.Kind, Is.EqualTo(FlowValueKind.Float));
            Assert.That(entry.Value.FloatValue, Is.EqualTo(2.5f));
            Assert.That(entry.Persistence, Is.EqualTo(FlowBlackboardPersistence.Persistent));
        }

        [Test]
        public void ClosedExecutionGroupIsReleasedWhileRunContinues()
        {
            var graph = new FlowGraphData { FlowId = "tests.group.cleanup", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "parallel", TypeId = FlowBuiltInNodes.ParallelTypeId });
            graph.Nodes.Add(new FlowNodeData { Id = "a", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "b", TypeId = "flow.pass" });
            graph.Nodes.Add(new FlowNodeData { Id = "join", TypeId = FlowBuiltInNodes.JoinTypeId });
            var delay = new FlowNodeData { Id = "delay", TypeId = "flow.delay" };
            delay.Parameters.Set("seconds", FlowValue.FromDouble(100d));
            graph.Nodes.Add(delay);
            AddEdge(graph, "entry", "next", "parallel", "in");
            AddEdge(graph, "parallel", "branch", "a", "in");
            AddEdge(graph, "parallel", "branch", "b", "in");
            AddEdge(graph, "a", "next", "join", "in");
            AddEdge(graph, "b", "next", "join", "in");
            AddEdge(graph, "join", "joined", "delay", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Running));
            Assert.That(run.ActiveExecutionCount, Is.EqualTo(1));
            Assert.That(run.ExecutionGroupCount, Is.EqualTo(0));
        }

        [Test]
        public void CancelHandlerSeesCancelledButUndisposedExecutionToken()
        {
            var registry = FlowBuiltInNodes.CreateRegistry();
            var handler = new CancelLifecycleHandler();
            registry.Register(new FlowNodeDescriptor(
                "test.cancel.lifecycle", 1, "Cancel Lifecycle", "Tests", FlowNodeExecutionMode.Completion,
                new[] { new FlowPortDescriptor("in", "in", FlowPortDirection.Input) }), handler);

            var graph = new FlowGraphData { FlowId = "tests.cancel.lifecycle", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "wait", TypeId = "test.cancel.lifecycle" });
            AddEdge(graph, "entry", "next", "wait", "in");
            FlowCompileResult compile = FlowCompiler.Compile(graph, registry);
            Assert.That(compile.Succeeded, Is.True);

            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.ActiveExecutionCount, Is.EqualTo(1));
            run.Cancel();

            Assert.That(handler.CancelCalled, Is.True);
            Assert.That(handler.CancellationRequested, Is.True);
            Assert.That(handler.CouldCreateChild, Is.True);
        }

        [Test]
        public void TrainingWorkflowOrchestrationCompletesFromExternalFacts()
        {
            var graph = new FlowGraphData { FlowId = "tests.training.workflow", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(OperationNode("intro", "media.play_intro"));
            graph.Nodes.Add(OperationNode("school", "scene.load", "scene", FlowValue.FromString("school")));
            graph.Nodes.Add(OperationNode("showIntro", "ui.show_intro"));
            graph.Nodes.Add(new FlowNodeData { Id = "race", TypeId = FlowBuiltInNodes.RaceTypeId });
            var waitExecute = new FlowNodeData { Id = "waitExecute", TypeId = "flow.wait.signal" };
            waitExecute.Parameters.Set("signal", FlowValue.FromString("ui.execute"));
            waitExecute.Parameters.Set("scope", FlowValue.FromString("Host"));
            graph.Nodes.Add(waitExecute);
            var executeTimeout = new FlowNodeData { Id = "executeTimeout", TypeId = "flow.delay" };
            executeTimeout.Parameters.Set("seconds", FlowValue.FromDouble(10d));
            graph.Nodes.Add(executeTimeout);
            graph.Nodes.Add(new FlowNodeData { Id = "executeJoin", TypeId = FlowBuiltInNodes.JoinTypeId });
            graph.Nodes.Add(OperationNode("guide", "guide.show", "binding", FlowValue.FromBindingReference("AssemblyPoint")));
            var waitAssembly = new FlowNodeData { Id = "waitAssembly", TypeId = "flow.wait.state" };
            waitAssembly.Parameters.Set("state", FlowValue.FromString("assembly.completed"));
            waitAssembly.Parameters.Set("expected", FlowValue.FromBool(true));
            graph.Nodes.Add(waitAssembly);
            graph.Nodes.Add(OperationNode("forest", "scene.load", "scene", FlowValue.FromString("forest")));
            graph.Nodes.Add(OperationNode("patrol", "patrol.start"));
            var waitPatrol = new FlowNodeData { Id = "waitPatrol", TypeId = "flow.wait.state" };
            waitPatrol.Parameters.Set("state", FlowValue.FromString("patrol.completed"));
            waitPatrol.Parameters.Set("expected", FlowValue.FromBool(true));
            graph.Nodes.Add(waitPatrol);
            graph.Nodes.Add(OperationNode("summary", "ui.show_summary"));
            var finishDelay = new FlowNodeData { Id = "finishDelay", TypeId = "flow.delay" };
            finishDelay.Parameters.Set("seconds", FlowValue.FromDouble(10d));
            graph.Nodes.Add(finishDelay);
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });

            AddEdge(graph, "entry", "next", "intro", "in");
            AddEdge(graph, "intro", "succeeded", "school", "in");
            AddEdge(graph, "school", "succeeded", "showIntro", "in");
            AddEdge(graph, "showIntro", "succeeded", "race", "in");
            AddEdge(graph, "race", "branch", "waitExecute", "in");
            AddEdge(graph, "race", "branch", "executeTimeout", "in");
            AddEdge(graph, "waitExecute", "received", "executeJoin", "in");
            AddEdge(graph, "executeTimeout", "completed", "executeJoin", "in");
            AddEdge(graph, "executeJoin", "joined", "guide", "in");
            AddEdge(graph, "guide", "succeeded", "waitAssembly", "in");
            AddEdge(graph, "waitAssembly", "changed", "forest", "in");
            AddEdge(graph, "forest", "succeeded", "patrol", "in");
            AddEdge(graph, "patrol", "succeeded", "waitPatrol", "in");
            AddEdge(graph, "waitPatrol", "changed", "summary", "in");
            AddEdge(graph, "summary", "succeeded", "finishDelay", "in");
            AddEdge(graph, "finishDelay", "completed", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var adapter = new RecordingOperationAdapter();
            var operations = new FlowOperationRegistry();
            operations.Register("media.play_intro", adapter);
            operations.Register("scene.load", adapter);
            operations.Register("ui.show_intro", adapter);
            operations.Register("guide.show", adapter);
            operations.Register("patrol.start", adapter);
            operations.Register("ui.show_summary", adapter);
            var services = new FlowRuntimeServices(operations: operations);
            var runner = new FlowRunner(services);
            FlowRun run = runner.Start(compile.Plan);

            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Running));
            services.Signals.Publish("ui.execute", FlowSignalScope.Host);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            services.States.Set(new FlowStateKey("assembly.completed"), FlowValue.FromBool(true), FlowStateLifetime.External);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            services.States.Set(new FlowStateKey("patrol.completed"), FlowValue.FromBool(true), FlowStateLifetime.External);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Running));
            runner.Tick(new FlowTimeSnapshot(10d, 10d, 10d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
            Assert.That(adapter.Requests.Count, Is.EqualTo(7));
            Assert.That(adapter.Requests[1].TryGetArgument("scene", out FlowValue schoolScene), Is.True);
            Assert.That(schoolScene.StringValue, Is.EqualTo("school"));
            Assert.That(adapter.Requests[3].TryGetArgument("binding", out FlowValue binding), Is.True);
            Assert.That(binding.Kind, Is.EqualTo(FlowValueKind.BindingReference));
            Assert.That(binding.BindingReferenceValue.Id, Is.EqualTo("AssemblyPoint"));
            Assert.That(adapter.Requests[4].TryGetArgument("scene", out FlowValue forestScene), Is.True);
            Assert.That(forestScene.StringValue, Is.EqualTo("forest"));
        }

        private sealed class CancelLifecycleHandler : IFlowNodeHandler
        {
            public bool CancelCalled { get; private set; }
            public bool CancellationRequested { get; private set; }
            public bool CouldCreateChild { get; private set; }

            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node)
            {
                CancelCalled = true;
                CancellationRequested = context.Cancellation.IsCancellationRequested;
                try
                {
                    FlowCancellationToken child = context.Cancellation.CreateChild();
                    CouldCreateChild = child.IsCancellationRequested;
                    child.Dispose();
                }
                catch (InvalidOperationException)
                {
                    CouldCreateChild = false;
                }
            }
        }

        [Test]
        public void ConditionBranchRequiresTypedCondition()
        {
            var graph = new FlowGraphData { FlowId = "tests.condition.missing", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData { Id = "branch", TypeId = "flow.branch.condition" });
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "branch", "in");
            AddEdge(graph, "branch", "true", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.False);
            bool found = false;
            for (int i = 0; i < compile.Issues.Count; i++)
                found |= compile.Issues[i].Code == FlowValidationErrorCode.InvalidCondition;
            Assert.That(found, Is.True);
        }

        [Test]
        public void ConditionBranchEvaluatesBlackboardComparison()
        {
            var graph = new FlowGraphData { FlowId = "tests.condition.runtime", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData
            {
                Id = "branch",
                TypeId = "flow.branch.condition",
                Condition = FlowCondition.Compare(
                    FlowCondition.BlackboardValue("score"),
                    FlowComparisonOperator.GreaterOrEqual,
                    new FlowCondition { Kind = FlowConditionKind.Constant, Constant = FlowValue.FromInt(80) })
            });
            graph.Nodes.Add(new FlowNodeData { Id = "yes", TypeId = "flow.complete" });
            graph.Nodes.Add(new FlowNodeData { Id = "no", TypeId = "flow.pass" });
            AddEdge(graph, "entry", "next", "branch", "in");
            AddEdge(graph, "branch", "true", "yes", "in");
            AddEdge(graph, "branch", "false", "no", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var board = new FlowBlackboard();
            board.Set("score", FlowValue.FromInt(90));
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan, board);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed));
        }

        [Test]
        public void ConditionSemanticsParticipateInPlanHash()
        {
            FlowGraphData first = CreateConditionHashGraph(80);
            FlowGraphData second = CreateConditionHashGraph(81);
            FlowCompileResult a = FlowCompiler.Compile(first, FlowBuiltInNodes.CreateRegistry());
            FlowCompileResult b = FlowCompiler.Compile(second, FlowBuiltInNodes.CreateRegistry());
            Assert.That(a.Succeeded, Is.True);
            Assert.That(b.Succeeded, Is.True);
            Assert.That(a.Plan.PlanHash, Is.Not.EqualTo(b.Plan.PlanHash));
        }

        private static FlowGraphData CreateConditionHashGraph(int threshold)
        {
            var graph = new FlowGraphData { FlowId = "tests.condition.hash", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(new FlowNodeData
            {
                Id = "branch", TypeId = "flow.branch.condition",
                Condition = FlowCondition.Compare(
                    FlowCondition.BlackboardValue("score"), FlowComparisonOperator.GreaterOrEqual,
                    new FlowCondition { Kind = FlowConditionKind.Constant, Constant = FlowValue.FromInt(threshold) })
            });
            graph.Nodes.Add(new FlowNodeData { Id = "yes", TypeId = "flow.complete" });
            graph.Nodes.Add(new FlowNodeData { Id = "no", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "branch", "in");
            AddEdge(graph, "branch", "true", "yes", "in");
            AddEdge(graph, "branch", "false", "no", "in");
            return graph;
        }

        private sealed class RecordingOperationAdapter : IFlowOperationAdapter
        {
            public readonly System.Collections.Generic.List<FlowOperationRequest> Requests =
                new System.Collections.Generic.List<FlowOperationRequest>();

            public void Start(
                in FlowOperationContext context,
                in FlowOperationRequest request,
                FlowOperationHandle handle,
                Action<FlowOperationResult> complete)
            {
                Requests.Add(request);
                complete(FlowOperationResult.Success());
            }

            public void Cancel(in FlowOperationContext context, FlowOperationHandle handle)
            {
            }
        }

        private sealed class CapturingOperationAdapter : IFlowOperationAdapter
        {
            private readonly FlowOperationResult _result;

            public int StartCount { get; private set; }
            public FlowOperationRequest LastRequest { get; private set; }

            public CapturingOperationAdapter(FlowOperationResult result)
            {
                _result = result;
            }

            public void Start(
                in FlowOperationContext context,
                in FlowOperationRequest request,
                FlowOperationHandle handle,
                System.Action<FlowOperationResult> complete)
            {
                StartCount++;
                LastRequest = request;
                complete(_result);
            }

            public void Cancel(in FlowOperationContext context, FlowOperationHandle handle)
            {
            }
        }

        private sealed class AlwaysPollingSource : IFlowPollingSource
        {
            public bool TryPoll(in FlowTimeSnapshot time, out FlowValue value)
            {
                value = FlowValue.FromBool(true);
                return true;
            }
        }

        [Test]
        public void ExplicitFailNodeFailsRunWithBusinessFailure()
        {
            var graph = new FlowGraphData { FlowId = "tests.explicit.fail", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            var fail = new FlowNodeData { Id = "fail", TypeId = "flow.fail" };
            fail.Parameters.Set("message", FlowValue.FromString("drill failed"));
            graph.Nodes.Add(fail);
            AddEdge(graph, "entry", "next", "fail", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            var runner = new FlowRunner();
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Failed));
            Assert.That(run.LastError.Code, Is.EqualTo(FlowRuntimeErrorCode.BusinessFailure));
            Assert.That(run.LastError.Message, Is.EqualTo("drill failed"));
        }

        [Test]
        public void CompilerWarnsWhenOperationFailureBranchesAreUnrouted()
        {
            var graph = new FlowGraphData { FlowId = "tests.failure.route.warning", EntryNodeId = "entry" };
            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });
            graph.Nodes.Add(OperationNode("operation", "demo.operation"));
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });
            AddEdge(graph, "entry", "next", "operation", "in");
            AddEdge(graph, "operation", "succeeded", "complete", "in");

            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True);
            int warnings = 0;
            for (int i = 0; i < compile.Issues.Count; i++)
                if (compile.Issues[i].Code == FlowValidationErrorCode.UnroutedRecommendedOutput) warnings++;
            Assert.That(warnings, Is.EqualTo(2));
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

        private static FlowNodeData OperationNode(
            string id,
            string operationId,
            string argumentKey = null,
            FlowValue argumentValue = default(FlowValue))
        {
            var node = new FlowNodeData { Id = id, TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString(operationId));
            if (!string.IsNullOrEmpty(argumentKey)) node.Parameters.Set(argumentKey, argumentValue);
            return node;
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
