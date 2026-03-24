using UnityEngine;
using StellarFramework;

namespace StellarFramework.Examples
{
    /// <summary>
    /// RaycastKit 综合使用示例
    /// 演示 UI 遮挡拦截、3D 屏幕射线与视线检测
    /// </summary>
    public class Example_RaycastKit : MonoBehaviour
    {
        public Transform EnemyHead;
        public LayerMask ObstacleLayer;

        private void Update()
        {
            HandleMouseClick();
            HandleLineOfSight();
        }

        private void HandleMouseClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // 规范 1：前置拦截 UI 遮挡。如果点击在 UI 上，直接 return，防止点穿 UI 触发 3D 逻辑
            if (RaycastKit.IsPointerOverUI())
            {
                LogKit.Log("[Example_RaycastKit] 点击被 UI 遮挡，已拦截");
                return;
            }

            // 规范 2：使用封装好的射线检测，内部已处理 Camera.main 的缓存与安全获取
            if (RaycastKit.Raycast3D(Input.mousePosition, out RaycastHit hit))
            {
                LogKit.Log($"[Example_RaycastKit] 射线击中 3D 物体: {hit.collider.name}, 坐标: {hit.point}");
            }
        }

        private void HandleLineOfSight()
        {
            if (EnemyHead == null) return;

            // 规范 3：视线检测 (Line of Sight)。判断当前物体到目标之间是否有障碍物
            if (RaycastKit.HasLineOfSight(transform.position, EnemyHead, out RaycastHit hitInfo, ObstacleLayer))
            {
                Debug.DrawLine(transform.position, EnemyHead.position, Color.green);
            }
            else
            {
                Debug.DrawLine(transform.position, hitInfo.point, Color.red);
            }
        }
    }
}