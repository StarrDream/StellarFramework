using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RaycastTool : MonoBehaviour
    {
        [Header("Activation")] public bool autoActivateOnStart = true;
        public bool isActive;

        [Header("Ray Config")] public float distance = 10f;
        public Vector3 rayEulerAngles = Vector3.zero;
        public bool useLocalSpace = true;

        [Header("Filtering")] public List<string> tagWhitelist = new();
        public LayerMask layerWhitelist = ~0;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;

        [Header("Gizmos")] public bool drawGizmos = true;
        public bool drawInEditor = true;
        public bool drawWhileInactive = true;
        public Color colorNoHit = Color.yellow;
        public Color colorHitFiltered = Color.green;
        public Color colorHitNonFiltered = new(1f, 0.55f, 0.1f);
        public bool drawArrowHead = true;
        [Range(0.02f, 0.5f)] public float arrowHeadSize = 0.18f;

        [Header("LogKit")] public bool enableLogKitLog = true;
        public bool logEveryFilteredHit = true;
        public bool logEveryNonFilteredHit;
        public bool richTextLogColor = true;

        [Header("Events")] public GameObjectEvent onHitEvent;
        public GameObjectEvent onExitEvent;

        private bool _hasFilteredHit;
        private bool _hasRawHit;
        private RaycastHit _lastFilteredHitInfo;
        private GameObject _lastFilteredHitObj;
        private GameObject _lastNonFilteredObj;
        private RaycastHit _lastRawHitInfo;
        private GameObject _lastRawHitObj;

        public GameObject CurrentHitObject => _hasFilteredHit ? _lastFilteredHitObj : null;

        private void Start()
        {
            if (Application.isPlaying && autoActivateOnStart) Activate();
        }

        private void Update()
        {
            if (!Application.isPlaying || !isActive) return;
            PerformCast();
        }

        public void Activate()
        {
            if (!isActive)
            {
                isActive = true;
                LogKitLog("[STATE] Activated");
            }
        }

        public void Deactivate()
        {
            if (isActive)
            {
                isActive = false;
                LogKitLog("[STATE] Deactivated");
            }
        }

        public void ToggleActive()
        {
            if (isActive) Deactivate();
            else Activate();
        }

        public void ForceCast()
        {
            if (Application.isPlaying) PerformCast();
        }

        private void PerformCast()
        {
            var origin = transform.position;
            var dir = GetDirection();
            if (dir == Vector3.zero) return;

            // 使用 RaycastKit 统一调用
            var rawHit = RaycastKit.RaycastWorld(origin, dir, out var rawHitInfo, distance, layerWhitelist, triggerInteraction);

            _hasRawHit = rawHit;
            if (rawHit) _lastRawHitInfo = rawHitInfo;
            _lastRawHitObj = rawHit ? rawHitInfo.collider.gameObject : null;

            if (!rawHit)
            {
                HandleNoRawHit();
                return;
            }

            var go = rawHitInfo.collider.gameObject;
            var passLayer = (layerWhitelist.value & (1 << go.layer)) != 0;
            var passTag = tagWhitelist.Count == 0 || tagWhitelist.Contains(go.tag);
            var filtered = passLayer && passTag;

            if (filtered) HandleFilteredHit(rawHitInfo);
            else HandleNonFilteredHit(rawHitInfo, passLayer, passTag);
        }

        private void HandleFilteredHit(RaycastHit hitInfo)
        {
            var go = hitInfo.collider.gameObject;
            var changed = go != _lastFilteredHitObj;

            _hasFilteredHit = true;
            _lastFilteredHitObj = go;
            _lastFilteredHitInfo = hitInfo;

            if (logEveryFilteredHit || changed)
                LogColored($"[HIT:FILTERED] {go.name} dist={hitInfo.distance:F2}", "#32C832");

            onHitEvent?.Invoke(go);
            _lastNonFilteredObj = null;
        }

        private void HandleNonFilteredHit(RaycastHit hitInfo, bool passLayer, bool passTag)
        {
            var go = hitInfo.collider.gameObject;
            var changed = go != _lastNonFilteredObj;

            if (_hasFilteredHit)
            {
                LogKitLog("[EXIT] Left filtered object");
                onExitEvent?.Invoke(go);
                _hasFilteredHit = false;
                _lastFilteredHitObj = null;
            }

            if (logEveryNonFilteredHit || changed)
                LogColored($"[HIT:NONFILTER] {go.name} (L:{passLayer} T:{passTag})", "#FF8C1A");

            _lastNonFilteredObj = go;
        }

        private void HandleNoRawHit()
        {
            if (_hasFilteredHit)
            {
                LogKitLog("[EXIT] Left filtered object");
                onExitEvent?.Invoke(null);
            }

            _hasFilteredHit = false;
            _lastFilteredHitObj = null;
            if (_lastRawHitObj != null) LogKitLog("[MISS] No hit");
            _lastRawHitObj = null;
            _lastNonFilteredObj = null;
        }

        private Vector3 GetDirection()
        {
            var localDir = Quaternion.Euler(rayEulerAngles) * Vector3.forward;
            return useLocalSpace ? transform.TransformDirection(localDir.normalized) : localDir.normalized;
        }

        private void LogKitLog(string msg)
        {
            if (enableLogKitLog) LogKit.Log($"[RaycastTool:{name}] {msg}");
        }

        private void LogColored(string msg, string hex)
        {
            if (enableLogKitLog) LogKit.Log(richTextLogColor ? $"[RaycastTool:{name}] <color={hex}>{msg}</color>" : $"[RaycastTool:{name}] {msg}");
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!Application.isPlaying && !drawInEditor) return;
            if (Application.isPlaying && !isActive && !drawWhileInactive) return;

            var origin = transform.position;
            var dir = GetDirection();
            if (dir == Vector3.zero) return;

            Color c = colorNoHit;
            Vector3 end = origin + dir * distance;

            if (Application.isPlaying && _hasRawHit)
            {
                if (_hasFilteredHit)
                {
                    c = colorHitFiltered;
                    end = _lastFilteredHitInfo.point;
                }
                else
                {
                    c = colorHitNonFiltered;
                    end = _lastRawHitInfo.point;
                }
            }

            Gizmos.color = new Color(c.r, c.g, c.b, 0.95f);
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawSphere(end, 0.06f);
            if (drawArrowHead) DrawArrow(origin, dir, (end - origin).magnitude, c);
        }

        private void DrawArrow(Vector3 origin, Vector3 dir, float dist, Color color)
        {
            var headLen = Mathf.Clamp01(arrowHeadSize) * dist;
            if (headLen <= 0f) return;
            var tip = origin + dir * dist;
            var right = Vector3.Cross(dir, Vector3.up).normalized;
            if (right == Vector3.zero) right = Vector3.right;
            var up = Vector3.Cross(right, dir).normalized;
            var back = tip - dir * headLen;
            var wing = headLen * 0.5f;

            Gizmos.color = color;
            Gizmos.DrawLine(tip, back + right * wing);
            Gizmos.DrawLine(tip, back - right * wing);
            Gizmos.DrawLine(tip, back + up * wing);
            Gizmos.DrawLine(tip, back - up * wing);
        }

        [Serializable]
        public class GameObjectEvent : UnityEvent<GameObject>
        {
        }
    }
}