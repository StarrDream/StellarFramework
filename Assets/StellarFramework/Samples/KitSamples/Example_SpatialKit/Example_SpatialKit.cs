using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>SpatialKit 最小样例：连续二维点、动态变更、矩形/圆形查询和有限半径最近邻。</summary>
    public sealed class Example_SpatialKit : MonoBehaviour
    {
        private const int PointCapacity = 72;
        private const float SampleBucketSize = 4f;
        private readonly SpatialId[] _ids = new SpatialId[PointCapacity];
        private readonly SpatialPoint[] _positions = new SpatialPoint[PointCapacity];
        private readonly bool[] _present = new bool[PointCapacity];
        private readonly SpatialId[] _queryBuffer = new SpatialId[PointCapacity];
        private SpatialIndex2D _index;
        private int _nextId;
        private int _selectedSlot;
        private SpatialQueryResult _lastQuery;
        private SpatialId _lastNearest;
        private string _lastOperation = "尚未执行操作。";
        private SpatialRect _lastRect;
        private SpatialPoint _lastCircleCenter;
        private float _lastCircleRadius;
        private bool _hasRect;
        private bool _hasCircle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _sectionStyle;
        private bool _initialized;

        private void Awake() => ResetData();

        private void OnEnable()
        {
            if (!_initialized) ResetData();
        }

        private void ResetData()
        {
            _index = new SpatialIndex2D(SampleBucketSize, PointCapacity);
            Array.Clear(_present, 0, _present.Length);
            _nextId = 1;
            _selectedSlot = 0;
            _lastQuery = new SpatialQueryResult(0, 0);
            _lastNearest = default(SpatialId);
            _hasRect = false;
            _hasCircle = false;

            for (int i = 0; i < 64; i++)
            {
                float x = (i % 12) - 6f + (i % 3) * 0.25f;
                float y = (i / 12) - 3f + (i % 4) * 0.2f;
                _ids[i] = new SpatialId(_nextId++);
                _positions[i] = new SpatialPoint(x, y);
                _present[i] = _index.TryInsert(_ids[i], _positions[i]).Success;
            }

            _lastOperation = "Reset：插入 64 个连续二维点（含负坐标与小数坐标）。";
            _initialized = true;
        }

        private void InsertPoint()
        {
            for (int i = 0; i < PointCapacity; i++)
            {
                if (_present[i]) continue;
                _ids[i] = new SpatialId(_nextId++);
                _positions[i] = new SpatialPoint(-6f + i * 0.2f, 5f);
                SpatialMutationResult result = _index.TryInsert(_ids[i], _positions[i]);
                _present[i] = result.Success;
                _selectedSlot = i;
                _lastOperation = string.Format("Insert {0}: {1}", _ids[i], result);
                return;
            }

            _lastOperation = "Insert：样例容量已满。";
        }

        private void MoveSelected()
        {
            if (!_present[_selectedSlot])
            {
                _lastOperation = "Move：当前没有选中的点。";
                return;
            }

            SpatialPoint old = _positions[_selectedSlot];
            SpatialPoint next = new SpatialPoint(old.X + 0.75f, old.Y + 0.35f);
            SpatialMutationResult result = _index.TryUpdatePosition(_ids[_selectedSlot], next);
            if (result.Success) _positions[_selectedSlot] = next;
            _lastOperation = string.Format("Move Selected {0}: {1}", _ids[_selectedSlot], result);
        }

        private void RemoveSelected()
        {
            if (!_present[_selectedSlot])
            {
                _lastOperation = "Remove：当前没有选中的点。";
                return;
            }

            SpatialMutationResult result = _index.TryRemove(_ids[_selectedSlot]);
            if (result.Success) _present[_selectedSlot] = false;
            _lastOperation = string.Format("Remove Selected {0}: {1}", _ids[_selectedSlot], result);
        }

        private void QueryRect()
        {
            _lastRect = new SpatialRect(-4f, -2f, 4f, 3f);
            _lastQuery = _index.QueryRect(_lastRect, _queryBuffer);
            _hasRect = true;
            _hasCircle = false;
            _lastOperation = "Query Rect：半开区间 [Min, Max)。";
        }

        private void QueryCircle()
        {
            _lastCircleCenter = new SpatialPoint(0f, 0f);
            _lastCircleRadius = 5f;
            _lastQuery = _index.QueryCircle(_lastCircleCenter, _lastCircleRadius, _queryBuffer);
            _hasCircle = true;
            _hasRect = false;
            _lastOperation = "Query Circle：闭圆边界 distance <= radius。";
        }

        private void FindNearest(bool excludeSelected)
        {
            SpatialPoint center = new SpatialPoint(0f, 0f);
            bool found;
            if (excludeSelected && _present[_selectedSlot])
                found = _index.TryFindNearest(center, 8f, _ids[_selectedSlot], out _lastNearest);
            else
                found = _index.TryFindNearest(center, 8f, out _lastNearest);

            _lastOperation = found
                ? string.Format("Find Nearest{0}: {1}", excludeSelected ? " Excluding Selected" : string.Empty, _lastNearest)
                : "Find Nearest：指定半径内没有点。";
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 420f, Screen.height - 32f), GUI.skin.box);
            GUILayout.Label("SpatialKit / Playable Sample", _titleStyle);
            GUILayout.Label("连续二维点索引：不等同于 GridKit 的整数格子", _bodyStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset")) ResetData();
                if (GUILayout.Button("Insert")) InsertPoint();
                if (GUILayout.Button("Move Selected")) MoveSelected();
                if (GUILayout.Button("Remove Selected")) RemoveSelected();
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Query Rect")) QueryRect();
                if (GUILayout.Button("Query Circle")) QueryCircle();
                if (GUILayout.Button("Nearest")) FindNearest(false);
                if (GUILayout.Button("Nearest Excluding")) FindNearest(true);
            }

            GUILayout.Space(8f);
            GUILayout.Label("STATE", _sectionStyle);
            GUILayout.Label(string.Format(
                "BucketSize: {0}\nCount: {1}\nSelected ID: {2}\nSelected Position: {3}\nLast Operation: {4}\nWrittenCount: {5}\nMatchCount: {6}\nTruncated: {7}\nNearest ID: {8}",
                SampleBucketSize, _index.Count, SelectedId(), SelectedPosition(), _lastOperation,
                _lastQuery.WrittenCount, _lastQuery.MatchCount, _lastQuery.IsTruncated,
                _lastNearest.IsValid ? _lastNearest.ToString() : "<none>"), _bodyStyle);

            GUILayout.Space(8f);
            GUILayout.Label("VIEW", _sectionStyle);
            Rect view = GUILayoutUtility.GetRect(380f, 380f, GUILayout.ExpandWidth(false));
            DrawView(view);
            GUILayout.Label("浅蓝：Rect；浅黄：Circle；绿色：Selected。点位使用连续坐标。", _bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawView(Rect view)
        {
            GUI.Box(view, GUIContent.none);
            const float min = -8f;
            const float max = 8f;
            for (int i = 0; i < PointCapacity; i++)
            {
                if (!_present[i]) continue;
                SpatialPoint point = _positions[i];
                float x = view.x + (point.X - min) / (max - min) * view.width;
                float y = view.yMax - (point.Y - min) / (max - min) * view.height;
                Color previous = GUI.color;
                GUI.color = i == _selectedSlot ? Color.green : Color.white;
                GUI.Box(new Rect(x - 4f, y - 4f, 8f, 8f), GUIContent.none);
                GUI.color = previous;
            }

            if (_hasRect)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.3f, 0.8f, 1f, 0.25f);
                GUI.Box(WorldRectToGui(_lastRect, view, min, max), GUIContent.none);
                GUI.color = previous;
            }

            if (_hasCircle)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.8f, 0.25f, 0.25f);
                float diameter = _lastCircleRadius * 2f / (max - min) * view.width;
                float cx = view.x + (_lastCircleCenter.X - min) / (max - min) * view.width;
                float cy = view.yMax - (_lastCircleCenter.Y - min) / (max - min) * view.height;
                GUI.Box(new Rect(cx - diameter * 0.5f, cy - diameter * 0.5f, diameter, diameter), GUIContent.none);
                GUI.color = previous;
            }
        }

        private static Rect WorldRectToGui(SpatialRect rect, Rect view, float min, float max)
        {
            float x = view.x + (rect.MinX - min) / (max - min) * view.width;
            float y = view.yMax - (rect.MaxExclusiveY - min) / (max - min) * view.height;
            float width = (rect.MaxExclusiveX - rect.MinX) / (max - min) * view.width;
            float height = (rect.MaxExclusiveY - rect.MinY) / (max - min) * view.height;
            return new Rect(x, y, width, height);
        }

        private SpatialId SelectedId() => _present[_selectedSlot] ? _ids[_selectedSlot] : default(SpatialId);
        private SpatialPoint SelectedPosition() => _present[_selectedSlot] ? _positions[_selectedSlot] : default(SpatialPoint);

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
    }
}
