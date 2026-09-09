using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public readonly struct FlowSignalEnvelope
    {
        public FlowSignalId SignalId { get; }
        public FlowSignalScope Scope { get; }
        public FlowRunId RunId { get; }
        public FlowBindingHandle SourceBinding { get; }
        public string SourceKey { get; }
        public FlowValue Payload { get; }
        public long Sequence { get; }
        public double EnqueuedUnscaledSeconds { get; }

        public FlowSignalEnvelope(
            FlowSignalId signalId,
            FlowSignalScope scope,
            FlowRunId runId,
            FlowBindingHandle sourceBinding,
            string sourceKey,
            FlowValue payload,
            long sequence,
            double enqueuedUnscaledSeconds = 0d)
        {
            SignalId = signalId;
            Scope = scope;
            RunId = runId;
            SourceBinding = sourceBinding;
            SourceKey = sourceKey;
            Payload = payload;
            Sequence = sequence;
            EnqueuedUnscaledSeconds = enqueuedUnscaledSeconds;
        }
    }

    public sealed class FlowSignalRouter
    {
        private sealed class Subscriber
        {
            internal int Id;
            internal FlowSignalScope Scope;
            internal FlowRunId RunId;
            internal string SourceKey;
            internal Action<FlowSignalEnvelope> Callback;
            internal bool Active;
        }

        private readonly Dictionary<FlowSignalId, List<Subscriber>> _buckets =
            new Dictionary<FlowSignalId, List<Subscriber>>();
        private readonly Queue<FlowSignalEnvelope> _ingress = new Queue<FlowSignalEnvelope>();
        private long _sequence;
        private int _nextSubscriberId;
        private double _currentUnscaledSeconds;
        private double _oldestEnqueuedSeconds;
        private bool _hasOldestEnqueued;

        public int PendingNotificationCount => _ingress.Count;
        public long LastPublishedSequence => _sequence;
        public double OldestNotificationAge => !_hasOldestEnqueued
            ? 0d
            : Math.Max(0d, _currentUnscaledSeconds - _oldestEnqueuedSeconds);

        internal void ObserveTime(in FlowTimeSnapshot time)
        {
            _currentUnscaledSeconds = time.UnscaledSeconds;
        }

        public FlowSignalSubscription Subscribe(
            FlowSignalId signalId,
            FlowSignalScope scope,
            FlowRunId runId,
            Action<FlowSignalEnvelope> callback) =>
            Subscribe(signalId, scope, runId, null, callback);

        /// <summary>可选的稳定 SourceKey 过滤；空值表示接收该 SignalId 下的所有来源。</summary>
        public FlowSignalSubscription Subscribe(
            FlowSignalId signalId,
            FlowSignalScope scope,
            FlowRunId runId,
            string sourceKey,
            Action<FlowSignalEnvelope> callback)
        {
            if (!signalId.IsValid) throw new ArgumentException("SignalId 不能为空。", nameof(signalId));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (scope == FlowSignalScope.RunLocal && !runId.IsValid)
                throw new ArgumentException("RunLocal Signal 必须提供有效 RunId。", nameof(runId));

            if (!_buckets.TryGetValue(signalId, out List<Subscriber> bucket))
            {
                bucket = new List<Subscriber>();
                _buckets.Add(signalId, bucket);
            }

            var subscriber = new Subscriber
            {
                Id = ++_nextSubscriberId,
                Scope = scope,
                RunId = runId,
                SourceKey = sourceKey ?? string.Empty,
                Callback = callback,
                Active = true
            };
            bucket.Add(subscriber);
            return new FlowSignalSubscription(this, signalId, subscriber.Id);
        }

        public long Publish(
            FlowSignalId signalId,
            FlowSignalScope scope,
            FlowRunId runId = default(FlowRunId),
            FlowValue payload = default(FlowValue),
            FlowBindingHandle sourceBinding = default(FlowBindingHandle),
            string sourceKey = null)
        {
            if (!signalId.IsValid) throw new ArgumentException("SignalId 不能为空。", nameof(signalId));
            if (scope == FlowSignalScope.RunLocal && !runId.IsValid)
                throw new ArgumentException("RunLocal Signal 必须提供有效 RunId。", nameof(runId));
            long sequence = ++_sequence;
            if (!_hasOldestEnqueued)
            {
                _oldestEnqueuedSeconds = _currentUnscaledSeconds;
                _hasOldestEnqueued = true;
            }

            _ingress.Enqueue(new FlowSignalEnvelope(
                signalId,
                scope,
                runId,
                sourceBinding,
                sourceKey,
                payload,
                sequence,
                _currentUnscaledSeconds));
            return sequence;
        }

        /// <summary>按预算从队列派发；回调中再次 Publish 的通知不会递归派发。</summary>
        public int Drain(int maxNotifications)
        {
            if (maxNotifications <= 0) return 0;
            int dispatched = 0;
            while (dispatched < maxNotifications && _ingress.Count > 0)
            {
                FlowSignalEnvelope envelope = _ingress.Dequeue();
                // The dequeued item is no longer part of the backlog. If it was
                // the last item, a callback that publishes a new signal below
                // must start a fresh age measurement for that new backlog.
                if (_ingress.Count == 0) _hasOldestEnqueued = false;
                if (_buckets.TryGetValue(envelope.SignalId, out List<Subscriber> bucket))
                {
                    int subscriberCount = bucket.Count;
                    for (int i = 0; i < subscriberCount; i++)
                    {
                        Subscriber subscriber = bucket[i];
                        if (!subscriber.Active || !Matches(subscriber, envelope)) continue;
                        subscriber.Callback(envelope);
                    }

                    CompactBucket(envelope.SignalId, bucket);
                }

                dispatched++;
            }

            if (_ingress.Count == 0) _hasOldestEnqueued = false;

            return dispatched;
        }

        internal void Unsubscribe(FlowSignalId signalId, int subscriberId)
        {
            if (!_buckets.TryGetValue(signalId, out List<Subscriber> bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id == subscriberId)
                {
                    bucket[i].Active = false;
                    break;
                }
            }
        }

        private static bool Matches(Subscriber subscriber, FlowSignalEnvelope envelope)
        {
            if (subscriber.Scope != envelope.Scope) return false;
            if (subscriber.Scope == FlowSignalScope.RunLocal && !subscriber.RunId.Equals(envelope.RunId)) return false;
            return string.IsNullOrEmpty(subscriber.SourceKey) ||
                string.Equals(subscriber.SourceKey, envelope.SourceKey ?? string.Empty, StringComparison.Ordinal);
        }

        private void CompactBucket(FlowSignalId signalId, List<Subscriber> bucket)
        {
            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                if (!bucket[i].Active) bucket.RemoveAt(i);
            }

            if (bucket.Count == 0) _buckets.Remove(signalId);
        }
    }

    public sealed class FlowSignalSubscription : IDisposable
    {
        private FlowSignalRouter _router;
        private readonly FlowSignalId _signalId;
        private readonly int _subscriberId;

        internal FlowSignalSubscription(FlowSignalRouter router, FlowSignalId signalId, int subscriberId)
        {
            _router = router;
            _signalId = signalId;
            _subscriberId = subscriberId;
        }

        public bool IsDisposed => _router == null;

        public void Dispose()
        {
            if (_router == null) return;
            _router.Unsubscribe(_signalId, _subscriberId);
            _router = null;
        }
    }

    public readonly struct FlowStateKey : IEquatable<FlowStateKey>
    {
        public FlowStateId StateId { get; }
        public string SourceKey { get; }

        public FlowStateKey(FlowStateId stateId, string sourceKey = null)
        {
            StateId = stateId;
            SourceKey = sourceKey ?? string.Empty;
        }

        public bool Equals(FlowStateKey other) => StateId.Equals(other.StateId) &&
            string.Equals(SourceKey, other.SourceKey, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FlowStateKey other && Equals(other);
        public override int GetHashCode() => unchecked((StateId.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(SourceKey));
        public override string ToString() => string.IsNullOrEmpty(SourceKey) ? StateId.Value : StateId.Value + "@" + SourceKey;
    }

    public readonly struct FlowStateSnapshot
    {
        public bool Exists { get; }
        public FlowValue Value { get; }
        public long Revision { get; }
        public FlowStateLifetime Lifetime { get; }
        public FlowStateKey Key { get; }

        public FlowStateSnapshot(bool exists, FlowValue value, long revision, FlowStateLifetime lifetime, FlowStateKey key)
        {
            Exists = exists;
            Value = value;
            Revision = revision;
            Lifetime = lifetime;
            Key = key;
        }
    }

    public readonly struct FlowStateChange
    {
        public FlowStateSnapshot Previous { get; }
        public FlowStateSnapshot Current { get; }

        public FlowStateChange(FlowStateSnapshot previous, FlowStateSnapshot current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public sealed class FlowStateStore
    {
        private sealed class Entry
        {
            internal FlowValue Value;
            internal long Revision;
            internal FlowStateLifetime Lifetime;
        }

        private sealed class Subscriber
        {
            internal int Id;
            internal Action<FlowStateChange> Callback;
            internal bool Active;
        }

        private readonly Dictionary<FlowStateKey, Entry> _entries = new Dictionary<FlowStateKey, Entry>();
        private readonly Dictionary<FlowStateKey, List<Subscriber>> _buckets =
            new Dictionary<FlowStateKey, List<Subscriber>>();
        private int _nextSubscriberId;

        public int StateCount => _entries.Count;

        public bool TryGet(FlowStateKey key, out FlowStateSnapshot snapshot)
        {
            if (_entries.TryGetValue(key, out Entry entry))
            {
                snapshot = new FlowStateSnapshot(true, entry.Value, entry.Revision, entry.Lifetime, key);
                return true;
            }

            snapshot = new FlowStateSnapshot(false, FlowValue.None, 0, FlowStateLifetime.Run, key);
            return false;
        }

        public bool Set(FlowStateKey key, FlowValue value, FlowStateLifetime lifetime = FlowStateLifetime.Run)
        {
            if (!key.StateId.IsValid) throw new ArgumentException("StateId 不能为空。", nameof(key));
            FlowStateSnapshot previous = TryGet(key, out FlowStateSnapshot oldSnapshot)
                ? oldSnapshot
                : new FlowStateSnapshot(false, FlowValue.None, 0, lifetime, key);
            if (previous.Exists && previous.Value.Equals(value) && previous.Lifetime == lifetime) return false;

            var entry = new Entry
            {
                Value = value,
                Revision = previous.Revision + 1,
                Lifetime = lifetime
            };
            _entries[key] = entry;
            var current = new FlowStateSnapshot(true, value, entry.Revision, lifetime, key);
            if (_buckets.TryGetValue(key, out List<Subscriber> bucket))
            {
                var change = new FlowStateChange(previous, current);
                int subscriberCount = bucket.Count;
                for (int i = 0; i < subscriberCount; i++)
                {
                    if (bucket[i].Active) bucket[i].Callback(change);
                }

                CompactBucket(key, bucket);
            }

            return true;
        }

        public FlowPersistentStateEntry[] CapturePersistentEntries()
        {
            var result = new List<FlowPersistentStateEntry>();
            foreach (KeyValuePair<FlowStateKey, Entry> pair in _entries)
            {
                if (pair.Value.Lifetime != FlowStateLifetime.Persistent) continue;
                result.Add(new FlowPersistentStateEntry(
                    pair.Key.StateId.Value,
                    pair.Key.SourceKey,
                    pair.Value.Value,
                    pair.Value.Revision,
                    pair.Value.Lifetime));
            }

            result.Sort((left, right) =>
            {
                int state = string.CompareOrdinal(left.StateId, right.StateId);
                return state != 0 ? state : string.CompareOrdinal(left.SourceKey, right.SourceKey);
            });
            return result.ToArray();
        }

        public void RestorePersistent(FlowPersistentStateEntry entry)
        {
            if (string.IsNullOrEmpty(entry.StateId)) throw new ArgumentException("Persistent StateId 不能为空。", nameof(entry));
            if (entry.Lifetime != FlowStateLifetime.Persistent)
                throw new ArgumentException("只能恢复 Persistent State。", nameof(entry));
            if (entry.Revision < 0) throw new ArgumentOutOfRangeException(nameof(entry), "State Revision 不能为负数。");

            _entries[new FlowStateKey(entry.StateId, entry.SourceKey)] = new Entry
            {
                Value = entry.Value,
                Revision = entry.Revision,
                Lifetime = entry.Lifetime
            };
        }

        public FlowStateObservation SubscribeAndSnapshot(FlowStateKey key, Action<FlowStateChange> callback)
        {
            if (!key.StateId.IsValid) throw new ArgumentException("StateId 不能为空。", nameof(key));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!_buckets.TryGetValue(key, out List<Subscriber> bucket))
            {
                bucket = new List<Subscriber>();
                _buckets.Add(key, bucket);
            }

            var subscriber = new Subscriber
            {
                Id = ++_nextSubscriberId,
                Callback = callback,
                Active = true
            };
            bucket.Add(subscriber);
            TryGet(key, out FlowStateSnapshot snapshot);
            return new FlowStateObservation(snapshot, new FlowStateSubscription(this, key, subscriber.Id));
        }

        public void ClearLifetime(FlowStateLifetime lifetime)
        {
            var removed = new List<FlowStateKey>();
            foreach (KeyValuePair<FlowStateKey, Entry> pair in _entries)
            {
                if (pair.Value.Lifetime == lifetime) removed.Add(pair.Key);
            }

            for (int i = 0; i < removed.Count; i++) _entries.Remove(removed[i]);
        }

        internal void Unsubscribe(FlowStateKey key, int subscriberId)
        {
            if (!_buckets.TryGetValue(key, out List<Subscriber> bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id == subscriberId)
                {
                    bucket[i].Active = false;
                    break;
                }
            }
        }

        private void CompactBucket(FlowStateKey key, List<Subscriber> bucket)
        {
            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                if (!bucket[i].Active) bucket.RemoveAt(i);
            }

            if (bucket.Count == 0) _buckets.Remove(key);
        }
    }

    public readonly struct FlowStateObservation
    {
        public FlowStateSnapshot Snapshot { get; }
        public FlowStateSubscription Subscription { get; }

        public FlowStateObservation(FlowStateSnapshot snapshot, FlowStateSubscription subscription)
        {
            Snapshot = snapshot;
            Subscription = subscription;
        }
    }

    public sealed class FlowStateSubscription : IDisposable
    {
        private FlowStateStore _store;
        private readonly FlowStateKey _key;
        private readonly int _subscriberId;

        internal FlowStateSubscription(FlowStateStore store, FlowStateKey key, int subscriberId)
        {
            _store = store;
            _key = key;
            _subscriberId = subscriberId;
        }

        public void Dispose()
        {
            if (_store == null) return;
            _store.Unsubscribe(_key, _subscriberId);
            _store = null;
        }
    }

    public enum FlowBlackboardPersistence
    {
        Transient,
        Persistent,
        Reconstructable
    }

    [Serializable]
    public struct FlowBlackboardEntry
    {
        public string Key;
        public FlowValue Value;
        public FlowBlackboardPersistence Persistence;

        public FlowBlackboardEntry(string key, FlowValue value, FlowBlackboardPersistence persistence)
        {
            Key = key;
            Value = value;
            Persistence = persistence;
        }
    }

    public readonly struct FlowBlackboardChange
    {
        public FlowBlackboardEntry Previous { get; }
        public FlowBlackboardEntry Current { get; }
        public long Revision { get; }

        public FlowBlackboardChange(FlowBlackboardEntry previous, FlowBlackboardEntry current, long revision)
        {
            Previous = previous;
            Current = current;
            Revision = revision;
        }
    }

    public sealed class FlowBlackboard
    {
        private sealed class Subscriber
        {
            internal int Id;
            internal Action<FlowBlackboardChange> Callback;
            internal bool Active;
        }

        private readonly Dictionary<string, FlowBlackboardEntry> _entries =
            new Dictionary<string, FlowBlackboardEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Subscriber>> _subscribers =
            new Dictionary<string, List<Subscriber>>(StringComparer.Ordinal);
        private int _nextSubscriberId;
        private long _revision;

        public int Count => _entries.Count;
        public long Revision => _revision;

        public bool TryGet(string key, out FlowValue value)
        {
            if (!string.IsNullOrEmpty(key) && _entries.TryGetValue(key, out FlowBlackboardEntry entry))
            {
                value = entry.Value;
                return true;
            }

            value = FlowValue.None;
            return false;
        }

        public bool TryGetEntry(string key, out FlowBlackboardEntry entry)
        {
            if (!string.IsNullOrEmpty(key) && _entries.TryGetValue(key, out entry)) return true;
            entry = default(FlowBlackboardEntry);
            return false;
        }

        public void Set(string key, FlowValue value, FlowBlackboardPersistence persistence = FlowBlackboardPersistence.Transient)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Blackboard key 不能为空。", nameof(key));
            var current = new FlowBlackboardEntry(key, value, persistence);
            Apply(current, true);
        }

        public FlowBlackboardSubscription Subscribe(string key, Action<FlowBlackboardChange> callback)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Blackboard key 不能为空。", nameof(key));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!_subscribers.TryGetValue(key, out List<Subscriber> bucket))
            {
                bucket = new List<Subscriber>();
                _subscribers.Add(key, bucket);
            }

            var subscriber = new Subscriber { Id = ++_nextSubscriberId, Callback = callback, Active = true };
            bucket.Add(subscriber);
            return new FlowBlackboardSubscription(this, key, subscriber.Id);
        }

        public FlowBlackboardBatch BeginBatch() => new FlowBlackboardBatch(this);

        public FlowBlackboardEntry[] CapturePersistentEntries()
        {
            var result = new List<FlowBlackboardEntry>();
            foreach (KeyValuePair<string, FlowBlackboardEntry> pair in _entries)
            {
                if (pair.Value.Persistence == FlowBlackboardPersistence.Persistent) result.Add(pair.Value);
            }

            result.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            return result.ToArray();
        }

        internal void CommitBatch(List<FlowBlackboardEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;
            var changes = new List<FlowBlackboardChange>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                FlowBlackboardEntry current = entries[i];
                if (string.IsNullOrEmpty(current.Key)) throw new ArgumentException("Blackboard key 不能为空。", nameof(entries));
                if (_entries.TryGetValue(current.Key, out FlowBlackboardEntry previous) &&
                    previous.Value.Equals(current.Value) && previous.Persistence == current.Persistence)
                {
                    continue;
                }

                _entries[current.Key] = current;
                changes.Add(new FlowBlackboardChange(
                    previous,
                    current,
                    0));
            }

            if (changes.Count == 0) return;
            _revision++;
            for (int i = 0; i < changes.Count; i++)
            {
                FlowBlackboardChange original = changes[i];
                Notify(original.Current.Key, new FlowBlackboardChange(original.Previous, original.Current, _revision));
            }
        }

        internal void Unsubscribe(string key, int subscriberId)
        {
            if (!_subscribers.TryGetValue(key, out List<Subscriber> bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id == subscriberId)
                {
                    bucket[i].Active = false;
                    break;
                }
            }
        }

        private void Apply(FlowBlackboardEntry current, bool notify)
        {
            FlowBlackboardEntry previous = default(FlowBlackboardEntry);
            if (_entries.TryGetValue(current.Key, out FlowBlackboardEntry existing))
            {
                previous = existing;
                if (existing.Value.Equals(current.Value) && existing.Persistence == current.Persistence) return;
            }

            _entries[current.Key] = current;
            _revision++;
            if (notify) Notify(current.Key, new FlowBlackboardChange(previous, current, _revision));
        }

        private void Notify(string key, FlowBlackboardChange change)
        {
            if (!_subscribers.TryGetValue(key, out List<Subscriber> bucket)) return;
            int subscriberCount = bucket.Count;
            for (int i = 0; i < subscriberCount; i++)
            {
                if (bucket[i].Active) bucket[i].Callback(change);
            }

            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                if (!bucket[i].Active) bucket.RemoveAt(i);
            }

            if (bucket.Count == 0) _subscribers.Remove(key);
        }
    }

    public sealed class FlowBlackboardBatch : IDisposable
    {
        private readonly FlowBlackboard _blackboard;
        private readonly List<FlowBlackboardEntry> _entries = new List<FlowBlackboardEntry>();
        private bool _committed;

        internal FlowBlackboardBatch(FlowBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Set(string key, FlowValue value, FlowBlackboardPersistence persistence = FlowBlackboardPersistence.Transient)
        {
            if (_committed) throw new InvalidOperationException("已提交的 BlackboardBatch 不能继续写入。");
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Key, key, StringComparison.Ordinal))
                {
                    _entries[i] = new FlowBlackboardEntry(key, value, persistence);
                    return;
                }
            }

            _entries.Add(new FlowBlackboardEntry(key, value, persistence));
        }

        public void Commit()
        {
            if (_committed) throw new InvalidOperationException("BlackboardBatch 不能重复提交。");
            _committed = true;
            _blackboard.CommitBatch(_entries);
        }

        public void Dispose()
        {
            if (!_committed) _entries.Clear();
        }
    }

    public sealed class FlowBlackboardSubscription : IDisposable
    {
        private FlowBlackboard _blackboard;
        private readonly string _key;
        private readonly int _subscriberId;

        internal FlowBlackboardSubscription(FlowBlackboard blackboard, string key, int subscriberId)
        {
            _blackboard = blackboard;
            _key = key;
            _subscriberId = subscriberId;
        }

        public void Dispose()
        {
            if (_blackboard == null) return;
            _blackboard.Unsubscribe(_key, _subscriberId);
            _blackboard = null;
        }
    }
}
