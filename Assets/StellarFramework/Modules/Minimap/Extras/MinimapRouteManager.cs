using System.Collections.Generic;
using Minimap.Core;
using UnityEngine;

namespace Minimap.Extras
{
    /// <summary>
    /// 小地图路径导航管理器
    /// 用法：调用 DrawRoute(List<Vector3> worldPoints) 即可
    /// </summary>
    [RequireComponent(typeof(UIMapLineRenderer))]
    public class MinimapRouteManager : MonoBehaviour
    {
        private UIMapLineRenderer _lineRenderer;
        private List<Vector2> _cacheUIPoints = new List<Vector2>(64);

        private void Awake()
        {
            _lineRenderer = GetComponent<UIMapLineRenderer>();
            // 确保射线不阻挡鼠标操作地图
            _lineRenderer.raycastTarget = false;
        }

        /// <summary>
        /// 输入世界坐标路径点，绘制小地图导航线
        /// </summary>
        /// <param name="worldPathPoints">通常来自 NavMeshAgent.path.corners</param>
        public void DrawRoute(Vector3[] worldPathPoints)
        {
            if (MinimapManager.Instance == null || MinimapManager.Instance.mapConfig == null)
                return;

            var config = MinimapManager.Instance.mapConfig;
            _cacheUIPoints.Clear();

            // 遍历所有世界坐标点，转换为 UI 坐标
            for (int i = 0; i < worldPathPoints.Length; i++)
            {
                Vector3 worldPos = worldPathPoints[i];

                // 核心：使用之前的 CoordinateMapper 进行转换
                Vector2 uiPos = CoordinateMapper.WorldToMapPosition(worldPos, config);

                _cacheUIPoints.Add(uiPos);
            }

            // 提交给渲染器
            _lineRenderer.SetPoints(_cacheUIPoints);
        }

        /// <summary>
        /// 清除路径
        /// </summary>
        public void ClearRoute()
        {
            _lineRenderer.SetPoints(null);
        }
    }
}