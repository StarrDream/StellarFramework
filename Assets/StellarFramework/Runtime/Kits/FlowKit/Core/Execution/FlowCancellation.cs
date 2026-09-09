using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    /// <summary>Run/Execution 的取消树。父作用域取消会传播到所有子作用域。</summary>
    public sealed class FlowCancellationToken
    {
        private readonly FlowCancellationToken _parent;
        private readonly List<FlowCancellationToken> _children = new List<FlowCancellationToken>();
        private int _parentIndex = -1;
        private bool _cancelled;
        private bool _disposed;

        public FlowCancellationToken(FlowCancellationToken parent = null)
        {
            _parent = parent;
            if (parent != null)
            {
                if (parent._disposed) throw new InvalidOperationException("不能从已释放的 CancellationToken 创建子作用域。");
                _parentIndex = parent._children.Count;
                parent._children.Add(this);
            }
        }

        public bool IsCancellationRequested => _cancelled || (_parent != null && _parent.IsCancellationRequested);

        public FlowCancellationToken CreateChild()
        {
            if (_disposed) throw new InvalidOperationException("已释放的 CancellationToken 不能创建子作用域。");
            var child = new FlowCancellationToken(this);
            if (IsCancellationRequested) child.Cancel();
            return child;
        }

        public void Cancel()
        {
            if (_cancelled) return;
            _cancelled = true;
            for (int i = 0; i < _children.Count; i++) _children[i].Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Cancel();
            _disposed = true;
            if (_parent != null && _parentIndex >= 0 && _parentIndex < _parent._children.Count)
            {
                int last = _parent._children.Count - 1;
                FlowCancellationToken moved = _parent._children[last];
                _parent._children[_parentIndex] = moved;
                moved._parentIndex = _parentIndex;
                _parent._children.RemoveAt(last);
            }

            _parentIndex = -1;
            _children.Clear();
        }

        public void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested) throw new OperationCanceledException();
        }
    }

    [Serializable]
    public sealed class FlowRetryPolicy
    {
        public int MaxAttempts = 1;
        public double InitialDelaySeconds;
        public double BackoffMultiplier = 2d;
        public bool AllowReplaySensitive;

        public void Validate(FlowEffectSemantics effectSemantics)
        {
            if (MaxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(MaxAttempts));
            if (double.IsNaN(InitialDelaySeconds) || double.IsInfinity(InitialDelaySeconds) || InitialDelaySeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(InitialDelaySeconds));
            if (double.IsNaN(BackoffMultiplier) || double.IsInfinity(BackoffMultiplier) || BackoffMultiplier < 1d)
                throw new ArgumentOutOfRangeException(nameof(BackoffMultiplier));
            if ((effectSemantics & FlowEffectSemantics.ReplaySensitive) != 0 && !AllowReplaySensitive && MaxAttempts > 1)
                throw new InvalidOperationException("ReplaySensitive effect 默认禁止 Retry，必须显式 AllowReplaySensitive。");
        }

        public double GetDelaySeconds(int failedAttempt)
        {
            if (failedAttempt < 0) throw new ArgumentOutOfRangeException(nameof(failedAttempt));
            return InitialDelaySeconds * Math.Pow(BackoffMultiplier, failedAttempt);
        }
    }
}
