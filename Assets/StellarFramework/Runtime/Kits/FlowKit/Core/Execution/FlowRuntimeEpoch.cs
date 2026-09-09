using System;
using System.Threading;

namespace StellarFramework.FlowKit
{
    /// <summary>
    /// 宿主生命周期代次。Domain Reload、重建 Host 或显式重置运行时后递增，旧 Run/回调会被拒绝。
    /// </summary>
    public sealed class FlowRuntimeEpoch
    {
        private int _value = 1;

        public int Value => Volatile.Read(ref _value);

        public int Advance()
        {
            int next = Interlocked.Increment(ref _value);
            if (next <= 0)
            {
                Interlocked.Exchange(ref _value, 1);
                return 1;
            }

            return next;
        }

        public bool IsCurrent(int epoch) => epoch > 0 && epoch == Value;
    }
}
