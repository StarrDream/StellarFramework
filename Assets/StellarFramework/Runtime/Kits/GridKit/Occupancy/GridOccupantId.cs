using System;

namespace StellarFramework
{
    /// <summary>整数占用者 ID：0 表示 Empty，正数为有效 Occupant。</summary>
    public readonly struct GridOccupantId : IEquatable<GridOccupantId>
    {
        public int Value { get; }
        public bool IsEmpty => Value == 0;
        public bool IsValid => Value > 0;
        public static GridOccupantId None => new GridOccupantId(0);

        public GridOccupantId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "GridOccupantId cannot be negative.");
            Value = value;
        }

        public bool Equals(GridOccupantId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GridOccupantId && Equals((GridOccupantId)obj);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(GridOccupantId left, GridOccupantId right) => left.Equals(right);
        public static bool operator !=(GridOccupantId left, GridOccupantId right) => !left.Equals(right);
    }
}
