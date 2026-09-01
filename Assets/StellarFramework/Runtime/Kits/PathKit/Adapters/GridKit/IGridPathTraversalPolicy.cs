namespace StellarFramework
{
    /// <summary>
    /// Application-owned grid traversal state. The adapter deliberately does not know
    /// about terrain, occupancy, doors or any other world model.
    /// </summary>
    public interface IGridPathTraversalPolicy
    {
        long MinimumOrthogonalCost { get; }
        long MinimumDiagonalCost { get; }

        bool IsWalkable(GridCoord coord);
        bool CanTraverse(GridCoord from, GridCoord to);
        long GetTraversalCost(GridCoord from, GridCoord to);
    }
}
