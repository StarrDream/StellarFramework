using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework
{
    [DisallowMultipleComponent]
    public class MouseFollower : MonoBehaviour
    {
        public enum FollowMode
        {
            TwoD,
            ThreeDPlane,
            ThreeDRaycast
        }

        public enum PlaneNormalMode
        {
            WorldUp,
            Custom
        }

        [Header("基本配置")] public FollowMode followMode = FollowMode.TwoD;
        public Camera targetCamera;
        public bool autoStart = true;

        [Tooltip("TwoD / FixedDepth 模式深度")] public float fixedDepth;

        [Header("ThreeDPlane 模式配置")] public bool useInitialPlane = true;
        public PlaneNormalMode planeNormalMode = PlaneNormalMode.WorldUp;
        public Vector3 customPlaneNormal = Vector3.up;
        public Vector3 planePoint = Vector3.zero;

        [Header("ThreeDRaycast 模式配置")] public LayerMask raycastLayers = ~0;
        public bool keepLastPositionIfNoHit = true;

        [Header("平滑 & 约束")] public bool smooth = true;
        public float smoothTime = 0.08f;
        public bool freezeX;
        public bool freezeY;
        public bool freezeZ;

        [Header("事件回调")] public UnityEvent onFollowStarted;
        public UnityEvent onFollowStopped;
        public UnityEvent onReset;

        private float depthFromCamera;
        private Plane followPlane;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 velocityRef;

        public bool IsFollowing { get; private set; }

        private void Awake()
        {
            if (!targetCamera) targetCamera = Camera.main;
            initialPosition = transform.position;
            initialRotation = transform.rotation;

            if (followMode == FollowMode.TwoD)
            {
                depthFromCamera = targetCamera
                    ? (transform.position - targetCamera.transform.position).magnitude
                    : 10f;
                if (fixedDepth > 0f) depthFromCamera = fixedDepth;
            }
            else if (followMode == FollowMode.ThreeDPlane)
            {
                RebuildPlane();
            }

            if (autoStart) StartFollow();
        }

        private void Update()
        {
            if (!IsFollowing || !targetCamera) return;

            if (TryGetTargetPosition(out Vector3 targetPos))
            {
                var current = transform.position;
                if (freezeX) targetPos.x = current.x;
                if (freezeY) targetPos.y = current.y;
                if (freezeZ) targetPos.z = current.z;

                if (smooth)
                    transform.position = Vector3.SmoothDamp(current, targetPos, ref velocityRef, Mathf.Max(0.0001f, smoothTime));
                else
                    transform.position = targetPos;
            }
        }

        private bool TryGetTargetPosition(out Vector3 targetPos)
        {
            targetPos = transform.position;
            var mousePos = Input.mousePosition;

            switch (followMode)
            {
                case FollowMode.TwoD:
                    var sp = mousePos;
                    sp.z = depthFromCamera;
                    targetPos = targetCamera.ScreenToWorldPoint(sp);
                    return true;

                case FollowMode.ThreeDPlane:
                    var ray = targetCamera.ScreenPointToRay(mousePos);
                    if (followPlane.Raycast(ray, out float enter))
                    {
                        targetPos = ray.GetPoint(enter);
                        return true;
                    }

                    return false;

                case FollowMode.ThreeDRaycast:
                    // 使用 RaycastKit 替代 Physics.Raycast
                    if (RaycastKit.Raycast3D(mousePos, out var hit, 1000f, raycastLayers, targetCamera, QueryTriggerInteraction.Ignore))
                    {
                        targetPos = hit.point;
                        return true;
                    }

                    return !keepLastPositionIfNoHit;
            }

            return false;
        }

        public void StartFollow()
        {
            if (IsFollowing) return;
            IsFollowing = true;
            onFollowStarted?.Invoke();
        }

        public void StopFollow()
        {
            if (!IsFollowing) return;
            IsFollowing = false;
            onFollowStopped?.Invoke();
        }

        public void ResetPosition(bool stopFollowAfterReset = false)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            velocityRef = Vector3.zero;
            if (stopFollowAfterReset) StopFollow();
            onReset?.Invoke();
        }

        public void RebuildPlane()
        {
            var planeN = planeNormalMode == PlaneNormalMode.WorldUp ? Vector3.up : customPlaneNormal.normalized;
            var planeOrigin = useInitialPlane ? initialPosition : planePoint;
            followPlane = new Plane(planeN, planeOrigin);
        }

        public void SetFollowMode(FollowMode mode)
        {
            followMode = mode;
            if (mode == FollowMode.TwoD)
            {
                depthFromCamera = targetCamera ? (transform.position - targetCamera.transform.position).magnitude : 10f;
                if (fixedDepth > 0f) depthFromCamera = fixedDepth;
            }
            else if (mode == FollowMode.ThreeDPlane)
            {
                RebuildPlane();
            }
        }
    }
}