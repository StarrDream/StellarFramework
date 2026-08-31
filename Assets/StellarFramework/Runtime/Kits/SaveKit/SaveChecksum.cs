using System;

namespace StellarFramework
{
    public interface ISaveChecksum
    {
        ulong Compute(byte[] data, int offset, int count);
    }

    /// <summary>无外部依赖的 xxHash64 实现，用于损坏检测，不提供安全签名或防作弊能力。</summary>
    public sealed class XxHash64Checksum : ISaveChecksum
    {
        private const ulong Prime1 = 11400714785074694791UL;
        private const ulong Prime2 = 14029467366897019727UL;
        private const ulong Prime3 = 1609587929392839161UL;
        private const ulong Prime4 = 9650029242287828579UL;
        private const ulong Prime5 = 2870177450012600261UL;

        public ulong Compute(byte[] data, int offset, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset > data.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));

            int index = offset;
            int end = offset + count;
            ulong hash;
            if (count >= 32)
            {
                int limit = end - 32;
                ulong v1 = unchecked(Prime1 + Prime2);
                ulong v2 = Prime2;
                ulong v3 = 0;
                ulong v4 = unchecked(0UL - Prime1);
                while (index <= limit)
                {
                    v1 = Round(v1, Read64(data, index)); index += 8;
                    v2 = Round(v2, Read64(data, index)); index += 8;
                    v3 = Round(v3, Read64(data, index)); index += 8;
                    v4 = Round(v4, Read64(data, index)); index += 8;
                }

                hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
                hash = MergeRound(hash, v1);
                hash = MergeRound(hash, v2);
                hash = MergeRound(hash, v3);
                hash = MergeRound(hash, v4);
            }
            else
            {
                hash = Prime5;
            }

            hash += (ulong)count;
            while (index <= end - 8)
            {
                ulong lane = Round(0, Read64(data, index));
                hash ^= lane;
                hash = RotateLeft(hash, 27) * Prime1 + Prime4;
                index += 8;
            }

            if (index <= end - 4)
            {
                hash ^= (ulong)Read32(data, index) * Prime1;
                hash = RotateLeft(hash, 23) * Prime2 + Prime3;
                index += 4;
            }

            while (index < end)
            {
                hash ^= data[index] * Prime5;
                hash = RotateLeft(hash, 11) * Prime1;
                index++;
            }

            hash ^= hash >> 33;
            hash *= Prime2;
            hash ^= hash >> 29;
            hash *= Prime3;
            hash ^= hash >> 32;
            return hash;
        }

        private static ulong Round(ulong acc, ulong input)
        {
            acc += input * Prime2;
            acc = RotateLeft(acc, 31);
            return acc * Prime1;
        }

        private static ulong MergeRound(ulong acc, ulong value)
        {
            value = Round(0, value);
            acc ^= value;
            return acc * Prime1 + Prime4;
        }

        private static ulong Read64(byte[] data, int offset)
        {
            return (ulong)data[offset] |
                ((ulong)data[offset + 1] << 8) |
                ((ulong)data[offset + 2] << 16) |
                ((ulong)data[offset + 3] << 24) |
                ((ulong)data[offset + 4] << 32) |
                ((ulong)data[offset + 5] << 40) |
                ((ulong)data[offset + 6] << 48) |
                ((ulong)data[offset + 7] << 56);
        }

        private static uint Read32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    }
}
