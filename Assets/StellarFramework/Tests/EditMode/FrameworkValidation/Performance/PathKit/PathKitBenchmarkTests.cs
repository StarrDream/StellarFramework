using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>PathKit trend benchmarks; no fixed millisecond threshold is imposed.</summary>
    public sealed class PathKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void PathKitCoreBenchmark_RectangularScalesAndAlgorithms()
        {
            int[] scales = { 64, 256, 512 };
            for (int i = 0; i < scales.Length; i++)
            {
                int scale = scales[i];
                RectGraph graph = new RectGraph(scale, scale);
                AStarPathfinder aStar = new AStarPathfinder(scale * 2);
                DijkstraPathfinder dijkstra = new DijkstraPathfinder(scale * 2);
                PathNodeId start = new PathNodeId(1);
                PathNodeId goal = new PathNodeId(scale * scale);
                PathNodeId[] buffer = new PathNodeId[scale * 2 + 2];

                Stopwatch aWatch = Stopwatch.StartNew();
                PathSearchResult a = aStar.FindPath(graph, new PathSearchRequest(start, goal, int.MaxValue), buffer.AsSpan());
                aWatch.Stop();
                long aChecksum = Checksum(buffer, a.WrittenCount);

                Array.Clear(buffer, 0, buffer.Length);
                Stopwatch dWatch = Stopwatch.StartNew();
                PathSearchResult d = dijkstra.FindPath(graph, new PathSearchRequest(start, goal, int.MaxValue), buffer.AsSpan());
                dWatch.Stop();
                long dChecksum = Checksum(buffer, d.WrittenCount);

                Assert.That(a.Status, Is.EqualTo(PathSearchStatus.Success));
                Assert.That(d.Status, Is.EqualTo(PathSearchStatus.Success));
                Assert.That(a.TotalCost, Is.EqualTo(d.TotalCost));
                Assert.That(a.WrittenCount, Is.EqualTo(d.WrittenCount));
                string message = string.Format(
                    "PathKit Core scale={0}x{0} env={1} AStarMs={2:F3} AStarExpanded={3} DijkstraMs={4:F3} DijkstraExpanded={5} PathLength={6} Cost={7} HeapTrend={8}/{9} Checksums={10}/{11}",
                    scale, Application.unityVersion, aWatch.Elapsed.TotalMilliseconds, a.ExpandedNodeCount,
                    dWatch.Elapsed.TotalMilliseconds, d.ExpandedNodeCount, a.WrittenCount, a.TotalCost,
                    a.ExpandedNodeCount, d.ExpandedNodeCount, aChecksum, dChecksum);
                TestContext.Progress.WriteLine(message);
                UnityEngine.Debug.Log(message);
            }
        }

        [Test, Category("Benchmark")]
        public void PathKitCoreBenchmark_Repeated1000AndNoPathBarrierStress()
        {
            const int scale = 64;
            RectGraph graph = new RectGraph(scale, scale);
            AStarPathfinder aStar = new AStarPathfinder(scale * 2);
            DijkstraPathfinder dijkstra = new DijkstraPathfinder(scale * 2);
            PathNodeId start = new PathNodeId(1);
            PathNodeId goal = new PathNodeId(scale * scale);
            PathNodeId[] buffer = new PathNodeId[scale * 2 + 2];

            aStar.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan());
            dijkstra.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan());
            long allocatedBefore = GC.GetTotalMemory(false);
            long checksum = 0;
            Stopwatch aWatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                PathSearchResult result = aStar.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan());
                checksum += result.TotalCost + result.WrittenCount + result.ExpandedNodeCount;
            }
            aWatch.Stop();
            Stopwatch dWatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                PathSearchResult result = dijkstra.FindPath(graph, new PathSearchRequest(start, goal), buffer.AsSpan());
                checksum += result.TotalCost + result.WrittenCount + result.ExpandedNodeCount;
            }
            dWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            const int noPathScale = 256;
            const int barrierColumn = noPathScale / 2;
            RectGraph noPathGraph = new RectGraph(noPathScale, noPathScale, barrierColumn);
            PathNodeId noPathStart = new PathNodeId(1);
            PathNodeId noPathGoal = new PathNodeId(noPathScale * noPathScale);
            PathSearchRequest noPathRequest = new PathSearchRequest(
                noPathStart, noPathGoal, noPathScale * noPathScale);
            PathNodeId[] noPathBuffer = new PathNodeId[noPathScale * 2 + 2];
            AStarPathfinder noPathAStar = new AStarPathfinder(noPathScale * noPathScale / 2);
            DijkstraPathfinder noPathDijkstra = new DijkstraPathfinder(noPathScale * noPathScale / 2);

            // Warm both reusable workspaces before timing the real barrier traversal.
            PathSearchResult warmupAStar = noPathAStar.FindPath(noPathGraph, noPathRequest, noPathBuffer.AsSpan());
            PathSearchResult warmupDijkstra = noPathDijkstra.FindPath(noPathGraph, noPathRequest, noPathBuffer.AsSpan());
            Assert.That(warmupAStar.Status, Is.EqualTo(PathSearchStatus.NoPath));
            Assert.That(warmupDijkstra.Status, Is.EqualTo(PathSearchStatus.NoPath));

            Stopwatch noPathAStarWatch = Stopwatch.StartNew();
            PathSearchResult noPathAStarResult = noPathAStar.FindPath(
                noPathGraph, noPathRequest, noPathBuffer.AsSpan());
            noPathAStarWatch.Stop();
            Stopwatch noPathDijkstraWatch = Stopwatch.StartNew();
            PathSearchResult noPathDijkstraResult = noPathDijkstra.FindPath(
                noPathGraph, noPathRequest, noPathBuffer.AsSpan());
            noPathDijkstraWatch.Stop();

            Assert.That(noPathAStarResult.Status, Is.EqualTo(PathSearchStatus.NoPath));
            Assert.That(noPathAStarResult.WrittenCount, Is.EqualTo(0));
            Assert.That(noPathAStarResult.ExpandedNodeCount, Is.GreaterThan(1));
            Assert.That(noPathDijkstraResult.Status, Is.EqualTo(PathSearchStatus.NoPath));
            Assert.That(noPathDijkstraResult.WrittenCount, Is.EqualTo(0));
            Assert.That(noPathDijkstraResult.ExpandedNodeCount, Is.GreaterThan(1));
            string message = string.Format(
                "PathKit Core repeated env={0} scale={1} Iterations=1000 AStarMs={2:F3} DijkstraMs={3:F3} NoPathWorkload=BarrierRectGraph NoPathScale={4}x{4} BarrierColumn={5} NoPathAStarMs={6:F3} NoPathAStarExpanded={7} NoPathDijkstraMs={8:F3} NoPathDijkstraExpanded={9} NoPathWritten={10}/{11} Checksum={12} ManagedHeapDelta={13}",
                Application.unityVersion, scale, aWatch.Elapsed.TotalMilliseconds, dWatch.Elapsed.TotalMilliseconds,
                noPathScale, barrierColumn, noPathAStarWatch.Elapsed.TotalMilliseconds,
                noPathAStarResult.ExpandedNodeCount, noPathDijkstraWatch.Elapsed.TotalMilliseconds,
                noPathDijkstraResult.ExpandedNodeCount, noPathAStarResult.WrittenCount,
                noPathDijkstraResult.WrittenCount, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        [Test, Category("Benchmark")]
        public void PathKitCoreBenchmark_OneMillionLogicalNodesBoundedAStar()
        {
            const int width = 1000;
            const int height = 1000;
            RectGraph graph = new RectGraph(width, height);
            PathNodeId[] buffer = new PathNodeId[width + height + 2];
            long allocatedBefore = GC.GetTotalMemory(false);
            Stopwatch watch = Stopwatch.StartNew();
            PathSearchResult result = new AStarPathfinder(width + height).FindPath(graph,
                new PathSearchRequest(new PathNodeId(1), new PathNodeId(width * height), 100000), buffer.AsSpan());
            watch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Success));
            string message = string.Format(
                "PathKit Core logical=1M ({0}x{1}) env={2} AStarMs={3:F3} Expanded={4} PathLength={5} Cost={6} HeapTrend={7} Checksum={8} ManagedHeapDelta={9}",
                width, height, Application.unityVersion, watch.Elapsed.TotalMilliseconds, result.ExpandedNodeCount,
                result.WrittenCount, result.TotalCost, result.ExpandedNodeCount, Checksum(buffer, result.WrittenCount), allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        [Test, Category("Benchmark")]
        public void PathKitGridAdapterBenchmark_256FourAndEightWay()
        {
            const int scale = 256;
            GridRect bounds = new GridRect(new GridCoord(-scale / 2, -scale / 2), new GridSize(scale, scale));
            BenchmarkGridPolicy policy = new BenchmarkGridPolicy(bounds);
            PathNodeId[] buffer = new PathNodeId[scale * 2 + 2];
            GridPathGraph four = new GridPathGraph(bounds, policy, GridPathNeighborMode.FourWay);
            GridPathGraph eight = new GridPathGraph(bounds, policy, GridPathNeighborMode.EightWay);
            PathNodeId start = four.TryGetNodeId(bounds.Min, out PathNodeId startId) ? startId : default(PathNodeId);
            GridCoord goalCoord = new GridCoord((int)bounds.MaxExclusiveX - 1, (int)bounds.MaxExclusiveY - 1);
            PathNodeId goal = four.TryGetNodeId(goalCoord, out PathNodeId goalId) ? goalId : default(PathNodeId);
            AStarPathfinder aStar = new AStarPathfinder(scale * 2);
            DijkstraPathfinder dijkstra = new DijkstraPathfinder(scale * 2);

            Stopwatch fourWatch = Stopwatch.StartNew();
            PathSearchResult fourResult = aStar.FindPath(four, new PathSearchRequest(start, goal, 200000), buffer.AsSpan());
            fourWatch.Stop();
            Array.Clear(buffer, 0, buffer.Length);
            Stopwatch eightWatch = Stopwatch.StartNew();
            PathSearchResult eightResult = dijkstra.FindPath(eight, new PathSearchRequest(start, goal, 200000), buffer.AsSpan());
            eightWatch.Stop();
            Assert.That(fourResult.Status, Is.EqualTo(PathSearchStatus.Success));
            Assert.That(eightResult.Status, Is.EqualTo(PathSearchStatus.Success));
            string message = string.Format(
                "PathKit GridAdapter scale={0}x{0} env={1} FourWayAStarMs={2:F3} FourWayExpanded={3} EightWayDijkstraMs={4:F3} EightWayExpanded={5} Cost={6} PathLength={7} HeapTrend={8}/{9} Checksum={10}",
                scale, Application.unityVersion, fourWatch.Elapsed.TotalMilliseconds, fourResult.ExpandedNodeCount,
                eightWatch.Elapsed.TotalMilliseconds, eightResult.ExpandedNodeCount, fourResult.TotalCost,
                fourResult.WrittenCount, fourResult.ExpandedNodeCount, eightResult.ExpandedNodeCount,
                Checksum(buffer, eightResult.WrittenCount));
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        private static long Checksum(PathNodeId[] path, int count)
        {
            long checksum = 0;
            for (int i = 0; i < count; i++) checksum = unchecked(checksum * 31 + path[i].Value);
            return checksum;
        }

        private sealed class RectGraph : IPathGraph
        {
            private readonly int _width;
            private readonly int _height;
            private readonly int _barrierColumn;

            internal RectGraph(int width, int height, int barrierColumn = -1)
            {
                if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
                if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
                if (barrierColumn < -1 || barrierColumn >= width)
                {
                    throw new ArgumentOutOfRangeException(nameof(barrierColumn));
                }

                _width = width;
                _height = height;
                _barrierColumn = barrierColumn;
            }

            public bool ContainsNode(PathNodeId node) => node.IsValid && node.Value <= _width * _height;

            public int GetNeighborCount(PathNodeId node)
            {
                if (!ContainsNode(node)) return 0;
                int index = node.Value - 1;
                if (IsBlocked(index)) return 0;

                int count = 0;
                for (int neighborIndex = 0; neighborIndex < 2; neighborIndex++)
                {
                    if (!IsBlocked(GetCandidateIndex(index, neighborIndex))) count++;
                }

                return count;
            }

            public PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex)
            {
                if (!ContainsNode(node)) throw new ArgumentOutOfRangeException(nameof(node));
                int index = node.Value - 1;
                if (IsBlocked(index)) throw new InvalidOperationException("Blocked node has no outgoing edges.");
                int visibleNeighbor = 0;
                for (int candidate = 0; candidate < 2; candidate++)
                {
                    int candidateIndex = GetCandidateIndex(index, candidate);
                    if (IsBlocked(candidateIndex)) continue;
                    if (visibleNeighbor++ == neighborIndex)
                    {
                        return new PathNeighbor(new PathNodeId(candidateIndex + 1), 1);
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(neighborIndex));
            }

            private int GetCandidateIndex(int index, int neighborIndex)
            {
                int x = index % _width;
                int y = index / _width;
                if (neighborIndex == 0)
                {
                    return x + 1 < _width ? index + 1 : index + (_width > 1 ? -1 : 0);
                }

                if (neighborIndex == 1)
                {
                    return y + 1 < _height ? index + _width : index - _width;
                }

                throw new ArgumentOutOfRangeException(nameof(neighborIndex));
            }

            private bool IsBlocked(int index)
            {
                return _barrierColumn >= 0 && index % _width == _barrierColumn;
            }

            public long EstimateCost(PathNodeId from, PathNodeId goal)
            {
                int fromIndex = from.Value - 1;
                int goalIndex = goal.Value - 1;
                return Math.Abs(fromIndex % _width - goalIndex % _width) +
                       Math.Abs(fromIndex / _width - goalIndex / _width);
            }
        }

        private sealed class BenchmarkGridPolicy : IGridPathTraversalPolicy
        {
            private readonly GridRect _bounds;
            internal BenchmarkGridPolicy(GridRect bounds) { _bounds = bounds; }
            public long MinimumOrthogonalCost => 1;
            public long MinimumDiagonalCost => 2;
            public bool IsWalkable(GridCoord coord) => _bounds.Contains(coord);
            public bool CanTraverse(GridCoord from, GridCoord to) => true;
            public long GetTraversalCost(GridCoord from, GridCoord to) => from.X != to.X && from.Y != to.Y ? 2 : 1;
        }
    }
}
