using System;

namespace StellarFramework
{
    /// <summary>Optional topology capability for data that belongs to shared cell vertices.</summary>
    public interface IGridVertexTopology<TCell, TVertex>
        where TCell : struct
        where TVertex : struct
    {
        int MaxVertexCountPerCell { get; }
        int MaxAdjacentCellsPerVertex { get; }

        int WriteVertices(TCell cell, Span<TVertex> destination);

        int WriteAdjacentCells(TVertex vertex, Span<TCell> destination);
    }
}
