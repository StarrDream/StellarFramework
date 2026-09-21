using System;

namespace StellarFramework
{
    /// <summary>描述一次 Occupancy 检查/提交的成功结果或首个冲突信息。</summary>
    /// <remarks>失败结果用于诊断，不代表发生了部分提交；GridOccupancy 的 TryOccupy/TryRelease 保持失败原子性。</remarks>
    public readonly struct GridOccupancyResult : IEquatable<GridOccupancyResult>
    {
        /// <summary>获取操作是否成功。</summary>
        public bool Success { get; }

        /// <summary>获取失败原因；成功时为 None。</summary>
        public GridOccupancyError Error { get; }

        /// <summary>获取检测到问题的逻辑坐标。</summary>
        public GridCoord ConflictCoord { get; }

        /// <summary>获取冲突坐标原有的占用者。</summary>
        public GridOccupantId ExistingOccupant { get; }

        private GridOccupancyResult(bool success, GridOccupancyError error, GridCoord conflictCoord,
            GridOccupantId existingOccupant)
        {
            Success = success;
            Error = error;
            ConflictCoord = conflictCoord;
            ExistingOccupant = existingOccupant;
        }

        /// <summary>创建标准成功结果。</summary>
        public static GridOccupancyResult Succeeded() =>
            new GridOccupancyResult(true, GridOccupancyError.None, default(GridCoord), GridOccupantId.None);

        /// <summary>创建包含失败原因和冲突上下文的结果。</summary>
        public static GridOccupancyResult Failed(GridOccupancyError error, GridCoord conflictCoord,
            GridOccupantId existingOccupant)
        {
            return new GridOccupancyResult(false, error, conflictCoord, existingOccupant);
        }

        /// <inheritdoc />
        public bool Equals(GridOccupancyResult other) => Success == other.Success && Error == other.Error &&
            ConflictCoord == other.ConflictCoord && ExistingOccupant == other.ExistingOccupant;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GridOccupancyResult && Equals((GridOccupancyResult)obj);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((((Success ? 1 : 0) * 397) ^ (int)Error) * 397 + ConflictCoord.GetHashCode());

        /// <inheritdoc />
        public override string ToString() => Success ? "Success" :
            string.Format("{0} at {1} (existing {2})", Error, ConflictCoord, ExistingOccupant);

        public static bool operator ==(GridOccupancyResult left, GridOccupancyResult right) => left.Equals(right);
        public static bool operator !=(GridOccupancyResult left, GridOccupancyResult right) => !left.Equals(right);
    }
}
