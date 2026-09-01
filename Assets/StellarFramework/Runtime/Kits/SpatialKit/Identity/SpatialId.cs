using System;

namespace StellarFramework
{
    /// <summary>
    /// 由业务侧分配的空间索引身份标识。零表示无效标识，SpatialKit 不负责生成 ID。
    /// </summary>
    public readonly struct SpatialId : IEquatable<SpatialId>
    {
        /// <summary>数值形式的标识。</summary>
        public int Value { get; }

        /// <summary>创建空间索引标识。负数不被接受，零保留为无效标识。</summary>
        public SpatialId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "SpatialId 不能为负数。");
            }

            Value = value;
        }

        /// <summary>标识是否可用于索引。</summary>
        public bool IsValid => Value > 0;

        /// <summary>标识是否为无效的零值。</summary>
        public bool IsInvalid => Value == 0;

        public bool Equals(SpatialId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SpatialId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(SpatialId left, SpatialId right) => left.Equals(right);
        public static bool operator !=(SpatialId left, SpatialId right) => !left.Equals(right);
    }
}
