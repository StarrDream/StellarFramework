using System;

namespace StellarFramework.WorldGenKit
{
    /// <summary>
    /// Typed holder for a project/import adapter source. WorldGen Core does not prescribe source query semantics.
    /// </summary>
    public sealed class ExternalChannelStorage<TSource, T> : IWorldChannelStorage<T>
        where TSource : class
    {
        public WorldChannelStorageKind Kind => WorldChannelStorageKind.External;
        public TSource Source { get; }

        public ExternalChannelStorage(TSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}
