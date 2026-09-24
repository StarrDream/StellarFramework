using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 一个带权重的候选值。
    /// 权重必须是有限的非负数；权重为 0 的候选永远不会被抽中。
    /// </summary>
    /// <typeparam name="T">候选值类型。</typeparam>
    [Serializable]
    public readonly struct WeightedValue<T>
    {
        /// <summary>创建一个带权重的候选值。</summary>
        public WeightedValue(T value, float weight)
        {
            Value = value;
            Weight = weight;
        }

        /// <summary>候选值。</summary>
        public T Value { get; }

        /// <summary>非负权重。</summary>
        public float Weight { get; }
    }

    /// <summary>
    /// 低分配带权随机工具。
    /// 不使用 LINQ，也不会为了单次抽取创建临时集合。
    /// </summary>
    public static class WeightedRandom
    {
        private const float MaxSampleBelowOne = 0.99999994f;

        /// <summary>
        /// 使用 Unity 随机源抽取一个值。
        /// </summary>
        public static bool TryChoose<T>(IReadOnlyList<WeightedValue<T>> candidates, out T value)
        {
            return TryChoose(candidates, UnityEngine.Random.value, out value);
        }

        /// <summary>
        /// 使用确定的 [0,1] 采样值抽取一个候选。
        /// 这个重载适合测试、回放和需要外部控制随机性的系统。
        /// </summary>
        /// <remarks>
        /// 如果集合为空、全部权重为 0，或存在负数/NaN/Infinity 权重，则返回 false。
        /// </remarks>
        public static bool TryChoose<T>(
            IReadOnlyList<WeightedValue<T>> candidates,
            float sample01,
            out T value)
        {
            value = default;
            if (candidates == null || candidates.Count == 0 || float.IsNaN(sample01))
            {
                return false;
            }

            double totalWeight = 0d;
            int lastPositiveIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                float weight = candidates[i].Weight;
                if (weight < 0f || float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    return false;
                }

                if (weight <= 0f)
                {
                    continue;
                }

                totalWeight += weight;
                lastPositiveIndex = i;
            }

            if (lastPositiveIndex < 0 || totalWeight <= 0d || double.IsInfinity(totalWeight))
            {
                return false;
            }

            float normalized = Mathf.Clamp(sample01, 0f, MaxSampleBelowOne);
            double threshold = normalized * totalWeight;
            double accumulated = 0d;

            for (int i = 0; i < candidates.Count; i++)
            {
                float weight = candidates[i].Weight;
                if (weight <= 0f)
                {
                    continue;
                }

                accumulated += weight;
                if (threshold < accumulated)
                {
                    value = candidates[i].Value;
                    return true;
                }
            }

            // 浮点累计误差的兜底：最后一个正权重候选必然合法。
            value = candidates[lastPositiveIndex].Value;
            return true;
        }
    }
}
