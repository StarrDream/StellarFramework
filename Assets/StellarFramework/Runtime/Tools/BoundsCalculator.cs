using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    ///     通过碰撞体计算物体的边界范围
    ///     输出每个轴的最小值和最大值
    /// </summary>
    public class BoundsCalculator : MonoBehaviour
    {
        [Header("边界范围")] [SerializeField] private float xMin; // X轴最小值
        [SerializeField] private float xMax; // X轴最大值
        [SerializeField] private float yMin; // Y轴最小值
        [SerializeField] private float yMax; // Y轴最大值
        [SerializeField] private float zMin; // Z轴最小值
        [SerializeField] private float zMax; // Z轴最大值

        [Header("中心点和尺寸")] [SerializeField] private Vector3 center; // 边界的中心点
        [SerializeField] private Vector3 size; // 物体的长宽高

        [Header("设置")] [SerializeField] private bool autoCalculateOnStart = true; // 是否在Start时自动计算
        [SerializeField] private bool includeChildren = true; // 是否包含子物体的碰撞体

        private Bounds bounds;

        private void Start()
        {
            if (autoCalculateOnStart) CalculateBounds();
        }

        /// <summary>
        ///     在Scene视图中绘制边界框
        /// </summary>
        private void OnDrawGizmos()
        {
            if (bounds.size != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        /// <summary>
        ///     在选中时绘制更明显的边界框和坐标轴
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (bounds.size != Vector3.zero)
            {
                // 绘制边界框
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(bounds.center, bounds.size);

                // 绘制中心点
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(bounds.center, 0.1f);

                // 绘制边界的8个顶点
                var corners = new Vector3[8];
                corners[0] = new Vector3(xMin, yMin, zMin);
                corners[1] = new Vector3(xMax, yMin, zMin);
                corners[2] = new Vector3(xMin, yMax, zMin);
                corners[3] = new Vector3(xMax, yMax, zMin);
                corners[4] = new Vector3(xMin, yMin, zMax);
                corners[5] = new Vector3(xMax, yMin, zMax);
                corners[6] = new Vector3(xMin, yMax, zMax);
                corners[7] = new Vector3(xMax, yMax, zMax);

                Gizmos.color = Color.cyan;
                foreach (var corner in corners) Gizmos.DrawSphere(corner, 0.05f);
            }
        }

        /// <summary>
        ///     计算物体的边界
        /// </summary>
        [ContextMenu("计算边界大小")]
        public void CalculateBounds()
        {
            Collider[] colliders;

            if (includeChildren)
                // 获取自身及所有子物体的碰撞体
                colliders = GetComponentsInChildren<Collider>();
            else
                // 只获取自身的碰撞体
                colliders = GetComponents<Collider>();

            if (colliders.Length == 0)
            {
                LogKit.LogWarning($"物体 {gameObject.name} 上没有找到碰撞体！");
                return;
            }

            // 初始化边界为第一个碰撞体的边界
            bounds = colliders[0].bounds;

            // 遍历所有碰撞体，扩展边界以包含所有碰撞体
            for (var i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);

            // 计算边界的最小值和最大值
            var min = bounds.min;
            var max = bounds.max;

            xMin = min.x;
            xMax = max.x;
            yMin = min.y;
            yMax = max.y;
            zMin = min.z;
            zMax = max.z;

            // 更新中心点和尺寸
            center = bounds.center;
            size = bounds.size;

            LogKit.Log($"物体: {gameObject.name}\n" +
                      $"X: {xMin:F3} ~ {xMax:F3}\n" +
                      $"Y: {yMin:F3} ~ {yMax:F3}\n" +
                      $"Z: {zMin:F3} ~ {zMax:F3}\n" +
                      $"中心点: ({center.x:F3}, {center.y:F3}, {center.z:F3})\n" +
                      $"尺寸: 长={size.x:F3}, 高={size.y:F3}, 宽={size.z:F3}");
        }

        /// <summary>
        ///     获取X轴范围
        /// </summary>
        public void GetXRange(out float min, out float max)
        {
            min = xMin;
            max = xMax;
        }

        /// <summary>
        ///     获取Y轴范围
        /// </summary>
        public void GetYRange(out float min, out float max)
        {
            min = yMin;
            max = yMax;
        }

        /// <summary>
        ///     获取Z轴范围
        /// </summary>
        public void GetZRange(out float min, out float max)
        {
            min = zMin;
            max = zMax;
        }

        /// <summary>
        ///     获取所有边界值
        /// </summary>
        public void GetAllBounds(out float xMin, out float xMax,
            out float yMin, out float yMax,
            out float zMin, out float zMax)
        {
            xMin = this.xMin;
            xMax = this.xMax;
            yMin = this.yMin;
            yMax = this.yMax;
            zMin = this.zMin;
            zMax = this.zMax;
        }

        /// <summary>
        ///     获取边界中心点
        /// </summary>
        public Vector3 GetCenter()
        {
            return center;
        }

        /// <summary>
        ///     获取边界尺寸
        /// </summary>
        public Vector3 GetSize()
        {
            return size;
        }

        /// <summary>
        ///     获取完整的Bounds对象
        /// </summary>
        public Bounds GetBounds()
        {
            return bounds;
        }

        /// <summary>
        ///     打印边界信息到控制台
        /// </summary>
        public void PrintBounds()
        {
            LogKit.Log($"=== {gameObject.name} 边界信息 ===\n" +
                      $"X轴: {xMin:F3} 到 {xMax:F3}\n" +
                      $"Y轴: {yMin:F3} 到 {yMax:F3}\n" +
                      $"Z轴: {zMin:F3} 到 {zMax:F3}\n" +
                      $"中心点: ({center.x:F3}, {center.y:F3}, {center.z:F3})");
        }
    }
}