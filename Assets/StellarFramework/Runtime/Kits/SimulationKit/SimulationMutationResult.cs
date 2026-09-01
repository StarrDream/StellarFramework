using System;

namespace StellarFramework
{
    /// <summary>SimulationScheduler 写操作结果。</summary>
    /// <remarks>
    /// 失败时，已注册 Entry 的业务调度状态（ID、Interval、NextDue 和堆内容）保持不变。
    /// 对于带 nowTick 的调用，Scheduler 仍会按照时间单调契约记录已观察到的时间。
    /// </remarks>
    public readonly struct SimulationMutationResult : IEquatable<SimulationMutationResult>
    {
        public bool Success { get; }
        public SimulationMutationError Error { get; }

        private SimulationMutationResult(bool success, SimulationMutationError error)
        {
            Success = success;
            Error = error;
        }

        public static SimulationMutationResult Succeeded() =>
            new SimulationMutationResult(true, SimulationMutationError.None);

        public static SimulationMutationResult Failed(SimulationMutationError error) =>
            new SimulationMutationResult(false, error);

        public bool Equals(SimulationMutationResult other) => Success == other.Success && Error == other.Error;
        public override bool Equals(object obj) => obj is SimulationMutationResult other && Equals(other);
        public override int GetHashCode() => unchecked(((Success ? 1 : 0) * 397) ^ (int)Error);
        public override string ToString() => Success ? "Success" : Error.ToString();

        public static bool operator ==(SimulationMutationResult left, SimulationMutationResult right) => left.Equals(right);
        public static bool operator !=(SimulationMutationResult left, SimulationMutationResult right) => !left.Equals(right);
    }
}
