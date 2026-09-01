using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>SpatialKit 最小样例：连续二维点、动态变更、矩形/圆形查询和有限半径最近邻。</summary>
    public sealed class Example_SpatialKit : MonoBehaviour
    {
        private const int PointCapacity = 72;
        private const float SampleBucketSize = 4f;
        private const int InitialPointCount = 64;
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

            for (int i = 0; i < InitialPointCount; i++)
            {
                SpatialPoint position = MakeInitialPosition(i);
                _ids[i] = new SpatialId(_nextId++);
                _positions[i] = position;
                _present[i] = _index.TryInsert(_ids[i], _positions[i]).Success;
            }

            _lastOperation = "Reset：插入 64 个连续二维点；ID 1/2/3 位于圆边界，ID 4 位于外接方框角落。";
            _initialized = true;
        }

        private void InsertPoint()
        {
            ClearLastQueryState();
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
            ClearLastQueryState();
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
            ClearLastQueryState();
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
            ClearLastQueryState();
            _lastRect = new SpatialRect(-4f, -2f, 4f, 3f);
            _lastQuery = _index.QueryRect(_lastRect, _queryBuffer);
            _hasRect = true;
            _hasCircle = false;
            _lastOperation = "Query Rect：半开区间 [Min, Max)。";
        }

        private void QueryCircle()
        {
            ClearLastQueryState();
            _lastCircleCenter = new SpatialPoint(0f, 0f);
            _lastCircleRadius = 5f;
            _lastQuery = _index.QueryCircle(_lastCircleCenter, _lastCircleRadius, _queryBuffer);
            _hasCircle = true;
            _hasRect = false;
            _lastOperation = "Query Circle：闭圆边界 distance <= radius。";
        }

        private void FindNearest(bool excludeSelected)
        {
            ClearLastQueryState();
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
                "BucketSize: {0}\nCount: {1}\nSelected ID: {2}\nSelected Position: {3}\nLast Operation: {4}\nLast Query: {5}\nWrittenCount: {6}\nMatchCount: {7}\nTruncated: {8}\nNearest ID: {9}",
                SampleBucketSize, _index.Count, SelectedId(), SelectedPosition(), _lastOperation,
                LastQueryName(), _lastQuery.WrittenCount, _lastQuery.MatchCount, _lastQuery.IsTruncated,
                _lastNearest.IsValid ? _lastNearest.ToString() : "<none>"), _bodyStyle);

            GUILayout.Space(8f);
            GUILayout.Label("VIEW", _sectionStyle);
            Rect view = GUILayoutUtility.GetRect(380f, 380f, GUILayout.ExpandWidth(false));
            DrawView(view);
            GUILayout.Label("浅蓝框：Rect；黄色圆线：Circle；黄色点：QueryMatched；绿色：Selected；青色：Nearest。高亮来自 SpatialKit 实际返回 ID。", _bodyStyle);
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
                Vector2 guiPoint = WorldToGui(point.X, point.Y, view, min, max);
                Color previous = GUI.color;
                GUI.color = PointColor(i, _ids[i]);
                GUI.DrawTexture(new Rect(guiPoint.x - 4f, guiPoint.y - 4f, 8f, 8f), Texture2D.whiteTexture);
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
                Vector2 center = WorldToGui(_lastCircleCenter.X, _lastCircleCenter.Y, view, min, max);
                float radiusX = _lastCircleRadius / (max - min) * view.width;
                float radiusY = _lastCircleRadius / (max - min) * view.height;
                DrawGuiCircle(center, radiusX, radiusY, new Color(1f, 0.8f, 0.25f, 0.9f), 48, 2f);
            }
        }

        private static Rect WorldRectToGui(SpatialRect rect, Rect view, float min, float max)
        {
            Vector2 topLeft = WorldToGui(rect.MinX, rect.MaxExclusiveY, view, min, max);
            Vector2 bottomRight = WorldToGui(rect.MaxExclusiveX, rect.MinY, view, min, max);
            return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
        }

        private static Vector2 WorldToGui(float x, float y, Rect view, float min, float max)
        {
            return new Vector2(
                view.x + (x - min) / (max - min) * view.width,
                view.yMax - (y - min) / (max - min) * view.height);
        }

        private static void DrawGuiCircle(Vector2 center, float radiusX, float radiusY, Color color,
            int segmentCount, float thickness)
        {
            Vector2 previous = CirclePoint(center, radiusX, radiusY, 0f);
            float fullTurn = Mathf.PI * 2f;
            for (int i = 1; i <= segmentCount; i++)
            {
                Vector2 current = CirclePoint(center, radiusX, radiusY, fullTurn * i / segmentCount);
                DrawGuiLine(previous, current, color, thickness);
                previous = current;
            }
        }

        private static Vector2 CirclePoint(Vector2 center, float radiusX, float radiusY, float angle)
        {
            return new Vector2(
                center.x + Mathf.Cos(angle) * radiusX,
                center.y - Mathf.Sin(angle) * radiusY);
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private Color PointColor(int slot, SpatialId id)
        {
            if (_lastNearest.IsValid && id == _lastNearest)
            {
                return Color.cyan;
            }

            if (slot == _selectedSlot)
            {
                return Color.green;
            }

            if (IsQueryMatched(id))
            {
                return Color.yellow;
            }

            return Color.white;
        }

        private bool IsQueryMatched(SpatialId id)
        {
            if (!_hasRect && !_hasCircle)
            {
                return false;
            }

            for (int i = 0; i < _lastQuery.WrittenCount; i++)
            {
                if (_queryBuffer[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private string LastQueryName()
        {
            if (_hasRect) return "Rect [Min,Max)";
            if (_hasCircle) return "Circle distance <= radius";
            return "<none>";
        }

        private void ClearLastQueryState()
        {
            _lastQuery = new SpatialQueryResult(0, 0);
            _lastNearest = default(SpatialId);
            _hasRect = false;
            _hasCircle = false;
        }

        private static SpatialPoint MakeInitialPosition(int index)
        {
            switch (index)
            {
                case 0: return new SpatialPoint(5f, 0f);
                case 1: return new SpatialPoint(0f, 5f);
                case 2: return new SpatialPoint(3f, 4f);
                case 3: return new SpatialPoint(5f, 5f);
                default:
                    int gridIndex = index - 4;
                    float x = (gridIndex % 12) - 6f + (gridIndex % 3) * 0.25f;
                    float y = (gridIndex / 12) - 3f + (gridIndex % 4) * 0.2f;
                    return new SpatialPoint(x, y);
            }
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
