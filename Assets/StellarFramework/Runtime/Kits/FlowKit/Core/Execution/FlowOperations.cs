using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public enum FlowOperationStatus
    {
        Succeeded,
        Failed,
        Cancelled
    }

    /// <summary>可选的外部 Operation 观察面；具体异步实现仍通过 IFlowOperationAdapter 接入。</summary>
    public interface IFlowOperation
    {
        FlowOperationStatus Status { get; }
        void Cancel();
    }

    public readonly struct FlowOperationHandle : IEquatable<FlowOperationHandle>
    {
        public int Slot { get; }
        public int Generation { get; }
        public bool IsValid => Slot >= 0 && Generation > 0;

        public FlowOperationHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(FlowOperationHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FlowOperationHandle other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397) ^ Generation);
        public override string ToString() => $"{Slot}:{Generation}";
    }

    public readonly struct FlowOperationResult
    {
        public FlowOperationStatus Status { get; }
        public FlowValue Payload { get; }
        public string Error { get; }

        public FlowOperationResult(FlowOperationStatus status, FlowValue payload, string error = null)
        {
            Status = status;
            Payload = payload;
            Error = error ?? string.Empty;
        }

        public static FlowOperationResult Success(FlowValue payload = default(FlowValue)) =>
            new FlowOperationResult(FlowOperationStatus.Succeeded, payload);
        public static FlowOperationResult Failure(string error) =>
            new FlowOperationResult(FlowOperationStatus.Failed, FlowValue.None, error);
        public static FlowOperationResult Cancelled(string error = null) =>
            new FlowOperationResult(FlowOperationStatus.Cancelled, FlowValue.None, error);
    }

    /// <summary>Compiled operation-node input delivered to an external adapter.</summary>
    public readonly struct FlowOperationRequest
    {
        public string OperationId { get; }
        public FlowValue InputPayload { get; }
        public FlowPropertyBagSnapshot Arguments { get; }

        public FlowOperationRequest(
            string operationId,
            FlowValue inputPayload,
            FlowPropertyBagSnapshot arguments)
        {
            if (string.IsNullOrEmpty(operationId))
                throw new ArgumentException("OperationId cannot be empty.", nameof(operationId));
            OperationId = operationId;
            InputPayload = inputPayload;
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        public bool TryGetArgument(string key, out FlowValue value) => Arguments.TryGet(key, out value);
    }

    public readonly struct FlowOperationContext
    {
        public FlowRunId RunId { get; }
        public string PersistentRunId { get; }
        public FlowExecutionId ExecutionId { get; }
        public FlowBindingRegistry Bindings { get; }
        public FlowCapabilitySet Capabilities { get; }
        public FlowCancellationToken Cancellation { get; }
        public int RuntimeEpoch { get; }
        public FlowOwnerToken OwnerToken { get; }
        public FlowIdempotencyKey IdempotencyKey { get; }

        public FlowOperationContext(
            FlowRunId runId,
            FlowExecutionId executionId,
            FlowBindingRegistry bindings,
            FlowCapabilitySet capabilities,
            FlowCancellationToken cancellation = null)
            : this(runId, string.Empty, executionId, bindings, capabilities, cancellation)
        {
        }

        public FlowOperationContext(
            FlowRunId runId,
            string persistentRunId,
            FlowExecutionId executionId,
            FlowBindingRegistry bindings,
            FlowCapabilitySet capabilities,
            FlowCancellationToken cancellation = null,
            int runtimeEpoch = 0,
            FlowIdempotencyKey idempotencyKey = default(FlowIdempotencyKey),
            FlowOwnerToken ownerToken = default(FlowOwnerToken))
        {
            RunId = runId;
            PersistentRunId = persistentRunId ?? string.Empty;
            ExecutionId = executionId;
            Bindings = bindings;
            Capabilities = capabilities;
            Cancellation = cancellation;
            RuntimeEpoch = runtimeEpoch;
            OwnerToken = ownerToken.IsValid
                ? ownerToken
                : new FlowOwnerToken(runId, executionId, 1, runtimeEpoch);
            IdempotencyKey = idempotencyKey;
        }
    }

    /// <summary>
    /// 业务/平台异步操作的显式适配边界。Core 不引用具体 SDK。
    /// 完成回调必须在宿主调度线程执行；后台线程结果由适配器先投递回宿主线程。
    /// </summary>
    public interface IFlowOperationAdapter
    {
        void Start(
            in FlowOperationContext context,
            in FlowOperationRequest request,
            FlowOperationHandle handle,
            Action<FlowOperationResult> complete);
        void Cancel(in FlowOperationContext context, FlowOperationHandle handle);
    }

    public sealed class FlowOperationRegistry
    {
        private sealed class ActiveOperation
        {
            internal FlowOperationHandle Handle;
            internal IFlowOperationAdapter Adapter;
            internal FlowOperationContext Context;
            internal Action<FlowOperationResult> Complete;
            internal bool Active;
        }

        private readonly Dictionary<string, IFlowOperationAdapter> _adapters =
            new Dictionary<string, IFlowOperationAdapter>(StringComparer.Ordinal);
        private readonly Dictionary<int, ActiveOperation> _active = new Dictionary<int, ActiveOperation>();
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private int _nextSlot;

        public int AdapterCount => _adapters.Count;
        public int ActiveCount => _active.Count;

        public FlowOperationRegistry Register(string operationId, IFlowOperationAdapter adapter)
        {
            if (string.IsNullOrEmpty(operationId)) throw new ArgumentException("OperationId 不能为空。", nameof(operationId));
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (!_adapters.TryAdd(operationId, adapter))
                throw new InvalidOperationException($"OperationId 已注册: {operationId}");
            return this;
        }

        public bool Contains(string operationId) => !string.IsNullOrEmpty(operationId) && _adapters.ContainsKey(operationId);

        public bool Start(
            string operationId,
            in FlowOperationContext context,
            in FlowOperationRequest request,
            Action<FlowOperationResult> complete,
            out FlowOperationHandle handle)
        {
            if (string.IsNullOrEmpty(operationId) || complete == null || !_adapters.TryGetValue(operationId, out IFlowOperationAdapter adapter))
            {
                handle = default(FlowOperationHandle);
                return false;
            }

            int slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : _nextSlot++;
            int generation = _generations.TryGetValue(slot, out int previous) ? previous + 1 : 1;
            _generations[slot] = generation;
            handle = new FlowOperationHandle(slot, generation);
            var active = new ActiveOperation
            {
                Handle = handle,
                Adapter = adapter,
                Context = context,
                Complete = complete,
                Active = true
            };
            _active.Add(slot, active);
            try
            {
                FlowOperationHandle registeredHandle = handle;
                adapter.Start(context, request, registeredHandle, result => Complete(registeredHandle, result));
            }
            catch (Exception exception)
            {
                Complete(handle, FlowOperationResult.Failure(exception.Message));
            }

            return true;
        }

        public bool Complete(FlowOperationHandle handle, FlowOperationResult result)
        {
            if (!TryTake(handle, out ActiveOperation active)) return false;
            active.Complete(result);
            return true;
        }

        public bool Cancel(FlowOperationHandle handle)
        {
            if (!TryTake(handle, out ActiveOperation active)) return false;
            active.Active = false;
            try
            {
                active.Adapter.Cancel(active.Context, handle);
            }
            finally
            {
                active.Complete(FlowOperationResult.Cancelled());
            }
            return true;
        }

        private bool TryTake(FlowOperationHandle handle, out ActiveOperation active)
        {
            if (!handle.IsValid || !_active.TryGetValue(handle.Slot, out active) ||
                !active.Active || active.Handle.Generation != handle.Generation)
            {
                active = null;
                return false;
            }

            active.Active = false;
            _active.Remove(handle.Slot);
            _freeSlots.Push(handle.Slot);
            return true;
        }
    }

    public sealed class FlowCapabilitySet
    {
        private readonly HashSet<FlowCapabilityId> _values = new HashSet<FlowCapabilityId>();

        public IReadOnlyCollection<FlowCapabilityId> Values => _values;

        public FlowCapabilitySet Add(FlowCapabilityId capability)
        {
            if (capability.IsValid) _values.Add(capability);
            return this;
        }

        public FlowCapabilitySet AddRange(IReadOnlyList<FlowCapabilityId> capabilities)
        {
            if (capabilities == null) return this;
            for (int i = 0; i < capabilities.Count; i++) Add(capabilities[i]);
            return this;
        }

        public bool Contains(FlowCapabilityId capability) => capability.IsValid && _values.Contains(capability);
        public bool ContainsAll(IReadOnlyList<FlowCapabilityId> capabilities)
        {
            if (capabilities == null) return true;
            for (int i = 0; i < capabilities.Count; i++)
            {
                if (!Contains(capabilities[i])) return false;
            }

            return true;
        }
    }

    /// <summary>稳定 BindingId 到运行对象的显式表。不会使用 Find 或 Unity 实例 ID。</summary>
    public sealed class FlowBindingRegistry
    {
        private sealed class Entry
        {
            internal FlowBindingId Id;
            internal object Value;
            internal int Generation;
            internal bool Active;
        }

        private readonly Dictionary<FlowBindingId, int> _slots = new Dictionary<FlowBindingId, int>();
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Stack<int> _freeSlots = new Stack<int>();

        public int Count { get; private set; }

        public FlowBindingHandle Bind(FlowBindingId id, object value)
        {
            if (!id.IsValid) throw new ArgumentException("BindingId 不能为空。", nameof(id));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (_slots.TryGetValue(id, out int existingSlot))
            {
                Entry existing = _entries[existingSlot];
                if (existing.Active) throw new InvalidOperationException($"BindingId 已绑定: {id}");
            }

            int slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : _entries.Count;
            Entry entry;
            if (slot == _entries.Count)
            {
                entry = new Entry { Id = id, Generation = 1 };
                _entries.Add(entry);
            }
            else
            {
                entry = _entries[slot];
                entry.Id = id;
                entry.Generation++;
            }

            entry.Value = value;
            entry.Active = true;
            _slots[id] = slot;
            Count++;
            return new FlowBindingHandle(slot, entry.Generation);
        }

        public bool Unbind(FlowBindingId id)
        {
            return TryGetHandle(id, out FlowBindingHandle handle) && Unbind(handle);
        }

        public bool Unbind(FlowBindingHandle handle)
        {
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _entries.Count) return false;
            Entry entry = _entries[handle.Slot];
            if (!entry.Active || entry.Generation != handle.Generation) return false;
            entry.Active = false;
            entry.Value = null;
            _slots.Remove(entry.Id);
            _freeSlots.Push(handle.Slot);
            Count--;
            return true;
        }

        public bool TryGetHandle(FlowBindingId id, out FlowBindingHandle handle)
        {
            if (id.IsValid && _slots.TryGetValue(id, out int slot))
            {
                Entry entry = _entries[slot];
                if (entry.Active)
                {
                    handle = new FlowBindingHandle(slot, entry.Generation);
                    return true;
                }
            }

            handle = default(FlowBindingHandle);
            return false;
        }

        public bool TryResolve(FlowBindingId id, out object value)
        {
            if (TryGetHandle(id, out FlowBindingHandle handle))
            {
                return TryResolve(handle, out value);
            }

            value = null;
            return false;
        }

        public bool TryResolve(FlowBindingReference reference, out object value)
        {
            if (reference.IsValid) return TryResolve(reference.ToBindingId(), out value);
            value = null;
            return false;
        }

        public bool TryResolve(FlowBindingHandle handle, out object value)
        {
            if (handle.IsValid && handle.Slot < _entries.Count)
            {
                Entry entry = _entries[handle.Slot];
                if (entry.Active && entry.Generation == handle.Generation)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }

    public enum FlowTraceEventKind
    {
        RunStarted,
        NodeActivated,
        NodeCompleted,
        NodeFailed,
        RunCompleted,
        RunFailed,
        RunCancelled,
        SignalPublished,
        TimerScheduled,
        TimerFired,
        OperationStarted,
        OperationCompleted
    }

    public readonly struct FlowTraceEvent
    {
        public FlowTraceEventKind Kind { get; }
        public FlowRunId RunId { get; }
        public FlowExecutionIdentity Identity { get; }
        public string NodeId { get; }
        public string Message { get; }
        public long Sequence { get; }

        public FlowTraceEvent(
            FlowTraceEventKind kind,
            FlowRunId runId,
            FlowExecutionIdentity identity,
            string nodeId,
            string message,
            long sequence)
        {
            Kind = kind;
            RunId = runId;
            Identity = identity;
            NodeId = nodeId ?? string.Empty;
            Message = message ?? string.Empty;
            Sequence = sequence;
        }
    }

    public interface IFlowTraceSink
    {
        void Write(in FlowTraceEvent traceEvent);
    }

    public sealed class FlowTraceRingBuffer : IFlowTraceSink
    {
        private readonly FlowTraceEvent[] _buffer;
        private int _next;
        private int _count;

        public FlowTraceRingBuffer(int capacity = 256)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new FlowTraceEvent[capacity];
        }

        public int Count => _count;
        public void Write(in FlowTraceEvent traceEvent)
        {
            _buffer[_next] = traceEvent;
            _next = (_next + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public FlowTraceEvent[] Snapshot()
        {
            var result = new FlowTraceEvent[_count];
            int start = (_next - _count + _buffer.Length) % _buffer.Length;
            for (int i = 0; i < _count; i++) result[i] = _buffer[(start + i) % _buffer.Length];
            return result;
        }
    }
}
