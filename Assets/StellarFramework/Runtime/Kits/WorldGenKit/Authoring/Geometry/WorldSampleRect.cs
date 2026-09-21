using System;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public readonly struct WorldSampleRect : IEquatable<WorldSampleRect>
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int RightExclusive => checked(X + Width);
        public int TopExclusive => checked(Y + Height);
        public int Count => checked(Width * Height);

        public WorldSampleRect(int x, int y, int width, int height)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            _ = checked(x + width);
            _ = checked(y + height);
            _ = checked(width * height);
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Contains(int x, int y) =>
            x >= X && x < RightExclusive && y >= Y && y < TopExclusive;

        public bool FitsWithin(in WorldPlanarSampleLayout layout) =>
            RightExclusive <= layout.Width && TopExclusive <= layout.Height;

        public WorldSampleRect Union(in WorldSampleRect other)
        {
            int minX = Math.Min(X, other.X);
            int minY = Math.Min(Y, other.Y);
            int maxX = Math.Max(RightExclusive, other.RightExclusive);
            int maxY = Math.Max(TopExclusive, other.TopExclusive);
            return new WorldSampleRect(minX, minY, checked(maxX - minX), checked(maxY - minY));
        }

        public WorldSampleRect ExpandClamped(int amount, in WorldPlanarSampleLayout layout)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int minX = Math.Max(0, X - amount);
            int minY = Math.Max(0, Y - amount);
            int maxX = Math.Min(layout.Width, checked(RightExclusive + amount));
            int maxY = Math.Min(layout.Height, checked(TopExclusive + amount));
            return new WorldSampleRect(minX, minY, maxX - minX, maxY - minY);
        }

        public bool Equals(WorldSampleRect other) =>
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is WorldSampleRect other && Equals(other);
        public override int GetHashCode() => unchecked((((X * 397) ^ Y) * 397 ^ Width) * 397 ^ Height);
        public override string ToString() => $"({X},{Y}) {Width}x{Height}";
        public static bool operator ==(WorldSampleRect left, WorldSampleRect right) => left.Equals(right);
        public static bool operator !=(WorldSampleRect left, WorldSampleRect right) => !left.Equals(right);
    }
}
