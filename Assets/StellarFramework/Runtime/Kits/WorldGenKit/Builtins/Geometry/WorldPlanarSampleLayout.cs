using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    /// <summary>
    /// Dense row-major planar sample layout. WorldGenerationRunKey.X/Y are interpreted as the
    /// absolute logical origin of this tile; this keeps adjacent tiles on one global sample field.
    /// </summary>
    public readonly struct WorldPlanarSampleLayout : IEquatable<WorldPlanarSampleLayout>
    {
        public int Width { get; }
        public int Height { get; }
        public long SampleStep { get; }
        public int Count { get; }

        public WorldPlanarSampleLayout(int width, int height, long sampleStep = 1L)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (sampleStep <= 0L) throw new ArgumentOutOfRangeException(nameof(sampleStep));

            long count = (long)width * height;
            if (count > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(height), "Sample count cannot exceed Int32.MaxValue.");

            Width = width;
            Height = height;
            SampleStep = sampleStep;
            Count = (int)count;
        }

        public int GetIndex(int x, int y)
        {
            if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            return (y * Width) + x;
        }

        public long GetAbsoluteX(in WorldGenerationRunKey runKey, int localX)
        {
            if ((uint)localX >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(localX));
            return checked(runKey.X + checked((long)localX * SampleStep));
        }

        public long GetAbsoluteY(in WorldGenerationRunKey runKey, int localY)
        {
            if ((uint)localY >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(localY));
            return checked(runKey.Y + checked((long)localY * SampleStep));
        }

        public bool Equals(WorldPlanarSampleLayout other) =>
            Width == other.Width && Height == other.Height && SampleStep == other.SampleStep;

        public override bool Equals(object obj) => obj is WorldPlanarSampleLayout other && Equals(other);
        public override int GetHashCode() => unchecked(((Width * 397) ^ Height) * 397 ^ SampleStep.GetHashCode());
        public static bool operator ==(WorldPlanarSampleLayout left, WorldPlanarSampleLayout right) => left.Equals(right);
        public static bool operator !=(WorldPlanarSampleLayout left, WorldPlanarSampleLayout right) => !left.Equals(right);
    }
}
