using System;

namespace StellarFramework.WorldGenKit.Resources
{
    /// <summary>
    /// Minimal absolute planar generation domain owned by Resources so the resource system does not
    /// depend on WorldGenKit.Builtins just to reuse its sample layout type.
    /// </summary>
    public readonly struct WorldResourcePlanarDomain : IEquatable<WorldResourcePlanarDomain>
    {
        public int Width { get; }
        public int Height { get; }
        public long SampleStep { get; }
        public long OriginX { get; }
        public long OriginY { get; }
        public int Count { get; }

        public WorldResourcePlanarDomain(
            int width,
            int height,
            long sampleStep = 1L,
            long originX = 0L,
            long originY = 0L)
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
            OriginX = originX;
            OriginY = originY;
            Count = (int)count;
        }

        public int GetIndex(int x, int y)
        {
            if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            return (y * Width) + x;
        }

        public long GetAbsoluteX(int localX)
        {
            if ((uint)localX >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(localX));
            return checked(OriginX + checked((long)localX * SampleStep));
        }

        public long GetAbsoluteY(int localY)
        {
            if ((uint)localY >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(localY));
            return checked(OriginY + checked((long)localY * SampleStep));
        }

        public bool Equals(WorldResourcePlanarDomain other) =>
            Width == other.Width && Height == other.Height && SampleStep == other.SampleStep &&
            OriginX == other.OriginX && OriginY == other.OriginY;
        public override bool Equals(object obj) => obj is WorldResourcePlanarDomain other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ SampleStep.GetHashCode();
                hash = (hash * 397) ^ OriginX.GetHashCode();
                return (hash * 397) ^ OriginY.GetHashCode();
            }
        }
    }
}
