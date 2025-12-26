using System.Runtime.CompilerServices; // 用于内联优化
using UnityEngine;

namespace StellarFramework
{
    public static class MathUtil
    {
        /// <summary>
        /// 计算 2D 平面距离 (忽略 Y 轴)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 激进内联，减少高频调用的栈开销
        public static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 计算 2D 平面距离的平方 (高性能版本)
        /// 如果只是比较距离大小（如 dist < attackRange），请务必使用此方法，避免开方运算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance2DSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// 角度转方向向量 (Y轴平面)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AngleToDirection(float angle)
        {
            // 预计算 Deg2Rad 常量是编译期完成的，这里直接乘即可
            float radian = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radian), 0, Mathf.Cos(radian));
        }

        /// <summary>
        /// 方向向量转角度 (Y轴平面)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DirectionToAngle(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 简单的数值重映射 (Remap)
        /// 将 value 从 [from1, to1] 映射到 [from2, to2]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        // SmoothDampMove 只是对 Unity API 的简单包装，内联即可
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SmoothDampMove(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
        {
            return Vector3.SmoothDamp(current, target, ref velocity, smoothTime);
        }

        // 缓动函数
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 在立方体区域内随机取点
        /// </summary>
        public static Vector3 RandomVector3(Vector3 min, Vector3 max)
        {
            return new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
            );
        }
    }
}