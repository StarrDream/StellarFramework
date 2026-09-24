using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>Billboard 旋转模式。</summary>
    public enum BillboardAxisMode
    {
        /// <summary>完整朝向相机。</summary>
        Full,
        /// <summary>只绕世界 Y 轴旋转，常用于角色头顶 UI。</summary>
        YAxisOnly
    }

    /// <summary>
    /// 通用 Billboard 组件。
    /// 目标相机可显式指定；未指定时只在需要时缓存一次 Camera.main。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UniversalBillboard : MonoBehaviour
    {
        [Tooltip("目标相机；为空时尝试使用 MainCamera。")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private BillboardAxisMode axisMode = BillboardAxisMode.YAxisOnly;
        [Tooltip("部分模型的正面朝向与 Unity forward 相反时启用。")]
        [SerializeField] private bool reverseFacing;
        [SerializeField] private bool smoothRotation;
        [Min(0f)] [SerializeField] private float smoothSpeed = 12f;
        [SerializeField] private bool useUnscaledTime;

        private Transform _cachedTransform;
        private Transform _cameraTransform;

        private void Awake()
        {
            _cachedTransform = transform;
            RefreshCameraReference();
        }

        private void LateUpdate()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>显式设置目标相机。</summary>
        public void SetCamera(Camera camera)
        {
            targetCamera = camera;
            _cameraTransform = camera != null ? camera.transform : null;
        }

        /// <summary>重新解析相机引用。</summary>
        public bool RefreshCameraReference()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _cameraTransform = targetCamera != null ? targetCamera.transform : null;
            return _cameraTransform != null;
        }

        /// <summary>执行一次朝向更新。</summary>
        public void Tick(float deltaTime)
        {
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }

            if (_cameraTransform == null && !RefreshCameraReference())
            {
                return;
            }

            Vector3 targetPosition = _cameraTransform.position;
            if (axisMode == BillboardAxisMode.YAxisOnly)
            {
                targetPosition.y = _cachedTransform.position.y;
            }

            Vector3 direction = targetPosition - _cachedTransform.position;
            if (reverseFacing)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (!smoothRotation || smoothSpeed <= 0f)
            {
                _cachedTransform.rotation = desiredRotation;
                return;
            }

            float factor = 1f - Mathf.Exp(-smoothSpeed * Mathf.Max(0f, deltaTime));
            _cachedTransform.rotation = Quaternion.Slerp(_cachedTransform.rotation, desiredRotation, factor);
        }
    }
}
