using System;

namespace StellarFramework
{
    public readonly struct GridOccupancyResult : IEquatable<GridOccupancyResult>
    {
        public bool Success { get; }
        public GridOccupancyError Error { get; }
        public GridCoord ConflictCoord { get; }
        public GridOccupantId ExistingOccupant { get; }

        private GridOccupancyResult(bool success, GridOccupancyError error, GridCoord conflictCoord,
            GridOccupantId existingOccupant)
        {
            Success = success;
            Error = error;
            ConflictCoord = conflictCoord;
            ExistingOccupant = existingOccupant;
        }

        public static GridOccupancyResult Succeeded() =>
            new GridOccupancyResult(true, GridOccupancyError.None, default(GridCoord), GridOccupantId.None);

        public static GridOccupancyResult Failed(GridOccupancyError error, GridCoord conflictCoord,
            GridOccupantId existingOccupant)
        {
            return new GridOccupancyResult(false, error, conflictCoord, existingOccupant);
        }

        public bool Equals(GridOccupancyResult other) => Success == other.Success && Error == other.Error &&
            ConflictCoord == other.ConflictCoord && ExistingOccupant == other.ExistingOccupant;
        public override bool Equals(object obj) => obj is GridOccupancyResult && Equals((GridOccupancyResult)obj);
        public override int GetHashCode() => unchecked((((Success ? 1 : 0) * 397) ^ (int)Error) * 397 + ConflictCoord.GetHashCode());
        public override string ToString() => Success ? "Success" :
            string.Format("{0} at {1} (existing {2})", Error, ConflictCoord, ExistingOccupant);

        public static bool operator ==(GridOccupancyResult left, GridOccupancyResult right) => left.Equals(right);
        public static bool operator !=(GridOccupancyResult left, GridOccupancyResult right) => !left.Equals(right);
    }
}
