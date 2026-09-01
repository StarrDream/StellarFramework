using System;

namespace StellarFramework
{
    /// <summary>
    /// 由业务侧分配的模拟对象身份标识。零表示无效标识，SimulationKit 不负责生成 ID。
    /// </summary>
    public readonly struct SimulationId : IEquatable<SimulationId>
    {
        /// <summary>数值形式的标识。</summary>
        public int Value { get; }

        /// <summary>创建模拟对象标识。负数不被接受，零保留为无效标识。</summary>
        public SimulationId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "SimulationId 不能为负数。");
            }

            Value = value;
        }

        /// <summary>标识是否可用于调度。</summary>
        public bool IsValid => Value > 0;

        /// <summary>标识是否为无效的零值。</summary>
        public bool IsInvalid => Value == 0;

        public bool Equals(SimulationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SimulationId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(SimulationId left, SimulationId right) => left.Equals(right);
        public static bool operator !=(SimulationId left, SimulationId right) => !left.Equals(right);
    }
}
