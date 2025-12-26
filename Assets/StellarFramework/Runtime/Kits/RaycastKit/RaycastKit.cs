// ========== RaycastKit.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/RaycastKit/RaycastKit.cs

using UnityEngine;
using UnityEngine.EventSystems;

namespace StellarFramework
{
    /// <summary>
    /// 射线检测工具箱 (核心库)
    /// <para>职责：统一处理 Screen-to-World, World-to-World, 2D/3D 以及 UI 遮挡检测。</para>
    /// <para>优势：集中管理摄像机获取逻辑、UI 遮挡逻辑，减少重复代码。</para>
    /// </summary>
    public static class RaycastKit
    {
        // 缓存主相机引用，避免频繁调用 Camera.main (Camera.main 在旧版本 Unity 中开销较大)
        private static Camera _cachedMainCamera;

        private static int _lastSceneIndex = -1;

        /// <summary>
        /// 获取安全的摄像机实例
        /// <para>优先使用传入的 customCamera，如果为空则使用缓存的 Camera.main</para>
        /// </summary>
        public static Camera GetSafeCamera(Camera customCamera = null)
        {
            if (customCamera != null) return customCamera;

            // [修复] 检测场景切换，防止引用已销毁的相机
            int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
            if (currentSceneIndex != _lastSceneIndex)
            {
                _cachedMainCamera = null;
                _lastSceneIndex = currentSceneIndex;
            }

            if (_cachedMainCamera == null) _cachedMainCamera = Camera.main;
            return _cachedMainCamera;
        }

        #region UI Blocking (UI遮挡检测)

        /// <summary>
        /// 检查指针是否悬停在 UI 上 (EventSystem)
        /// <para>兼容 移动端触摸 和 桌面端鼠标</para>
        /// </summary>
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            //  移动端优先检测触摸
#if UNITY_ANDROID || UNITY_IOS
            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            // 编辑器模拟触摸时的兜底（使用 -1 代表鼠标）
            return EventSystem.current.IsPointerOverGameObject(-1);
#else
    // 桌面端使用无参版本
    return EventSystem.current.IsPointerOverGameObject();
#endif
        }

        #endregion

        #region Screen to World (3D)

        /// <summary>
        /// 从屏幕点发射射线检测 3D 物体 (通用)
        /// </summary>
        /// <param name="screenPos">屏幕坐标 (Input.mousePosition)</param>
        /// <param name="hitInfo">输出的碰撞信息</param>
        /// <param name="maxDistance">最大检测距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="camera">指定相机 (为空则自动获取)</param>
        /// <param name="queryTrigger">是否检测 Trigger 类型的碰撞体</param>
        public static bool Raycast3D(Vector2 screenPos, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = ~0, Camera camera = null,
            QueryTriggerInteraction queryTrigger = QueryTriggerInteraction.UseGlobal)
        {
            var cam = GetSafeCamera(camera);
            if (cam == null)
            {
                hitInfo = default;
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out hitInfo, maxDistance, layerMask, queryTrigger);
        }

        /// <summary>
        /// 检查屏幕点是否击中了 *特定* 的 Collider (3D)
        /// <para>优化：会自动将 LayerMask 限制为目标所在的 Layer，减少不必要的运算</para>
        /// </summary>
        public static bool IsTargetUnderScreenPoint(Vector2 screenPos, Collider targetCollider, out Vector3 hitPoint, Camera camera = null, int layerMask = ~0)
        {
            hitPoint = Vector3.zero;
            if (targetCollider == null) return false;

            // 性能优化：仅在目标所在的 Layer 进行射线检测
            // 这样可以避免射线被其他 Layer 的物体挡住，或者浪费计算资源
            int finalMask = layerMask & (1 << targetCollider.gameObject.layer);

            if (Raycast3D(screenPos, out var hit, Mathf.Infinity, finalMask, camera))
            {
                // 再次确认击中的确实是目标 Collider (防止同 Layer 其他物体遮挡)
                if (hit.collider == targetCollider)
                {
                    hitPoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Screen to World (2D)

        /// <summary>
        /// 从屏幕点发射射线检测 2D 物体 (Sprite/UI)
        /// </summary>
        public static bool Raycast2D(Vector2 screenPos, out RaycastHit2D hitInfo, float maxDistance = Mathf.Infinity, int layerMask = ~0, Camera camera = null)
        {
            var cam = GetSafeCamera(camera);
            if (cam == null)
            {
                hitInfo = default;
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);

            // 注意：Physics2D.GetRayIntersection 比 Physics2D.Raycast 更适合这种场景
            // 因为它支持透视相机发射的 3D 射线去检测 2D Collider
            hitInfo = Physics2D.GetRayIntersection(ray, maxDistance, layerMask);
            return hitInfo.collider != null;
        }

        /// <summary>
        /// 检查屏幕点是否击中了 *特定* 的 Collider2D
        /// </summary>
        public static bool IsTargetUnderScreenPoint2D(Vector2 screenPos, Collider2D targetCollider, out Vector3 hitPoint, Camera camera = null, int layerMask = ~0)
        {
            hitPoint = Vector3.zero;
            if (targetCollider == null) return false;

            // 性能优化：限制 LayerMask
            int finalMask = layerMask & (1 << targetCollider.gameObject.layer);

            if (Raycast2D(screenPos, out var hit, Mathf.Infinity, finalMask, camera))
            {
                if (hit.collider == targetCollider)
                {
                    hitPoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region World to World (通用射线)

        /// <summary>
        /// 世界空间通用射线检测 (Physics.Raycast 的简单封装)
        /// </summary>
        public static bool RaycastWorld(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float distance, int layerMask = ~0,
            QueryTriggerInteraction queryTrigger = QueryTriggerInteraction.UseGlobal)
        {
            return Physics.Raycast(origin, direction, out hitInfo, distance, layerMask, queryTrigger);
        }

        /// <summary>
        /// 视线检测 (Line of Sight)
        /// <para>判断从 origin 到 target 之间是否有障碍物遮挡</para>
        /// </summary>
        /// <param name="checkChildColliders">如果击中了 target 的子物体，是否也算作"无遮挡"</param>
        public static bool HasLineOfSight(Vector3 origin, Transform target, out RaycastHit hitInfo, int layerMask = ~0, bool checkChildColliders = true)
        {
            if (target == null)
            {
                hitInfo = default;
                return false;
            }

            Vector3 direction = target.position - origin;
            float distance = direction.magnitude;

            // 发射射线
            if (Physics.Raycast(origin, direction, out hitInfo, distance, layerMask, QueryTriggerInteraction.Ignore))
            {
                // 情况1: 直接击中目标本身 -> 有视线
                if (hitInfo.transform == target) return true;

                // 情况2: 击中了目标的子物体 (例如目标的某个部位) -> 有视线
                if (checkChildColliders && hitInfo.transform.IsChildOf(target)) return true;

                // 情况3: 击中了其他物体 -> 视线被遮挡
                return false;
            }

            // 情况4: 射线没有击中任何东西
            // 这通常意味着中间没有障碍物（但也可能意味着没击中目标，如果目标没有Collider）
            // 在视线检测逻辑中，未击中障碍物通常视为"可见"
            return true;
        }

        #endregion
    }
}