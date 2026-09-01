using System;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class PathKitGridKitAdapterTests
    {
        private static readonly GridCoord Origin = new GridCoord(0, 0);

        [Test]
        public void NegativeBoundsMapAndRoundTripRowMajorNodeIds()
        {
            GridRect bounds = new GridRect(new GridCoord(-10, -20), new GridSize(20, 40));
            TestPolicy policy = new TestPolicy(bounds);
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            GridCoord[] coordinates =
            {
                bounds.Min,
                new GridCoord(-9, -20),
                new GridCoord(0, 0),
                new GridCoord(9, 19)
            };

            for (int i = 0; i < coordinates.Length; i++)
            {
                Assert.That(graph.TryGetNodeId(coordinates[i], out PathNodeId node), Is.True);
                Assert.That(node.Value, Is.EqualTo(i == 0 ? 1 : i == 1 ? 2 : i == 2 ? 411 : 800));
                Assert.That(graph.TryGetCoord(node, out GridCoord roundTrip), Is.True);
                Assert.That(roundTrip, Is.EqualTo(coordinates[i]));
            }

            Assert.That(graph.TryGetNodeId(new GridCoord(10, 19), out _), Is.False);
            Assert.That(graph.TryGetCoord(default(PathNodeId), out _), Is.False);
            Assert.That(graph.ContainsNode(new PathNodeId(800)), Is.True);
            Assert.That(graph.ContainsNode(new PathNodeId(801)), Is.False);
        }

        [Test]
        public void FourWayAndEightWayNeighborCountsAreStable()
        {
            GridRect bounds = new GridRect(new GridCoord(-1, -1), new GridSize(3, 3));
            TestPolicy policy = new TestPolicy(bounds);
            GridPathGraph four = new GridPathGraph(bounds, policy, GridPathNeighborMode.FourWay);
            GridPathGraph eight = new GridPathGraph(bounds, policy, GridPathNeighborMode.EightWay);
            Assert.That(four.GetNeighborCount(NodeAt(four, Origin)), Is.EqualTo(4));
            Assert.That(eight.GetNeighborCount(NodeAt(eight, Origin)), Is.EqualTo(8));
            Assert.That(four.GetNeighbor(NodeAt(four, Origin), 0).Node,
                Is.EqualTo(NodeAt(four, new GridCoord(0, 1))));
        }

        [Test]
        public void BlockedSourceAndTargetAreOmitted()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(3, 3));
            TestPolicy policy = new TestPolicy(bounds);
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            policy.SetWalkable(new GridCoord(0, 0), false);
            Assert.That(graph.GetNeighborCount(NodeAt(graph, new GridCoord(0, 0))), Is.EqualTo(0));
            policy.SetWalkable(new GridCoord(0, 0), true);
            policy.SetWalkable(new GridCoord(0, 1), false);
            Assert.That(graph.GetNeighborCount(NodeAt(graph, new GridCoord(0, 0))), Is.EqualTo(1));
            Assert.That(graph.GetNeighbor(NodeAt(graph, new GridCoord(0, 0)), 0).Node,
                Is.EqualTo(NodeAt(graph, new GridCoord(1, 0))));
        }

        [Test]
        public void CornerPolicyControlsDiagonalGap()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(2, 2));
            TestPolicy policy = new TestPolicy(bounds);
            policy.SetWalkable(new GridCoord(1, 0), false);
            policy.SetWalkable(new GridCoord(0, 1), false);
            GridPathGraph noCorner = new GridPathGraph(bounds, policy, GridPathNeighborMode.EightWay,
                GridPathDiagonalPolicy.NoCornerCut);
            GridPathGraph allowCorner = new GridPathGraph(bounds, policy, GridPathNeighborMode.EightWay,
                GridPathDiagonalPolicy.AllowCornerCut);
            PathNodeId start = NodeAt(noCorner, Origin);
            Assert.That(noCorner.GetNeighborCount(start), Is.EqualTo(0));
            Assert.That(allowCorner.GetNeighborCount(NodeAt(allowCorner, Origin)), Is.EqualTo(1));
        }

        [Test]
        public void CanTraverseCanRepresentOneWayEdges()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(3, 1));
            TestPolicy policy = new TestPolicy(bounds) { OnlyRight = true };
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            Assert.That(graph.GetNeighborCount(NodeAt(graph, new GridCoord(1, 0))), Is.EqualTo(1));
            Assert.That(graph.GetNeighbor(NodeAt(graph, new GridCoord(1, 0)), 0).Node,
                Is.EqualTo(NodeAt(graph, new GridCoord(2, 0))));
        }

        [Test]
        public void WeightedPolicyUsesDeclaredMinimumsAndBothAlgorithmsAgree()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(3, 2));
            TestPolicy policy = new TestPolicy(bounds);
            policy.SetCost(new GridCoord(1, 0), 5000);
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            PathNodeId[] aBuffer = new PathNodeId[16];
            PathNodeId[] dBuffer = new PathNodeId[16];
            PathNodeId start = NodeAt(graph, new GridCoord(0, 0));
            PathNodeId goal = NodeAt(graph, new GridCoord(2, 0));
            PathSearchResult a = new AStarPathfinder(16).FindPath(graph,
                new PathSearchRequest(start, goal), aBuffer.AsSpan());
            PathSearchResult d = new DijkstraPathfinder(16).FindPath(graph,
                new PathSearchRequest(start, goal), dBuffer.AsSpan());
            Assert.That(a.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(d.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(a.TotalCost, Is.EqualTo(d.TotalCost));
            Assert.That(a.TotalCost, Is.LessThan(5500));
        }

        [Test]
        public void MinimumCostViolationFailsLoudly()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(2, 1));
            TestPolicy policy = new TestPolicy(bounds) { MinimumOrthogonalCostValue = 1000, ReturnTooCheap = true };
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            Assert.That(() => graph.GetNeighborCount(NodeAt(graph, Origin)), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GridHeuristicsUseLongSafeLowerBoundFormulas()
        {
            GridRect bounds = new GridRect(new GridCoord(-2, -2), new GridSize(5, 5));
            TestPolicy policy = new TestPolicy(bounds)
            {
                MinimumOrthogonalCostValue = 100,
                MinimumDiagonalCostValue = 150
            };
            GridPathGraph four = new GridPathGraph(bounds, policy, GridPathNeighborMode.FourWay);
            GridPathGraph eight = new GridPathGraph(bounds, policy, GridPathNeighborMode.EightWay);
            Assert.That(four.EstimateCost(NodeAt(four, new GridCoord(-2, -2)),
                NodeAt(four, new GridCoord(2, 2))), Is.EqualTo(800));
            Assert.That(eight.EstimateCost(NodeAt(eight, new GridCoord(-2, -2)),
                NodeAt(eight, new GridCoord(2, 2))), Is.EqualTo(600));
        }

        [Test]
        public void DynamicWalkabilityIsReadOnEachSearch()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(3, 1));
            TestPolicy policy = new TestPolicy(bounds);
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            PathNodeId start = NodeAt(graph, Origin);
            PathNodeId goal = NodeAt(graph, new GridCoord(2, 0));
            DijkstraPathfinder pathfinder = new DijkstraPathfinder(8);
            PathNodeId[] buffer = new PathNodeId[8];
            Assert.That(pathfinder.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan()).Success, Is.True);
            policy.SetWalkable(new GridCoord(1, 0), false);
            Assert.That(pathfinder.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan()).Status,
                Is.EqualTo(PathSearchStatus.NoPath));
        }

        [Test]
        public void ExtremeGridHeuristicDoesNotWrap()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(int.MaxValue - 1, 1));
            TestPolicy policy = new TestPolicy(bounds, false) { MinimumOrthogonalCostValue = long.MaxValue };
            GridPathGraph graph = new GridPathGraph(bounds, policy);
            PathNodeId start = new PathNodeId(1);
            PathNodeId goal = new PathNodeId(int.MaxValue - 1);
            Assert.That(() => graph.EstimateCost(start, goal), Throws.TypeOf<OverflowException>());
            PathSearchResult result = new AStarPathfinder().FindPath(graph,
                new PathSearchRequest(start, goal, 1), Span<PathNodeId>.Empty);
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.CostOverflow));
        }

        [Test]
        public void ConstructorRejectsInvalidMinimumsAndOversizedArea()
        {
            GridRect bounds = new GridRect(new GridCoord(0, 0), new GridSize(2, 1));
            Assert.That(() => new GridPathGraph(bounds, new TestPolicy(bounds) { MinimumOrthogonalCostValue = 0 }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            GridRect tooLarge = new GridRect(new GridCoord(0, 0), new GridSize(int.MaxValue, 1));
            // The constructor must reject the area before a policy attempts to allocate a
            // backing cell array for more than Int32.MaxValue - 1 nodes.
            Assert.That(() => new GridPathGraph(tooLarge, new TestPolicy(tooLarge, false)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static PathNodeId NodeAt(GridPathGraph graph, GridCoord coord)
        {
            Assert.That(graph.TryGetNodeId(coord, out PathNodeId node), Is.True);
            return node;
        }

        private sealed class TestPolicy : IGridPathTraversalPolicy
        {
            private readonly GridRect _bounds;
            private readonly bool[] _walkable;
            private readonly long[] _cost;

            internal TestPolicy(GridRect bounds, bool allocateCells = true)
            {
                _bounds = bounds;
                _walkable = allocateCells ? new bool[(int)bounds.Area] : Array.Empty<bool>();
                _cost = allocateCells ? new long[(int)bounds.Area] : Array.Empty<long>();
                for (int i = 0; i < _walkable.Length; i++) { _walkable[i] = true; _cost[i] = 100; }
            }

            internal long MinimumOrthogonalCostValue { get; set; } = 100;
            internal long MinimumDiagonalCostValue { get; set; } = 140;
            internal bool ReturnTooCheap { get; set; }
            internal bool OnlyRight { get; set; }
            public long MinimumOrthogonalCost => MinimumOrthogonalCostValue;
            public long MinimumDiagonalCost => MinimumDiagonalCostValue;

            public bool IsWalkable(GridCoord coord) => _bounds.Contains(coord) &&
                (_walkable.Length == 0 || _walkable[Index(coord)]);

            public bool CanTraverse(GridCoord from, GridCoord to)
            {
                if (!_bounds.Contains(from) || !_bounds.Contains(to)) return false;
                return !OnlyRight || to.X == from.X + 1;
            }

            public long GetTraversalCost(GridCoord from, GridCoord to)
            {
                if (ReturnTooCheap) return MinimumOrthogonalCostValue - 1;
                long baseCost = from.X != to.X && from.Y != to.Y ? MinimumDiagonalCostValue : MinimumOrthogonalCostValue;
                return Math.Max(baseCost, _cost.Length == 0 ? baseCost : _cost[Index(to)]);
            }

            internal void SetWalkable(GridCoord coord, bool value) { _walkable[Index(coord)] = value; }
            internal void SetCost(GridCoord coord, long value) { _cost[Index(coord)] = value; }

            private int Index(GridCoord coord)
            {
                long x = (long)coord.X - _bounds.Min.X;
                long y = (long)coord.Y - _bounds.Min.Y;
                return checked((int)(y * _bounds.Size.Width + x));
            }
        }
    }
}
