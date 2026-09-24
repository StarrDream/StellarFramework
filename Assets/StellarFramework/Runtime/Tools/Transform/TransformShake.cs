using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 轻量、无第三方依赖的 Transform 抖动组件。
    /// 适合相机子节点、武器挂点或独立视觉 Pivot；如果目标 Transform 同时被其他系统直接写 localPosition/localRotation，
    /// 推荐把本组件放到专用子节点以避免所有权冲突。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransformShake : MonoBehaviour
    {
        [Header("Amplitude")]
        [Tooltip("Trauma=1 时最大的本地位置偏移幅度。")]
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.25f, 0.25f, 0f);

        [Tooltip("Trauma=1 时最大的本地欧拉角偏移幅度。")]
        [SerializeField] private Vector3 rotationAmplitude = new Vector3(1.5f, 1.5f, 1f);

        [Header("Noise")]
        [Tooltip("Perlin Noise 前进速度。")]
        [Min(0f)] [SerializeField] private float frequency = 20f;

        [Tooltip("每秒减少的 Trauma。")]
        [Min(0f)] [SerializeField] private float decayPerSecond = 1.5f;

        [Tooltip("Trauma 曲线指数。2 表示常见的平方衰减视觉效果。")]
        [Min(1f)] [SerializeField] private float traumaExponent = 2f;

        [Tooltip("固定噪声种子，便于回放和测试。")]
        [SerializeField] private int seed = 1337;

        [SerializeField] private bool useUnscaledTime;

        private Transform _cachedTransform;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _trauma;
        private float _noiseTime;
        private bool _baseCaptured;

        /// <summary>当前 Trauma，范围 0..1。</summary>
        public float Trauma => _trauma;

        private void Awake()
        {
            EnsureTransform();
        }

        private void LateUpdate()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void OnDisable()
        {
            RestoreBaseTransform();
        }

        /// <summary>
        /// 累加 Trauma。负值按 0 处理，最终值夹在 0..1。
        /// 第一次进入抖动状态时会捕获当前本地 Transform 作为恢复基线。
        /// </summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount))
            {
                return;
            }

            EnsureBaseCaptured();
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        /// <summary>直接设置 Trauma，值会夹在 0..1。</summary>
        public void SetTrauma(float value)
        {
            if (float.IsNaN(value))
            {
                value = 0f;
            }

            float clamped = Mathf.Clamp01(value);
            if (clamped > 0f)
            {
                EnsureBaseCaptured();
            }

            _trauma = clamped;
            if (_trauma <= 0f)
            {
                RestoreBaseTransform();
            }
        }

        /// <summary>
        /// 执行一次抖动更新。公开 Tick 便于测试、回放或统一调度。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_trauma <= 0f)
            {
                return;
            }

            EnsureBaseCaptured();
            float dt = Mathf.Max(0f, deltaTime);
            _noiseTime += dt * frequency;

            float strength = Mathf.Pow(_trauma, traumaExponent);
            Vector3 positionNoise = new Vector3(
                SignedNoise(seed + 11, _noiseTime),
                SignedNoise(seed + 23, _noiseTime),
                SignedNoise(seed + 37, _noiseTime));
            Vector3 rotationNoise = new Vector3(
                SignedNoise(seed + 53, _noiseTime),
                SignedNoise(seed + 71, _noiseTime),
                SignedNoise(seed + 89, _noiseTime));

            _cachedTransform.localPosition = _baseLocalPosition + Vector3.Scale(positionAmplitude, positionNoise) * strength;
            _cachedTransform.localRotation = _baseLocalRotation * Quaternion.Euler(Vector3.Scale(rotationAmplitude, rotationNoise) * strength);

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * dt);
            if (_trauma <= 0f)
            {
                RestoreBaseTransform();
            }
        }

        /// <summary>立即停止并恢复开始抖动前捕获的本地 Transform。</summary>
        public void Stop()
        {
            _trauma = 0f;
            RestoreBaseTransform();
        }

        /// <summary>
        /// 重新以当前 Transform 作为抖动基线。用于项目在抖动期间主动切换视觉 Pivot 姿态的场景。
        /// </summary>
        public void RecaptureBaseTransform()
        {
            EnsureTransform();
            _baseLocalPosition = _cachedTransform.localPosition;
            _baseLocalRotation = _cachedTransform.localRotation;
            _baseCaptured = true;
        }

        private void EnsureBaseCaptured()
        {
            if (_baseCaptured)
            {
                return;
            }

            RecaptureBaseTransform();
        }

        private void RestoreBaseTransform()
        {
            if (!_baseCaptured)
            {
                return;
            }

            EnsureTransform();
            _cachedTransform.localPosition = _baseLocalPosition;
            _cachedTransform.localRotation = _baseLocalRotation;
            _baseCaptured = false;
        }

        private void EnsureTransform()
        {
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }
        }

        private static float SignedNoise(int channelSeed, float time)
        {
            return Mathf.PerlinNoise(channelSeed * 0.0137f, time) * 2f - 1f;
        }
    }
}
