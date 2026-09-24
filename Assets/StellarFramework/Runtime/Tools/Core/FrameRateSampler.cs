using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 一次帧率采样窗口的只读统计结果。
    /// 数值基于调用方持续提交的 deltaTime 计算，不负责任何 UI 展示。
    /// </summary>
    public readonly struct FrameRateSnapshot
    {
        internal FrameRateSnapshot(
            float currentFps,
            float averageFps,
            float minFps,
            float maxFps,
            int sampleCount)
        {
            CurrentFps = currentFps;
            AverageFps = averageFps;
            MinFps = minFps;
            MaxFps = maxFps;
            SampleCount = sampleCount;
        }

        /// <summary>最近一个刷新区间计算得到的 FPS。</summary>
        public float CurrentFps { get; }

        /// <summary>当前环形窗口内有效 FPS 样本的平均值。</summary>
        public float AverageFps { get; }

        /// <summary>当前环形窗口内有效 FPS 样本的最小值。</summary>
        public float MinFps { get; }

        /// <summary>当前环形窗口内有效 FPS 样本的最大值。</summary>
        public float MaxFps { get; }

        /// <summary>当前统计窗口内已经写入的有效 FPS 样本数量。</summary>
        public int SampleCount { get; }
    }

    /// <summary>
    /// 无 UI、无 LINQ、固定数组的帧率采样器。
    /// 构造时只分配一次环形缓冲区，后续 <see cref="PushFrame"/> 不创建工作集合。
    /// </summary>
    public sealed class FrameRateSampler
    {
        private readonly float[] _samples;
        private readonly float _refreshInterval;

        private int _sampleWriteIndex;
        private int _validSampleCount;
        private int _framesInInterval;
        private float _elapsedInInterval;
        private FrameRateSnapshot _snapshot;

        /// <summary>
        /// 创建帧率采样器。
        /// </summary>
        /// <param name="sampleWindow">保存多少个 FPS 刷新样本，必须大于 0。</param>
        /// <param name="refreshInterval">多少秒生成一次 FPS 样本；0 表示每个有效 deltaTime 都生成样本。</param>
        public FrameRateSampler(int sampleWindow = 60, float refreshInterval = 0.5f)
        {
            if (sampleWindow <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleWindow), sampleWindow, "Sample window must be greater than zero.");
            }

            if (float.IsNaN(refreshInterval) || float.IsInfinity(refreshInterval) || refreshInterval < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshInterval), refreshInterval, "Refresh interval must be finite and non-negative.");
            }

            _samples = new float[sampleWindow];
            _refreshInterval = refreshInterval;
            _snapshot = new FrameRateSnapshot(0f, 0f, 0f, 0f, 0);
        }

        /// <summary>最近一次已经完成的统计快照。</summary>
        public FrameRateSnapshot Snapshot => _snapshot;

        /// <summary>固定采样窗口容量。</summary>
        public int SampleWindow => _samples.Length;

        /// <summary>生成一个 FPS 样本前需要累计的时间。</summary>
        public float RefreshInterval => _refreshInterval;

        /// <summary>
        /// 提交一帧耗时。返回 true 表示本次提交生成了新的 FPS 快照。
        /// 非正数、NaN、Infinity 会被忽略，避免暂停帧或异常输入污染统计。
        /// </summary>
        public bool PushFrame(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return false;
            }

            _framesInInterval++;
            _elapsedInInterval += deltaTime;

            if (_refreshInterval > 0f && _elapsedInInterval < _refreshInterval)
            {
                return false;
            }

            float currentFps = _framesInInterval / _elapsedInInterval;
            _framesInInterval = 0;
            _elapsedInInterval = 0f;

            _samples[_sampleWriteIndex] = currentFps;
            _sampleWriteIndex = (_sampleWriteIndex + 1) % _samples.Length;
            if (_validSampleCount < _samples.Length)
            {
                _validSampleCount++;
            }

            RecalculateSnapshot(currentFps);
            return true;
        }

        /// <summary>清空所有统计值，但保留已经分配的采样缓冲区。</summary>
        public void Reset()
        {
            Array.Clear(_samples, 0, _samples.Length);
            _sampleWriteIndex = 0;
            _validSampleCount = 0;
            _framesInInterval = 0;
            _elapsedInInterval = 0f;
            _snapshot = new FrameRateSnapshot(0f, 0f, 0f, 0f, 0);
        }

        private void RecalculateSnapshot(float currentFps)
        {
            float sum = 0f;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < _validSampleCount; i++)
            {
                float sample = _samples[i];
                sum += sample;
                if (sample < min) min = sample;
                if (sample > max) max = sample;
            }

            float average = _validSampleCount > 0 ? sum / _validSampleCount : 0f;
            _snapshot = new FrameRateSnapshot(
                currentFps,
                average,
                _validSampleCount > 0 ? min : 0f,
                _validSampleCount > 0 ? max : 0f,
                _validSampleCount);
        }
    }

    /// <summary>
    /// Unity 生命周期驱动的帧率监控组件。
    /// 只提供数值与事件，不创建 OnGUI、Texture、字符串或任何显示层对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrameRateMonitor : MonoBehaviour
    {
        [Tooltip("保留多少个刷新样本用于 Avg/Min/Max。")]
        [Min(1)] [SerializeField] private int sampleWindow = 60;

        [Tooltip("生成一次 FPS 样本的时间间隔；0 表示每帧刷新。")]
        [Min(0f)] [SerializeField] private float refreshInterval = 0.5f;

        [Tooltip("通常建议开启，避免 Time.timeScale=0 时统计停止。")]
        [SerializeField] private bool useUnscaledTime = true;

        private FrameRateSampler _sampler;

        /// <summary>产生新统计快照时触发。</summary>
        public event Action<FrameRateSnapshot> Updated;

        /// <summary>当前统计快照；尚未采样时全部为 0。</summary>
        public FrameRateSnapshot Snapshot => EnsureSampler().Snapshot;

        private void Awake()
        {
            EnsureSampler();
        }

        private void Update()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>
        /// 外部驱动一次采样。返回 true 表示生成了新的统计快照。
        /// </summary>
        public bool Tick(float deltaTime)
        {
            FrameRateSampler sampler = EnsureSampler();
            if (!sampler.PushFrame(deltaTime))
            {
                return false;
            }

            Updated?.Invoke(sampler.Snapshot);
            return true;
        }

        /// <summary>重置统计但复用现有缓冲区。</summary>
        public void ResetStatistics()
        {
            EnsureSampler().Reset();
        }

        private FrameRateSampler EnsureSampler()
        {
            if (_sampler == null)
            {
                _sampler = new FrameRateSampler(Mathf.Max(1, sampleWindow), Mathf.Max(0f, refreshInterval));
            }

            return _sampler;
        }
    }
}
