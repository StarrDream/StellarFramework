using System;

namespace StellarFramework
{
    /// <summary>Optional topology capability for data that belongs to shared cell edges.</summary>
    public interface IGridEdgeTopology<TCell, TEdge>
        where TCell : struct
        where TEdge : struct
    {
        int MaxEdgeCountPerCell { get; }

        int WriteEdges(TCell cell, Span<TEdge> destination);

        bool TryGetAdjacentCells(TEdge edge, out TCell first, out TCell second);
    }
}
