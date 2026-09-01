using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// Core-only PathKit sample. The graph is deliberately not a grid: it has weighted,
    /// directed edges, two equal-cost alternatives and a disconnected node.
    /// </summary>
    public sealed class Example_PathKit : MonoBehaviour
    {
        private sealed class SampleGraph : IPathGraph
        {
            private readonly PathNodeId[] _nodes;
            private readonly Vector3[] _positions;
            private readonly PathNeighbor[][] _neighbors;

            internal SampleGraph()
            {
                _nodes = new PathNodeId[16];
                _positions = new Vector3[16];
                _neighbors = new PathNeighbor[16][];
                for (int i = 0; i < _nodes.Length; i++)
                {
                    _nodes[i] = new PathNodeId(i + 1);
                    _neighbors[i] = Array.Empty<PathNeighbor>();
                }

                // Two equal-cost routes (1-2-4-8-12 and 1-3-5-9-12) make tie ordering visible.
                _neighbors[0] = new[] { new PathNeighbor(_nodes[1], 2), new PathNeighbor(_nodes[2], 2) };
                _neighbors[1] = new[] { new PathNeighbor(_nodes[3], 2), new PathNeighbor(_nodes[4], 4) };
                _neighbors[2] = new[] { new PathNeighbor(_nodes[4], 2), new PathNeighbor(_nodes[5], 5) };
                _neighbors[3] = new[] { new PathNeighbor(_nodes[6], 2), new PathNeighbor(_nodes[7], 10) };
                _neighbors[4] = new[] { new PathNeighbor(_nodes[8], 2), new PathNeighbor(_nodes[7], 1) };
                _neighbors[5] = new[] { new PathNeighbor(_nodes[8], 2) };
                _neighbors[6] = new[] { new PathNeighbor(_nodes[10], 2) };
                _neighbors[7] = new[] { new PathNeighbor(_nodes[10], 2), new PathNeighbor(_nodes[11], 2) };
                _neighbors[8] = new[] { new PathNeighbor(_nodes[11], 2) };
                _neighbors[9] = new[] { new PathNeighbor(_nodes[10], 1) };
                _neighbors[10] = new[] { new PathNeighbor(_nodes[11], 1) };

                for (int i = 0; i < _positions.Length; i++)
                {
                    int row = i / 4;
                    int column = i % 4;
                    _positions[i] = new Vector3(column * 1.8f - 2.7f, (3 - row) * 1.3f - 1.2f, 0f);
                }
            }

            internal int Count => _nodes.Length;
            internal PathNodeId GetNode(int index) => _nodes[index];
            internal Vector3 GetPosition(PathNodeId node) => _positions[node.Value - 1];

            public bool ContainsNode(PathNodeId node) => node.IsValid && node.Value <= _nodes.Length;

            public int GetNeighborCount(PathNodeId node)
            {
                return ContainsNode(node) ? _neighbors[node.Value - 1].Length : 0;
            }

            public PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex)
            {
                if (!ContainsNode(node)) throw new ArgumentOutOfRangeException(nameof(node));
                return _neighbors[node.Value - 1][neighborIndex];
            }

            public long EstimateCost(PathNodeId from, PathNodeId goal)
            {
                if (!ContainsNode(from) || !ContainsNode(goal)) return 0;
                Vector3 delta = _positions[from.Value - 1] - _positions[goal.Value - 1];
                return (long)(Mathf.Abs(delta.x) + Mathf.Abs(delta.y));
            }
        }

        private readonly PathNodeId[] _pathBuffer = new PathNodeId[32];
        private readonly SampleGraph _graph = new SampleGraph();
        private AStarPathfinder _aStar;
        private DijkstraPathfinder _dijkstra;
        private PathSearchResult _result;
        private PathNodeId _start;
        private PathNodeId _goal;
        private bool _useAStar = true;
        private bool _showEdges = true;
        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;

        private void Awake()
        {
            _aStar = new AStarPathfinder(32);
            _dijkstra = new DijkstraPathfinder(32);
            _start = _graph.GetNode(0);
            _goal = _graph.GetNode(11);
            ResetSearch();
        }

        private void ResetSearch()
        {
            Array.Clear(_pathBuffer, 0, _pathBuffer.Length);
            _result = default(PathSearchResult);
        }

        private void SelectStart(int delta)
        {
            int index = (_start.Value - 1 + delta + _graph.Count) % _graph.Count;
            _start = _graph.GetNode(index);
            ResetSearch();
        }

        private void SelectGoal(int delta)
        {
            int index = (_goal.Value - 1 + delta + _graph.Count) % _graph.Count;
            _goal = _graph.GetNode(index);
            ResetSearch();
        }

        private void FindPath(bool aStar)
        {
            _useAStar = aStar;
            IPathfinder pathfinder = aStar ? (IPathfinder)_aStar : _dijkstra;
            _result = pathfinder.FindPath(_graph,
                new PathSearchRequest(_start, _goal, 256), _pathBuffer.AsSpan());
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 430f, 660f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("PathKit / Graph-first Playable", _titleStyle);
            GUILayout.Label("Core only：自定义有向图、加权边、A* / Dijkstra 与原子路径输出。", _bodyStyle);

            GUILayout.Label("REQUEST", _sectionStyle);
            GUILayout.Label(string.Format("Start: {0}    Goal: {1}\nAlgorithm: {2}",
                _start, _goal, _useAStar ? "A*" : "Dijkstra"), _bodyStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start -")) SelectStart(-1);
                if (GUILayout.Button("Start +")) SelectStart(1);
                if (GUILayout.Button("Goal -")) SelectGoal(-1);
                if (GUILayout.Button("Goal +")) SelectGoal(1);
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run A*", GUILayout.Height(28f))) FindPath(true);
                if (GUILayout.Button("Run Dijkstra", GUILayout.Height(28f))) FindPath(false);
                if (GUILayout.Button("Reset", GUILayout.Height(28f))) ResetSearch();
            }
            if (GUILayout.Button(_showEdges ? "Hide Edge Overlay" : "Show Edge Overlay")) _showEdges = !_showEdges;

            GUILayout.Label("RESULT", _sectionStyle);
            GUILayout.Label(_result.Status == 0
                ? "尚未执行搜索。"
                : string.Format("Status: {0}\nCost: {1}\nWritten / Required: {2} / {3}\nExpanded: {4}\nPath: {5}",
                    _result.Status, _result.TotalCost, _result.WrittenCount, _result.RequiredNodeCount,
                    _result.ExpandedNodeCount, FormatPath()), _bodyStyle);

            GUILayout.Label("PATH HIGHLIGHT", _sectionStyle);
            DrawPathNodes();

            GUILayout.Label("GRAPH NOTES", _sectionStyle);
            GUILayout.Label("节点 16 是 disconnected；节点 1 到 12 有两条相同成本的候选路径。重复运行可以观察稳定的 NodeId tie-break。", _bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string FormatPath()
        {
            if (_result.WrittenCount == 0) return "<none>";
            string text = _pathBuffer[0].ToString();
            for (int i = 1; i < _result.WrittenCount; i++) text += " -> " + _pathBuffer[i];
            return text;
        }

        private void DrawPathNodes()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            for (int row = 0; row < 4; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < 4; column++)
                {
                    PathNodeId node = _graph.GetNode(row * 4 + column);
                    Color previous = GUI.color;
                    GUI.color = IsOnPath(node) ? new Color(1f, 0.82f, 0.2f) :
                        node == _start ? new Color(0.35f, 1f, 0.45f) :
                        node == _goal ? new Color(1f, 0.4f, 0.4f) : Color.white;
                    GUILayout.Label(node.ToString(), _bodyStyle, GUILayout.Width(48f));
                    GUI.color = previous;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private bool IsOnPath(PathNodeId node)
        {
            for (int i = 0; i < _result.WrittenCount; i++)
            {
                if (_pathBuffer[i] == node) return true;
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (_graph == null) return;
            if (_showEdges)
            {
                Gizmos.color = new Color(0.25f, 0.35f, 0.5f, 0.65f);
                for (int i = 0; i < _graph.Count; i++)
                {
                    PathNodeId node = _graph.GetNode(i);
                    for (int n = 0; n < _graph.GetNeighborCount(node); n++)
                    {
                        PathNeighbor neighbor = _graph.GetNeighbor(node, n);
                        Gizmos.DrawLine(_graph.GetPosition(node), _graph.GetPosition(neighbor.Node));
                    }
                }
            }

            for (int i = 0; i < _graph.Count; i++)
            {
                PathNodeId node = _graph.GetNode(i);
                Gizmos.color = node == _start ? Color.green : node == _goal ? Color.red : Color.white;
                Gizmos.DrawSphere(_graph.GetPosition(node), 0.14f);
            }

            if (_result.Success)
            {
                Gizmos.color = Color.yellow;
                for (int i = 1; i < _result.WrittenCount; i++)
                {
                    Gizmos.DrawLine(_graph.GetPosition(_pathBuffer[i - 1]), _graph.GetPosition(_pathBuffer[i]));
                }
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
    }
}
