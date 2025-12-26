using Minimap.Core;
using Minimap.Markers;
using StellarFramework;
using UnityEngine;

namespace Minimap.Extras
{
    /// <summary>
    /// 任务导航控制器 (通用版)
    /// 功能：
    /// 1. 在指定位置生成任务图标
    /// 2. 接收任意的世界坐标数组绘制路径
    /// </summary>
    public class QuestNavigator : MonoBehaviour
    {
        [Header("图标样式")] public MapMarker questMarkerPrefab; // 指定任务专用的 Prefab
        public Sprite questIcon;
        public Color questColor = Color.yellow;
        [Tooltip("图标是否始终吸附在地图边缘 (指示方向)")] public bool clampToBorder = true;

        [Header("模块引用")] [Tooltip("必须引用场景中的 RouteManager 用于画线")]
        public MinimapRouteManager routeManager;

        // 内部状态：当前的任务图标实例
        private MapMarker _currentQuestMarker;

        // 内部状态：为了让MinimapManager能追踪位置，我们需要一个临时的GameObject
        private GameObject _tempTargetObj;

        /// <summary>
        /// 模式 A: 仅显示任务图标 (不画线)
        /// </summary>
        public void SetQuestMarker(Vector3 worldPosition)
        {
            CreateOrUpdateMarker(worldPosition);

            // 如果之前有线，清空掉
            if (routeManager != null) routeManager.ClearRoute();

            LogKit.Log($"[QuestNavigator] 任务点已设定在: {worldPosition}");
        }

        /// <summary>
        /// 模式 B: 显示图标 + 自定义路径
        /// 图标会自动生成在路径的【最后一个点】
        /// </summary>
        /// <param name="pathPoints">世界坐标数组 (起点 -> ... -> 终点)</param>
        public void SetQuestPath(Vector3[] pathPoints)
        {
            if (pathPoints == null || pathPoints.Length == 0)
            {
                LogKit.LogError("[QuestNavigator] 传入的路径数组为空！");
                return;
            }

            // 1. 在终点生成图标
            Vector3 endPoint = pathPoints[pathPoints.Length - 1];
            CreateOrUpdateMarker(endPoint);

            // 2. 画线
            if (routeManager != null)
            {
                routeManager.DrawRoute(pathPoints);
            }

            LogKit.Log($"[QuestNavigator] 自定义路径已绘制，节点数: {pathPoints.Length}");
        }

        /// <summary>
        /// 模式 C: 显式指定图标位置 + 自定义路径
        /// (适用于：路径终点和图标位置不完全重合的情况，比如路径只导向门口，图标在屋内)
        /// </summary>
        public void SetQuestPath(Vector3 targetPos, Vector3[] pathPoints)
        {
            CreateOrUpdateMarker(targetPos);

            if (routeManager != null && pathPoints != null)
            {
                routeManager.DrawRoute(pathPoints);
            }
        }

        /// <summary>
        /// 清理当前任务 (移除图标和线条)
        /// </summary>
        public void ClearQuest()
        {
            // 1. 移除小地图图标
            if (_currentQuestMarker != null && MinimapManager.Instance != null)
            {
                MinimapManager.Instance.RemoveMarker(_currentQuestMarker);
                _currentQuestMarker = null;
            }

            // 2. 销毁临时物体
            if (_tempTargetObj != null)
            {
                Destroy(_tempTargetObj);
                _tempTargetObj = null;
            }

            // 3. 清除线条
            if (routeManager != null)
            {
                routeManager.ClearRoute();
            }

            LogKit.Log("[QuestNavigator] 任务导航已清理");
        }

        // --- 内部私有方法 ---

        private void CreateOrUpdateMarker(Vector3 pos)
        {
            // 如果临时物体不存在，创建一个
            if (_tempTargetObj == null)
            {
                _tempTargetObj = new GameObject("Quest_Marker_Ref");
            }

            _tempTargetObj.transform.position = pos;

            // 如果图标还没注册，注册一个
            if (_currentQuestMarker == null && MinimapManager.Instance != null)
            {
                _currentQuestMarker = MinimapManager.Instance.Register(
                    target: _tempTargetObj.transform,
                    prefabOverride: questMarkerPrefab, // 传入任务专用 Prefab
                    icon: questIcon,
                    color: questColor,
                    syncRotation: false,
                    clampToBorder: clampToBorder,
                    hideOutOfBounds: false
                );
            }
        }
    }
}