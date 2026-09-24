using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>无分配 Overlap Query 支持的形状。</summary>
    public enum PhysicsOverlapShape
    {
        /// <summary>球形区域。</summary>
        Sphere,
        /// <summary>可旋转盒体区域。</summary>
        Box,
        /// <summary>沿 Orientation 本地 Y 轴延伸的胶囊区域。</summary>
        Capsule
    }

    /// <summary>
    /// 一次 Physics Overlap 请求。
    /// 调用方持有结果数组，RuntimeTools 不创建 Collider 工作集合。
    /// </summary>
    [Serializable]
    public struct PhysicsOverlapRequest
    {
        /// <summary>Overlap 形状。</summary>
        public PhysicsOverlapShape Shape;
        /// <summary>世界空间中心。</summary>
        public Vector3 Center;
        /// <summary>Sphere/Capsule 半径，负值按 0 处理。</summary>
        public float Radius;
        /// <summary>Box 半尺寸，内部按绝对值使用。</summary>
        public Vector3 HalfExtents;
        /// <summary>Capsule 总高度；最终不会小于直径。</summary>
        public float CapsuleHeight;
        /// <summary>Box 朝向，或 Capsule 本地 Y 轴朝向。</summary>
        public Quaternion Orientation;
        /// <summary>参与检测的 LayerMask。</summary>
        public LayerMask LayerMask;
        /// <summary>Trigger 查询策略。</summary>
        public QueryTriggerInteraction TriggerInteraction;

        /// <summary>创建球形 Overlap 请求。</summary>
        public static PhysicsOverlapRequest Sphere(
            Vector3 center,
            float radius,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return new PhysicsOverlapRequest
            {
                Shape = PhysicsOverlapShape.Sphere,
                Center = center,
                Radius = radius,
                HalfExtents = Vector3.zero,
                CapsuleHeight = 0f,
                Orientation = Quaternion.identity,
                LayerMask = layerMask,
                TriggerInteraction = triggerInteraction
            };
        }

        /// <summary>创建盒体 Overlap 请求。</summary>
        public static PhysicsOverlapRequest Box(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion orientation,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return new PhysicsOverlapRequest
            {
                Shape = PhysicsOverlapShape.Box,
                Center = center,
                Radius = 0f,
                HalfExtents = halfExtents,
                CapsuleHeight = 0f,
                Orientation = orientation,
                LayerMask = layerMask,
                TriggerInteraction = triggerInteraction
            };
        }

        /// <summary>创建胶囊体 Overlap 请求。</summary>
        public static PhysicsOverlapRequest Capsule(
            Vector3 center,
            float radius,
            float height,
            Quaternion orientation,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return new PhysicsOverlapRequest
            {
                Shape = PhysicsOverlapShape.Capsule,
                Center = center,
                Radius = radius,
                HalfExtents = Vector3.zero,
                CapsuleHeight = height,
                Orientation = orientation,
                LayerMask = layerMask,
                TriggerInteraction = triggerInteraction
            };
        }
    }

    /// <summary>
    /// 对 Sphere/Box/Capsule OverlapNonAlloc 的统一薄封装。
    /// 结果直接写入调用方数组，返回值与 Unity NonAlloc API 一致：数组满时只返回可写入的数量。
    /// </summary>
    public static class PhysicsOverlap
    {
        /// <summary>
        /// 执行一次无分配 Overlap Query。
        /// results 为 null 或长度为 0 时直接返回 0。
        /// </summary>
        public static int QueryNonAlloc(in PhysicsOverlapRequest request, Collider[] results)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            switch (request.Shape)
            {
                case PhysicsOverlapShape.Sphere:
                    return UnityEngine.Physics.OverlapSphereNonAlloc(
                        request.Center,
                        Mathf.Max(0f, request.Radius),
                        results,
                        request.LayerMask,
                        request.TriggerInteraction);

                case PhysicsOverlapShape.Box:
                    return UnityEngine.Physics.OverlapBoxNonAlloc(
                        request.Center,
                        Abs(request.HalfExtents),
                        results,
                        request.Orientation,
                        request.LayerMask,
                        request.TriggerInteraction);

                case PhysicsOverlapShape.Capsule:
                    GetCapsuleEndpoints(in request, out Vector3 pointA, out Vector3 pointB, out float radius);
                    return UnityEngine.Physics.OverlapCapsuleNonAlloc(
                        pointA,
                        pointB,
                        radius,
                        results,
                        request.LayerMask,
                        request.TriggerInteraction);

                default:
                    return 0;
            }
        }

        private static void GetCapsuleEndpoints(
            in PhysicsOverlapRequest request,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            radius = Mathf.Max(0f, request.Radius);
            float height = Mathf.Max(radius * 2f, request.CapsuleHeight);
            float segmentHalfLength = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 axis = request.Orientation * Vector3.up;
            pointA = request.Center + axis * segmentHalfLength;
            pointB = request.Center - axis * segmentHalfLength;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
