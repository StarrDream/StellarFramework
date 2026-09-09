using System;

namespace StellarFramework.FlowKit
{
    /// <summary>FlowKit 内置节点的显式注册。业务节点应以同样方式通过 Registry 注入。</summary>
    public static class FlowBuiltInNodes
    {
        public const string ParallelTypeId = "flow.parallel";
        public const string RaceTypeId = "flow.race";
        public const string JoinTypeId = "flow.join";

        public static FlowNodeRegistry CreateRegistry()
        {
            var registry = new FlowNodeRegistry();
            Register(registry, "flow.entry", "Entry", new[] { Out("next") }, null, FlowNodeExecutionMode.Immediate, new EntryHandler(), effectSemantics: FlowEffectSemantics.Pure);
            Register(registry, "flow.pass", "Pass", new[] { In("in"), Out("next") }, null, FlowNodeExecutionMode.Immediate, new PassHandler(), effectSemantics: FlowEffectSemantics.Pure);
            Register(registry, "flow.complete", "Complete Flow", new[] { In("in"), Out("completed", FlowPortSemantic.Success) }, null, FlowNodeExecutionMode.Immediate, new CompleteHandler(), effectSemantics: FlowEffectSemantics.Pure, completesFlow: true);
            Register(registry, "flow.fail", "Fail Flow", new[] { In("in") }, new[]
            {
                Prop("message", FlowValueKind.String, false)
            }, FlowNodeExecutionMode.Immediate, new FailFlowHandler(), effectSemantics: FlowEffectSemantics.Pure);
            Register(registry, "flow.branch.bool", "Branch (Bool)", new[] { In("in"), Out("true", FlowPortSemantic.ConditionTrue), Out("false", FlowPortSemantic.ConditionFalse) }, new[]
            {
                Prop("value", FlowValueKind.Bool, false),
                Prop("blackboardKey", FlowValueKind.String, false)
            }, FlowNodeExecutionMode.Immediate, new BranchHandler(), effectSemantics: FlowEffectSemantics.Pure);
            Register(registry, "flow.branch.condition", "Branch (Condition)", new[] { In("in"), Out("true", FlowPortSemantic.ConditionTrue), Out("false", FlowPortSemantic.ConditionFalse) }, null,
                FlowNodeExecutionMode.Immediate, new ConditionBranchHandler(), effectSemantics: FlowEffectSemantics.Pure, requiresCondition: true);
            Register(registry, "flow.delay", "Delay", new[] { In("in"), Out("completed") }, new[]
            {
                Prop("seconds", FlowValueKind.Double, true),
                Prop("timeDomain", FlowValueKind.String, false, "Scaled", "Unscaled", "FlowTime")
            }, FlowNodeExecutionMode.Completion, new DelayHandler());
            Register(registry, "flow.wait.signal", "Wait Signal", new[] { In("in"), Out("received") }, new[]
            {
                Prop("signal", FlowValueKind.String, true),
                Prop("scope", FlowValueKind.String, false, "RunLocal", "Host"),
                Prop("sourceKey", FlowValueKind.String, false)
            }, FlowNodeExecutionMode.Completion, new WaitSignalHandler());
            Register(registry, "flow.wait.state", "Wait State", new[] { In("in"), Out("changed") }, new[]
            {
                Prop("state", FlowValueKind.String, true),
                Prop("sourceKey", FlowValueKind.String, false),
                Prop("expected", FlowValueKind.Any, false),
                Prop("waitMode", FlowValueKind.String, false, "CurrentOrFuture", "FutureChange", "FutureMatch")
            }, FlowNodeExecutionMode.Completion, new WaitStateHandler());
            Register(registry, "flow.stable.for", "Stable For", new[] { In("in"), Out("stable") }, new[]
            {
                Prop("state", FlowValueKind.String, true),
                Prop("seconds", FlowValueKind.Double, true),
                Prop("timeDomain", FlowValueKind.String, false, "Scaled", "Unscaled", "FlowTime"),
                Prop("sourceKey", FlowValueKind.String, false),
                Prop("expected", FlowValueKind.Any, false)
            }, FlowNodeExecutionMode.Completion, new StableForHandler());
            Register(registry, "flow.wait.blackboard", "Wait Blackboard", new[] { In("in"), Out("changed") }, new[]
            {
                Prop("key", FlowValueKind.String, true),
                Prop("expected", FlowValueKind.Any, false)
            }, FlowNodeExecutionMode.Completion, new WaitBlackboardHandler());
            Register(registry, "flow.set.blackboard", "Set Blackboard", new[] { In("in"), Out("next") }, new[]
            {
                Prop("key", FlowValueKind.String, true),
                Prop("value", FlowValueKind.Any, true),
                Prop("persistence", FlowValueKind.String, false, "Transient", "Persistent", "Reconstructable")
            }, FlowNodeExecutionMode.Immediate, new SetBlackboardHandler(), effectSemantics: FlowEffectSemantics.Idempotent);
            Register(registry, "flow.increment.blackboard", "Increment Blackboard", new[] { In("in"), Out("next") }, new[]
            {
                Prop("key", FlowValueKind.String, true),
                Prop("amount", FlowValueKind.Any, false)
            }, FlowNodeExecutionMode.Immediate, new IncrementBlackboardHandler());
            Register(registry, "flow.emit.signal", "Emit Signal", new[] { In("in"), Out("next") }, new[]
            {
                Prop("signal", FlowValueKind.String, true),
                Prop("scope", FlowValueKind.String, false, "RunLocal", "Host"),
                Prop("payload", FlowValueKind.Any, false)
            }, FlowNodeExecutionMode.Immediate, new EmitSignalHandler());
            Register(registry, "flow.operation", "Operation", new[]
            {
                In("in"),
                Out("succeeded", FlowPortSemantic.Success),
                Out("failed", FlowPortSemantic.Failure, true),
                Out("cancelled", FlowPortSemantic.Cancelled, true)
            }, new[]
            {
                Prop("operation", FlowValueKind.String, true)
            }, FlowNodeExecutionMode.Operation, new OperationHandler(), allowAdditionalProperties: true);
            Register(registry, ParallelTypeId, "Parallel", new[] { In("in"), Out("branch") }, null, FlowNodeExecutionMode.Immediate, new ForkHandler());
            Register(registry, RaceTypeId, "Race", new[] { In("in"), Out("branch") }, null, FlowNodeExecutionMode.Immediate, new ForkHandler());
            Register(registry, JoinTypeId, "Join", new[] { In("in"), Out("joined") }, null, FlowNodeExecutionMode.Completion, new JoinHandler());
            return registry;
        }

        private static void Register(
            FlowNodeRegistry registry,
            string typeId,
            string displayName,
            FlowPortDescriptor[] ports,
            FlowPropertyDescriptor[] properties,
            FlowNodeExecutionMode mode,
            IFlowNodeHandler handler,
            FlowEffectSemantics effectSemantics = FlowEffectSemantics.None,
            bool completesFlow = false,
            bool allowAdditionalProperties = false,
            bool requiresCondition = false)
        {
            registry.Register(new FlowNodeDescriptor(
                typeId,
                1,
                displayName,
                "FlowKit",
                mode,
                ports,
                properties,
                effectSemantics: effectSemantics,
                completesFlow: completesFlow,
                allowAdditionalProperties: allowAdditionalProperties,
                requiresCondition: requiresCondition), handler);
        }

        private static FlowPortDescriptor In(string id) => new FlowPortDescriptor(id, id, FlowPortDirection.Input);
        private static FlowPortDescriptor Out(string id, FlowPortSemantic semantic = FlowPortSemantic.Normal, bool recommendedRoute = false) =>
            new FlowPortDescriptor(id, id, FlowPortDirection.Output, semantic, recommendedRoute);
        private static FlowPropertyDescriptor Prop(string key, FlowValueKind kind, bool required, params string[] allowedStringValues) =>
            new FlowPropertyDescriptor(key, key, kind, required, allowedStringValues);

        private sealed class EntryHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle) => handle.TryComplete("next");
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class PassHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle) => handle.TryComplete("next", context.InputPayload);
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class CompleteHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle) => handle.TryComplete("completed", context.InputPayload);
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class FailFlowHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                string message = TryGetString(node, "message", out string configured)
                    ? configured
                    : "Flow entered an explicit failure terminal.";
                context.Fail(handle, FlowRuntimeErrorCode.BusinessFailure, message);
            }
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class BranchHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                bool value;
                if (node.Parameters.TryGet("blackboardKey", out FlowValue keyValue) && keyValue.Kind == FlowValueKind.String &&
                    !string.IsNullOrEmpty(keyValue.StringValue))
                {
                    if (!context.Blackboard.TryGet(keyValue.StringValue, out FlowValue boardValue) || boardValue.Kind != FlowValueKind.Bool)
                    {
                        context.Fail(handle, FlowRuntimeErrorCode.MissingProperty, "Branch blackboardKey 没有 Bool 值。");
                        return;
                    }

                    value = boardValue.BoolValue;
                }
                else if (node.Parameters.TryGet("value", out FlowValue valueParameter) && valueParameter.Kind == FlowValueKind.Bool)
                {
                    value = valueParameter.BoolValue;
                }
                else
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingProperty, "Branch 需要 value 或 blackboardKey。");
                    return;
                }

                handle.TryComplete(value ? "true" : "false");
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class ConditionBranchHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (node.Condition == null)
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, "Condition branch has no compiled condition.");
                    return;
                }
                try
                {
                    bool result = FlowConditionEvaluator.Evaluate(node.Condition, context.Blackboard, context.States);
                    handle.TryComplete(result ? "true" : "false", context.InputPayload);
                }
                catch (Exception exception)
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty,
                        "Condition evaluation failed: " + exception.Message, exception);
                }
            }
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class DelayHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!node.Parameters.TryGet("seconds", out FlowValue seconds) || !seconds.TryGetNumber(out double value) || value < 0d)
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, "Delay.seconds 必须是非负数。");
                    return;
                }

                FlowTimeDomain domain = ParseTimeDomain(node);
                double due = context.Time.Get(domain) + value;
                FlowTimerScheduler timers = context.Timers;
                FlowValue inputPayload = context.InputPayload;
                FlowTimerHandle timer = timers.Schedule(domain, due, _ => handle.TryComplete("completed", inputPayload));
                handle.Track(new TimerCancellation(timers, timer));
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class WaitSignalHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "signal", out string signal))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingSignal, "WaitSignal.signal 不能为空。");
                    return;
                }

                FlowSignalScope scope = ParseScope(node);
                FlowRunId runId = scope == FlowSignalScope.RunLocal ? context.Run.RunId : default(FlowRunId);
                string sourceKey = TryGetString(node, "sourceKey", out string source) ? source : null;
                FlowSignalSubscription subscription = context.Signals.Subscribe(
                    signal,
                    scope,
                    runId,
                    sourceKey,
                    envelope => handle.TryComplete("received", envelope.Payload));
                handle.Track(subscription);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class WaitStateHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "state", out string state))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingState, "WaitState.state 不能为空。");
                    return;
                }

                string sourceKey = TryGetString(node, "sourceKey", out string source) ? source : null;
                bool hasExpected = node.Parameters.TryGet("expected", out FlowValue expected);
                FlowStateWaitMode mode = ParseWaitMode(node);
                FlowStateObservation observation = context.States.SubscribeAndSnapshot(
                    new FlowStateKey(state, sourceKey),
                    change =>
                    {
                        if (mode == FlowStateWaitMode.FutureChange ||
                            MatchesExpected(expected, hasExpected, change.Current.Value))
                            handle.TryComplete("changed", change.Current.Value);
                    });
                handle.Track(observation.Subscription);
                if (mode == FlowStateWaitMode.CurrentOrFuture && observation.Snapshot.Exists &&
                    MatchesExpected(expected, hasExpected, observation.Snapshot.Value))
                    handle.TryComplete("changed", observation.Snapshot.Value);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class WaitBlackboardHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "key", out string key))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingProperty, "WaitBlackboard.key 不能为空。");
                    return;
                }

                bool hasExpected = node.Parameters.TryGet("expected", out FlowValue expected);
                FlowBlackboardSubscription subscription = context.Blackboard.Subscribe(
                    key,
                    change =>
                    {
                        if (MatchesExpected(expected, hasExpected, change.Current.Value)) handle.TryComplete("changed", change.Current.Value);
                    });
                handle.Track(subscription);
                if (context.Blackboard.TryGet(key, out FlowValue value) && MatchesExpected(expected, hasExpected, value))
                    handle.TryComplete("changed", value);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class StableForHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "state", out string state) ||
                    !node.Parameters.TryGet("seconds", out FlowValue seconds) || !seconds.TryGetNumber(out double duration) || duration < 0d)
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, "StableFor 需要 state 和非负 seconds。");
                    return;
                }

                string sourceKey = TryGetString(node, "sourceKey", out string source) ? source : null;
                bool hasExpected = node.Parameters.TryGet("expected", out FlowValue expected);
                FlowTimerScheduler timers = context.Timers;
                FlowTimeDomain domain = ParseTimeDomain(node);
                FlowRunContext run = context.Run;
                var currentCancellation = new ReplaceableTimerCancellation(timers);
                handle.Track(currentCancellation);

                Action<FlowValue> arm = value =>
                {
                    if (!MatchesExpected(expected, hasExpected, value))
                    {
                        currentCancellation.CancelCurrent();
                        return;
                    }

                    FlowTimerHandle timer = timers.Schedule(domain, run.Time.Get(domain) + duration, _ => handle.TryComplete("stable", value));
                    currentCancellation.Replace(timer);
                };

                FlowStateObservation observation = context.States.SubscribeAndSnapshot(
                    new FlowStateKey(state, sourceKey),
                    change => arm(change.Current.Value));
                handle.Track(observation.Subscription);
                if (observation.Snapshot.Exists) arm(observation.Snapshot.Value);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class SetBlackboardHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "key", out string key) || !node.Parameters.TryGet("value", out FlowValue value))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingProperty, "SetBlackboard 需要 key 和 value。");
                    return;
                }

                context.Blackboard.Set(key, value, ParsePersistence(node));
                handle.TryComplete("next", value);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class IncrementBlackboardHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "key", out string key) || !context.Blackboard.TryGetEntry(key, out FlowBlackboardEntry entry))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingProperty, "IncrementBlackboard.key 必须指向现有数值。");
                    return;
                }

                FlowValue amount = node.Parameters.TryGet("amount", out FlowValue configuredAmount)
                    ? configuredAmount
                    : FlowValue.FromInt(1);
                if (!amount.TryGetNumber(out double numericAmount))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, "IncrementBlackboard.amount 必须是数字。");
                    return;
                }

                FlowValue output;
                try
                {
                    switch (entry.Value.Kind)
                    {
                        case FlowValueKind.Int:
                            if (!TryGetIntegralDelta(amount, out long intDelta)) throw new InvalidOperationException("Int 增量必须是整数。");
                            long intResult = checked((long)entry.Value.IntValue + intDelta);
                            if (intResult < int.MinValue || intResult > int.MaxValue) throw new OverflowException();
                            output = FlowValue.FromInt((int)intResult);
                            break;
                        case FlowValueKind.Long:
                            if (!TryGetIntegralDelta(amount, out long longDelta)) throw new InvalidOperationException("Long 增量必须是整数。");
                            output = FlowValue.FromLong(checked(entry.Value.LongValue + longDelta));
                            break;
                        case FlowValueKind.Float:
                            double floatResult = entry.Value.FloatValue + numericAmount;
                            if (double.IsNaN(floatResult) || double.IsInfinity(floatResult) ||
                                floatResult < -float.MaxValue || floatResult > float.MaxValue) throw new OverflowException();
                            output = FlowValue.FromFloat((float)floatResult);
                            break;
                        case FlowValueKind.Double:
                            double doubleResult = entry.Value.DoubleValue + numericAmount;
                            if (double.IsNaN(doubleResult) || double.IsInfinity(doubleResult)) throw new OverflowException();
                            output = FlowValue.FromDouble(doubleResult);
                            break;
                        default:
                            context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, "IncrementBlackboard.key 必须指向现有数值。");
                            return;
                    }
                }
                catch (Exception exception) when (exception is OverflowException || exception is InvalidOperationException)
                {
                    context.Fail(handle, FlowRuntimeErrorCode.InvalidProperty, $"IncrementBlackboard 数值无效: {exception.Message}");
                    return;
                }

                context.Blackboard.Set(key, output, entry.Persistence);
                handle.TryComplete("next", output);
            }

            private static bool TryGetIntegralDelta(FlowValue value, out long result)
            {
                switch (value.Kind)
                {
                    case FlowValueKind.Int: result = value.IntValue; return true;
                    case FlowValueKind.Long: result = value.LongValue; return true;
                    case FlowValueKind.Float:
                    case FlowValueKind.Double:
                        if (!value.TryGetNumber(out double number) || double.IsNaN(number) || double.IsInfinity(number) ||
                            number < long.MinValue || number > long.MaxValue || Math.Truncate(number) != number)
                        {
                            result = 0;
                            return false;
                        }
                        result = (long)number;
                        return true;
                    default:
                        result = 0;
                        return false;
                }
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class EmitSignalHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "signal", out string signal))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingSignal, "EmitSignal.signal 不能为空。");
                    return;
                }

                FlowValue payload = node.Parameters.TryGet("payload", out FlowValue value) ? value : context.InputPayload;
                FlowSignalScope scope = ParseScope(node);
                context.Signals.Publish(signal, scope, scope == FlowSignalScope.RunLocal ? context.Run.RunId : default(FlowRunId), payload);
                handle.TryComplete("next", payload);
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class OperationHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle)
            {
                if (!TryGetString(node, "operation", out string operation))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingOperationAdapter, "Operation.operation 不能为空。");
                    return;
                }

                var operationContext = new FlowOperationContext(
                    context.Run.RunId,
                    context.Run.PersistentRunId,
                    handle.ExecutionId,
                    context.Bindings,
                    context.Run.Capabilities,
                    context.Cancellation,
                    context.RuntimeEpoch.Value,
                    new FlowIdempotencyKey(
                        context.Run.PersistentRunId,
                        handle.NodeId,
                        0,
                        context.Identity.Lineage,
                        "operation"),
                    new FlowOwnerToken(context.Identity));
                var request = new FlowOperationRequest(operation, context.InputPayload, node.Parameters);
                if (!context.Operations.Start(
                    operation,
                    in operationContext,
                    in request,
                    result =>
                    {
                        if (result.Status == FlowOperationStatus.Succeeded)
                            handle.TryComplete("succeeded", result.Payload);
                        else if (result.Status == FlowOperationStatus.Cancelled)
                            handle.TryComplete("cancelled", FlowValue.FromString(result.Error));
                        else
                            handle.TryComplete("failed", FlowValue.FromString(result.Error));
                    },
                    out FlowOperationHandle operationHandle))
                {
                    context.Fail(handle, FlowRuntimeErrorCode.MissingOperationAdapter, $"未注册 Operation: {operation}");
                    return;
                }

                handle.Track(new OperationCancellation(context.Operations, operationHandle));
            }

            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class ForkHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle) => handle.TryComplete("branch", context.InputPayload);
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class JoinHandler : IFlowNodeHandler
        {
            public void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle) => context.Run.RegisterJoin(handle);
            public void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node) { }
        }

        private sealed class TimerCancellation : IDisposable
        {
            private FlowTimerScheduler _scheduler;
            private readonly FlowTimerHandle _handle;
            internal TimerCancellation(FlowTimerScheduler scheduler, FlowTimerHandle handle) { _scheduler = scheduler; _handle = handle; }
            public void Dispose() { if (_scheduler != null) { _scheduler.Cancel(_handle); _scheduler = null; } }
        }

        private sealed class ReplaceableTimerCancellation : IDisposable
        {
            private FlowTimerScheduler _scheduler;
            private FlowTimerHandle _handle;
            private bool _hasHandle;

            internal ReplaceableTimerCancellation(FlowTimerScheduler scheduler)
            {
                _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            }

            internal void Replace(FlowTimerHandle handle)
            {
                CancelCurrent();
                if (_scheduler == null)
                {
                    return;
                }

                _handle = handle;
                _hasHandle = true;
            }

            internal void CancelCurrent()
            {
                if (_scheduler != null && _hasHandle) _scheduler.Cancel(_handle);
                _hasHandle = false;
            }

            public void Dispose()
            {
                if (_scheduler == null) return;
                CancelCurrent();
                _scheduler = null;
            }
        }

        private sealed class OperationCancellation : IDisposable
        {
            private FlowOperationRegistry _registry;
            private readonly FlowOperationHandle _handle;
            internal OperationCancellation(FlowOperationRegistry registry, FlowOperationHandle handle) { _registry = registry; _handle = handle; }
            public void Dispose() { if (_registry != null) { _registry.Cancel(_handle); _registry = null; } }
        }

        private static bool TryGetString(FlowCompiledNode node, string key, out string value)
        {
            if (node.Parameters.TryGet(key, out FlowValue parameter) && parameter.Kind == FlowValueKind.String &&
                !string.IsNullOrEmpty(parameter.StringValue))
            {
                value = parameter.StringValue;
                return true;
            }

            value = null;
            return false;
        }

        private static bool MatchesExpected(FlowValue expected, bool hasExpected, FlowValue value)
        {
            return !hasExpected || expected.Kind == FlowValueKind.Any || expected.Kind == FlowValueKind.None || expected.Equals(value);
        }

        private static FlowTimeDomain ParseTimeDomain(FlowCompiledNode node)
        {
            if (!TryGetString(node, "timeDomain", out string value)) return FlowTimeDomain.Scaled;
            if (string.Equals(value, "Unscaled", StringComparison.OrdinalIgnoreCase)) return FlowTimeDomain.Unscaled;
            if (string.Equals(value, "FlowTime", StringComparison.OrdinalIgnoreCase)) return FlowTimeDomain.FlowTime;
            return FlowTimeDomain.Scaled;
        }

        private static FlowSignalScope ParseScope(FlowCompiledNode node)
        {
            return TryGetString(node, "scope", out string value) && string.Equals(value, "Host", StringComparison.OrdinalIgnoreCase)
                ? FlowSignalScope.Host
                : FlowSignalScope.RunLocal;
        }

        private static FlowBlackboardPersistence ParsePersistence(FlowCompiledNode node)
        {
            if (!TryGetString(node, "persistence", out string value)) return FlowBlackboardPersistence.Transient;
            if (string.Equals(value, "Persistent", StringComparison.OrdinalIgnoreCase)) return FlowBlackboardPersistence.Persistent;
            if (string.Equals(value, "Reconstructable", StringComparison.OrdinalIgnoreCase)) return FlowBlackboardPersistence.Reconstructable;
            return FlowBlackboardPersistence.Transient;
        }

        private static FlowStateWaitMode ParseWaitMode(FlowCompiledNode node)
        {
            if (!TryGetString(node, "waitMode", out string value)) return FlowStateWaitMode.CurrentOrFuture;
            if (string.Equals(value, "FutureChange", StringComparison.OrdinalIgnoreCase)) return FlowStateWaitMode.FutureChange;
            if (string.Equals(value, "FutureMatch", StringComparison.OrdinalIgnoreCase)) return FlowStateWaitMode.FutureMatch;
            return FlowStateWaitMode.CurrentOrFuture;
        }
    }
}
