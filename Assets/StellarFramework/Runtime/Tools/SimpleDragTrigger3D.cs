using UnityEngine;

namespace StellarFramework
{
    [RequireComponent(typeof(Collider))]
    public class SimpleDragTrigger3D : MonoBehaviour
    {
        [Header("基础")] public bool enableDrag = true;
        public Camera dragCamera;

        [Header("轴锁定（勾选即该轴不动）")] public bool lockX;
        public bool lockY;
        public bool lockZ;

        [Tooltip("每次开始拖拽时是否重新记录锁定轴的参考值（一般保持勾选）")]
        public bool refreshLockAnchorEachDrag = true;

        [Header("拖拽边界控制")] [Tooltip("是否启用拖拽边界限制")]
        public bool enableBounds;

        [Tooltip("边界中心点（相对于自身初始位置的偏移）")] public Vector3 boundsCenter = Vector3.zero;

        [Tooltip("边界大小")] public Vector3 boundsSize = Vector3.one;

        [Header("调试")] public bool showLogKitLog;
        [Tooltip("在Scene视图中显示边界框")] public bool showBoundsGizmo = true;
        private float _depth; // 起始屏幕深度
        private Vector3 _initialLocalPosition; // 初始局部位置，用作边界参考点

        // 内部状态
        private bool _isDragging;
        private Vector3 _lockAnchor; // 锁定轴参考值（拖拽开始时记录，局部坐标）
        private Vector3 _offset; // 物体世界位置 - 指针在该深度的世界点

        private void Awake()
        {
            if (dragCamera == null) dragCamera = Camera.main;
            // 记录初始局部位置作为边界参考点
            _initialLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            if (!_isDragging) return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.GetMouseButton(0))
                DragByPointer(Input.mousePosition);
            else
                EndDrag();
#elif UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                DragByPointer(t.position);
            else if (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended)
                EndDrag();
        }
        else
        {
            EndDrag();
        }
#endif
        }

        /// <summary>
        ///     在Scene视图中绘制固定的边界框
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showBoundsGizmo || !enableBounds) return;

            // 计算边界框的实际世界位置
            var boundsActualCenter = _initialLocalPosition + boundsCenter;
            Vector3 worldCenter;

            if (transform.parent != null)
                // 将局部坐标转换为世界坐标
                worldCenter = transform.parent.TransformPoint(boundsActualCenter);
            else
                worldCenter = boundsActualCenter;

            // 设置边界框颜色
            Gizmos.color = _isDragging ? Color.red : Color.green;

            // 绘制固定的边界框（不跟随物体移动）
            var oldMatrix = Gizmos.matrix;
            if (transform.parent != null)
            {
                // 使用父物体的变换矩阵来正确显示边界框的旋转和缩放
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.parent.rotation, transform.parent.lossyScale);
                Gizmos.DrawWireCube(Vector3.zero, boundsSize);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boundsSize);
            }

            Gizmos.matrix = oldMatrix;
        }

        private void OnMouseDown()
        {
            if (!enableDrag) return;
            StartDrag(Input.mousePosition);
        }

        private void StartDrag(Vector3 pointerScreen)
        {
            if (dragCamera == null) return;

            // 使用世界坐标计算屏幕深度和偏移（因为相机和屏幕投射都是基于世界坐标）
            var startWorldPos = transform.position;
            var startScreen = dragCamera.WorldToScreenPoint(startWorldPos);
            _depth = startScreen.z;

            var pointerWorld = dragCamera.ScreenToWorldPoint(new Vector3(pointerScreen.x, pointerScreen.y, _depth));
            _offset = startWorldPos - pointerWorld;

            // 锁定锚点使用局部坐标
            if (refreshLockAnchorEachDrag || !_isDragging)
                _lockAnchor = transform.localPosition;

            _isDragging = true;

            if (showLogKitLog)
                LogKit.Log($"[SimpleDragTrigger3D] Start drag depth={_depth:F2}, offset={_offset}, localAnchor={_lockAnchor}");
        }

        private void DragByPointer(Vector3 pointerScreen)
        {
            if (dragCamera == null) return;

            // 计算目标世界坐标
            var pointerWorld = dragCamera.ScreenToWorldPoint(new Vector3(pointerScreen.x, pointerScreen.y, _depth));
            var candidateWorld = pointerWorld + _offset;

            // 转换为局部坐标
            Vector3 candidateLocal;
            if (transform.parent != null)
                candidateLocal = transform.parent.InverseTransformPoint(candidateWorld);
            else
                candidateLocal = candidateWorld;

            // 应用轴锁：保持开始时该轴的局部坐标值
            if (lockX) candidateLocal.x = _lockAnchor.x;
            if (lockY) candidateLocal.y = _lockAnchor.y;
            if (lockZ) candidateLocal.z = _lockAnchor.z;

            // 应用边界限制
            if (enableBounds) candidateLocal = ClampToBounds(candidateLocal);

            transform.localPosition = candidateLocal;
        }

        /// <summary>
        ///     将位置限制在边界范围内（基于初始位置的固定边界）
        /// </summary>
        /// <param name="localPos">待限制的局部坐标</param>
        /// <returns>限制后的局部坐标</returns>
        private Vector3 ClampToBounds(Vector3 localPos)
        {
            // 边界基于初始位置 + 边界中心偏移
            var boundsActualCenter = _initialLocalPosition + boundsCenter;
            var min = boundsActualCenter - boundsSize * 0.5f;
            var max = boundsActualCenter + boundsSize * 0.5f;

            return new Vector3(
                Mathf.Clamp(localPos.x, min.x, max.x),
                Mathf.Clamp(localPos.y, min.y, max.y),
                Mathf.Clamp(localPos.z, min.z, max.z)
            );
        }

        private void EndDrag()
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (showLogKitLog)
                LogKit.Log($"[SimpleDragTrigger3D] End drag final local={transform.localPosition}");
        }

        // 运行期接口（可选）
        public void SetAxisLocks(bool x, bool y, bool z)
        {
            lockX = x;
            lockY = y;
            lockZ = z;
            // 立即强制应用（保持当前锁锚）
            var p = transform.localPosition;
            if (lockX) p.x = _lockAnchor.x;
            if (lockY) p.y = _lockAnchor.y;
            if (lockZ) p.z = _lockAnchor.z;

            // 应用边界限制
            if (enableBounds) p = ClampToBounds(p);

            transform.localPosition = p;
        }

        public void SetEnableDrag(bool v)
        {
            enableDrag = v;
        }

        public void RefreshLockAnchorNow()
        {
            _lockAnchor = transform.localPosition;
        }

        /// <summary>
        ///     设置边界控制
        /// </summary>
        /// <param name="enable">是否启用边界</param>
        /// <param name="center">边界中心偏移（相对于初始位置）</param>
        /// <param name="size">边界大小</param>
        public void SetBounds(bool enable, Vector3 center, Vector3 size)
        {
            enableBounds = enable;
            boundsCenter = center;
            boundsSize = size;

            // 如果启用边界，立即应用到当前位置
            if (enableBounds) transform.localPosition = ClampToBounds(transform.localPosition);
        }

        /// <summary>
        ///     重置边界参考点为当前位置
        /// </summary>
        public void ResetBoundsReference()
        {
            _initialLocalPosition = transform.localPosition;
        }
    }
}