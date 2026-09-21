using System;

namespace StellarFramework
{
    /// <summary>
    /// Allocation-free axial Hex topology.
    /// Neighbor order is East, NorthEast, NorthWest, West, SouthWest, SouthEast.
    /// </summary>
    public readonly struct HexTopology :
        IGridTopology<HexCoord>,
        IGridEdgeTopology<HexCoord, HexEdge>,
        IGridVertexTopology<HexCoord, HexVertex>
    {
        public int MaxNeighborCount => 6;
        public int MaxEdgeCountPerCell => 6;
        public int MaxVertexCountPerCell => 6;
        public int MaxAdjacentCellsPerVertex => 3;

        public int WriteNeighbors(HexCoord center, Span<HexCoord> destination)
        {
            RequireCapacity(destination.Length, MaxNeighborCount);

            int written = 0;
            for (int i = 0; i < MaxNeighborCount; i++)
            {
                if (TryGetNeighbor(center, (HexDirection)i, out HexCoord neighbor))
                {
                    destination[written++] = neighbor;
                }
            }

            return written;
        }

        public long GetDistance(HexCoord from, HexCoord to)
        {
            long dq = Math.Abs((long)from.Q - to.Q);
            long dr = Math.Abs((long)from.R - to.R);
            long fromS = -(long)from.Q - from.R;
            long toS = -(long)to.Q - to.R;
            long ds = Math.Abs(fromS - toS);
            return checked((dq + dr + ds) / 2L);
        }

        public bool TryGetNeighbor(HexCoord center, HexDirection direction, out HexCoord neighbor)
        {
            GetDirectionOffset(direction, out int q, out int r);
            return TryOffset(center, q, r, out neighbor);
        }

        public HexCoord GetNeighbor(HexCoord center, HexDirection direction)
        {
            if (!TryGetNeighbor(center, direction, out HexCoord neighbor))
            {
                throw new OverflowException("Hex neighbor cannot be represented by Int32 coordinates.");
            }

            return neighbor;
        }

        public bool TryGetEdge(HexCoord cell, HexDirection direction, out HexEdge edge)
        {
            if (!TryGetNeighbor(cell, direction, out HexCoord neighbor))
            {
                edge = default(HexEdge);
                return false;
            }

            edge = new HexEdge(cell, neighbor);
            return true;
        }

        public HexEdge GetEdge(HexCoord cell, HexDirection direction)
        {
            if (!TryGetEdge(cell, direction, out HexEdge edge))
            {
                throw new OverflowException("Hex edge cannot be represented by Int32 coordinates.");
            }

            return edge;
        }

        public int WriteEdges(HexCoord cell, Span<HexEdge> destination)
        {
            RequireCapacity(destination.Length, MaxEdgeCountPerCell);
            int written = 0;
            for (int i = 0; i < MaxEdgeCountPerCell; i++)
            {
                if (TryGetEdge(cell, (HexDirection)i, out HexEdge edge))
                {
                    destination[written++] = edge;
                }
            }

            return written;
        }

        public bool TryGetAdjacentCells(HexEdge edge, out HexCoord first, out HexCoord second)
        {
            if (!edge.IsValid || GetDistance(edge.First, edge.Second) != 1L)
            {
                first = default(HexCoord);
                second = default(HexCoord);
                return false;
            }

            first = edge.First;
            second = edge.Second;
            return true;
        }

        public bool TryGetVertex(HexCoord cell, int vertexIndex, out HexVertex vertex)
        {
            if ((uint)vertexIndex >= 6u)
            {
                vertex = default(HexVertex);
                return false;
            }

            HexDirection firstDirection = (HexDirection)vertexIndex;
            HexDirection secondDirection = (HexDirection)((vertexIndex + 1) % 6);
            if (!TryGetNeighbor(cell, firstDirection, out HexCoord firstNeighbor) ||
                !TryGetNeighbor(cell, secondDirection, out HexCoord secondNeighbor))
            {
                vertex = default(HexVertex);
                return false;
            }

            vertex = new HexVertex(cell, firstNeighbor, secondNeighbor);
            return true;
        }

        public HexVertex GetVertex(HexCoord cell, int vertexIndex)
        {
            if ((uint)vertexIndex >= 6u)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexIndex), vertexIndex,
                    "Hex vertex index must be between 0 and 5.");
            }

            if (!TryGetVertex(cell, vertexIndex, out HexVertex vertex))
            {
                throw new OverflowException("Hex vertex cannot be represented by Int32 coordinates.");
            }

            return vertex;
        }

        public int WriteVertices(HexCoord cell, Span<HexVertex> destination)
        {
            RequireCapacity(destination.Length, MaxVertexCountPerCell);
            int written = 0;
            for (int i = 0; i < MaxVertexCountPerCell; i++)
            {
                if (TryGetVertex(cell, i, out HexVertex vertex))
                {
                    destination[written++] = vertex;
                }
            }

            return written;
        }

        public int WriteAdjacentCells(HexVertex vertex, Span<HexCoord> destination)
        {
            RequireCapacity(destination.Length, MaxAdjacentCellsPerVertex);
            if (!vertex.IsValid)
            {
                return 0;
            }

            destination[0] = vertex.First;
            destination[1] = vertex.Second;
            destination[2] = vertex.Third;
            return 3;
        }

        public static long GetRingCellCount(int radius)
        {
            ValidateRadius(radius);
            return radius == 0 ? 1L : checked(6L * radius);
        }

        public static long GetRangeCellCount(int radius)
        {
            ValidateRadius(radius);
            return checked(1L + checked(3L * radius * checked((long)radius + 1L)));
        }

        public int WriteRing(HexCoord center, int radius, Span<HexCoord> destination)
        {
            long requiredLong = GetRingCellCount(radius);
            int required = RequireRepresentableCount(requiredLong);
            RequireCapacity(destination.Length, required);
            RequireCoordinateRadius(center, radius);

            if (radius == 0)
            {
                destination[0] = center;
                return 1;
            }

            HexCoord current = OffsetChecked(center, -radius, radius);
            int written = 0;

            for (int side = 0; side < 6; side++)
            {
                HexDirection direction = (HexDirection)side;
                for (int step = 0; step < radius; step++)
                {
                    destination[written++] = current;
                    current = GetNeighbor(current, direction);
                }
            }

            return written;
        }

        public int WriteRange(HexCoord center, int radius, Span<HexCoord> destination)
        {
            long requiredLong = GetRangeCellCount(radius);
            int required = RequireRepresentableCount(requiredLong);
            RequireCapacity(destination.Length, required);
            RequireCoordinateRadius(center, radius);

            int written = 0;
            for (int dq = -radius; dq <= radius; dq++)
            {
                int minDr = Math.Max(-radius, -dq - radius);
                int maxDr = Math.Min(radius, -dq + radius);
                for (int dr = minDr; dr <= maxDr; dr++)
                {
                    destination[written++] = OffsetChecked(center, dq, dr);
                }
            }

            return written;
        }

        private static bool TryOffset(HexCoord center, int deltaQ, int deltaR, out HexCoord result)
        {
            long q = (long)center.Q + deltaQ;
            long r = (long)center.R + deltaR;
            if (q < int.MinValue || q > int.MaxValue || r < int.MinValue || r > int.MaxValue)
            {
                result = default(HexCoord);
                return false;
            }

            result = new HexCoord((int)q, (int)r);
            return true;
        }

        private static HexCoord OffsetChecked(HexCoord center, int deltaQ, int deltaR)
        {
            if (!TryOffset(center, deltaQ, deltaR, out HexCoord result))
            {
                throw new OverflowException("Hex offset cannot be represented by Int32 coordinates.");
            }

            return result;
        }

        private static void GetDirectionOffset(HexDirection direction, out int q, out int r)
        {
            switch (direction)
            {
                case HexDirection.East:
                    q = 1;
                    r = 0;
                    return;
                case HexDirection.NorthEast:
                    q = 1;
                    r = -1;
                    return;
                case HexDirection.NorthWest:
                    q = 0;
                    r = -1;
                    return;
                case HexDirection.West:
                    q = -1;
                    r = 0;
                    return;
                case HexDirection.SouthWest:
                    q = -1;
                    r = 1;
                    return;
                case HexDirection.SouthEast:
                    q = 0;
                    r = 1;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown Hex direction.");
            }
        }

        private static void ValidateRadius(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius cannot be negative.");
            }
        }

        private static int RequireRepresentableCount(long count)
        {
            if (count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    "Requested Hex region exceeds the maximum Span length.");
            }

            return (int)count;
        }

        private static void RequireCapacity(int actual, int required)
        {
            if (actual < required)
            {
                throw new ArgumentException("Destination buffer is smaller than the required Hex result count.",
                    nameof(actual));
            }
        }

        private static void RequireCoordinateRadius(HexCoord center, int radius)
        {
            long minQ = (long)center.Q - radius;
            long maxQ = (long)center.Q + radius;
            long minR = (long)center.R - radius;
            long maxR = (long)center.R + radius;
            if (minQ < int.MinValue || maxQ > int.MaxValue || minR < int.MinValue || maxR > int.MaxValue)
            {
                throw new OverflowException("Requested Hex region exceeds Int32 coordinate range.");
            }
        }
    }
}
