using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public enum FlowValidationSeverity
    {
        Warning,
        Error
    }

    public enum FlowValidationErrorCode
    {
        None,
        MissingFlowId,
        InvalidSchemaVersion,
        MissingEntry,
        DuplicateNodeId,
        InvalidNodeId,
        UnknownTypeId,
        UnsupportedDefinitionVersion,
        MissingHandler,
        DuplicatePropertyKey,
        MissingRequiredProperty,
        InvalidPropertyType,
        InvalidCondition,
        InvalidEdgeNode,
        UnknownPort,
        InvalidPortDirection,
        DuplicateEdge,
        MissingMigrator,
        MigrationFailed,
        DefinitionDependencyCycle,
        ImmediateCycle,
        MissingCapability,
        MissingBinding,
        MissingAsset,
        InvalidResultMapping,
        PotentialInfiniteRetry,
        ReplaySensitiveRetry,
        CheckpointHasTransientDependency,
        UnroutedRecommendedOutput
    }

    public sealed class FlowValidationIssue
    {
        public FlowValidationSeverity Severity { get; }
        public FlowValidationErrorCode Code { get; }
        public string Message { get; }
        public string NodeId { get; }
        public string EdgeIndex { get; }

        public bool IsError => Severity == FlowValidationSeverity.Error;

        public FlowValidationIssue(
            FlowValidationSeverity severity,
            FlowValidationErrorCode code,
            string message,
            string nodeId = null,
            string edgeIndex = null)
        {
            Severity = severity;
            Code = code;
            Message = message ?? string.Empty;
            NodeId = nodeId;
            EdgeIndex = edgeIndex;
        }

        public override string ToString()
        {
            string location = string.IsNullOrEmpty(NodeId) ? string.Empty : $" [{NodeId}]";
            return $"{Severity} {Code}{location}: {Message}";
        }
    }

    public enum FlowRuntimeErrorCode
    {
        None,
        FlowStartRejected,
        UnknownOutputPort,
        HandlerFailure,
        MissingProperty,
        InvalidProperty,
        MissingSignal,
        MissingState,
        MissingOperationAdapter,
        OperationFailed,
        StaleCallback,
        UnroutedCompletion,
        RuntimeEpochChanged,
        ActivationBudgetExceeded,
        NotificationBudgetExceeded,
        SnapshotNotQuiescent,
        PlanHashMismatch,
        CleanupFailed,
        InvalidBinding,
        BusinessFailure
    }

    /// <summary>运行时错误必须携带定位信息，不能把异常伪装成成功。</summary>
    public sealed class FlowStructuredError
    {
        public FlowRuntimeErrorCode Code { get; }
        public string Message { get; }
        public string NodeId { get; }
        public FlowFrameId FrameId { get; }
        public FlowExecutionId ExecutionId { get; }
        public Exception SourceException { get; }

        public FlowStructuredError(
            FlowRuntimeErrorCode code,
            string message,
            string nodeId = null,
            FlowFrameId frameId = default(FlowFrameId),
            FlowExecutionId executionId = default(FlowExecutionId),
            Exception sourceException = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            NodeId = nodeId;
            FrameId = frameId;
            ExecutionId = executionId;
            SourceException = sourceException;
        }

        public override string ToString() => $"{Code}: {Message}";
    }

    public sealed class FlowCompileResult
    {
        private readonly FlowValidationIssue[] _issues;

        public FlowCompiledPlan Plan { get; }
        public IReadOnlyList<FlowValidationIssue> Issues => _issues;
        public bool Succeeded => Plan != null && !HasErrors;
        public bool HasErrors { get; }

        internal FlowCompileResult(FlowCompiledPlan plan, List<FlowValidationIssue> issues)
        {
            Plan = plan;
            _issues = issues == null ? Array.Empty<FlowValidationIssue>() : issues.ToArray();
            for (int i = 0; i < _issues.Length; i++)
            {
                if (_issues[i].IsError)
                {
                    HasErrors = true;
                    break;
                }
            }
        }
    }

    public sealed class FlowMigrationResult
    {
        private readonly FlowValidationIssue[] _issues;

        public FlowGraphData Graph { get; }
        public IReadOnlyList<FlowValidationIssue> Issues => _issues;
        public bool Succeeded { get; }

        internal FlowMigrationResult(FlowGraphData graph, List<FlowValidationIssue> issues, bool succeeded)
        {
            Graph = graph;
            _issues = issues == null ? Array.Empty<FlowValidationIssue>() : issues.ToArray();
            Succeeded = succeeded;
        }
    }
}
