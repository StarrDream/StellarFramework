using System;

namespace StellarFramework
{
    internal static class PathSearchRunner
    {
        internal static PathSearchResult Run(IPathGraph graph, PathSearchRequest request,
            Span<PathNodeId> destination, bool useHeuristic, PathSearchWorkspace workspace)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (request.MaxExpandedNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), request.MaxExpandedNodes,
                    "MaxExpandedNodes must be greater than zero.");
            }

            if (request.Start.IsInvalid)
            {
                return PathSearchResult.Failure(PathSearchStatus.InvalidStart, 0);
            }

            if (request.Goal.IsInvalid)
            {
                return PathSearchResult.Failure(PathSearchStatus.InvalidGoal, 0);
            }

            if (!graph.ContainsNode(request.Start))
            {
                return PathSearchResult.Failure(PathSearchStatus.StartNotFound, 0);
            }

            if (!graph.ContainsNode(request.Goal))
            {
                return PathSearchResult.Failure(PathSearchStatus.GoalNotFound, 0);
            }

            if (request.Start == request.Goal)
            {
                if (destination.Length < 1)
                {
                    return new PathSearchResult(PathSearchStatus.OutputBufferTooSmall, 0, 1, 0, 0);
                }

                destination[0] = request.Start;
                return new PathSearchResult(PathSearchStatus.Success, 1, 1, 0, 0);
            }

            workspace.Begin();
            long startHeuristic = 0;
            if (useHeuristic)
            {
                if (!TryReadHeuristic(graph, request.Start, request.Goal, out startHeuristic))
                {
                    return PathSearchResult.Failure(PathSearchStatus.CostOverflow, 0);
                }
            }

            int startIndex = workspace.AddRecord(new PathRecord
            {
                Node = request.Start,
                G = 0,
                H = startHeuristic,
                F = startHeuristic,
                ParentIndex = -1,
                State = PathRecordState.Open,
                OpenHeapIndex = -1
            });
            workspace.PushOpen(startIndex, useHeuristic);

            int expanded = 0;
            while (workspace.OpenCount > 0)
            {
                int currentIndex = workspace.PopOpen(useHeuristic);
                PathRecord current = workspace.GetRecord(currentIndex);
                if (current.Node == request.Goal)
                {
                    workspace.GetRecord(currentIndex).State = PathRecordState.Closed;
                    return BuildPathResult(workspace, currentIndex, destination, expanded);
                }

                if (expanded >= request.MaxExpandedNodes)
                {
                    return PathSearchResult.Failure(PathSearchStatus.ExpansionLimitReached, expanded);
                }

                workspace.GetRecord(currentIndex).State = PathRecordState.Closed;
                expanded++;

                int neighborCount = graph.GetNeighborCount(current.Node);
                if (neighborCount < 0)
                {
                    throw new InvalidOperationException("IPathGraph returned a negative neighbor count.");
                }

                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    PathNeighbor neighbor = graph.GetNeighbor(current.Node, neighborIndex);
                    ValidateNeighbor(graph, neighbor);

                    if (!TryAddCost(current.G, neighbor.Cost, out long newG))
                    {
                        return PathSearchResult.Failure(PathSearchStatus.CostOverflow, expanded);
                    }

                    if (workspace.TryGetRecordIndex(neighbor.Node, out int existingIndex))
                    {
                        PathRecord existing = workspace.GetRecord(existingIndex);
                        if (newG >= existing.G) continue;

                        if (!TryAddCost(newG, existing.H, out long newF))
                        {
                            return PathSearchResult.Failure(PathSearchStatus.CostOverflow, expanded);
                        }

                        ref PathRecord improved = ref workspace.GetRecord(existingIndex);
                        improved.G = newG;
                        improved.F = newF;
                        improved.ParentIndex = currentIndex;
                        if (existing.State == PathRecordState.Closed)
                        {
                            improved.State = PathRecordState.Open;
                            workspace.PushOpen(existingIndex, useHeuristic);
                        }
                        else
                        {
                            workspace.DecreaseOpenKey(existingIndex, useHeuristic);
                        }

                        continue;
                    }

                    long heuristic = 0;
                    if (useHeuristic && !TryReadHeuristic(graph, neighbor.Node, request.Goal, out heuristic))
                    {
                        return PathSearchResult.Failure(PathSearchStatus.CostOverflow, expanded);
                    }
                    if (!TryAddCost(newG, heuristic, out long f))
                    {
                        return PathSearchResult.Failure(PathSearchStatus.CostOverflow, expanded);
                    }

                    int discoveredIndex = workspace.AddRecord(new PathRecord
                    {
                        Node = neighbor.Node,
                        G = newG,
                        H = heuristic,
                        F = f,
                        ParentIndex = currentIndex,
                        State = PathRecordState.Open,
                        OpenHeapIndex = -1
                    });
                    workspace.PushOpen(discoveredIndex, useHeuristic);
                }
            }

            return PathSearchResult.Failure(PathSearchStatus.NoPath, expanded);
        }

        private static void ValidateNeighbor(IPathGraph graph, PathNeighbor neighbor)
        {
            if (!neighbor.Node.IsValid)
            {
                throw new InvalidOperationException("IPathGraph returned an invalid neighbor node.");
            }

            if (neighbor.Cost <= 0)
            {
                throw new InvalidOperationException("IPathGraph returned a non-positive neighbor cost.");
            }

            if (!graph.ContainsNode(neighbor.Node))
            {
                throw new InvalidOperationException("IPathGraph returned a neighbor outside the graph.");
            }
        }

        private static bool TryReadHeuristic(IPathGraph graph, PathNodeId from, PathNodeId goal, out long heuristic)
        {
            try
            {
                heuristic = graph.EstimateCost(from, goal);
            }
            catch (OverflowException)
            {
                heuristic = 0;
                return false;
            }

            if (heuristic < 0)
            {
                throw new InvalidOperationException("IPathGraph returned a negative heuristic.");
            }

            return true;
        }

        private static bool TryAddCost(long left, long right, out long result)
        {
            if (right > long.MaxValue - left)
            {
                result = 0;
                return false;
            }

            result = left + right;
            return true;
        }

        private static PathSearchResult BuildPathResult(PathSearchWorkspace workspace, int goalIndex,
            Span<PathNodeId> destination, int expanded)
        {
            int required = 0;
            int cursor = goalIndex;
            while (cursor >= 0)
            {
                required++;
                if (required > workspace.RecordCount)
                {
                    throw new InvalidOperationException("Path parent chain contains a cycle or invalid index.");
                }

                cursor = workspace.GetRecord(cursor).ParentIndex;
            }

            long totalCost = workspace.GetRecord(goalIndex).G;
            if (destination.Length < required)
            {
                return new PathSearchResult(PathSearchStatus.OutputBufferTooSmall, 0, required, totalCost, expanded);
            }

            int writeIndex = required - 1;
            cursor = goalIndex;
            while (cursor >= 0)
            {
                destination[writeIndex--] = workspace.GetRecord(cursor).Node;
                cursor = workspace.GetRecord(cursor).ParentIndex;
            }

            return new PathSearchResult(PathSearchStatus.Success, required, required, totalCost, expanded);
        }
    }
}
