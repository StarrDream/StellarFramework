using System;

namespace StellarFramework.WorldGenKit
{
    public delegate T WorldComputedValue<TContext, T>(in TContext context);

    public sealed class ComputedChannelStorage<TContext, T> : IWorldChannelStorage<T>
    {
        private readonly WorldComputedValue<TContext, T> _compute;

        public WorldChannelStorageKind Kind => WorldChannelStorageKind.Computed;

        public ComputedChannelStorage(WorldComputedValue<TContext, T> compute)
        {
            _compute = compute ?? throw new ArgumentNullException(nameof(compute));
        }

        public T Evaluate(in TContext context) => _compute(in context);
    }
}
