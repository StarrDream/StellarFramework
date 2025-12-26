using UnityEngine;

namespace StellarFramework.Tools
{
    /// <summary>
    /// 通用公告板组件 (合并了 BillboardYawOnly 和 FaceCamera)
    /// 支持：全轴/Y轴锁定、反向、平滑、编辑器预览
    /// </summary>
    [ExecuteAlways]
    public class UniversalBillboard : MonoBehaviour
    {
        [Header("目标设置")] [Tooltip("目标相机，为空则自动获取 MainCamera")]
        public Camera targetCamera;

        [Header("轴向控制")] [Tooltip("是否锁定 Y 轴 (仅在水平面旋转)")]
        public bool lockYAxis = true;

        [Tooltip("是否反向 (背对相机)")] public bool reverseFacing = false;

        [Header("平滑设置")] [Tooltip("是否启用平滑旋转 (运行时有效)")]
        public bool smoothRotation = false;

        [Tooltip("平滑速度")] public float rotationSpeed = 10f;

        // 缓存 Transform 提升性能
        private Transform _cachedTransform;
        private Transform _camTransform;

        private void Start()
        {
            _cachedTransform = transform;
            UpdateCameraReference();

            // LogKit 调试信息
            LogKit.Log($"[UniversalBillboard] 初始化完成: {name}, LockY: {lockYAxis}");
        }

        private void UpdateCameraReference()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                _camTransform = targetCamera.transform;
            }
        }

        private void LateUpdate()
        {
            // 编辑器模式下可能频繁切换相机，需要动态获取
            if (!Application.isPlaying || _camTransform == null)
            {
                UpdateCameraReference();
                if (_camTransform == null) return;
            }

            // 计算目标方向
            Vector3 targetPos = _camTransform.position;

            // 如果锁定 Y 轴，将目标高度强制设为当前物体高度
            if (lockYAxis)
            {
                targetPos.y = _cachedTransform.position.y;
            }

            // 计算朝向向量
            Vector3 direction = targetPos - _cachedTransform.position;

            // 处理反向
            if (reverseFacing)
            {
                direction = -direction;
            }

            // 避免向量为零导致的警告
            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // 应用旋转
            if (Application.isPlaying && smoothRotation)
            {
                _cachedTransform.rotation = Quaternion.Slerp(_cachedTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
            else
            {
                _cachedTransform.rotation = targetRotation;
            }
        }
    }
}