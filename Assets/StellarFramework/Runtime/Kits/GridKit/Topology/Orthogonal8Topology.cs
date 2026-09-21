using System;

namespace StellarFramework
{
    /// <summary>Square-grid 8-way topology using the existing GridCoord contract.</summary>
    public readonly struct Orthogonal8Topology : IGridTopology<GridCoord>
    {
        /// <inheritdoc />
        public int MaxNeighborCount => 8;

        /// <inheritdoc />
        public int WriteNeighbors(GridCoord center, Span<GridCoord> destination)
        {
            return GridNeighbors.WriteNeighbors8(center, destination);
        }

        /// <summary>返回八向正交网格的 Chebyshev 距离。</summary>
        public long GetDistance(GridCoord from, GridCoord to)
        {
            return GridDistance.Chebyshev(from, to);
        }
    }
}
