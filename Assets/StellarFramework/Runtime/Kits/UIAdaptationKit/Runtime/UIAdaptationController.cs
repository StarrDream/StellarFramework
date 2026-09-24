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
        private const double CutoutProbeIntervalSeconds = 0.5d;

        [SerializeField] private UIAdaptationProfile _profile;
        [SerializeField] private RectTransform _safeAreaRoot;
        [SerializeField] private RectTransform[] _additionalSafeAreaRoots =
            Array.Empty<RectTransform>();
        [SerializeField] private bool _applyOnEnable = true;

        private CanvasScaler _scaler;
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private Rect _lastSafeArea;
        private bool _lastSafeAreaDataValid = true;
        private Rect[] _lastCutouts = Array.Empty<Rect>();
        private int _lastObservedScreenWidth = -1;
        private int _lastObservedScreenHeight = -1;
        private Rect _lastObservedScreenSafeArea;
        private double _nextCutoutProbeTime;
        private bool _hasScreenSnapshot;
        private bool _probeSystemCutouts;
        private string _currentBreakpointId = string.Empty;

        public UIAdaptationProfile Profile => _profile;
        public RectTransform SafeAreaRoot => _safeAreaRoot;
        public IReadOnlyList<RectTransform> AdditionalSafeAreaRoots => _additionalSafeAreaRoots;
        public string CurrentBreakpointId => _currentBreakpointId;
        public bool HasCurrentGeometry => _lastWidth > 0 && _lastHeight > 0;
        public UIDisplayGeometry CurrentGeometry { get; private set; }
        public event Action<string> BreakpointChanged;
        public event Action<UIDisplayGeometry> DisplayGeometryChanged;

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

            if (!_hasScreenSnapshot)
            {
                ApplyCurrentScreen();
                return;
            }

            int width = Screen.width;
            int height = Screen.height;
            Rect safeArea = Screen.safeArea;
            if (_lastObservedScreenWidth != width ||
                _lastObservedScreenHeight != height ||
                _lastObservedScreenSafeArea != safeArea)
            {
                ApplyCurrentScreen();
                return;
            }

            if (!_probeSystemCutouts)
            {
                return;
            }

            double currentTime = Time.realtimeSinceStartupAsDouble;
            if (currentTime < _nextCutoutProbeTime)
            {
                return;
            }

            // Screen.cutouts can allocate a managed array. Probe it at a low fixed rate,
            // and pass this same snapshot to Apply if its normalized geometry changed.
            _nextCutoutProbeTime = currentTime + CutoutProbeIntervalSeconds;
            Rect[] cutouts = Screen.cutouts;
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            if (HasCutoutSnapshotChanged(cutouts, _lastCutouts, safeWidth, safeHeight))
            {
                ApplySystemSnapshot(width, height, safeArea, cutouts);
            }
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
            if (_profile == null)
            {
                return;
            }

            int width = Screen.width;
            int height = Screen.height;
            Rect safeArea = Screen.safeArea;
            Rect[] cutouts = Screen.cutouts;
            ApplySystemSnapshot(width, height, safeArea, cutouts);
        }

        public void Apply(int width, int height, Rect safeArea)
        {
            Apply(width, height, safeArea, Array.Empty<Rect>());
        }

        public void Apply(
            int width,
            int height,
            Rect safeArea,
            IReadOnlyList<Rect> cutouts)
        {
            // Apply is also the explicit input path for platform adapters and tests.
            // System cutout polling resumes when ApplyCurrentScreen/RefreshDisplayGeometry
            // is requested or when the observed screen dimensions/safe area change.
            _probeSystemCutouts = false;
            _lastObservedScreenWidth = width;
            _lastObservedScreenHeight = height;
            _lastObservedScreenSafeArea = safeArea;
            _hasScreenSnapshot = true;
            ApplyGeometry(width, height, safeArea, cutouts);
        }

        private void ApplySystemSnapshot(
            int width,
            int height,
            Rect safeArea,
            IReadOnlyList<Rect> cutouts)
        {
            _probeSystemCutouts = true;
            _lastObservedScreenWidth = width;
            _lastObservedScreenHeight = height;
            _lastObservedScreenSafeArea = safeArea;
            _hasScreenSnapshot = true;
            _nextCutoutProbeTime = Time.realtimeSinceStartupAsDouble +
                                   CutoutProbeIntervalSeconds;
            ApplyGeometry(width, height, safeArea, cutouts);
        }

        private void ApplyGeometry(
            int width,
            int height,
            Rect safeArea,
            IReadOnlyList<Rect> cutouts)
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
            bool safeAreaDataValid = IsSafeAreaInputValid(safeArea, safeWidth, safeHeight);
            Rect clamped = safeAreaDataValid
                ? ClampSafeArea(safeArea, safeWidth, safeHeight)
                : new Rect(0f, 0f, safeWidth, safeHeight);
            Rect[] normalizedCutouts = NormalizeCutouts(cutouts, safeWidth, safeHeight);
            bool geometryChanged =
                _lastWidth != safeWidth ||
                _lastHeight != safeHeight ||
                _lastSafeArea != clamped ||
                _lastSafeAreaDataValid != safeAreaDataValid ||
                !CutoutsEqual(_lastCutouts, normalizedCutouts);

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = _profile.DesignResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = match;

            if (_profile.ApplySafeArea)
            {
                ApplySafeArea(_safeAreaRoot, clamped, safeWidth, safeHeight);
                foreach (RectTransform additionalRoot in
                         _additionalSafeAreaRoots ?? Array.Empty<RectTransform>())
                {
                    ApplySafeArea(additionalRoot, clamped, safeWidth, safeHeight);
                }
            }

            _lastWidth = safeWidth;
            _lastHeight = safeHeight;
            _lastSafeArea = clamped;
            _lastSafeAreaDataValid = safeAreaDataValid;
            _lastCutouts = normalizedCutouts;
            CurrentGeometry = new UIDisplayGeometry(
                safeWidth,
                safeHeight,
                clamped,
                _lastCutouts,
                safeAreaDataValid);

            if (!string.Equals(_currentBreakpointId, nextBreakpointId, StringComparison.Ordinal))
            {
                _currentBreakpointId = nextBreakpointId;
                BreakpointChanged?.Invoke(_currentBreakpointId);
            }

            if (geometryChanged)
            {
                DisplayGeometryChanged?.Invoke(CurrentGeometry);
            }
        }

        public void RefreshDisplayGeometry()
        {
            ApplyCurrentScreen();
        }

        private static Rect ClampSafeArea(Rect safeArea, int width, int height)
        {
            float xMin = Mathf.Clamp(safeArea.xMin, 0f, width);
            float yMin = Mathf.Clamp(safeArea.yMin, 0f, height);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, width);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool IsSafeAreaInputValid(Rect safeArea, int width, int height)
        {
            if (!IsFinite(safeArea.xMin) ||
                !IsFinite(safeArea.yMin) ||
                !IsFinite(safeArea.xMax) ||
                !IsFinite(safeArea.yMax) ||
                safeArea.width <= 0f ||
                safeArea.height <= 0f)
            {
                return false;
            }

            const float tolerance = 1f;
            return safeArea.xMin >= -tolerance &&
                   safeArea.yMin >= -tolerance &&
                   safeArea.xMax <= width + tolerance &&
                   safeArea.yMax <= height + tolerance;
        }

        private static Rect[] NormalizeCutouts(
            IReadOnlyList<Rect> cutouts,
            int width,
            int height)
        {
            int count = cutouts?.Count ?? 0;
            if (count == 0)
            {
                return Array.Empty<Rect>();
            }

            Rect[] normalized = new Rect[count];
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryNormalizeCutout(cutouts[i], width, height, out Rect normalizedCutout))
                {
                    continue;
                }

                normalized[write++] = normalizedCutout;
            }

            if (write == 0)
            {
                return Array.Empty<Rect>();
            }
            if (write == normalized.Length)
            {
                return normalized;
            }

            Array.Resize(ref normalized, write);
            return normalized;
        }

        internal static bool HasCutoutSnapshotChanged(
            IReadOnlyList<Rect> snapshot,
            Rect[] normalizedCutouts,
            int width,
            int height)
        {
            normalizedCutouts = normalizedCutouts ?? Array.Empty<Rect>();
            int count = snapshot?.Count ?? 0;
            int normalizedIndex = 0;

            // Compare normalized rectangles in place. Invalid and fully clipped entries
            // are skipped exactly as they are by NormalizeCutouts, without allocating a
            // second array on the periodic probe path.
            for (int i = 0; i < count; i++)
            {
                if (!TryNormalizeCutout(snapshot[i], width, height, out Rect normalized))
                {
                    continue;
                }

                if (normalizedIndex >= normalizedCutouts.Length ||
                    normalizedCutouts[normalizedIndex] != normalized)
                {
                    return true;
                }

                normalizedIndex++;
            }

            return normalizedIndex != normalizedCutouts.Length;
        }

        private static bool TryNormalizeCutout(
            Rect raw,
            int width,
            int height,
            out Rect normalized)
        {
            normalized = default;
            if (!IsFinite(raw.xMin) ||
                !IsFinite(raw.yMin) ||
                !IsFinite(raw.xMax) ||
                !IsFinite(raw.yMax) ||
                raw.width <= 0f ||
                raw.height <= 0f)
            {
                return false;
            }

            float xMin = Mathf.Clamp(raw.xMin, 0f, width);
            float yMin = Mathf.Clamp(raw.yMin, 0f, height);
            float xMax = Mathf.Clamp(raw.xMax, xMin, width);
            float yMax = Mathf.Clamp(raw.yMax, yMin, height);
            if (xMax <= xMin || yMax <= yMin)
            {
                return false;
            }

            normalized = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool CutoutsEqual(Rect[] left, Rect[] right)
        {
            left = left ?? Array.Empty<Rect>();
            right = right ?? Array.Empty<Rect>();
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
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
