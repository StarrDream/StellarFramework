using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.UI.Adaptation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UIAdaptationController : MonoBehaviour
    {
        [SerializeField] private UIAdaptationProfile _profile;
        [SerializeField] private RectTransform _safeAreaRoot;
        [SerializeField] private RectTransform[] _additionalSafeAreaRoots =
            Array.Empty<RectTransform>();
        [SerializeField] private bool _applyOnEnable = true;

        private CanvasScaler _scaler;
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private Rect _lastSafeArea;
        private string _currentBreakpointId = string.Empty;

        public UIAdaptationProfile Profile => _profile;
        public RectTransform SafeAreaRoot => _safeAreaRoot;
        public IReadOnlyList<RectTransform> AdditionalSafeAreaRoots => _additionalSafeAreaRoots;
        public string CurrentBreakpointId => _currentBreakpointId;
        public event Action<string> BreakpointChanged;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
        }

        private void OnEnable()
        {
            if (_applyOnEnable)
            {
                ApplyCurrentScreen();
            }
        }

        private void LateUpdate()
        {
            if (_profile == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            if (_lastWidth == Screen.width &&
                _lastHeight == Screen.height &&
                _lastSafeArea == safeArea)
            {
                return;
            }

            Apply(Screen.width, Screen.height, safeArea);
        }

        public void Configure(UIAdaptationProfile profile, RectTransform safeAreaRoot)
        {
            _profile = profile;
            _safeAreaRoot = safeAreaRoot;
            _additionalSafeAreaRoots = Array.Empty<RectTransform>();
            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }
        }

        public void ConfigureSafeAreaRoots(UIAdaptationProfile profile, RectTransform[] safeAreaRoots)
        {
            _profile = profile;
            if (safeAreaRoots == null || safeAreaRoots.Length == 0)
            {
                _safeAreaRoot = null;
                _additionalSafeAreaRoots = Array.Empty<RectTransform>();
            }
            else
            {
                _safeAreaRoot = safeAreaRoots[0];
                if (safeAreaRoots.Length == 1)
                {
                    _additionalSafeAreaRoots = Array.Empty<RectTransform>();
                }
                else
                {
                    _additionalSafeAreaRoots = new RectTransform[safeAreaRoots.Length - 1];
                    Array.Copy(
                        safeAreaRoots,
                        1,
                        _additionalSafeAreaRoots,
                        0,
                        _additionalSafeAreaRoots.Length);
                }
            }

            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }
        }

        public void ApplyCurrentScreen()
        {
            Apply(Screen.width, Screen.height, Screen.safeArea);
        }

        public void Apply(int width, int height, Rect safeArea)
        {
            if (_profile == null)
            {
                return;
            }
            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }

            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            UIAdaptationBreakpoint breakpoint =
                _profile.ResolveBreakpoint(safeWidth, safeHeight, out float match);
            string nextBreakpointId = breakpoint == null ? "default" : breakpoint.Id;

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = _profile.DesignResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = match;

            if (_profile.ApplySafeArea)
            {
                Rect clamped = ClampSafeArea(safeArea, safeWidth, safeHeight);
                ApplySafeArea(_safeAreaRoot, clamped, safeWidth, safeHeight);
                foreach (RectTransform additionalRoot in
                         _additionalSafeAreaRoots ?? Array.Empty<RectTransform>())
                {
                    ApplySafeArea(additionalRoot, clamped, safeWidth, safeHeight);
                }
            }

            _lastWidth = safeWidth;
            _lastHeight = safeHeight;
            _lastSafeArea = safeArea;

            if (!string.Equals(_currentBreakpointId, nextBreakpointId, StringComparison.Ordinal))
            {
                _currentBreakpointId = nextBreakpointId;
                BreakpointChanged?.Invoke(_currentBreakpointId);
            }
        }

        private static Rect ClampSafeArea(Rect safeArea, int width, int height)
        {
            float xMin = Mathf.Clamp(safeArea.xMin, 0f, width);
            float yMin = Mathf.Clamp(safeArea.yMin, 0f, height);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, width);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void ApplySafeArea(
            RectTransform target,
            Rect safeArea,
            int width,
            int height)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(
                safeArea.xMin / width,
                safeArea.yMin / height);
            target.anchorMax = new Vector2(
                safeArea.xMax / width,
                safeArea.yMax / height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
