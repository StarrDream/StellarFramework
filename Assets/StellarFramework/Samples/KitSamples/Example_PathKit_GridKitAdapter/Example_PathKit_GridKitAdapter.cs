using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>GridKit adapter sample with negative bounds, dynamic cells and weighted terrain.</summary>
    public sealed class Example_PathKit_GridKitAdapter : MonoBehaviour
    {
        private sealed class SampleTraversalPolicy : IGridPathTraversalPolicy
        {
            private readonly GridRect _bounds;
            private readonly bool[] _walkable;
            private readonly int[] _cost;

            internal SampleTraversalPolicy(GridRect bounds)
            {
                _bounds = bounds;
                _walkable = new bool[(int)bounds.Area];
                _cost = new int[(int)bounds.Area];
                for (int i = 0; i < _walkable.Length; i++)
                {
                    _walkable[i] = true;
                    _cost[i] = 500;
                }

                // A corner scenario: the diagonal gap is closed by two side cells.
                SetWalkable(new GridCoord(-1, 0), false);
                SetWalkable(new GridCoord(0, -1), false);
                // Mud near the direct route; the farther road is cheaper in total cost.
                SetCost(new GridCoord(0, 2), 5000);
                SetCost(new GridCoord(1, 2), 5000);
                SetCost(new GridCoord(2, 2), 5000);
            }

            public long MinimumOrthogonalCost => 500;
            public long MinimumDiagonalCost => 700;

            internal bool GetWalkable(GridCoord coord) => _bounds.Contains(coord) && _walkable[GetIndex(coord)];
            internal int GetCost(GridCoord coord) => _bounds.Contains(coord) ? _cost[GetIndex(coord)] : 0;

            private void SetWalkable(GridCoord coord, bool value)
            {
                if (_bounds.Contains(coord)) _walkable[GetIndex(coord)] = value;
            }

            private void SetCost(GridCoord coord, int value)
            {
                if (_bounds.Contains(coord)) _cost[GetIndex(coord)] = value;
            }

            public bool IsWalkable(GridCoord coord) => GetWalkable(coord);

            public bool CanTraverse(GridCoord from, GridCoord to)
            {
                // The sample keeps the edge rule open; a production policy can combine
                // occupancy, one-way doors and terrain here.
                return _bounds.Contains(from) && _bounds.Contains(to);
            }

            public long GetTraversalCost(GridCoord from, GridCoord to)
            {
                bool diagonal = from.X != to.X && from.Y != to.Y;
                int baseCost = diagonal ? 700 : 500;
                int terrainCost = GetCost(to);
                return Math.Max(baseCost, terrainCost);
            }

            internal void ToggleWalkable(GridCoord coord)
            {
                if (_bounds.Contains(coord)) _walkable[GetIndex(coord)] = !_walkable[GetIndex(coord)];
            }

            internal void ToggleCost(GridCoord coord)
            {
                if (_bounds.Contains(coord))
                {
                    int index = GetIndex(coord);
                    _cost[index] = _cost[index] >= 5000 ? 500 : 5000;
                }
            }

            private int GetIndex(GridCoord coord)
            {
                long localX = (long)coord.X - _bounds.Min.X;
                long localY = (long)coord.Y - _bounds.Min.Y;
                return checked((int)(localY * _bounds.Size.Width + localX));
            }
        }

        private const int CellWidth = 34;
        private readonly PathNodeId[] _pathBuffer = new PathNodeId[128];
        private readonly GridRect _bounds = new GridRect(new GridCoord(-6, -4), new GridSize(12, 8));
        private SampleTraversalPolicy _policy;
        private GridPathGraph _graph;
        private AStarPathfinder _aStar;
        private DijkstraPathfinder _dijkstra;
        private PathSearchResult _result;
        private GridCoord _start = new GridCoord(-5, -3);
        private GridCoord _goal = new GridCoord(5, 3);
        private GridCoord _selected = new GridCoord(0, 0);
        private bool _eightWay;
        private bool _allowCornerCut;
        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _cellStyle;

        private void Awake()
        {
            _aStar = new AStarPathfinder(128);
            _dijkstra = new DijkstraPathfinder(128);
            _policy = new SampleTraversalPolicy(_bounds);
            RebuildGraph();
        }

        private void RebuildGraph()
        {
            _graph = new GridPathGraph(_bounds, _policy,
                _eightWay ? GridPathNeighborMode.EightWay : GridPathNeighborMode.FourWay,
                _allowCornerCut ? GridPathDiagonalPolicy.AllowCornerCut : GridPathDiagonalPolicy.NoCornerCut);
            Array.Clear(_pathBuffer, 0, _pathBuffer.Length);
            _result = default(PathSearchResult);
        }

        private void RunSearch(bool useAStar)
        {
            if (!_graph.TryGetNodeId(_start, out PathNodeId start) || !_graph.TryGetNodeId(_goal, out PathNodeId goal))
            {
                _result = default(PathSearchResult);
                return;
            }

            IPathfinder pathfinder = useAStar ? (IPathfinder)_aStar : _dijkstra;
            _result = pathfinder.FindPath(_graph, new PathSearchRequest(start, goal, 4096), _pathBuffer.AsSpan());
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 820f, 720f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("PathKit / GridKit Adapter Playable", _titleStyle);
            GUILayout.Label("GridPathGraph + TraversalPolicy：负坐标、阻挡、权重、四/八方向与转角规则。", _bodyStyle);

            GUILayout.Label("GRID / SELECTED CELL", _sectionStyle);
            GUILayout.Label(string.Format("Bounds Min={0}, Size={1}\nStart={2}  Goal={3}  Selected={4}",
                _bounds.Min, _bounds.Size, _start, _goal, _selected), _bodyStyle);
            DrawGrid();
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Selected as Start")) { _start = _selected; RebuildGraph(); }
                if (GUILayout.Button("Set Selected as Goal")) { _goal = _selected; RebuildGraph(); }
                if (GUILayout.Button("Toggle Blocked")) { _policy.ToggleWalkable(_selected); RebuildGraph(); }
                if (GUILayout.Button("Toggle Mud 5000")) { _policy.ToggleCost(_selected); RebuildGraph(); }
            }

            GUILayout.Label("SEARCH POLICY", _sectionStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_eightWay ? "EightWay ✓" : "FourWay")) { _eightWay = !_eightWay; RebuildGraph(); }
                if (GUILayout.Button(_allowCornerCut ? "AllowCornerCut ✓" : "NoCornerCut")) { _allowCornerCut = !_allowCornerCut; RebuildGraph(); }
                if (GUILayout.Button("Run A*", GUILayout.Height(28f))) RunSearch(true);
                if (GUILayout.Button("Run Dijkstra", GUILayout.Height(28f))) RunSearch(false);
                if (GUILayout.Button("Reset", GUILayout.Height(28f))) RebuildGraph();
            }

            GUILayout.Label("RESULT", _sectionStyle);
            GUILayout.Label(_result.Status == PathSearchStatus.None
                ? "尚未执行搜索。"
                : string.Format("Status: {0}\nCost: {1}\nWritten / Required: {2} / {3}\nExpanded: {4}\nPath: {5}",
                    _result.Status, _result.TotalCost, _result.WrittenCount, _result.RequiredNodeCount,
                    _result.ExpandedNodeCount, FormatPath()), _bodyStyle);
            GUILayout.Label("试验建议：切换 NoCornerCut / AllowCornerCut，再把泥地成本改为 5000，观察路线选择和总成本。", _bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGrid()
        {
            for (int y = checked((int)_bounds.MaxExclusiveY - 1); y >= _bounds.Min.Y; y--)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(y.ToString("+0;-0;0"), _cellStyle, GUILayout.Width(32f));
                    for (int x = _bounds.Min.X; x < _bounds.MaxExclusiveX; x++)
                    {
                        GridCoord coord = new GridCoord(x, y);
                        string text = !_policy.GetWalkable(coord) ? "#" : _policy.GetCost(coord) >= 5000 ? "M" : ".";
                        if (coord == _start) text = "S";
                        if (coord == _goal) text = "G";
                        if (IsOnPath(coord)) text = "*";
                        if (coord == _selected) text = "[" + text + "]";
                        if (GUILayout.Button(text, _cellStyle, GUILayout.Width(CellWidth), GUILayout.Height(24f))) _selected = coord;
                    }
                }
            }
            GUILayout.Label("# blocked   M mud cost 5000   . road cost 500   * path", _bodyStyle);
        }

        private bool IsOnPath(GridCoord coord)
        {
            if (!_result.Success) return false;
            for (int i = 0; i < _result.WrittenCount; i++)
            {
                if (_graph.TryGetCoord(_pathBuffer[i], out GridCoord pathCoord) && pathCoord == coord) return true;
            }

            return false;
        }

        private string FormatPath()
        {
            if (_result.WrittenCount == 0) return "<none>";
            string text = string.Empty;
            for (int i = 0; i < _result.WrittenCount; i++)
            {
                if (_graph.TryGetCoord(_pathBuffer[i], out GridCoord coord))
                {
                    if (text.Length > 0) text += " -> ";
                    text += coord;
                }
            }

            return text;
        }

        private void OnDrawGizmos()
        {
            if (_graph == null || _policy == null) return;
            for (int y = _bounds.Min.Y; y < _bounds.MaxExclusiveY; y++)
            {
                for (int x = _bounds.Min.X; x < _bounds.MaxExclusiveX; x++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    Gizmos.color = !_policy.GetWalkable(coord) ? new Color(0.15f, 0.15f, 0.15f) :
                        _policy.GetCost(coord) >= 5000 ? new Color(0.65f, 0.3f, 0.12f) : new Color(0.2f, 0.35f, 0.5f);
                    Gizmos.DrawCube(new Vector3(x, y, 0f), new Vector3(0.9f, 0.9f, 0.08f));
                }
            }

            if (_result.Success)
            {
                Gizmos.color = Color.yellow;
                for (int i = 1; i < _result.WrittenCount; i++)
                {
                    if (_graph.TryGetCoord(_pathBuffer[i - 1], out GridCoord a) &&
                        _graph.TryGetCoord(_pathBuffer[i], out GridCoord b))
                    {
                        Gizmos.DrawLine(new Vector3(a.X, a.Y, -0.1f), new Vector3(b.X, b.Y, -0.1f));
                    }
                }
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _cellStyle = new GUIStyle(GUI.skin.button) { fontSize = 10, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(1, 1, 1, 1) };
        }
    }
}
