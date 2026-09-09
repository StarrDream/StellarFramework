using System;

namespace StellarFramework.FlowKit
{
    /// <summary>运行时不依赖 CLR 类型名的稳定节点类型 ID。</summary>
    public readonly struct FlowNodeTypeId : IEquatable<FlowNodeTypeId>
    {
        public string Value { get; }

        public FlowNodeTypeId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(FlowNodeTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowNodeTypeId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowNodeTypeId(string value) => new FlowNodeTypeId(value);
    }

    /// <summary>流程内瞬时通知的稳定 ID。</summary>
    public readonly struct FlowSignalId : IEquatable<FlowSignalId>
    {
        public string Value { get; }

        public FlowSignalId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(FlowSignalId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowSignalId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowSignalId(string value) => new FlowSignalId(value);
    }

    /// <summary>外部世界状态的稳定 ID。</summary>
    public readonly struct FlowStateId : IEquatable<FlowStateId>
    {
        public string Value { get; }

        public FlowStateId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(FlowStateId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowStateId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowStateId(string value) => new FlowStateId(value);
    }

    /// <summary>场景静态绑定的稳定 ID。</summary>
    public readonly struct FlowBindingId : IEquatable<FlowBindingId>
    {
        public string Value { get; }

        public FlowBindingId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(FlowBindingId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowBindingId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowBindingId(string value) => new FlowBindingId(value);
    }

    /// <summary>资源引用的稳定 ID，具体加载方式由 AssetResolver 决定。</summary>
    public readonly struct FlowAssetId : IEquatable<FlowAssetId>
    {
        public string Value { get; }

        public FlowAssetId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(FlowAssetId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowAssetId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowAssetId(string value) => new FlowAssetId(value);
    }

    /// <summary>可写入 Graph JSON 的资源引用。运行时再转换为 FlowAssetId 交给 AssetResolver。</summary>
    [Serializable]
    public struct FlowAssetReference : IEquatable<FlowAssetReference>
    {
        public string Id;

        public FlowAssetReference(string id)
        {
            Id = id ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);
        public FlowAssetId ToAssetId() => new FlowAssetId(Id);
        public bool Equals(FlowAssetReference other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowAssetReference other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
        public static implicit operator FlowAssetReference(string value) => new FlowAssetReference(value);
    }

    /// <summary>不依赖 CLR 类型名的枚举/离散值引用，Id 由项目契约稳定定义。</summary>
    [Serializable]
    public struct FlowEnumReference : IEquatable<FlowEnumReference>
    {
        public string Id;

        public FlowEnumReference(string id)
        {
            Id = id ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);
        public bool Equals(FlowEnumReference other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowEnumReference other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
        public static implicit operator FlowEnumReference(string value) => new FlowEnumReference(value);
    }

    /// <summary>能力声明的稳定 ID。</summary>
    public readonly struct FlowCapabilityId : IEquatable<FlowCapabilityId>
    {
        public string Value { get; }

        public FlowCapabilityId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(FlowCapabilityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowCapabilityId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator FlowCapabilityId(string value) => new FlowCapabilityId(value);
    }

    /// <summary>运行实例 ID。ID 只用于运行态，不写入 Graph JSON。</summary>
    public readonly struct FlowRunId : IEquatable<FlowRunId>
    {
        private static long s_nextValue;

        public long Value { get; }
        public bool IsValid => Value > 0;

        public FlowRunId(long value)
        {
            Value = value;
        }

        public static FlowRunId Create() => new FlowRunId(System.Threading.Interlocked.Increment(ref s_nextValue));
        public bool Equals(FlowRunId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FlowRunId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    /// <summary>单次节点激活的运行态 ID。</summary>
    public readonly struct FlowExecutionId : IEquatable<FlowExecutionId>
    {
        public long Value { get; }
        public bool IsValid => Value > 0;

        public FlowExecutionId(long value)
        {
            Value = value;
        }

        public bool Equals(FlowExecutionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FlowExecutionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    /// <summary>编译子计划或 Composite 的执行帧 ID。</summary>
    public readonly struct FlowFrameId : IEquatable<FlowFrameId>
    {
        public long Value { get; }
        public bool IsValid => Value > 0;

        public FlowFrameId(long value)
        {
            Value = value;
        }

        public bool Equals(FlowFrameId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FlowFrameId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    /// <summary>运行态作用域 ID，用于取消和资源所有权。</summary>
    public readonly struct FlowScopeId : IEquatable<FlowScopeId>
    {
        public long Value { get; }
        public bool IsValid => Value > 0;

        public FlowScopeId(long value)
        {
            Value = value;
        }

        public bool Equals(FlowScopeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FlowScopeId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    /// <summary>RuntimeBinding 的 Slot + Generation，防止 Slot 复用误命中旧对象。</summary>
    public readonly struct FlowBindingHandle : IEquatable<FlowBindingHandle>
    {
        public int Slot { get; }
        public int Generation { get; }
        public bool IsValid => Slot >= 0 && Generation > 0;

        public FlowBindingHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(FlowBindingHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FlowBindingHandle other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397) ^ Generation);
        public override string ToString() => $"{Slot}:{Generation}";
    }

    public enum FlowValueKind
    {
        None,
        Any,
        Bool,
        Int,
        Long,
        Float,
        Double,
        String,
        Vector2,
        Vector3,
        Binding,
        Asset,
        Enum
    }

    public struct FlowVector2 : IEquatable<FlowVector2>
    {
        public float X;
        public float Y;

        public FlowVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(FlowVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is FlowVector2 other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
    }

    public struct FlowVector3 : IEquatable<FlowVector3>
    {
        public float X;
        public float Y;
        public float Z;

        public FlowVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(FlowVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is FlowVector3 other && Equals(other);
        public override int GetHashCode() => unchecked(((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());
    }

    /// <summary>
    /// 配置和低频完成结果使用的无装箱值容器。高频运行路径不使用 Dictionary&lt;string, object&gt;。
    /// </summary>
    public struct FlowValue : IEquatable<FlowValue>
    {
        public FlowValueKind Kind;
        public bool BoolValue;
        public int IntValue;
        public long LongValue;
        public float FloatValue;
        public double DoubleValue;
        public string StringValue;
        public FlowVector2 Vector2Value;
        public FlowVector3 Vector3Value;
        public FlowBindingHandle BindingValue;
        public FlowAssetReference AssetValue;
        public FlowEnumReference EnumValue;

        public static FlowValue None => default(FlowValue);
        public static FlowValue FromBool(bool value) => new FlowValue { Kind = FlowValueKind.Bool, BoolValue = value };
        public static FlowValue FromInt(int value) => new FlowValue { Kind = FlowValueKind.Int, IntValue = value };
        public static FlowValue FromLong(long value) => new FlowValue { Kind = FlowValueKind.Long, LongValue = value };
        public static FlowValue FromFloat(float value) => new FlowValue { Kind = FlowValueKind.Float, FloatValue = value };
        public static FlowValue FromDouble(double value) => new FlowValue { Kind = FlowValueKind.Double, DoubleValue = value };
        public static FlowValue FromString(string value) => new FlowValue { Kind = FlowValueKind.String, StringValue = value ?? string.Empty };
        public static FlowValue FromVector2(FlowVector2 value) => new FlowValue { Kind = FlowValueKind.Vector2, Vector2Value = value };
        public static FlowValue FromVector3(FlowVector3 value) => new FlowValue { Kind = FlowValueKind.Vector3, Vector3Value = value };
        public static FlowValue FromBinding(FlowBindingHandle value) => new FlowValue { Kind = FlowValueKind.Binding, BindingValue = value };
        public static FlowValue FromAsset(FlowAssetId value) => new FlowValue { Kind = FlowValueKind.Asset, AssetValue = new FlowAssetReference(value.Value) };
        public static FlowValue FromAsset(string value) => new FlowValue { Kind = FlowValueKind.Asset, AssetValue = new FlowAssetReference(value) };
        public static FlowValue FromEnum(string value) => new FlowValue { Kind = FlowValueKind.Enum, EnumValue = new FlowEnumReference(value) };

        public bool TryGetAsset(out FlowAssetId value)
        {
            if (Kind == FlowValueKind.Asset && AssetValue.IsValid)
            {
                value = AssetValue.ToAssetId();
                return true;
            }

            value = new FlowAssetId(string.Empty);
            return false;
        }

        public bool TryGetEnum(out FlowEnumReference value)
        {
            if (Kind == FlowValueKind.Enum && EnumValue.IsValid)
            {
                value = EnumValue;
                return true;
            }

            value = new FlowEnumReference(string.Empty);
            return false;
        }

        public bool TryGetNumber(out double value)
        {
            switch (Kind)
            {
                case FlowValueKind.Int:
                    value = IntValue;
                    return true;
                case FlowValueKind.Long:
                    value = LongValue;
                    return true;
                case FlowValueKind.Float:
                    value = FloatValue;
                    return true;
                case FlowValueKind.Double:
                    value = DoubleValue;
                    return true;
                default:
                    value = default(double);
                    return false;
            }
        }

        public bool Equals(FlowValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }

            switch (Kind)
            {
                case FlowValueKind.None:
                case FlowValueKind.Any:
                    return true;
                case FlowValueKind.Bool:
                    return BoolValue == other.BoolValue;
                case FlowValueKind.Int:
                    return IntValue == other.IntValue;
                case FlowValueKind.Long:
                    return LongValue == other.LongValue;
                case FlowValueKind.Float:
                    return FloatValue.Equals(other.FloatValue);
                case FlowValueKind.Double:
                    return DoubleValue.Equals(other.DoubleValue);
                case FlowValueKind.String:
                    return string.Equals(StringValue, other.StringValue, StringComparison.Ordinal);
                case FlowValueKind.Vector2:
                    return Vector2Value.Equals(other.Vector2Value);
                case FlowValueKind.Vector3:
                    return Vector3Value.Equals(other.Vector3Value);
                case FlowValueKind.Binding:
                    return BindingValue.Equals(other.BindingValue);
                case FlowValueKind.Asset:
                    return AssetValue.Equals(other.AssetValue);
                case FlowValueKind.Enum:
                    return EnumValue.Equals(other.EnumValue);
                default:
                    return false;
            }
        }

        public override bool Equals(object obj) => obj is FlowValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                switch (Kind)
                {
                    case FlowValueKind.Bool: hash = hash * 397 ^ BoolValue.GetHashCode(); break;
                    case FlowValueKind.Int: hash = hash * 397 ^ IntValue; break;
                    case FlowValueKind.Long: hash = hash * 397 ^ LongValue.GetHashCode(); break;
                    case FlowValueKind.Float: hash = hash * 397 ^ FloatValue.GetHashCode(); break;
                    case FlowValueKind.Double: hash = hash * 397 ^ DoubleValue.GetHashCode(); break;
                    case FlowValueKind.String: hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(StringValue ?? string.Empty); break;
                    case FlowValueKind.Vector2: hash = hash * 397 ^ Vector2Value.GetHashCode(); break;
                    case FlowValueKind.Vector3: hash = hash * 397 ^ Vector3Value.GetHashCode(); break;
                    case FlowValueKind.Binding: hash = hash * 397 ^ BindingValue.GetHashCode(); break;
                    case FlowValueKind.Asset: hash = hash * 397 ^ AssetValue.GetHashCode(); break;
                    case FlowValueKind.Enum: hash = hash * 397 ^ EnumValue.GetHashCode(); break;
                }

                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case FlowValueKind.Bool: return BoolValue.ToString();
                case FlowValueKind.Int: return IntValue.ToString();
                case FlowValueKind.Long: return LongValue.ToString();
                case FlowValueKind.Float: return FloatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Double: return DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.String: return StringValue ?? string.Empty;
                case FlowValueKind.Vector2: return $"({Vector2Value.X}, {Vector2Value.Y})";
                case FlowValueKind.Vector3: return $"({Vector3Value.X}, {Vector3Value.Y}, {Vector3Value.Z})";
                case FlowValueKind.Binding: return BindingValue.ToString();
                case FlowValueKind.Asset: return AssetValue.ToString();
                case FlowValueKind.Enum: return EnumValue.ToString();
                default: return string.Empty;
            }
        }
    }

    public enum FlowTimeDomain
    {
        Scaled,
        Unscaled,
        FlowTime
    }

    public readonly struct FlowDuration : IEquatable<FlowDuration>
    {
        public double Seconds { get; }

        public FlowDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "FlowDuration 必须是有限的非负秒数。");
            }

            Seconds = seconds;
        }

        public static FlowDuration FromSeconds(double seconds) => new FlowDuration(seconds);
        public bool Equals(FlowDuration other) => Seconds.Equals(other.Seconds);
        public override bool Equals(object obj) => obj is FlowDuration other && Equals(other);
        public override int GetHashCode() => Seconds.GetHashCode();
        public override string ToString() => Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
    }

    public struct FlowTimeSnapshot
    {
        public double ScaledSeconds;
        public double UnscaledSeconds;
        public double FlowSeconds;

        public FlowTimeSnapshot(double scaledSeconds, double unscaledSeconds, double flowSeconds)
        {
            if (double.IsNaN(scaledSeconds) || double.IsInfinity(scaledSeconds) ||
                double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) ||
                double.IsNaN(flowSeconds) || double.IsInfinity(flowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(scaledSeconds), "FlowTimeSnapshot 必须包含有限时间值。");
            }

            ScaledSeconds = scaledSeconds;
            UnscaledSeconds = unscaledSeconds;
            FlowSeconds = flowSeconds;
        }

        public double Get(FlowTimeDomain domain)
        {
            switch (domain)
            {
                case FlowTimeDomain.Scaled: return ScaledSeconds;
                case FlowTimeDomain.Unscaled: return UnscaledSeconds;
                case FlowTimeDomain.FlowTime: return FlowSeconds;
                default: throw new ArgumentOutOfRangeException(nameof(domain), domain, null);
            }
        }
    }

    public readonly struct FlowTimerHandle : IEquatable<FlowTimerHandle>
    {
        public int Slot { get; }
        public int Generation { get; }
        public bool IsValid => Slot >= 0 && Generation > 0;

        public FlowTimerHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(FlowTimerHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FlowTimerHandle other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397) ^ Generation);
        public override string ToString() => $"{Slot}:{Generation}";
    }

    public enum FlowRunStatus
    {
        Created,
        Running,
        Completed,
        Failed,
        Cancelled,
        Rejected
    }

    public enum FlowSignalScope
    {
        RunLocal,
        Host
    }

    public enum FlowStateLifetime
    {
        Run,
        Scene,
        Persistent,
        External
    }

    public enum FlowStateWaitMode
    {
        CurrentOrFuture,
        FutureChange,
        FutureMatch
    }

    public enum FlowCompletionPolicy
    {
        First,
        Any,
        All,
        NOfM
    }

    [Flags]
    public enum FlowEffectSemantics
    {
        None = 0,
        Pure = 1 << 0,
        Idempotent = 1 << 1,
        Compensatable = 1 << 2,
        ReplaySensitive = 1 << 3
    }

    /// <summary>并行分支和 Retry 使用的 token lineage。</summary>
    public readonly struct FlowTokenLineage : IEquatable<FlowTokenLineage>
    {
        public FlowRunId RunId { get; }
        public long ForkInstanceId { get; }
        public int BranchId { get; }
        public int IterationId { get; }

        public FlowTokenLineage(FlowRunId runId, long forkInstanceId, int branchId, int iterationId)
        {
            RunId = runId;
            ForkInstanceId = forkInstanceId;
            BranchId = branchId;
            IterationId = iterationId;
        }

        public bool Equals(FlowTokenLineage other) =>
            RunId.Equals(other.RunId) && ForkInstanceId == other.ForkInstanceId &&
            BranchId == other.BranchId && IterationId == other.IterationId;

        public override bool Equals(object obj) => obj is FlowTokenLineage other && Equals(other);
        public override int GetHashCode() => unchecked((((RunId.GetHashCode() * 397) ^ ForkInstanceId.GetHashCode()) * 397 ^ BranchId) * 397 ^ IterationId);
    }

    /// <summary>外部副作用的所有权标识；用于取消、审计和适配器侧资源仲裁。</summary>
    public readonly struct FlowOwnerToken : IEquatable<FlowOwnerToken>
    {
        public FlowRunId RunId { get; }
        public FlowExecutionId ExecutionId { get; }
        public int Generation { get; }
        public int RuntimeEpoch { get; }
        public FlowFrameId FrameId { get; }
        public FlowScopeId ScopeId { get; }
        public FlowTokenLineage Lineage { get; }

        public FlowOwnerToken(
            FlowRunId runId,
            FlowExecutionId executionId,
            int generation,
            int runtimeEpoch)
            : this(runId, executionId, generation, runtimeEpoch, default(FlowFrameId), default(FlowScopeId), default(FlowTokenLineage))
        {
        }

        public FlowOwnerToken(
            FlowRunId runId,
            FlowExecutionId executionId,
            int generation,
            int runtimeEpoch,
            FlowFrameId frameId,
            FlowScopeId scopeId,
            FlowTokenLineage lineage)
        {
            RunId = runId;
            ExecutionId = executionId;
            Generation = generation;
            RuntimeEpoch = runtimeEpoch;
            FrameId = frameId;
            ScopeId = scopeId;
            Lineage = lineage;
        }

        public FlowOwnerToken(FlowExecutionIdentity identity)
            : this(identity.RunId, identity.ExecutionId, identity.Generation, identity.RuntimeEpoch,
                identity.FrameId, identity.ScopeId, identity.Lineage)
        {
        }

        public bool IsValid => RunId.IsValid && ExecutionId.IsValid && Generation > 0 && RuntimeEpoch > 0;
        public bool Equals(FlowOwnerToken other) => RunId.Equals(other.RunId) &&
            ExecutionId.Equals(other.ExecutionId) && Generation == other.Generation && RuntimeEpoch == other.RuntimeEpoch &&
            FrameId.Equals(other.FrameId) && ScopeId.Equals(other.ScopeId) && Lineage.Equals(other.Lineage);
        public override bool Equals(object obj) => obj is FlowOwnerToken other && Equals(other);
        public override int GetHashCode() => unchecked(((((((((RunId.GetHashCode() * 397) ^ ExecutionId.GetHashCode()) * 397 ^ Generation) * 397) ^ RuntimeEpoch) * 397) ^ FrameId.GetHashCode()) * 397 ^ ScopeId.GetHashCode()) * 397 ^ Lineage.GetHashCode());
        public override string ToString() => RunId + "/" + ExecutionId + "/g" + Generation + "/e" + RuntimeEpoch + "/f" + FrameId + "/s" + ScopeId;
    }

    /// <summary>副作用适配器可选使用的稳定幂等键组成。</summary>
    public readonly struct FlowIdempotencyKey : IEquatable<FlowIdempotencyKey>
    {
        public string PersistentRunId { get; }
        public string SemanticNodeId { get; }
        public int Iteration { get; }
        public FlowTokenLineage Lineage { get; }
        public string EffectSlot { get; }

        public FlowIdempotencyKey(
            string persistentRunId,
            string semanticNodeId,
            int iteration,
            FlowTokenLineage lineage,
            string effectSlot)
        {
            PersistentRunId = persistentRunId ?? string.Empty;
            SemanticNodeId = semanticNodeId ?? string.Empty;
            Iteration = iteration;
            Lineage = lineage;
            EffectSlot = effectSlot ?? string.Empty;
        }

        public bool Equals(FlowIdempotencyKey other) =>
            string.Equals(PersistentRunId, other.PersistentRunId, StringComparison.Ordinal) &&
            string.Equals(SemanticNodeId, other.SemanticNodeId, StringComparison.Ordinal) &&
            Iteration == other.Iteration && Lineage.Equals(other.Lineage) &&
            string.Equals(EffectSlot, other.EffectSlot, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FlowIdempotencyKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(PersistentRunId);
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(SemanticNodeId);
                hash = hash * 397 ^ Iteration;
                hash = hash * 397 ^ Lineage.GetHashCode();
                return hash * 397 ^ StringComparer.Ordinal.GetHashCode(EffectSlot);
            }
        }
        public override string ToString() =>
            PersistentRunId + "/" + SemanticNodeId + "/" + Iteration + "/" +
            Lineage.ForkInstanceId + ":" + Lineage.BranchId + ":" + Lineage.IterationId + "/" + EffectSlot;
    }

    public readonly struct FlowExecutionIdentity : IEquatable<FlowExecutionIdentity>
    {
        public int RuntimeEpoch { get; }
        public FlowRunId RunId { get; }
        public FlowExecutionId ExecutionId { get; }
        public int Generation { get; }
        public FlowFrameId FrameId { get; }
        public FlowScopeId ScopeId { get; }
        public FlowTokenLineage Lineage { get; }

        public FlowExecutionIdentity(
            FlowRunId runId,
            FlowExecutionId executionId,
            int generation,
            FlowFrameId frameId,
            FlowScopeId scopeId,
            FlowTokenLineage lineage)
            : this(runId, executionId, generation, frameId, scopeId, lineage, 0)
        {
        }

        public FlowExecutionIdentity(
            FlowRunId runId,
            FlowExecutionId executionId,
            int generation,
            FlowFrameId frameId,
            FlowScopeId scopeId,
            FlowTokenLineage lineage,
            int runtimeEpoch)
        {
            RuntimeEpoch = runtimeEpoch;
            RunId = runId;
            ExecutionId = executionId;
            Generation = generation;
            FrameId = frameId;
            ScopeId = scopeId;
            Lineage = lineage;
        }

        public bool Equals(FlowExecutionIdentity other) =>
            RuntimeEpoch == other.RuntimeEpoch && RunId.Equals(other.RunId) &&
            ExecutionId.Equals(other.ExecutionId) && Generation == other.Generation &&
            FrameId.Equals(other.FrameId) && ScopeId.Equals(other.ScopeId) && Lineage.Equals(other.Lineage);

        public override bool Equals(object obj) => obj is FlowExecutionIdentity other && Equals(other);
        public override int GetHashCode() => unchecked(((((((((RuntimeEpoch * 397) ^ RunId.GetHashCode()) * 397) ^ ExecutionId.GetHashCode()) * 397 ^ Generation) * 397) ^ FrameId.GetHashCode()) * 397 ^ ScopeId.GetHashCode()) * 397 ^ Lineage.GetHashCode());
    }
}
