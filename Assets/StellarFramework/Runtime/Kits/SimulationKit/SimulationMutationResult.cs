using System;

namespace StellarFramework
{
    /// <summary>SimulationScheduler 写操作结果。失败时调度器内容保持不变。</summary>
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
