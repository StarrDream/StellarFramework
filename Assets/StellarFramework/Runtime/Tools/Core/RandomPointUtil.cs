using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 常用空间随机点工具。
    /// 所有 API 都直接返回局部偏移，调用方可以自行加到出生点或区域中心。
    /// </summary>
    public static class RandomPointUtil
    {
        /// <summary>返回 XZ 平面圆形区域内均匀分布的随机点。</summary>
        public static Vector3 InsideCircleXZ(float radius)
        {
            Vector2 point = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, radius);
            return new Vector3(point.x, 0f, point.y);
        }

        /// <summary>返回 XZ 平面圆周上的随机点。</summary>
        public static Vector3 OnCircleXZ(float radius)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float clampedRadius = Mathf.Max(0f, radius);
            return new Vector3(Mathf.Cos(angle) * clampedRadius, 0f, Mathf.Sin(angle) * clampedRadius);
        }

        /// <summary>返回球体内部均匀分布的随机点。</summary>
        public static Vector3 InsideSphere(float radius)
        {
            return UnityEngine.Random.insideUnitSphere * Mathf.Max(0f, radius);
        }

        /// <summary>返回 Bounds 内的随机点。</summary>
        public static Vector3 InsideBounds(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new Vector3(
                UnityEngine.Random.Range(min.x, max.x),
                UnityEngine.Random.Range(min.y, max.y),
                UnityEngine.Random.Range(min.z, max.z));
        }
    }
}
