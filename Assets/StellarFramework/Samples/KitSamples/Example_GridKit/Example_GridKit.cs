using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// GridKit 的可运行样例：负坐标 DenseGrid、邻居查询、Footprint 变换和
    /// GridOccupancy 的两遍原子占用/释放。
    ///
    /// 场景：Scenes/GridKit_Playable.unity
    /// 通过标准：切换选中格、旋转 Footprint，并让 B 在 A 的位置尝试占用；失败时
    /// 冲突格会被标出，A 的占用不会被部分写入。
    /// </summary>
    public sealed class Example_GridKit : MonoBehaviour
    {
        private const int GridWidth = 12;
        private const int GridHeight = 8;
        private const int CellButtonWidth = 42;

        private GridRect _bounds;
        private DenseGrid<int> _denseGrid;
        private GridOccupancy _occupancy;
        private GridFootprint _footprint;
        private GridTransform _transform;
        private GridCoord _selected;
        private readonly GridCoord[] _neighborBuffer = new GridCoord[8];
        private readonly GridCoord[] _footprintBuffer = new GridCoord[16];
        private int _neighborCount;
        private int _footprintCellCount;
        private GridOccupancyResult _lastResult;
        private string _lastOperation = "尚未执行占用操作。";
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _cellStyle;
        private Vector2 _scroll;
        private bool _reflectX;
        private bool _reflectY;
        private bool _initialized;

        private void Awake()
        {
            InitializeSample();
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                InitializeSample();
            }
        }

        private void InitializeSample()
        {
            _bounds = new GridRect(new GridCoord(-6, -4), new GridSize(GridWidth, GridHeight));
            _denseGrid = new DenseGrid<int>(_bounds);
            for (int index = 0; index < _denseGrid.Count; index++)
            {
                _denseGrid.GetRefByIndex(index) = index;
            }

            _occupancy = new GridOccupancy(_bounds);
            _footprint = new GridFootprint(
                new GridOffset(0, 0),
                new GridOffset(1, 0),
                new GridOffset(0, 1));
            _transform = GridTransform.Identity;
            _selected = new GridCoord(0, 0);
            _lastResult = GridOccupancyResult.Succeeded();
            _lastOperation = "已初始化 12 x 8 的负坐标网格。";
            _reflectX = false;
            _reflectY = false;
            _initialized = true;
            RefreshDerivedData();
        }

        private void RefreshDerivedData()
        {
            _neighborCount = GridNeighbors.WriteNeighbors4(_selected, _bounds, _neighborBuffer.AsSpan());
            _footprintCellCount = _footprint.TryWriteCells(
                _selected, _transform, _footprintBuffer.AsSpan(), out int written) ? written : 0;
        }

        private void SelectCell(GridCoord coord)
        {
            _selected = coord;
            RefreshDerivedData();
        }

        private void SetRotation(GridRotation rotation)
        {
            _transform = new GridTransform(rotation, _reflectX, _reflectY);
            RefreshDerivedData();
        }

        private void ToggleReflectX()
        {
            _reflectX = !_reflectX;
            SetRotation(_transform.Rotation);
        }

        private void ToggleReflectY()
        {
            _reflectY = !_reflectY;
            SetRotation(_transform.Rotation);
        }

        private void TryOccupy(int id, string label)
        {
            GridOccupantId occupant = new GridOccupantId(id);
            _lastResult = _occupancy.TryOccupy(occupant, _selected, _footprint, _transform);
            _lastOperation = string.Format("{0} @ {1}: {2}", label, _selected, _lastResult);
        }

        private void Release(int id)
        {
            GridOccupantId occupant = new GridOccupantId(id);
            _lastResult = _occupancy.TryRelease(occupant, _selected, _footprint, _transform);
            _lastOperation = string.Format("Release {0} @ {1}: {2}", id, _selected, _lastResult);
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 810f, 900f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("GridKit / Playable Sample", _titleStyle);
            GUILayout.Label("Geometry + DenseGrid + Footprint + 原子 Occupancy（无 Addressables / HybridCLR 依赖）", _bodyStyle);

            DrawSection("BOUNDS / COORDINATE", string.Format(
                "Bounds: Min={0}, Size={1}  MaxExclusive=({2}, {3})  Area={4}\nSelected: {5}  Index: {6}",
                _bounds.Min, _bounds.Size, _bounds.MaxExclusiveX, _bounds.MaxExclusiveY, _bounds.Area,
                _selected, _denseGrid.GetIndex(_selected)));

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select (0,0)", GUILayout.Height(26f))) SelectCell(new GridCoord(0, 0));
                if (GUILayout.Button("Select (-6,-4)", GUILayout.Height(26f))) SelectCell(_bounds.Min);
                if (GUILayout.Button("Select (5,3)", GUILayout.Height(26f))) SelectCell(new GridCoord(5, 3));
            }

            DrawSection("DENSE GRID / ROW-MAJOR", "按钮显示 DenseGrid<int> 的 row-major index；点击任意格切换 Selected。支持负坐标，底层仍是连续 T[]。");
            DrawGrid();

            DrawSection("NEIGHBORS / NO ALLOCATION API", string.Format(
                "4-neighbor（N, E, S, W；越界自动过滤）: {0}", FormatCoords(_neighborBuffer, _neighborCount)));

            DrawSection("FOOTPRINT / TRANSFORM", string.Format(
                "L 形 Footprint offsets（canonical Y→X）: {0}\nTransform: {1}\nWorld cells: {2}",
                FormatOffsets(_footprint.Offsets), _transform, _footprintCellCount == 0
                    ? "当前选中 Anchor 变换后溢出 Int32，TryWriteCells=false"
                    : FormatCoords(_footprintBuffer, _footprintCellCount)));
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("0°")) SetRotation(GridRotation.Deg0);
                if (GUILayout.Button("90°")) SetRotation(GridRotation.Deg90);
                if (GUILayout.Button("180°")) SetRotation(GridRotation.Deg180);
                if (GUILayout.Button("270°")) SetRotation(GridRotation.Deg270);
                if (GUILayout.Button(_reflectX ? "ReflectX ✓" : "ReflectX")) ToggleReflectX();
                if (GUILayout.Button(_reflectY ? "ReflectY ✓" : "ReflectY")) ToggleReflectY();
            }

            DrawSection("OCCUPANCY / TWO-PASS ATOMIC", "OccupantId=1 的 A 先占用，B 再在同一 Anchor 尝试占用；冲突时不会留下部分写入。Release 只允许正确 owner 释放。\n" + _lastOperation);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Occupy A (id=1)", GUILayout.Height(28f))) TryOccupy(1, "Occupy A");
                if (GUILayout.Button("Occupy B (id=2)", GUILayout.Height(28f))) TryOccupy(2, "Occupy B");
                if (GUILayout.Button("Release A", GUILayout.Height(28f))) Release(1);
                if (GUILayout.Button("Clear", GUILayout.Height(28f)))
                {
                    _occupancy.Clear();
                    _lastResult = GridOccupancyResult.Succeeded();
                    _lastOperation = "Occupancy.Clear() 已执行。";
                }
            }
            GUILayout.Label(string.Format("Last Result: {0}\nConflict: {1} / Existing: {2}",
                _lastResult, _lastResult.ConflictCoord, _lastResult.ExistingOccupant), _bodyStyle);

            DrawSection("SOURCE BOUNDARY", "该样例只引用 StellarFramework.GridKit.Core。GridKit Runtime 本身不引用 UnityEngine、Kit、UPM、全局 Manager 或生命周期驱动。");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGrid()
        {
            for (int y = checked((int)_bounds.MaxExclusiveY - 1); y >= _bounds.Min.Y; y--)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(y.ToString("+0;-0;0"), _cellStyle, GUILayout.Width(34f));
                    for (int x = _bounds.Min.X; x < _bounds.MaxExclusiveX; x++)
                    {
                        GridCoord coord = new GridCoord(x, y);
                        string label = _occupancy.TryGetOccupant(coord, out GridOccupantId occupant) && occupant.IsValid
                            ? occupant.Value.ToString()
                            : _denseGrid[coord].ToString();
                        if (coord == _selected)
                        {
                            label = "[" + label + "]";
                        }

                        if (GUILayout.Button(label, _cellStyle, GUILayout.Width(CellButtonWidth), GUILayout.Height(24f)))
                        {
                            SelectCell(coord);
                        }
                    }
                }
            }
            GUILayout.Label("X: -6 ... 5（左→右），Y: 3 ... -4（上→下）；格内数字是 DenseGrid index 或 OccupantId。", _bodyStyle);
        }

        private void DrawSection(string title, string body)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, _sectionStyle);
            GUILayout.Label(body ?? string.Empty, _bodyStyle);
        }

        private static string FormatCoords(GridCoord[] coords, int count)
        {
            if (count <= 0) return "<none>";
            string[] values = new string[count];
            for (int i = 0; i < count; i++) values[i] = coords[i].ToString();
            return string.Join(", ", values);
        }

        private static string FormatOffsets(System.Collections.Generic.IReadOnlyList<GridOffset> offsets)
        {
            string[] values = new string[offsets.Count];
            for (int i = 0; i < offsets.Count; i++) values[i] = offsets[i].ToString();
            return string.Join(", ", values);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };
            _cellStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(1, 1, 1, 1)
            };
        }
    }
}
