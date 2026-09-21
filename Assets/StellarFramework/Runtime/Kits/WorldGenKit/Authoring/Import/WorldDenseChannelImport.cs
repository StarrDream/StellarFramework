using System;

namespace StellarFramework.WorldGenKit.Authoring
{
    public static class WorldDenseChannelImport
    {
        public static void CopyExact<T>(ReadOnlySpan<T> source, DenseChannelStorage<T> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source.Length != destination.Length)
                throw new ArgumentException(
                    "Source sample count must exactly match destination Dense storage length.",
                    nameof(source));

            source.CopyTo(destination.AsSpan());
        }
    }
}
