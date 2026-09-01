using System;

namespace StellarFramework
{
    /// <summary>空间索引写操作结果。失败时索引逻辑状态保持不变。</summary>
    public readonly struct SpatialMutationResult : IEquatable<SpatialMutationResult>
    {
        public bool Success { get; }
        public SpatialMutationError Error { get; }

        private SpatialMutationResult(bool success, SpatialMutationError error)
        {
            Success = success;
            Error = error;
        }

        public static SpatialMutationResult Succeeded() =>
            new SpatialMutationResult(true, SpatialMutationError.None);

        public static SpatialMutationResult Failed(SpatialMutationError error) =>
            new SpatialMutationResult(false, error);

        public bool Equals(SpatialMutationResult other) => Success == other.Success && Error == other.Error;
        public override bool Equals(object obj) => obj is SpatialMutationResult other && Equals(other);
        public override int GetHashCode() => unchecked(((Success ? 1 : 0) * 397) ^ (int)Error);
        public override string ToString() => Success ? "Success" : Error.ToString();

        public static bool operator ==(SpatialMutationResult left, SpatialMutationResult right) => left.Equals(right);
        public static bool operator !=(SpatialMutationResult left, SpatialMutationResult right) => !left.Equals(right);
    }
}
