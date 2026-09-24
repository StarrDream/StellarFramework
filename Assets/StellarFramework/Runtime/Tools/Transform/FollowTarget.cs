using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 通用 Transform 跟随组件。
    /// 可独立控制位置、旋转和缩放，并支持捕获初始偏移和平滑跟随。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowTarget : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("需要跟随的目标 Transform。")]
        [SerializeField] private Transform target;

        [Header("跟随通道")]
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followRotation = true;
        [SerializeField] private bool followScale;

        [Header("偏移")]
        [Tooltip("启用后，Start 时把当前相对关系捕获为偏移；关闭时使用下面手工填写的偏移。")]
        [SerializeField] private bool preserveInitialOffset = true;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;
        [SerializeField] private Vector3 scaleMultiplier = Vector3.one;

        [Header("平滑")]
        [Tooltip("0 表示立即跟随；大于 0 时使用帧率无关指数平滑。")]
        [Min(0f)] [SerializeField] private float positionSmoothSpeed;
        [Min(0f)] [SerializeField] private float rotationSmoothSpeed;
        [Min(0f)] [SerializeField] private float scaleSmoothSpeed;
        [SerializeField] private bool useUnscaledTime;

        private Transform _cachedTransform;
        private bool _offsetCaptured;

        /// <summary>当前跟随目标。</summary>
        public Transform Target => target;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        private void Start()
        {
            if (preserveInitialOffset && target != null)
            {
                CaptureCurrentOffset();
            }
        }

        private void LateUpdate()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>设置新的跟随目标。</summary>
        public void SetTarget(Transform newTarget, bool captureOffset = true)
        {
            target = newTarget;
            _offsetCaptured = false;

            if (captureOffset && target != null)
            {
                CaptureCurrentOffset();
            }
        }

        /// <summary>把当前相对关系记录为偏移。</summary>
        public void CaptureCurrentOffset()
        {
            if (target == null)
            {
                return;
            }

            EnsureTransform();
            Quaternion inverseTargetRotation = Quaternion.Inverse(target.rotation);
            positionOffset = inverseTargetRotation * (_cachedTransform.position - target.position);
            rotationOffsetEuler = (inverseTargetRotation * _cachedTransform.rotation).eulerAngles;
            scaleMultiplier = DivideSafe(_cachedTransform.localScale, target.localScale);
            _offsetCaptured = true;
        }

        /// <summary>
        /// 执行一次跟随更新。公开该方法便于测试、回放或由外部统一调度器驱动。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            EnsureTransform();
            if (preserveInitialOffset && !_offsetCaptured)
            {
                CaptureCurrentOffset();
            }

            float dt = Mathf.Max(0f, deltaTime);

            if (followPosition)
            {
                Vector3 desiredPosition = target.position + target.rotation * positionOffset;
                _cachedTransform.position = positionSmoothSpeed <= 0f
                    ? desiredPosition
                    : Vector3.Lerp(_cachedTransform.position, desiredPosition,
                        ExponentialLerpFactor(positionSmoothSpeed, dt));
            }

            if (followRotation)
            {
                Quaternion desiredRotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
                _cachedTransform.rotation = rotationSmoothSpeed <= 0f
                    ? desiredRotation
                    : Quaternion.Slerp(_cachedTransform.rotation, desiredRotation,
                        ExponentialLerpFactor(rotationSmoothSpeed, dt));
            }

            if (followScale)
            {
                Vector3 desiredScale = Vector3.Scale(target.localScale, scaleMultiplier);
                _cachedTransform.localScale = scaleSmoothSpeed <= 0f
                    ? desiredScale
                    : Vector3.Lerp(_cachedTransform.localScale, desiredScale,
                        ExponentialLerpFactor(scaleSmoothSpeed, dt));
            }
        }

        private void EnsureTransform()
        {
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }
        }

        private static float ExponentialLerpFactor(float speed, float deltaTime)
        {
            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        private static Vector3 DivideSafe(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Approximately(divisor.x, 0f) ? 1f : value.x / divisor.x,
                Mathf.Approximately(divisor.y, 0f) ? 1f : value.y / divisor.y,
                Mathf.Approximately(divisor.z, 0f) ? 1f : value.z / divisor.z);
        }
    }
}
