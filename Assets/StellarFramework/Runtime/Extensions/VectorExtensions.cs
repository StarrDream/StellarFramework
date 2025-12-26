using UnityEngine;

namespace StellarFramework
{
    public static class VectorExtensions
    {
        #region 更改分量 (WithX/Y/Z)

        /// <summary>
        /// 返回一个新的Vector3，仅修改X值
        /// </summary>
        public static Vector3 WithX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);

        /// <summary>
        /// 返回一个新的Vector3，仅修改Y值
        /// </summary>
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);

        /// <summary>
        /// 返回一个新的Vector3，仅修改Z值
        /// </summary>
        public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);

        /// <summary>
        /// 增加X值
        /// </summary>
        public static Vector3 AddX(this Vector3 v, float x) => new Vector3(v.x + x, v.y, v.z);

        /// <summary>
        /// 增加Y值
        /// </summary>
        public static Vector3 AddY(this Vector3 v, float y) => new Vector3(v.x, v.y + y, v.z);

        #endregion

        #region Vector2 扩展

        public static Vector2 WithX(this Vector2 v, float x) => new Vector2(x, v.y);
        public static Vector2 WithY(this Vector2 v, float y) => new Vector2(v.x, y);

        #endregion

        #region 计算

        /// <summary>
        /// 忽略Y轴计算距离 (常用于3D游戏中的平面距离)
        /// </summary>
        public static float FlatDistance(this Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        /// <summary>
        /// 向量取反 (v * -1)
        /// </summary>
        public static Vector3 Negate(this Vector3 v) => v * -1;

        #endregion
    }
}