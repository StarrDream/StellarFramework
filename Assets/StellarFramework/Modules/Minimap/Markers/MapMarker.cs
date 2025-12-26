using Minimap.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Markers
{
    public class MapMarker : MonoBehaviour
    {
        [Header("UI 组件")] public Image iconImage;
        [Tooltip("可选：如果赋值，仅旋转此部分（如视野扇形）")] public RectTransform rotatingPart;

        // ---记录我是谁生的 ---
        [HideInInspector] public MapMarker originPrefab;

        private Transform _target;
        private MapConfig _config;
        private RectTransform _rt;
        private CanvasGroup _cg;

        private bool _syncRotation;
        private bool _clampToBorder;
        private bool _hideOutOfBounds;
        private float _rotationOffset;

        public bool IsValid => _target != null;

        public void Initialize()
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            if (iconImage == null) iconImage = GetComponent<Image>();
        }

        public void Setup(Transform target, MapConfig config, Sprite icon, Color color,
            bool syncRotation, bool clamp, bool hideOut, float rotationOffset)
        {
            _target = target;
            _config = config;
            _syncRotation = syncRotation;
            _clampToBorder = clamp;
            _hideOutOfBounds = hideOut;
            _rotationOffset = rotationOffset;

            // 如果传了特定的 icon，就替换；没传就用 Prefab 自带的
            if (icon != null && iconImage != null) iconImage.sprite = icon;
            if (iconImage != null) iconImage.color = color;

            if (_cg != null) _cg.alpha = 1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 保证新生成的在最上层

            OnGameUpdate();
        }

        public void OnGameUpdate()
        {
            if (_target == null) return;

            Vector3 worldPos = _target.position;
            Vector2 uiPos = CoordinateMapper.WorldToMapPosition(worldPos, _config);
            bool inBounds = CoordinateMapper.IsInBounds(worldPos, _config);

            // 1. 边界处理
            if (!inBounds)
            {
                if (_hideOutOfBounds)
                {
                    if (_cg) _cg.alpha = 0f;
                    return;
                }

                if (_clampToBorder)
                {
                    uiPos = CoordinateMapper.ClampToBorder(uiPos, _config);
                    if (_cg) _cg.alpha = 1f;
                }
            }
            else if (_cg) _cg.alpha = 1f;

            _rt.anchoredPosition = uiPos;

            // 2. 旋转处理
            if (_syncRotation)
            {
                float targetAngle = -_target.eulerAngles.y + _rotationOffset;
                Vector3 targetEuler = new Vector3(0, 0, targetAngle);

                if (rotatingPart != null)
                {
                    rotatingPart.localEulerAngles = targetEuler;
                    _rt.localEulerAngles = Vector3.zero;
                }
                else
                {
                    _rt.localEulerAngles = targetEuler;
                }
            }
            else
            {
                if (_rt.localEulerAngles != Vector3.zero) _rt.localEulerAngles = Vector3.zero;
            }
        }

        public void Deactivate()
        {
            _target = null;
            gameObject.SetActive(false);
        }
    }
}