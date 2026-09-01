using System;

namespace StellarFramework
{
    /// <summary>
    /// Adapts GridKit coordinates and an application traversal policy to the generic
    /// PathKit graph contract. Node ids are stable row-major local indices plus one.
    /// </summary>
    public sealed class GridPathGraph : IPathGraph
    {
        private readonly GridRect _bounds;
        private readonly IGridPathTraversalPolicy _traversalPolicy;
        private readonly GridPathNeighborMode _neighborMode;
        private readonly GridPathDiagonalPolicy _diagonalPolicy;

        public GridRect Bounds => _bounds;
        public IGridPathTraversalPolicy TraversalPolicy => _traversalPolicy;
        public GridPathNeighborMode NeighborMode => _neighborMode;
        public GridPathDiagonalPolicy DiagonalPolicy => _diagonalPolicy;

        public GridPathGraph(GridRect bounds, IGridPathTraversalPolicy traversalPolicy,
            GridPathNeighborMode neighborMode = GridPathNeighborMode.FourWay,
            GridPathDiagonalPolicy diagonalPolicy = GridPathDiagonalPolicy.NoCornerCut)
        {
            if (bounds.Area > int.MaxValue - 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds),
                    "GridPathGraph supports at most Int32.MaxValue - 1 cells.");
            }

            _traversalPolicy = traversalPolicy ?? throw new ArgumentNullException(nameof(traversalPolicy));
            if (_traversalPolicy.MinimumOrthogonalCost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traversalPolicy),
                    "MinimumOrthogonalCost must be positive.");
            }

            if (_traversalPolicy.MinimumDiagonalCost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traversalPolicy),
                    "MinimumDiagonalCost must be positive.");
            }

            if (neighborMode != GridPathNeighborMode.FourWay && neighborMode != GridPathNeighborMode.EightWay)
            {
                throw new ArgumentOutOfRangeException(nameof(neighborMode), neighborMode,
                    "Unknown grid neighbor mode.");
            }

            if (diagonalPolicy != GridPathDiagonalPolicy.NoCornerCut &&
                diagonalPolicy != GridPathDiagonalPolicy.AllowCornerCut)
            {
                throw new ArgumentOutOfRangeException(nameof(diagonalPolicy), diagonalPolicy,
                    "Unknown grid diagonal policy.");
            }

            _bounds = bounds;
            _neighborMode = neighborMode;
            _diagonalPolicy = diagonalPolicy;
        }

        public bool ContainsNode(PathNodeId node)
        {
            return TryGetCoord(node, out _);
        }

        public int GetNeighborCount(PathNodeId node)
        {
            if (!TryGetCoord(node, out GridCoord from) || !_traversalPolicy.IsWalkable(from))
            {
                return 0;
            }

            int count = 0;
            int maximum = _neighborMode == GridPathNeighborMode.FourWay ? 4 : 8;
            for (int ordinal = 0; ordinal < maximum; ordinal++)
            {
                if (TryGetCandidate(from, ordinal, out _, out _)) count++;
            }

            return count;
        }

        public PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex)
        {
            if (neighborIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(neighborIndex), neighborIndex,
                    "Neighbor index cannot be negative.");
            }

            if (!TryGetCoord(node, out GridCoord from))
            {
                throw new ArgumentOutOfRangeException(nameof(node), node,
                    "Node does not map to this GridPathGraph bounds.");
            }

            if (!_traversalPolicy.IsWalkable(from))
            {
                throw new ArgumentOutOfRangeException(nameof(node), node,
                    "A blocked source has no traversable neighbors.");
            }

            int maximum = _neighborMode == GridPathNeighborMode.FourWay ? 4 : 8;
            int validIndex = 0;
            for (int ordinal = 0; ordinal < maximum; ordinal++)
            {
                if (!TryGetCandidate(from, ordinal, out GridCoord to, out long cost)) continue;
                if (validIndex++ == neighborIndex)
                {
                    return new PathNeighbor(GetNodeId(to), cost);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(neighborIndex), neighborIndex,
                "Neighbor index exceeds the current traversable neighbor count.");
        }

        public long EstimateCost(PathNodeId from, PathNodeId goal)
        {
            if (!TryGetCoord(from, out GridCoord fromCoord))
            {
                throw new ArgumentOutOfRangeException(nameof(from), from,
                    "Node does not map to this GridPathGraph bounds.");
            }

            if (!TryGetCoord(goal, out GridCoord goalCoord))
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal,
                    "Node does not map to this GridPathGraph bounds.");
            }

            long dx = AbsDifference(fromCoord.X, goalCoord.X);
            long dy = AbsDifference(fromCoord.Y, goalCoord.Y);
            if (_neighborMode == GridPathNeighborMode.FourWay)
            {
                long distance = checked(dx + dy);
                return checked(distance * _traversalPolicy.MinimumOrthogonalCost);
            }

            long diagonal = Math.Min(dx, dy);
            long straight = Math.Max(dx, dy) - diagonal;
            long doubledOrthogonal = checked(_traversalPolicy.MinimumOrthogonalCost * 2L);
            long effectiveDiagonal = Math.Min(_traversalPolicy.MinimumDiagonalCost, doubledOrthogonal);
            return checked(checked(diagonal * effectiveDiagonal) +
                           checked(straight * _traversalPolicy.MinimumOrthogonalCost));
        }

        public bool TryGetNodeId(GridCoord coord, out PathNodeId node)
        {
            if (!_bounds.Contains(coord))
            {
                node = default(PathNodeId);
                return false;
            }

            long localX = (long)coord.X - _bounds.Min.X;
            long localY = (long)coord.Y - _bounds.Min.Y;
            long localIndex = checked(localY * _bounds.Size.Width + localX);
            node = new PathNodeId(checked((int)localIndex + 1));
            return true;
        }

        public bool TryGetCoord(PathNodeId node, out GridCoord coord)
        {
            if (!node.IsValid || _bounds.IsEmpty || node.Value > _bounds.Area)
            {
                coord = default(GridCoord);
                return false;
            }

            long localIndex = (long)node.Value - 1L;
            int width = _bounds.Size.Width;
            long localY = localIndex / width;
            long localX = localIndex % width;
            coord = new GridCoord(
                checked((int)((long)_bounds.Min.X + localX)),
                checked((int)((long)_bounds.Min.Y + localY)));
            return true;
        }

        private bool TryGetCandidate(GridCoord from, int ordinal, out GridCoord to, out long cost)
        {
            GetOffset(ordinal, out int offsetX, out int offsetY, out bool diagonal);
            if (!GridMath.TryOffset(from, new GridOffset(offsetX, offsetY), out to) ||
                !_bounds.Contains(to) || !_traversalPolicy.IsWalkable(to) ||
                !_traversalPolicy.CanTraverse(from, to))
            {
                cost = 0;
                return false;
            }

            if (diagonal && _diagonalPolicy == GridPathDiagonalPolicy.NoCornerCut)
            {
                bool sideAInRange = GridMath.TryOffset(from, new GridOffset(offsetX, 0), out GridCoord sideA);
                bool sideBInRange = GridMath.TryOffset(from, new GridOffset(0, offsetY), out GridCoord sideB);
                if (!sideAInRange || !sideBInRange || !_bounds.Contains(sideA) || !_bounds.Contains(sideB) ||
                    !_traversalPolicy.IsWalkable(sideA) || !_traversalPolicy.IsWalkable(sideB))
                {
                    cost = 0;
                    return false;
                }
            }

            cost = _traversalPolicy.GetTraversalCost(from, to);
            if (cost <= 0)
            {
                throw new InvalidOperationException("Grid traversal policy returned a non-positive cost.");
            }

            long minimum = diagonal
                ? _traversalPolicy.MinimumDiagonalCost
                : _traversalPolicy.MinimumOrthogonalCost;
            if (cost < minimum)
            {
                throw new InvalidOperationException(
                    "Grid traversal policy returned a cost below its declared minimum.");
            }

            return true;
        }

        private void GetOffset(int ordinal, out int x, out int y, out bool diagonal)
        {
            if (_neighborMode == GridPathNeighborMode.FourWay)
            {
                diagonal = false;
                switch (ordinal)
                {
                    case 0: x = 0; y = 1; return;
                    case 1: x = 1; y = 0; return;
                    case 2: x = 0; y = -1; return;
                    case 3: x = -1; y = 0; return;
                    default: throw new ArgumentOutOfRangeException(nameof(ordinal));
                }
            }

            diagonal = ordinal % 2 == 1;
            switch (ordinal)
            {
                case 0: x = 0; y = 1; diagonal = false; return;
                case 1: x = 1; y = 1; return;
                case 2: x = 1; y = 0; diagonal = false; return;
                case 3: x = 1; y = -1; return;
                case 4: x = 0; y = -1; diagonal = false; return;
                case 5: x = -1; y = -1; return;
                case 6: x = -1; y = 0; diagonal = false; return;
                case 7: x = -1; y = 1; return;
                default: throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
        }

        private static long AbsDifference(int left, int right)
        {
            long difference = (long)left - right;
            return difference < 0 ? -difference : difference;
        }

        private PathNodeId GetNodeId(GridCoord coord)
        {
            if (!TryGetNodeId(coord, out PathNodeId node))
            {
                throw new InvalidOperationException("Grid candidate was outside graph bounds.");
            }

            return node;
        }
    }
}
