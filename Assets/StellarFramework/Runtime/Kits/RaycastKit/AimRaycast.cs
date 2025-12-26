// ========== AimRaycast.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/RaycastKit/AimRaycast.cs

using UnityEngine;
using UnityEngine.Rendering;

namespace StellarFramework
{
    /// <summary>
    /// 瞄准射线组件
    /// <para>功能：计算当前物体朝向与目标的角度误差，检测视线，并提供可视化反馈。</para>
    /// <para>适用：炮塔瞄准、角色注视、武器激光指示器等。</para>
    /// </summary>
    public class AimRaycast : MonoBehaviour
    {
        // 定义哪个轴作为"前方/瞄准方向"
        public enum AimAxis
        {
            Forward, // Z+
            Back, // Z-
            Right, // X+
            Left, // X-
            Up, // Y+
            Down, // Y-
            Custom // 自定义轴
        }

        [Header("目标")] public Transform target; // 瞄准的目标

        [Header("瞄准轴设置")] public AimAxis aimAxis = AimAxis.Forward;
        public Vector3 customLocalAxis = Vector3.forward; // 自定义轴向 (局部坐标)
        public Vector3 worldUp = Vector3.up; // 用于计算水平角的参考上方向

        [Tooltip("反转水平误差值的符号")] public bool invertYawSign;
        [Tooltip("反转垂直误差值的符号")] public bool invertPitchSign;

        [Header("射线设置")] [Tooltip("射线检测距离，<=0 则自动使用到目标的距离")]
        public float raycastDistance;

        public LayerMask layerMask = ~0;
        [Tooltip("是否忽略障碍物检测 (始终认为有视线)")] public bool ignoreObstacles;

        [Header("调试输出")] public bool logAngles; // 在控制台打印角度信息
        public bool drawGizmos = true; // 在 Scene 视图绘制 Gizmos
        public Color aimRayColor = Color.yellow; // 射线颜色 (无视线)
        public Color hitColor = Color.red; // 击中点颜色
        public Color losColor = Color.green; // 射线颜色 (有视线)
        public Color toTargetColor = Color.cyan; // 理想目标连线颜色
        public Color aimAxisColor = new(1f, 0.5f, 0.1f); // 轴向指示器颜色

        [Header("运行时可视化 (LineRenderer)")] public bool runtimeVisualEnabled; // 是否在游戏运行时显示线条
        public bool showAimRayInBuild = true; // 显示瞄准射线
        public bool showTargetLineInBuild = true; // 显示目标连线
        public bool onlyShowAimRayWhenLOS; // 仅当有视线时才显示瞄准射线
        public Color aimRayColorLOS_Build = new(0.2f, 1f, 0.2f, 0.9f); // 运行时有视线颜色
        public Color aimRayColorBlocked_Build = new(1f, 0.9f, 0.2f, 0.8f); // 运行时无视线颜色
        public Color targetLineColor_Build = new(0.2f, 0.8f, 1f, 0.9f); // 运行时目标连线颜色
        public float lineWidth = 0.025f; // 线条宽度
        public Material lineMaterial; // 线条材质 (为空则自动创建)

        // --- 输出数据 (只读) ---
        public float targetYaw; // 目标在水平面上的绝对角度
        public float targetPitch; // 目标在垂直面上的绝对角度
        public float currentPitch; // 当前物体的俯仰角
        public float yawError; // 水平瞄准误差 (度)
        public float pitchError; // 垂直瞄准误差 (度)
        public bool hasLineOfSight; // 是否有清晰的视线 (未被遮挡)
        public RaycastHit lastHit; // 射线击中的信息

        // 内部缓存
        private Vector3 _aimDirWorld;
        private LineRenderer _aimLineRenderer;
        private LineRenderer _targetLineRenderer;
        private Material _runtimeCreatedMat;
        private bool _visualInitTried;

        private void Update()
        {
            if (target == null)
            {
                ClearRuntimeLines();
                return;
            }

            // 1. 获取当前的世界空间瞄准方向
            _aimDirWorld = GetAimDirectionWorld();
            var toTarget = target.position - transform.position;

            // 2. 计算角度和误差
            ComputeAbsoluteAngles(toTarget);
            ComputeErrors(toTarget);

            if (logAngles) LogKit.Log($"[Aim] Y={targetYaw:F1} P={targetPitch:F1} | ErrY={yawError:F1} ErrP={pitchError:F1}");

            // 3. 执行射线检测
            PerformRaycast();

            // 4. 更新运行时可视化线条
            UpdateRuntimeVisual();
        }

        /// <summary>
        /// 执行物理射线检测
        /// </summary>
        private void PerformRaycast()
        {
            var origin = transform.position;
            // 如果 raycastDistance <= 0，则距离动态设为到目标的距离
            var dist = raycastDistance > 0f ? raycastDistance : (target != null ? (target.position - origin).magnitude : 100f);

            hasLineOfSight = false;
            lastHit = default;

            // 如果忽略障碍物，仅根据角度误差判断是否"瞄准" (这里简化为始终有视线)
            if (ignoreObstacles)
            {
                // 也可以加上角度阈值判断： if (Mathf.Abs(yawError) < 1f && ...)
                hasLineOfSight = true;
                return;
            }

            // 使用 RaycastKit 统一调用世界射线检测
            if (RaycastKit.RaycastWorld(origin, _aimDirWorld, out var hit, dist, layerMask, QueryTriggerInteraction.Ignore))
            {
                lastHit = hit;
                // 判断逻辑：击中的是目标本身，或者是目标的子物体
                if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
                    hasLineOfSight = true;
            }
        }

        /// <summary>
        /// 获取当前配置轴向的世界方向向量
        /// </summary>
        private Vector3 GetAimDirectionWorld()
        {
            Vector3 local;
            switch (aimAxis)
            {
                case AimAxis.Forward: local = Vector3.forward; break;
                case AimAxis.Back: local = Vector3.back; break;
                case AimAxis.Right: local = Vector3.right; break;
                case AimAxis.Left: local = Vector3.left; break;
                case AimAxis.Up: local = Vector3.up; break;
                case AimAxis.Down: local = Vector3.down; break;
                case AimAxis.Custom: local = customLocalAxis; break;
                default: local = Vector3.forward; break;
            }

            return transform.TransformDirection(local.normalized);
        }

        /// <summary>
        /// 计算目标的绝对 Yaw/Pitch 角度
        /// </summary>
        private void ComputeAbsoluteAngles(Vector3 toTarget)
        {
            // 水平方向向量 (忽略 Y)
            var vh = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            targetYaw = Mathf.Atan2(vh.x, vh.z) * Mathf.Rad2Deg;

            // 垂直角度
            var horizLen = new Vector2(toTarget.x, toTarget.z).magnitude;
            targetPitch = Mathf.Atan2(toTarget.y, horizLen) * Mathf.Rad2Deg;

            // 计算自身的 Pitch
            var f = _aimDirWorld;
            var fHorizLen = new Vector2(f.x, f.z).magnitude;
            currentPitch = Mathf.Atan2(f.y, fHorizLen) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 计算当前朝向与目标的角度误差
        /// </summary>
        private void ComputeErrors(Vector3 toTarget)
        {
            var f = _aimDirWorld;
            // 投影到水平面
            var f_h = new Vector3(f.x, 0f, f.z).normalized;
            var t_h = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            // 使用点积和叉积计算带符号的 Yaw 误差
            var dot = Mathf.Clamp(Vector3.Dot(f_h, t_h), -1f, 1f);
            var crossY = Vector3.Cross(f_h, t_h).y;
            yawError = Mathf.Atan2(crossY, dot) * Mathf.Rad2Deg;

            // 计算 Pitch 误差 (目标 Pitch - 当前 Pitch)
            var horizLenT = new Vector2(toTarget.x, toTarget.z).magnitude;
            var targetPitchLocal = Mathf.Atan2(toTarget.y, horizLenT) * Mathf.Rad2Deg;

            var horizLenF = new Vector2(f.x, f.z).magnitude;
            var forwardPitch = Mathf.Atan2(f.y, horizLenF) * Mathf.Rad2Deg;

            pitchError = targetPitchLocal - forwardPitch;

            if (invertYawSign) yawError = -yawError;
            if (invertPitchSign) pitchError = -pitchError;
        }

        // --- 可视化相关逻辑 ---

        private void UpdateRuntimeVisual()
        {
            if (!runtimeVisualEnabled || target == null)
            {
                ClearRuntimeLines();
                return;
            }

            EnsureLineRenderers();

            var origin = transform.position;
            var dist = raycastDistance > 0f ? raycastDistance : (target.position - origin).magnitude;

            // 更新瞄准射线
            if (showAimRayInBuild && (!onlyShowAimRayWhenLOS || hasLineOfSight))
            {
                _aimLineRenderer.enabled = true;
                _aimLineRenderer.SetPosition(0, origin);
                _aimLineRenderer.SetPosition(1, origin + _aimDirWorld * dist);
                _aimLineRenderer.startColor = _aimLineRenderer.endColor = hasLineOfSight ? aimRayColorLOS_Build : aimRayColorBlocked_Build;
            }
            else _aimLineRenderer.enabled = false;

            // 更新目标连线
            if (showTargetLineInBuild)
            {
                _targetLineRenderer.enabled = true;
                _targetLineRenderer.SetPosition(0, origin);
                _targetLineRenderer.SetPosition(1, target.position);
                _targetLineRenderer.startColor = _targetLineRenderer.endColor = targetLineColor_Build;
            }
            else _targetLineRenderer.enabled = false;
        }

        private void EnsureLineRenderers()
        {
            if (_visualInitTried) return;
            _visualInitTried = true;

            // 创建默认材质
            if (lineMaterial == null && _runtimeCreatedMat == null)
            {
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _runtimeCreatedMat = new Material(shader) { color = Color.white };
            }

            _aimLineRenderer = CreateLineRenderer("__AimRay_Line");
            _targetLineRenderer = CreateLineRenderer("__Target_Line");
        }

        private LineRenderer CreateLineRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.widthMultiplier = lineWidth;
            lr.material = lineMaterial != null ? lineMaterial : _runtimeCreatedMat;
            lr.positionCount = 2;
            lr.enabled = false;
            return lr;
        }

        private void ClearRuntimeLines()
        {
            if (_aimLineRenderer != null) _aimLineRenderer.enabled = false;
            if (_targetLineRenderer != null) _targetLineRenderer.enabled = false;
        }

        private void OnDisable() => ClearRuntimeLines();

        private void OnDestroy()
        {
            if (_runtimeCreatedMat) Destroy(_runtimeCreatedMat);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            var origin = transform.position;
            var dir = Application.isPlaying ? _aimDirWorld : GetAimDirectionWorld();

            Gizmos.color = aimAxisColor;
            Gizmos.DrawLine(origin, origin + dir * 0.6f);

            if (target != null)
            {
                var dist = raycastDistance > 0f ? raycastDistance : (target.position - origin).magnitude;
                Gizmos.color = hasLineOfSight ? losColor : aimRayColor;
                Gizmos.DrawLine(origin, origin + dir * dist);
                Gizmos.color = toTargetColor;
                Gizmos.DrawLine(origin, target.position);
            }

            if (lastHit.collider != null)
            {
                Gizmos.color = hasLineOfSight ? losColor : hitColor;
                Gizmos.DrawSphere(lastHit.point, 0.05f);
            }
        }
    }
}