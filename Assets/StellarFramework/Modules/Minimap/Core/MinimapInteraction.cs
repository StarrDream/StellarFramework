using Minimap.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Minimap.Interaction
{
    /// <summary>
    /// 小地图交互控制器 (拖拽、缩放、跟随)
    /// </summary>
    public class MinimapInteraction : MonoBehaviour, IDragHandler, IScrollHandler, IBeginDragHandler
    {
        public static MinimapInteraction Instance { get; private set; }

        [Header("核心引用")] public RectTransform contentRoot;
        public Transform referenceCamera;
        public Transform playerTarget;

        [Header("缩放设置")] public float minScale = 0.5f;
        public float maxScale = 3.0f;
        public float zoomStep = 0.2f;
        public float smoothSpeed = 10f;

        [Header("旋转设置")] public bool rotateMapWithCamera = false;

        // 内部状态
        private bool _isFollowingPlayer = true;
        private float _currentScale = 1f;
        private Vector2 _targetPosition;
        private float _targetScale;
        private Quaternion _targetRotation = Quaternion.identity;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (contentRoot == null)
            {
                enabled = false;
                return;
            }

            _targetScale = _currentScale;
            _targetPosition = contentRoot.anchoredPosition;
            if (playerTarget != null) LocatePlayer();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 跟随
            if (_isFollowingPlayer && playerTarget != null)
            {
                var config = MinimapManager.Instance.mapConfig;
                if (config != null)
                {
                    Vector2 playerMapPos = CoordinateMapper.WorldToMapPosition(playerTarget.position, config);
                    _targetPosition = -playerMapPos * _targetScale;
                }
            }

            // 2. 旋转
            if (rotateMapWithCamera && referenceCamera != null)
            {
                float camY = referenceCamera.eulerAngles.y;
                _targetRotation = Quaternion.Euler(0, 0, camY);
            }
            else
            {
                _targetRotation = Quaternion.identity;
            }

            // 3. 应用变换
            _currentScale = Mathf.Lerp(_currentScale, _targetScale, dt * smoothSpeed);
            contentRoot.localScale = Vector3.one * _currentScale;
            contentRoot.anchoredPosition = Vector2.Lerp(contentRoot.anchoredPosition, _targetPosition, dt * smoothSpeed);
            contentRoot.localRotation = Quaternion.Lerp(contentRoot.localRotation, _targetRotation, dt * smoothSpeed);
        }

        // --- 事件 ---
        public void OnBeginDrag(PointerEventData eventData) => _isFollowingPlayer = false;

        public void OnDrag(PointerEventData eventData)
        {
            // 修正：拖拽灵敏度随缩放变化
            _targetPosition += eventData.delta / _currentScale;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta.y > 0) OnZoomInClick();
            else if (eventData.scrollDelta.y < 0) OnZoomOutClick();
        }

        // --- API ---
        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            LocatePlayer();
        }

        public void LocatePlayer()
        {
            if (playerTarget == null) return;
            _isFollowingPlayer = true;
        }

        public void OnZoomInClick() => _targetScale = Mathf.Clamp(_targetScale + zoomStep, minScale, maxScale);
        public void OnZoomOutClick() => _targetScale = Mathf.Clamp(_targetScale - zoomStep, minScale, maxScale);

        public void OnResetClick()
        {
            _targetScale = 1f;
            _targetRotation = Quaternion.identity;
            LocatePlayer();
        }

        public void OnToggleRotateMap(bool isOn)
        {
            rotateMapWithCamera = isOn;
            if (!isOn) _targetRotation = Quaternion.identity;
        }
    }
}