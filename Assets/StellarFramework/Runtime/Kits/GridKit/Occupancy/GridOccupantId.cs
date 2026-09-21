using System;

namespace StellarFramework
{
    /// <summary>整数占用者 ID：0 表示 Empty，正数为有效 Occupant。</summary>
    public readonly struct GridOccupantId : IEquatable<GridOccupantId>
    {
        /// <summary>获取底层正整数 owner id；0 表示 Empty。</summary>
        public int Value { get; }

        /// <summary>获取该值是否表示未占用。</summary>
        public bool IsEmpty => Value == 0;

        /// <summary>获取该值是否是有效占用者。</summary>
        public bool IsValid => Value > 0;

        /// <summary>获取 Empty/None 占用者值。</summary>
        public static GridOccupantId None => new GridOccupantId(0);

        /// <summary>创建一个占用者 id；0 保留为 Empty。</summary>
        public GridOccupantId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "GridOccupantId cannot be negative.");
            Value = value;
        }

        /// <inheritdoc />
        public bool Equals(GridOccupantId other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GridOccupantId && Equals((GridOccupantId)obj);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public override string ToString() => Value.ToString();

        public static bool operator ==(GridOccupantId left, GridOccupantId right) => left.Equals(right);
        public static bool operator !=(GridOccupantId left, GridOccupantId right) => !left.Equals(right);
    }
}
