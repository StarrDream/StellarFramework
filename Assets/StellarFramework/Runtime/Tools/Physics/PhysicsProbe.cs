using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>通用 Physics Cast 形状。</summary>
    public enum PhysicsProbeShape
    {
        /// <summary>普通 Raycast。</summary>
        Ray,
        /// <summary>球形 Sweep Cast。</summary>
        Sphere,
        /// <summary>盒体 Sweep Cast。</summary>
        Box,
        /// <summary>胶囊体 Sweep Cast。</summary>
        Capsule
    }

    /// <summary>
    /// 一次 Physics Probe 请求。
    /// Box 使用 <see cref="HalfExtents"/>；Sphere/Capsule 使用 <see cref="Radius"/>；
    /// Capsule 的总高度由 <see cref="CapsuleHeight"/> 指定，并沿 Orientation 的本地 Y 轴延伸。
    /// </summary>
    [Serializable]
    public struct PhysicsProbeRequest
    {
        /// <summary>Cast 形状。</summary>
        public PhysicsProbeShape Shape;
        /// <summary>世界空间起点；Capsule 时表示胶囊中心。</summary>
        public Vector3 Origin;
        /// <summary>世界空间检测方向；执行时会归一化。</summary>
        public Vector3 Direction;
        /// <summary>最大检测距离，负值按 0 处理。</summary>
        public float Distance;
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

        /// <summary>创建默认 Ray Probe。</summary>
        public static PhysicsProbeRequest Ray(
            Vector3 origin,
            Vector3 direction,
            float distance,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return new PhysicsProbeRequest
            {
                Shape = PhysicsProbeShape.Ray,
                Origin = origin,
                Direction = direction,
                Distance = distance,
                Radius = 0f,
                HalfExtents = Vector3.zero,
                CapsuleHeight = 0f,
                Orientation = Quaternion.identity,
                LayerMask = layerMask,
                TriggerInteraction = triggerInteraction
            };
        }
    }

    /// <summary>统一的 Physics Probe 命中结果。</summary>
    public readonly struct PhysicsProbeHit
    {
        internal PhysicsProbeHit(RaycastHit hit)
        {
            RawHit = hit;
        }

        /// <summary>Unity 原始 RaycastHit。</summary>
        public RaycastHit RawHit { get; }

        /// <summary>命中的 Collider。</summary>
        public Collider Collider => RawHit.collider;
        /// <summary>命中对象 Transform。</summary>
        public Transform Transform => RawHit.transform;
        /// <summary>命中对象关联 Rigidbody；可能为空。</summary>
        public Rigidbody Rigidbody => RawHit.rigidbody;
        /// <summary>世界空间命中点。</summary>
        public Vector3 Point => RawHit.point;
        /// <summary>世界空间命中法线。</summary>
        public Vector3 Normal => RawHit.normal;
        /// <summary>沿 Cast 方向的命中距离。</summary>
        public float Distance => RawHit.distance;
    }

    /// <summary>
    /// 对 Ray/Sphere/Box/Capsule Cast 的统一薄封装。
    /// 它不分配临时集合，也不缓存全局状态，适合被 GroundChecker 等更高层工具复用。
    /// </summary>
    public static class PhysicsProbe
    {
        /// <summary>执行一次 Probe。</summary>
        public static bool TryCast(in PhysicsProbeRequest request, out PhysicsProbeHit hit)
        {
            hit = default;
            if (!TryNormalizeRequest(in request, out Vector3 direction, out float distance))
            {
                return false;
            }

            RaycastHit rawHit;
            bool detected;
            switch (request.Shape)
            {
                case PhysicsProbeShape.Ray:
                    detected = UnityEngine.Physics.Raycast(
                        request.Origin,
                        direction,
                        out rawHit,
                        distance,
                        request.LayerMask,
                        request.TriggerInteraction);
                    break;

                case PhysicsProbeShape.Sphere:
                    detected = UnityEngine.Physics.SphereCast(
                        request.Origin,
                        Mathf.Max(0f, request.Radius),
                        direction,
                        out rawHit,
                        distance,
                        request.LayerMask,
                        request.TriggerInteraction);
                    break;

                case PhysicsProbeShape.Box:
                    detected = UnityEngine.Physics.BoxCast(
                        request.Origin,
                        Abs(request.HalfExtents),
                        direction,
                        out rawHit,
                        request.Orientation,
                        distance,
                        request.LayerMask,
                        request.TriggerInteraction);
                    break;

                case PhysicsProbeShape.Capsule:
                    GetCapsuleEndpoints(in request, out Vector3 pointA, out Vector3 pointB, out float radius);
                    detected = UnityEngine.Physics.CapsuleCast(
                        pointA,
                        pointB,
                        radius,
                        direction,
                        out rawHit,
                        distance,
                        request.LayerMask,
                        request.TriggerInteraction);
                    break;

                default:
                    return false;
            }

            if (!detected)
            {
                return false;
            }

            hit = new PhysicsProbeHit(rawHit);
            return true;
        }

        private static bool TryNormalizeRequest(
            in PhysicsProbeRequest request,
            out Vector3 direction,
            out float distance)
        {
            direction = request.Direction;
            distance = Mathf.Max(0f, request.Distance);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }

        private static void GetCapsuleEndpoints(
            in PhysicsProbeRequest request,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            radius = Mathf.Max(0f, request.Radius);
            float height = Mathf.Max(radius * 2f, request.CapsuleHeight);
            float segmentHalfLength = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 axis = request.Orientation * Vector3.up;
            pointA = request.Origin + axis * segmentHalfLength;
            pointB = request.Origin - axis * segmentHalfLength;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
