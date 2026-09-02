using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class PathKitCoreTests
    {
        private static PathNodeId Node(int value) => new PathNodeId(value);

        [Test]
        public void PathNodeIdUsesZeroAsInvalidAndRejectsNegative()
        {
            Assert.That(default(PathNodeId).IsInvalid, Is.True);
            Assert.That(Node(1).IsValid, Is.True);
            Assert.That(Node(int.MaxValue).IsValid, Is.True);
            Assert.That(() => new PathNodeId(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(Node(42), Is.EqualTo(new PathNodeId(42)));
            Assert.That(Node(42).GetHashCode(), Is.EqualTo(new PathNodeId(42).GetHashCode()));
            Assert.That(Node(42).ToString(), Is.EqualTo("42"));
        }

        [Test]
        public void PathNeighborRequiresValidNodeAndPositiveLongCost()
        {
            Assert.That(new PathNeighbor(Node(1), long.MaxValue).Cost, Is.EqualTo(long.MaxValue));
            Assert.That(() => new PathNeighbor(default(PathNodeId), 1), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new PathNeighbor(Node(1), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PathNeighbor(Node(1), -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RequestValidatesExpansionLimitAndDefaultRequestFailsLoudly()
        {
            Assert.That(new PathSearchRequest(Node(1), Node(2), 1).MaxExpandedNodes, Is.EqualTo(1));
            Assert.That(() => new PathSearchRequest(Node(1), Node(2), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PathSearchRequest(Node(1), Node(2), -1), Throws.TypeOf<ArgumentOutOfRangeException>());

            TestGraph graph = new TestGraph(2);
            graph.Add(1, 2, 1);
            Assert.That(() => new DijkstraPathfinder().FindPath(graph, default(PathSearchRequest), Span<PathNodeId>.Empty),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DefaultResult_IsNoneAndNotSuccess()
        {
            PathSearchResult result = default(PathSearchResult);

            Assert.That((int)PathSearchStatus.None, Is.EqualTo(0));
            Assert.That((int)PathSearchStatus.Success, Is.Not.EqualTo(0));
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.None));
            Assert.That(result.Success, Is.False);
            Assert.That(result.WrittenCount, Is.EqualTo(0));
            Assert.That(result.RequiredNodeCount, Is.EqualTo(0));
            Assert.That(result.TotalCost, Is.EqualTo(0));
            Assert.That(result.ExpandedNodeCount, Is.EqualTo(0));
        }

        [Test]
        public void FindPathNeverReturnsNoneForExecutedStatuses()
        {
            TestGraph successGraph = new TestGraph(2);
            successGraph.Add(1, 2, 1);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(successGraph,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan()),
                PathSearchStatus.Success);

            TestGraph noPathGraph = new TestGraph(2);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(noPathGraph,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan()),
                PathSearchStatus.NoPath);

            AssertExecutedStatus(new DijkstraPathfinder().FindPath(successGraph,
                new PathSearchRequest(default(PathNodeId), Node(2)), new PathNodeId[2].AsSpan()),
                PathSearchStatus.InvalidStart);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(successGraph,
                new PathSearchRequest(Node(1), default(PathNodeId)), new PathNodeId[2].AsSpan()),
                PathSearchStatus.InvalidGoal);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(successGraph,
                new PathSearchRequest(Node(1), Node(2)), Span<PathNodeId>.Empty),
                PathSearchStatus.OutputBufferTooSmall);

            TestGraph limitedGraph = new TestGraph(3);
            limitedGraph.Add(1, 2, 1);
            limitedGraph.Add(2, 3, 1);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(limitedGraph,
                new PathSearchRequest(Node(1), Node(3), 1), new PathNodeId[3].AsSpan()),
                PathSearchStatus.ExpansionLimitReached);

            TestGraph overflowGraph = new TestGraph(3);
            overflowGraph.Add(1, 2, long.MaxValue - 1);
            overflowGraph.Add(2, 3, 2);
            AssertExecutedStatus(new DijkstraPathfinder().FindPath(overflowGraph,
                new PathSearchRequest(Node(1), Node(3)), new PathNodeId[3].AsSpan()),
                PathSearchStatus.CostOverflow);
        }

        [Test]
        public void AStarAndDijkstraFindTheSameWeightedDirectedShortestPath()
        {
            TestGraph graph = new TestGraph(5);
            graph.Add(1, 2, 10);
            graph.Add(2, 5, 10);
            graph.Add(1, 3, 3);
            graph.Add(3, 4, 3);
            graph.Add(4, 5, 3);
            graph.Heuristic = (from, goal) => 0;
            PathNodeId[] aPath = new PathNodeId[8];
            PathNodeId[] dPath = new PathNodeId[8];

            PathSearchResult a = new AStarPathfinder(8).FindPath(graph,
                new PathSearchRequest(Node(1), Node(5)), aPath.AsSpan());
            PathSearchResult d = new DijkstraPathfinder(8).FindPath(graph,
                new PathSearchRequest(Node(1), Node(5)), dPath.AsSpan());

            Assert.That(a.Success, Is.True);
            Assert.That(d.Success, Is.True);
            Assert.That(a.TotalCost, Is.EqualTo(9));
            Assert.That(d.TotalCost, Is.EqualTo(9));
            AssertPath(aPath, a.WrittenCount, 1, 3, 4, 5);
            AssertPath(dPath, d.WrittenCount, 1, 3, 4, 5);
        }

        [Test]
        public void DirectedGraphDoesNotInventReverseEdges()
        {
            TestGraph graph = new TestGraph(2);
            graph.Add(1, 2, 1);
            PathNodeId[] buffer = new PathNodeId[4];
            PathSearchResult forward = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(2)), buffer.AsSpan());
            PathSearchResult reverse = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(2), Node(1)), buffer.AsSpan());
            Assert.That(forward.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(reverse.Status, Is.EqualTo(PathSearchStatus.NoPath));
        }

        [Test]
        public void StartGoalAndMissingNodeStatusesArePrecise()
        {
            TestGraph graph = new TestGraph(2);
            PathNodeId[] buffer = new PathNodeId[2];
            PathSearchResult same = new AStarPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(1)), buffer.AsSpan());
            Assert.That(same.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(same.WrittenCount, Is.EqualTo(1));
            Assert.That(same.TotalCost, Is.EqualTo(0));
            Assert.That(same.ExpandedNodeCount, Is.EqualTo(0));
            Assert.That(buffer[0], Is.EqualTo(Node(1)));

            PathSearchResult empty = new AStarPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(1)), Span<PathNodeId>.Empty);
            Assert.That(empty.Status, Is.EqualTo(PathSearchStatus.OutputBufferTooSmall));
            Assert.That(empty.RequiredNodeCount, Is.EqualTo(1));

            Assert.That(new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(default(PathNodeId), Node(1)), buffer.AsSpan()).Status,
                Is.EqualTo(PathSearchStatus.InvalidStart));
            Assert.That(new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), default(PathNodeId)), buffer.AsSpan()).Status,
                Is.EqualTo(PathSearchStatus.InvalidGoal));
            Assert.That(new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(3), Node(1)), buffer.AsSpan()).Status,
                Is.EqualTo(PathSearchStatus.StartNotFound));
            Assert.That(new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(3)), buffer.AsSpan()).Status,
                Is.EqualTo(PathSearchStatus.GoalNotFound));
        }

        [Test]
        public void NoPathAndExpansionLimitNeverReturnPartialOutput()
        {
            TestGraph graph = new TestGraph(4);
            graph.Add(1, 2, 1);
            graph.Add(2, 3, 1);
            PathNodeId[] buffer = { Node(99), Node(99), Node(99) };
            PathSearchResult noPath = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(4)), buffer.AsSpan());
            Assert.That(noPath.Status, Is.EqualTo(PathSearchStatus.NoPath));
            Assert.That(noPath.WrittenCount, Is.EqualTo(0));
            Assert.That(buffer[0], Is.EqualTo(Node(99)));

            PathSearchResult limited = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(3), 1), buffer.AsSpan());
            Assert.That(limited.Status, Is.EqualTo(PathSearchStatus.ExpansionLimitReached));
            Assert.That(limited.WrittenCount, Is.EqualTo(0));
        }

        [Test]
        public void OutputBufferTooSmallReportsExactPathWithoutWriting()
        {
            TestGraph graph = new TestGraph(6);
            for (int i = 1; i < 6; i++) graph.Add(i, i + 1, 2);
            PathNodeId[] small = { Node(90), Node(91), Node(92), Node(93), Node(94) };
            PathSearchResult result = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(6)), small.AsSpan());
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.OutputBufferTooSmall));
            Assert.That(result.RequiredNodeCount, Is.EqualTo(6));
            Assert.That(result.TotalCost, Is.EqualTo(10));
            Assert.That(result.WrittenCount, Is.EqualTo(0));
            Assert.That(small[0], Is.EqualTo(Node(90)));

            PathNodeId[] exact = new PathNodeId[6];
            PathSearchResult success = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(6)), exact.AsSpan());
            Assert.That(success.WrittenCount, Is.EqualTo(6));
            AssertPath(exact, success.WrittenCount, 1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void DijkstraNeverCallsHeuristic()
        {
            TestGraph graph = new TestGraph(2) { ThrowOnHeuristic = true };
            graph.Add(1, 2, 1);
            PathSearchResult result = new DijkstraPathfinder().FindPath(graph,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan());
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Success));
        }

        [Test]
        public void AStarRejectsNegativeHeuristicAndInvalidGraphContracts()
        {
            TestGraph negative = new TestGraph(2) { Heuristic = (from, goal) => -1 };
            negative.Add(1, 2, 1);
            Assert.That(() => new AStarPathfinder().FindPath(negative,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan()),
                Throws.TypeOf<InvalidOperationException>());

            TestGraph badCount = new TestGraph(2) { ForcedNeighborCount = -1 };
            Assert.That(() => new DijkstraPathfinder().FindPath(badCount,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan()),
                Throws.TypeOf<InvalidOperationException>());

            TestGraph badNeighbor = new TestGraph(2) { ReturnInvalidNeighbor = true };
            badNeighbor.Add(1, 2, 1);
            Assert.That(() => new DijkstraPathfinder().FindPath(badNeighbor,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[2].AsSpan()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CostAndFOverflowReturnStatusWithoutWrapping()
        {
            TestGraph edgeOverflow = new TestGraph(3);
            edgeOverflow.Add(1, 2, long.MaxValue - 1);
            edgeOverflow.Add(2, 3, 2);
            PathSearchResult edgeResult = new DijkstraPathfinder().FindPath(edgeOverflow,
                new PathSearchRequest(Node(1), Node(3)), new PathNodeId[4].AsSpan());
            Assert.That(edgeResult.Status, Is.EqualTo(PathSearchStatus.CostOverflow));

            TestGraph fOverflow = new TestGraph(2) { Heuristic = (from, goal) => from == 2 ? 2 : 0 };
            fOverflow.Add(1, 2, long.MaxValue - 1);
            PathSearchResult fResult = new AStarPathfinder().FindPath(fOverflow,
                new PathSearchRequest(Node(1), Node(2)), new PathNodeId[4].AsSpan());
            Assert.That(fResult.Status, Is.EqualTo(PathSearchStatus.CostOverflow));
        }

        [Test]
        public void AStarReopensClosedNodeForInconsistentAdmissibleHeuristic()
        {
            TestGraph graph = new TestGraph(5)
            {
                Heuristic = (from, goal) => from == 3 ? 3 : from == 4 ? 1 : 0
            };
            graph.Add(1, 2, 4); // worse first route to A
            graph.Add(1, 3, 1);
            graph.Add(2, 5, 1);
            graph.Add(3, 4, 1);
            graph.Add(4, 2, 1); // better route reaches A after it was closed
            PathNodeId[] buffer = new PathNodeId[8];
            PathSearchResult result = new AStarPathfinder(8).FindPath(graph,
                new PathSearchRequest(Node(1), Node(5)), buffer.AsSpan());
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(result.TotalCost, Is.EqualTo(4));
            AssertPath(buffer, result.WrittenCount, 1, 3, 4, 2, 5);
        }

        [Test]
        public void DeterministicTieAndWorkspaceReuseStayStable()
        {
            TestGraph graph = new TestGraph(4);
            graph.Add(1, 2, 1);
            graph.Add(1, 3, 1);
            graph.Add(2, 4, 1);
            graph.Add(3, 4, 1);
            AStarPathfinder pathfinder = new AStarPathfinder(8);
            PathNodeId[] buffer = new PathNodeId[8];
            string first = null;
            for (int i = 0; i < 120; i++)
            {
                PathSearchResult result = pathfinder.FindPath(graph,
                    new PathSearchRequest(Node(1), Node(4)), buffer.AsSpan());
                Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Success));
                string current = buffer[0] + ":" + buffer[1] + ":" + buffer[2];
                first = first ?? current;
                Assert.That(current, Is.EqualTo(first));
            }

            PathSearchResult noPath = pathfinder.FindPath(graph,
                new PathSearchRequest(Node(4), Node(1)), buffer.AsSpan());
            Assert.That(noPath.Status, Is.EqualTo(PathSearchStatus.NoPath));
            PathSearchResult afterFailure = pathfinder.FindPath(graph,
                new PathSearchRequest(Node(1), Node(4)), buffer.AsSpan());
            Assert.That(afterFailure.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(buffer[1], Is.EqualTo(Node(2)));
        }

        private static void AssertPath(PathNodeId[] actual, int count, params int[] expected)
        {
            Assert.That(count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(actual[i], Is.EqualTo(Node(expected[i])));
        }

        private static void AssertExecutedStatus(PathSearchResult result, PathSearchStatus expected)
        {
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Status, Is.Not.EqualTo(PathSearchStatus.None));
        }

        private sealed class TestGraph : IPathGraph
        {
            private readonly List<PathNeighbor>[] _neighbors;

            internal TestGraph(int nodeCount)
            {
                _neighbors = new List<PathNeighbor>[nodeCount];
                for (int i = 0; i < nodeCount; i++) _neighbors[i] = new List<PathNeighbor>();
                Heuristic = (from, goal) => 0;
            }

            internal Func<int, int, long> Heuristic { get; set; }
            internal bool ThrowOnHeuristic { get; set; }
            internal int ForcedNeighborCount { get; set; } = int.MinValue;
            internal bool ReturnInvalidNeighbor { get; set; }

            internal void Add(int from, int to, long cost) => _neighbors[from - 1].Add(new PathNeighbor(Node(to), cost));

            public bool ContainsNode(PathNodeId node) => node.IsValid && node.Value <= _neighbors.Length;

            public int GetNeighborCount(PathNodeId node)
            {
                if (ForcedNeighborCount != int.MinValue) return ForcedNeighborCount;
                return _neighbors[node.Value - 1].Count;
            }

            public PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex)
            {
                if (ReturnInvalidNeighbor) return default(PathNeighbor);
                return _neighbors[node.Value - 1][neighborIndex];
            }

            public long EstimateCost(PathNodeId from, PathNodeId goal)
            {
                if (ThrowOnHeuristic) throw new Exception("Dijkstra must not call heuristic.");
                return Heuristic(from.Value, goal.Value);
            }
        }
    }
}
