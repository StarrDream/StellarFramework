using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.UI.Adaptation
{
    public enum UIDisplayAvoidanceMode
    {
        None = 0,
        SafeArea = 1,
        PreciseCutout = 2
    }

    public enum UIDisplayFallbackMode
    {
        Automatic = 0,
        SafeArea = 1,
        EdgePadding = 2,
        None = 3
    }

    public enum UIDisplayResolvedMode
    {
        None = 0,
        SafeArea = 1,
        PreciseCutout = 2,
        EdgePadding = 3
    }

    public enum UICutoutSource
    {
        System = 0,
        Manual = 1,
        SystemAndManual = 2
    }

    public enum UICutoutEdge
    {
        Top = 0,
        Bottom = 1,
        Any = 2
    }

    public enum UICutoutAvoidanceAxis
    {
        Auto = 0,
        Horizontal = 1,
        Vertical = 2
    }

    [Serializable]
    public sealed class UIManualExclusionZone
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Rect _normalizedRect = new Rect(0.45f, 0.92f, 0.10f, 0.06f);
        [SerializeField] private UIAdaptationOrientation _orientation = UIAdaptationOrientation.Any;

        public bool Enabled => _enabled;
        public Rect NormalizedRect => _normalizedRect;
        public UIAdaptationOrientation Orientation => _orientation;

        public void Configure(
            Rect normalizedRect,
            UIAdaptationOrientation orientation = UIAdaptationOrientation.Any,
            bool enabled = true)
        {
            _normalizedRect = normalizedRect;
            _orientation = orientation;
            _enabled = enabled;
        }

        public bool TryGetScreenRect(int width, int height, out Rect screenRect)
        {
            screenRect = default;
            if (!_enabled)
            {
                return false;
            }

            UIAdaptationOrientation current =
                UIAdaptationProfile.ResolveOrientation(width, height);
            if (_orientation != UIAdaptationOrientation.Any && _orientation != current)
            {
                return false;
            }

            float xMin = Mathf.Clamp01(_normalizedRect.xMin) * width;
            float yMin = Mathf.Clamp01(_normalizedRect.yMin) * height;
            float xMax = Mathf.Clamp01(_normalizedRect.xMax) * width;
            float yMax = Mathf.Clamp01(_normalizedRect.yMax) * height;
            if (xMax <= xMin || yMax <= yMin)
            {
                return false;
            }

            screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }

    [Serializable]
    public sealed class UICutoutTarget
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private UICutoutAvoidanceAxis _axis = UICutoutAvoidanceAxis.Auto;

        [NonSerialized] private Vector2 _baselineAnchoredPosition;
        [NonSerialized] private Vector2 _lastAppliedAnchoredOffset;
        [NonSerialized] private bool _baselineCaptured;

        public RectTransform Target => _target;
        public UICutoutAvoidanceAxis Axis => _axis;

        public UICutoutTarget()
        {
        }

        public UICutoutTarget(RectTransform target, UICutoutAvoidanceAxis axis)
        {
            _target = target;
            _axis = axis;
        }

        public void Configure(RectTransform target, UICutoutAvoidanceAxis axis)
        {
            _target = target;
            _axis = axis;
            _baselineCaptured = false;
            _lastAppliedAnchoredOffset = Vector2.zero;
        }

        internal void RestoreBaseline()
        {
            if (_target == null || !_baselineCaptured)
            {
                return;
            }

            _target.anchoredPosition = _baselineAnchoredPosition;
            _lastAppliedAnchoredOffset = Vector2.zero;
        }

        internal void CaptureBaseline()
        {
            if (_target == null)
            {
                _baselineCaptured = false;
                _lastAppliedAnchoredOffset = Vector2.zero;
                return;
            }

            _baselineAnchoredPosition = _target.anchoredPosition;
            _lastAppliedAnchoredOffset = Vector2.zero;
            _baselineCaptured = true;
        }

        internal void ApplyAnchoredOffset(Vector2 offset)
        {
            if (_target == null || !_baselineCaptured)
            {
                return;
            }

            _lastAppliedAnchoredOffset = offset;
            _target.anchoredPosition = _baselineAnchoredPosition + offset;
        }

        internal void ForgetAppliedOffsetAndCaptureCurrent()
        {
            _lastAppliedAnchoredOffset = Vector2.zero;
            CaptureBaseline();
        }
    }

    /// <summary>
    /// Pure screen-space solver used by <see cref="UICutoutAwareLayout"/>.
    /// It moves a target only when that target intersects an exclusion zone.
    /// </summary>
    public static class UICutoutLayoutSolver
    {
        private const float Epsilon = 0.01f;

        public static Vector2 CalculateOffset(
            Rect targetRect,
            Rect allowedBounds,
            IReadOnlyList<Rect> exclusions,
            UICutoutAvoidanceAxis axis,
            UICutoutEdge edge,
            out bool resolved)
        {
            Vector2 total = Vector2.zero;
            Rect moved = targetRect;
            int exclusionCount = exclusions?.Count ?? 0;
            int maxIterations = Mathf.Max(2, exclusionCount * 2 + 2);

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool foundOverlap = false;
                for (int i = 0; i < exclusionCount; i++)
                {
                    Rect exclusion = exclusions[i];
                    if (!moved.Overlaps(exclusion, true))
                    {
                        continue;
                    }

                    foundOverlap = true;
                    if (!TryResolveSingleOverlap(
                            moved,
                            exclusion,
                            allowedBounds,
                            axis,
                            edge,
                            out Vector2 delta))
                    {
                        resolved = false;
                        return total;
                    }

                    total += delta;
                    moved.position += delta;
                    break;
                }

                if (!foundOverlap)
                {
                    resolved = Contains(allowedBounds, moved);
                    return total;
                }
            }

            resolved = false;
            return total;
        }

        private static bool TryResolveSingleOverlap(
            Rect target,
            Rect exclusion,
            Rect bounds,
            UICutoutAvoidanceAxis axis,
            UICutoutEdge edge,
            out Vector2 bestDelta)
        {
            bestDelta = Vector2.zero;
            float bestCost = float.PositiveInfinity;

            if (axis != UICutoutAvoidanceAxis.Vertical)
            {
                Consider(
                    new Vector2(exclusion.xMin - target.xMax - Epsilon, 0f),
                    target,
                    bounds,
                    ref bestDelta,
                    ref bestCost);
                Consider(
                    new Vector2(exclusion.xMax - target.xMin + Epsilon, 0f),
                    target,
                    bounds,
                    ref bestDelta,
                    ref bestCost);
            }

            if (axis != UICutoutAvoidanceAxis.Horizontal)
            {
                if (edge == UICutoutEdge.Top || edge == UICutoutEdge.Any)
                {
                    Consider(
                        new Vector2(0f, exclusion.yMin - target.yMax - Epsilon),
                        target,
                        bounds,
                        ref bestDelta,
                        ref bestCost);
                }

                if (edge == UICutoutEdge.Bottom || edge == UICutoutEdge.Any)
                {
                    Consider(
                        new Vector2(0f, exclusion.yMax - target.yMin + Epsilon),
                        target,
                        bounds,
                        ref bestDelta,
                        ref bestCost);
                }
            }

            return !float.IsPositiveInfinity(bestCost);
        }

        private static void Consider(
            Vector2 delta,
            Rect target,
            Rect bounds,
            ref Vector2 bestDelta,
            ref float bestCost)
        {
            Rect candidate = target;
            candidate.position += delta;
            if (!Contains(bounds, candidate))
            {
                return;
            }

            float cost = delta.sqrMagnitude;
            if (cost >= bestCost)
            {
                return;
            }

            bestCost = cost;
            bestDelta = delta;
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - Epsilon &&
                   inner.yMin >= outer.yMin - Epsilon &&
                   inner.xMax <= outer.xMax + Epsilon &&
                   inner.yMax <= outer.yMax + Epsilon;
        }

        public static Vector2 CalculateContainmentOffset(Rect targetRect, Rect allowedBounds)
        {
            float x = 0f;
            float y = 0f;

            if (targetRect.xMin < allowedBounds.xMin)
            {
                x = allowedBounds.xMin - targetRect.xMin;
            }
            else if (targetRect.xMax > allowedBounds.xMax)
            {
                x = allowedBounds.xMax - targetRect.xMax;
            }

            if (targetRect.yMin < allowedBounds.yMin)
            {
                y = allowedBounds.yMin - targetRect.yMin;
            }
            else if (targetRect.yMax > allowedBounds.yMax)
            {
                y = allowedBounds.yMax - targetRect.yMax;
            }

            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Selectable display-avoidance policy for HUD/navigation controls.
    /// Authors choose None / SafeArea / PreciseCutout as design intent. PreciseCutout can automatically
    /// fall back to SafeArea or reference-pixel EdgePadding when platform geometry is missing or unusable.
    /// Unlike SafeAreaRoot this component operates on configured targets instead of resizing a shared root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICutoutAwareLayout : MonoBehaviour
    {
        [SerializeField] private UIAdaptationController _controller;
        [SerializeField] private UIDisplayAvoidanceMode _mode = UIDisplayAvoidanceMode.PreciseCutout;
        [SerializeField] private UIDisplayFallbackMode _fallback = UIDisplayFallbackMode.Automatic;
        [SerializeField] private UICutoutSource _source = UICutoutSource.System;
        [SerializeField] private UICutoutEdge _edge = UICutoutEdge.Top;
        [SerializeField, Min(0f)] private float _cutoutPaddingReferencePixels = 20f;
        [SerializeField, Min(0f)] private float _edgePaddingReferencePixels = 12f;
        [SerializeField, Min(1f)] private float _edgeBandReferencePixels = 220f;
        [SerializeField] private bool _respectSafeAreaSideInsets = true;
        [SerializeField] private UIManualExclusionZone[] _manualExclusions =
            Array.Empty<UIManualExclusionZone>();
        [SerializeField] private UICutoutTarget[] _targets = Array.Empty<UICutoutTarget>();

        private readonly List<Rect> _exclusionBuffer = new List<Rect>(4);
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private string _lastBreakpointId = string.Empty;

        public UIAdaptationController Controller => _controller;
        public UIDisplayAvoidanceMode Mode => _mode;
        public UIDisplayFallbackMode Fallback => _fallback;
        public UIDisplayResolvedMode EffectiveMode { get; private set; }
        public UICutoutSource Source => _source;
        public UICutoutEdge Edge => _edge;
        public IReadOnlyList<UICutoutTarget> Targets => _targets;
        public IReadOnlyList<UIManualExclusionZone> ManualExclusions => _manualExclusions;

        private void OnEnable()
        {
            ResolveController();
            if (_controller == null)
            {
                return;
            }

            _controller.DisplayGeometryChanged += HandleDisplayGeometryChanged;
            _lastBreakpointId = _controller.CurrentBreakpointId;
            CaptureCurrentLayout();
            if (_controller.HasCurrentGeometry)
            {
                ApplyGeometry(_controller.CurrentGeometry);
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.DisplayGeometryChanged -= HandleDisplayGeometryChanged;
            }

            RestoreBaselines();
        }

        public void Configure(
            UIAdaptationController controller,
            UICutoutSource source,
            UICutoutEdge edge,
            UICutoutTarget[] targets,
            UIManualExclusionZone[] manualExclusions = null)
        {
            if (_controller != null && isActiveAndEnabled)
            {
                _controller.DisplayGeometryChanged -= HandleDisplayGeometryChanged;
            }

            RestoreBaselines();
            _controller = controller;
            _source = source;
            _edge = edge;
            _targets = targets ?? Array.Empty<UICutoutTarget>();
            _manualExclusions = manualExclusions ?? Array.Empty<UIManualExclusionZone>();
            CaptureCurrentLayout();

            if (_controller != null && isActiveAndEnabled)
            {
                _controller.DisplayGeometryChanged += HandleDisplayGeometryChanged;
            }
        }

        public void ConfigureAvoidance(
            UIDisplayAvoidanceMode mode,
            UIDisplayFallbackMode fallback = UIDisplayFallbackMode.Automatic)
        {
            _mode = mode;
            _fallback = fallback;
        }

        public void SetSpacing(
            float cutoutPaddingReferencePixels,
            float edgePaddingReferencePixels,
            float edgeBandReferencePixels)
        {
            _cutoutPaddingReferencePixels = Mathf.Max(0f, cutoutPaddingReferencePixels);
            _edgePaddingReferencePixels = Mathf.Max(0f, edgePaddingReferencePixels);
            _edgeBandReferencePixels = Mathf.Max(1f, edgeBandReferencePixels);
        }

        /// <summary>
        /// Re-captures the authored RectTransform positions as the no-cutout baseline.
        /// Call this after another system intentionally changes the layout without changing breakpoint.
        /// </summary>
        public void CaptureCurrentLayout()
        {
            foreach (UICutoutTarget target in _targets ?? Array.Empty<UICutoutTarget>())
            {
                target?.RestoreBaseline();
                target?.CaptureBaseline();
            }
        }

        public void RestoreBaselines()
        {
            foreach (UICutoutTarget target in _targets ?? Array.Empty<UICutoutTarget>())
            {
                target?.RestoreBaseline();
            }
        }

        public void RefreshNow()
        {
            ResolveController();
            if (_controller == null)
            {
                return;
            }

            if (!_controller.HasCurrentGeometry)
            {
                _controller.ApplyCurrentScreen();
            }

            ApplyGeometry(_controller.CurrentGeometry);
        }

        public void ApplyGeometry(UIDisplayGeometry geometry)
        {
            if (geometry.Width <= 0 || geometry.Height <= 0)
            {
                return;
            }

            BuildExclusions(geometry);
            EffectiveMode = ResolveEffectiveMode(geometry);
            Rect preciseAllowedBounds = BuildAllowedBounds(geometry);
            Rect safeAreaBounds = BuildSafeAreaBounds(geometry);
            Rect edgePaddingBounds = BuildEdgePaddingBounds(geometry);

            foreach (UICutoutTarget rule in _targets ?? Array.Empty<UICutoutTarget>())
            {
                RectTransform target = rule?.Target;
                if (target == null)
                {
                    continue;
                }

                rule.RestoreBaseline();
                if (EffectiveMode == UIDisplayResolvedMode.None)
                {
                    continue;
                }

                Rect targetScreenRect = GetScreenRect(target);
                Vector2 screenOffset;

                if (EffectiveMode == UIDisplayResolvedMode.SafeArea ||
                    EffectiveMode == UIDisplayResolvedMode.EdgePadding)
                {
                    Rect bounds = EffectiveMode == UIDisplayResolvedMode.EdgePadding
                        ? edgePaddingBounds
                        : safeAreaBounds;
                    screenOffset = UICutoutLayoutSolver.CalculateContainmentOffset(
                        targetScreenRect,
                        bounds);
                }
                else
                {
                    screenOffset = UICutoutLayoutSolver.CalculateOffset(
                        targetScreenRect,
                        preciseAllowedBounds,
                        _exclusionBuffer,
                        rule.Axis,
                        _edge,
                        out bool resolved);

                    if (!resolved)
                    {
                        Rect fallbackBounds = ResolveFallbackBounds(
                            geometry,
                            safeAreaBounds,
                            edgePaddingBounds,
                            out UIDisplayResolvedMode fallbackMode);
                        EffectiveMode = fallbackMode;
                        screenOffset = fallbackMode == UIDisplayResolvedMode.None
                            ? Vector2.zero
                            : UICutoutLayoutSolver.CalculateContainmentOffset(
                                targetScreenRect,
                                fallbackBounds);
                    }
                }

                if (screenOffset.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Vector2 anchoredOffset = ScreenDeltaToAnchoredDelta(target, screenOffset);
                rule.ApplyAnchoredOffset(anchoredOffset);
            }
        }

        private UIDisplayResolvedMode ResolveEffectiveMode(UIDisplayGeometry geometry)
        {
            if (_mode == UIDisplayAvoidanceMode.None)
            {
                return UIDisplayResolvedMode.None;
            }

            if (_mode == UIDisplayAvoidanceMode.SafeArea)
            {
                return HasUsableSafeArea(geometry)
                    ? UIDisplayResolvedMode.SafeArea
                    : ResolveFallbackMode(geometry);
            }

            if (_exclusionBuffer.Count > 0)
            {
                return UIDisplayResolvedMode.PreciseCutout;
            }

            return ResolveFallbackMode(geometry);
        }

        private UIDisplayResolvedMode ResolveFallbackMode(UIDisplayGeometry geometry)
        {
            switch (_fallback)
            {
                case UIDisplayFallbackMode.None:
                    return UIDisplayResolvedMode.None;
                case UIDisplayFallbackMode.SafeArea:
                    return HasUsableSafeArea(geometry)
                        ? UIDisplayResolvedMode.SafeArea
                        : UIDisplayResolvedMode.None;
                case UIDisplayFallbackMode.EdgePadding:
                    return UIDisplayResolvedMode.EdgePadding;
                case UIDisplayFallbackMode.Automatic:
                default:
                    return HasNonFullSafeArea(geometry)
                        ? UIDisplayResolvedMode.SafeArea
                        : UIDisplayResolvedMode.EdgePadding;
            }
        }

        private Rect ResolveFallbackBounds(
            UIDisplayGeometry geometry,
            Rect safeAreaBounds,
            Rect edgePaddingBounds,
            out UIDisplayResolvedMode mode)
        {
            switch (_fallback)
            {
                case UIDisplayFallbackMode.None:
                    mode = UIDisplayResolvedMode.None;
                    return edgePaddingBounds;
                case UIDisplayFallbackMode.EdgePadding:
                    mode = UIDisplayResolvedMode.EdgePadding;
                    return edgePaddingBounds;
                case UIDisplayFallbackMode.SafeArea:
                    if (HasUsableSafeArea(geometry))
                    {
                        mode = UIDisplayResolvedMode.SafeArea;
                        return safeAreaBounds;
                    }

                    mode = UIDisplayResolvedMode.None;
                    return edgePaddingBounds;
                case UIDisplayFallbackMode.Automatic:
                default:
                    if (HasNonFullSafeArea(geometry))
                    {
                        mode = UIDisplayResolvedMode.SafeArea;
                        return safeAreaBounds;
                    }

                    mode = UIDisplayResolvedMode.EdgePadding;
                    return edgePaddingBounds;
            }
        }

        private void HandleDisplayGeometryChanged(UIDisplayGeometry geometry)
        {
            string breakpointId = _controller == null
                ? string.Empty
                : _controller.CurrentBreakpointId;
            if (!string.Equals(_lastBreakpointId, breakpointId, StringComparison.Ordinal))
            {
                // LayoutVariant is notified before DisplayGeometryChanged. When the breakpoint changed,
                // its newly authored snapshot is now the baseline and must not be offset by stale data.
                foreach (UICutoutTarget target in _targets ?? Array.Empty<UICutoutTarget>())
                {
                    target?.ForgetAppliedOffsetAndCaptureCurrent();
                }
                _lastBreakpointId = breakpointId;
            }

            ApplyGeometry(geometry);
        }

        private void ResolveController()
        {
            if (_controller != null)
            {
                return;
            }

            _controller = GetComponentInParent<UIAdaptationController>();
            if (_controller == null)
            {
                _controller = FindObjectOfType<UIAdaptationController>();
            }
        }

        private void BuildExclusions(UIDisplayGeometry geometry)
        {
            _exclusionBuffer.Clear();
            float scale = CalculateReferenceToScreenScale(geometry.Width, geometry.Height);
            float padding = _cutoutPaddingReferencePixels * scale;
            float band = _edgeBandReferencePixels * scale;

            if (_source == UICutoutSource.System || _source == UICutoutSource.SystemAndManual)
            {
                for (int i = 0; i < geometry.CutoutCount; i++)
                {
                    AddExclusionIfRelevant(geometry.GetCutout(i), geometry, padding, band);
                }
            }

            if (_source == UICutoutSource.Manual || _source == UICutoutSource.SystemAndManual)
            {
                foreach (UIManualExclusionZone manual in
                         _manualExclusions ?? Array.Empty<UIManualExclusionZone>())
                {
                    if (manual != null &&
                        manual.TryGetScreenRect(geometry.Width, geometry.Height, out Rect rect))
                    {
                        AddExclusionIfRelevant(rect, geometry, padding, band);
                    }
                }
            }
        }

        private void AddExclusionIfRelevant(
            Rect rect,
            UIDisplayGeometry geometry,
            float padding,
            float band)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            bool relevant = _edge == UICutoutEdge.Any ||
                            (_edge == UICutoutEdge.Top && rect.yMax >= geometry.Height - band) ||
                            (_edge == UICutoutEdge.Bottom && rect.yMin <= band);
            if (!relevant)
            {
                return;
            }

            Rect expanded = Rect.MinMaxRect(
                Mathf.Max(0f, rect.xMin - padding),
                Mathf.Max(0f, rect.yMin - padding),
                Mathf.Min(geometry.Width, rect.xMax + padding),
                Mathf.Min(geometry.Height, rect.yMax + padding));
            _exclusionBuffer.Add(expanded);
        }

        private Rect BuildAllowedBounds(UIDisplayGeometry geometry)
        {
            float scale = CalculateReferenceToScreenScale(geometry.Width, geometry.Height);
            float edgePadding = _edgePaddingReferencePixels * scale;
            float xMin = edgePadding;
            float xMax = geometry.Width - edgePadding;

            if (_respectSafeAreaSideInsets)
            {
                xMin = Mathf.Max(xMin, geometry.SafeArea.xMin);
                xMax = Mathf.Min(xMax, geometry.SafeArea.xMax);
            }

            return Rect.MinMaxRect(
                xMin,
                edgePadding,
                Mathf.Max(xMin, xMax),
                Mathf.Max(edgePadding, geometry.Height - edgePadding));
        }

        private Rect BuildSafeAreaBounds(UIDisplayGeometry geometry)
        {
            if (!HasUsableSafeArea(geometry))
            {
                return BuildEdgePaddingBounds(geometry);
            }

            float scale = CalculateReferenceToScreenScale(geometry.Width, geometry.Height);
            float padding = _edgePaddingReferencePixels * scale;
            Rect safe = geometry.SafeArea;
            float xMin = Mathf.Min(safe.xMax, safe.xMin + padding);
            float yMin = Mathf.Min(safe.yMax, safe.yMin + padding);
            float xMax = Mathf.Max(xMin, safe.xMax - padding);
            float yMax = Mathf.Max(yMin, safe.yMax - padding);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Rect BuildEdgePaddingBounds(UIDisplayGeometry geometry)
        {
            float scale = CalculateReferenceToScreenScale(geometry.Width, geometry.Height);
            float padding = _edgePaddingReferencePixels * scale;
            return Rect.MinMaxRect(
                padding,
                padding,
                Mathf.Max(padding, geometry.Width - padding),
                Mathf.Max(padding, geometry.Height - padding));
        }

        private static bool HasUsableSafeArea(UIDisplayGeometry geometry)
        {
            Rect safe = geometry.SafeArea;
            return geometry.SafeAreaDataValid &&
                   safe.width > 0.5f &&
                   safe.height > 0.5f &&
                   safe.xMin >= 0f &&
                   safe.yMin >= 0f &&
                   safe.xMax <= geometry.Width + 0.5f &&
                   safe.yMax <= geometry.Height + 0.5f;
        }

        private static bool HasNonFullSafeArea(UIDisplayGeometry geometry)
        {
            return HasUsableSafeArea(geometry) && geometry.HasSafeAreaInsets;
        }

        private float CalculateReferenceToScreenScale(int width, int height)
        {
            Vector2 design = _controller != null && _controller.Profile != null
                ? _controller.Profile.DesignResolution
                : new Vector2(1920f, 1080f);
            float designShortEdge = Mathf.Max(1f, Mathf.Min(design.x, design.y));
            float screenShortEdge = Mathf.Max(1f, Mathf.Min(width, height));
            return screenShortEdge / designShortEdge;
        }

        private Rect GetScreenRect(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            target.GetWorldCorners(_worldCorners);

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 ScreenDeltaToAnchoredDelta(
            RectTransform target,
            Vector2 screenDelta)
        {
            RectTransform parent = target.parent as RectTransform;
            if (parent == null)
            {
                return screenDelta;
            }

            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    Vector2.zero,
                    camera,
                    out Vector2 localZero) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenDelta,
                    camera,
                    out Vector2 localDeltaPoint))
            {
                return Vector2.zero;
            }

            return localDeltaPoint - localZero;
        }
    }
}
