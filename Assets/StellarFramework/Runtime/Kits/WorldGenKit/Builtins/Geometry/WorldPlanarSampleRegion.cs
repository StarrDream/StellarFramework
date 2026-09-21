using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public readonly struct WorldPlanarSampleRegion : IEquatable<WorldPlanarSampleRegion>
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int RightExclusive => checked(X + Width);
        public int TopExclusive => checked(Y + Height);

        public WorldPlanarSampleRegion(int x, int y, int width, int height)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            _ = checked(x + width);
            _ = checked(y + height);
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool FitsWithin(in WorldPlanarSampleLayout layout) =>
            RightExclusive <= layout.Width && TopExclusive <= layout.Height;

        public static WorldPlanarSampleRegion Whole(in WorldPlanarSampleLayout layout) =>
            new WorldPlanarSampleRegion(0, 0, layout.Width, layout.Height);

        public bool Equals(WorldPlanarSampleRegion other) =>
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is WorldPlanarSampleRegion other && Equals(other);
        public override int GetHashCode() => unchecked((((X * 397) ^ Y) * 397 ^ Width) * 397 ^ Height);
        public static bool operator ==(WorldPlanarSampleRegion left, WorldPlanarSampleRegion right) => left.Equals(right);
        public static bool operator !=(WorldPlanarSampleRegion left, WorldPlanarSampleRegion right) => !left.Equals(right);
    }
}
