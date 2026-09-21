using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldGenerationSeed : IEquatable<WorldGenerationSeed>
    {
        public ulong Value { get; }

        public WorldGenerationSeed(ulong value) => Value = value;

        public WorldGenerationSeed Derive(
            long x,
            long y,
            WorldGenerationStageId stageId,
            ulong localKey = 0UL)
        {
            if (!stageId.IsValid) throw new ArgumentException("Stage ID must be valid.", nameof(stageId));
            return new WorldGenerationSeed(WorldGenStableHash64.Derive(Value, x, y, stageId.Value, localKey));
        }

        public bool Equals(WorldGenerationSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldGenerationSeed other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16");
        public static bool operator ==(WorldGenerationSeed left, WorldGenerationSeed right) => left.Equals(right);
        public static bool operator !=(WorldGenerationSeed left, WorldGenerationSeed right) => !left.Equals(right);
    }

    internal static class WorldGenStableHash64
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong Derive(ulong seed, long x, long y, string stableId, ulong localKey)
        {
            ulong hash = OffsetBasis;
            AppendUInt64(ref hash, seed);
            AppendUInt64(ref hash, unchecked((ulong)x));
            AppendUInt64(ref hash, unchecked((ulong)y));
            AppendString(ref hash, stableId);
            AppendUInt64(ref hash, localKey);
            return Avalanche(hash);
        }

        internal static ulong HashString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            ulong hash = OffsetBasis;
            AppendString(ref hash, value);
            return Avalanche(hash);
        }

        internal static ulong Combine(ulong current, ulong value)
        {
            ulong hash = OffsetBasis;
            AppendUInt64(ref hash, current);
            AppendUInt64(ref hash, value);
            return Avalanche(hash);
        }

        internal static ulong DeriveCompiledNoise(
            ulong seed,
            long x,
            long y,
            ulong stableIdHash,
            ulong localKey)
        {
            ulong hash = MixFast(seed ^ stableIdHash);
            hash = MixFast(hash ^ unchecked((ulong)x));
            hash = MixFast(hash ^ unchecked((ulong)y));
            return MixFast(hash ^ localKey);
        }

        private static void AppendString(ref ulong hash, string value)
        {
            AppendUInt64(ref hash, (ulong)value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                ushort c = value[i];
                AppendByte(ref hash, (byte)c);
                AppendByte(ref hash, (byte)(c >> 8));
            }
        }

        private static void AppendUInt64(ref ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
                AppendByte(ref hash, (byte)(value >> shift));
        }

        private static void AppendByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= Prime;
        }

        private static ulong Avalanche(ulong value)
        {
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            value *= 0xc4ceb9fe1a85ec53UL;
            value ^= value >> 33;
            return value;
        }

        private static ulong MixFast(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
