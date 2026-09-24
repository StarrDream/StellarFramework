using System;
using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 低分配地面检测组件。
    /// 使用 PhysicsProbe 统一执行 Ray/Sphere/Box/Capsule Cast，并通过稳定帧数过滤短暂接触抖动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundChecker : MonoBehaviour
    {
        [Header("Probe")]
        [SerializeField] private PhysicsProbeShape shape = PhysicsProbeShape.Sphere;
        [Tooltip("检测起点相对本物体的本地偏移。")]
        [SerializeField] private Vector3 localOffset;
        [Tooltip("检测方向。默认使用世界向下；开启 Local Direction 后会随物体旋转。")]
        [SerializeField] private Vector3 direction = Vector3.down;
        [SerializeField] private bool directionInLocalSpace;
        [Min(0f)] [SerializeField] private float distance = 0.25f;
        [Min(0f)] [SerializeField] private float radius = 0.2f;
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.2f, 0.05f, 0.2f);
        [Min(0f)] [SerializeField] private float capsuleHeight = 1f;

        [Header("Filter")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [Tooltip("连续命中多少个 FixedUpdate 后才切换到 Grounded。")]
        [Min(1)] [SerializeField] private int stableFrames = 2;

        [Header("Events")]
        [SerializeField] private UnityEvent onGrounded = new UnityEvent();
        [SerializeField] private UnityEvent onAirborne = new UnityEvent();

        private int _detectedFrames;

        /// <summary>当前稳定地面状态。</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>最近一次成功 Probe 的命中信息。</summary>
        public PhysicsProbeHit LastHit { get; private set; }

        /// <summary>C# 侧 Grounded 状态事件。</summary>
        public event Action Grounded;

        /// <summary>C# 侧 Airborne 状态事件。</summary>
        public event Action Airborne;

        /// <summary>运行时配置 Probe 几何。</summary>
        public void ConfigureProbe(
            PhysicsProbeShape probeShape,
            Vector3 offset,
            Vector3 probeDirection,
            float probeDistance,
            float probeRadius = 0.2f,
            Vector3? halfExtents = null,
            float probeCapsuleHeight = 1f,
            bool localDirection = false)
        {
            shape = probeShape;
            localOffset = offset;
            direction = probeDirection;
            distance = Mathf.Max(0f, probeDistance);
            radius = Mathf.Max(0f, probeRadius);
            boxHalfExtents = halfExtents ?? boxHalfExtents;
            capsuleHeight = Mathf.Max(0f, probeCapsuleHeight);
            directionInLocalSpace = localDirection;
        }

        /// <summary>运行时配置 Layer、Trigger 与稳定帧数。</summary>
        public void ConfigureFilter(
            LayerMask layers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore,
            int requiredStableFrames = 2)
        {
            groundLayers = layers;
            triggerInteraction = queryTriggerInteraction;
            stableFrames = Mathf.Max(1, requiredStableFrames);
            _detectedFrames = 0;
            IsGrounded = false;
            LastHit = default;
        }

        private void FixedUpdate()
        {
            Evaluate();
        }

        /// <summary>立即执行一次检测并更新稳定状态。</summary>
        public bool Evaluate()
        {
            bool detected = Probe(out PhysicsProbeHit hit);
            if (detected)
            {
                LastHit = hit;
                _detectedFrames++;
            }
            else
            {
                _detectedFrames = 0;
                LastHit = default;
            }

            bool newState = detected && _detectedFrames >= Mathf.Max(1, stableFrames);
            if (newState == IsGrounded)
            {
                return IsGrounded;
            }

            IsGrounded = newState;
            if (IsGrounded)
            {
                onGrounded?.Invoke();
                Grounded?.Invoke();
            }
            else
            {
                onAirborne?.Invoke();
                Airborne?.Invoke();
            }

            return IsGrounded;
        }

        /// <summary>只执行物理查询，不修改稳定帧计数和状态。</summary>
        public bool Probe(out PhysicsProbeHit hit)
        {
            Transform cachedTransform = transform;
            Vector3 worldDirection = directionInLocalSpace
                ? cachedTransform.TransformDirection(direction)
                : direction;

            var request = new PhysicsProbeRequest
            {
                Shape = shape,
                Origin = cachedTransform.TransformPoint(localOffset),
                Direction = worldDirection,
                Distance = distance,
                Radius = radius,
                HalfExtents = boxHalfExtents,
                CapsuleHeight = capsuleHeight,
                Orientation = cachedTransform.rotation,
                LayerMask = groundLayers,
                TriggerInteraction = triggerInteraction
            };
            return PhysicsProbe.TryCast(in request, out hit);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.TransformPoint(localOffset);
            Vector3 worldDirection = directionInLocalSpace
                ? transform.TransformDirection(direction)
                : direction;
            if (worldDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            worldDirection.Normalize();
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawLine(origin, origin + worldDirection * distance);

            if (shape == PhysicsProbeShape.Sphere)
            {
                Gizmos.DrawWireSphere(origin, radius);
                Gizmos.DrawWireSphere(origin + worldDirection * distance, radius);
            }
            else if (shape == PhysicsProbeShape.Box)
            {
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(origin, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
                Gizmos.matrix = old;
            }
        }
#endif
    }
}
