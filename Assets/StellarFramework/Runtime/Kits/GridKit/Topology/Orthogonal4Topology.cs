using System;

namespace StellarFramework
{
    /// <summary>Square-grid 4-way topology using the existing GridCoord contract.</summary>
    public readonly struct Orthogonal4Topology : IGridTopology<GridCoord>
    {
        /// <inheritdoc />
        public int MaxNeighborCount => 4;

        /// <inheritdoc />
        public int WriteNeighbors(GridCoord center, Span<GridCoord> destination)
        {
            return GridNeighbors.WriteNeighbors4(center, destination);
        }

        /// <summary>返回四向正交网格的 Manhattan 距离。</summary>
        public long GetDistance(GridCoord from, GridCoord to)
        {
            return GridDistance.Manhattan(from, to);
        }
    }
}
