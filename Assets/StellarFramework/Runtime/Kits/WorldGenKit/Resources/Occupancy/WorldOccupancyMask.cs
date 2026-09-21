using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldOccupancyMask : IEquatable<WorldOccupancyMask>
    {
        internal ulong Bits { get; }
        public bool IsEmpty => Bits == 0UL;
        public static WorldOccupancyMask None => default(WorldOccupancyMask);

        internal WorldOccupancyMask(ulong bits) => Bits = bits;

        public bool Overlaps(WorldOccupancyMask other) => (Bits & other.Bits) != 0UL;
        public bool Contains(WorldOccupancyHandle handle) => (Bits & handle.Bit) != 0UL;
        public WorldOccupancyMask With(WorldOccupancyHandle handle) => new WorldOccupancyMask(Bits | handle.Bit);
        public WorldOccupancyMask Union(WorldOccupancyMask other) => new WorldOccupancyMask(Bits | other.Bits);

        public bool Equals(WorldOccupancyMask other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is WorldOccupancyMask other && Equals(other);
        public override int GetHashCode() => Bits.GetHashCode();
        public override string ToString() => "0x" + Bits.ToString("X16");
        public static bool operator ==(WorldOccupancyMask left, WorldOccupancyMask right) => left.Equals(right);
        public static bool operator !=(WorldOccupancyMask left, WorldOccupancyMask right) => !left.Equals(right);
    }
}
