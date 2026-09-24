using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 让 Transform 按固定角速度持续旋转的轻量组件。
    /// 适合展示物体、指示器、简单机关和环境装饰。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rotator : MonoBehaviour
    {
        [Tooltip("每秒欧拉角旋转速度。")]
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);
        [SerializeField] private Space space = Space.Self;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool randomizeInitialRotation;

        private Transform _cachedTransform;

        private void Awake()
        {
            _cachedTransform = transform;
            if (randomizeInitialRotation)
            {
                _cachedTransform.rotation = Random.rotation;
            }
        }

        private void Update()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>执行一次旋转更新。</summary>
        public void Tick(float deltaTime)
        {
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }

            if (deltaTime <= 0f || degreesPerSecond == Vector3.zero)
            {
                return;
            }

            _cachedTransform.Rotate(degreesPerSecond * deltaTime, space);
        }

        /// <summary>运行时设置旋转速度。</summary>
        public void SetSpeed(Vector3 newDegreesPerSecond)
        {
            degreesPerSecond = newDegreesPerSecond;
        }
    }
}
