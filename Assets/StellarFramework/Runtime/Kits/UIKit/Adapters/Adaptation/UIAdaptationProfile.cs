using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.UI.Adaptation
{
    public enum UIAdaptationOrientation
    {
        Any = 0,
        Portrait = 1,
        Landscape = 2
    }

    [Serializable]
    public sealed class UIAdaptationBreakpoint
    {
        [SerializeField] private string _id = "default";
        [SerializeField] private float _minAspect = 0f;
        [SerializeField] private float _maxAspect = 100f;
        [SerializeField] private UIAdaptationOrientation _orientation = UIAdaptationOrientation.Any;
        [SerializeField, Range(0f, 1f)] private float _matchWidthOrHeight = 0.5f;

        public string Id => string.IsNullOrWhiteSpace(_id) ? "default" : _id.Trim();
        public float MinAspect => Mathf.Max(0f, _minAspect);
        public float MaxAspect => Mathf.Max(MinAspect, _maxAspect);
        public UIAdaptationOrientation Orientation => _orientation;
        public float MatchWidthOrHeight => Mathf.Clamp01(_matchWidthOrHeight);

        public void Configure(
            string id,
            float minAspect,
            float maxAspect,
            UIAdaptationOrientation orientation,
            float matchWidthOrHeight)
        {
            _id = id;
            _minAspect = Mathf.Max(0f, minAspect);
            _maxAspect = Mathf.Max(_minAspect, maxAspect);
            _orientation = orientation;
            _matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
        }

        public bool Matches(float aspect, UIAdaptationOrientation orientation)
        {
            if (aspect < MinAspect || aspect > MaxAspect)
            {
                return false;
            }

            return _orientation == UIAdaptationOrientation.Any ||
                   _orientation == orientation;
        }
    }

    [CreateAssetMenu(
        fileName = "UIAdaptationProfile",
        menuName = "StellarFramework/UIKit/UI Adaptation Profile")]
    public sealed class UIAdaptationProfile : ScriptableObject
    {
        [SerializeField] private Vector2 _designResolution = new Vector2(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float _defaultMatchWidthOrHeight = 0.5f;
        [SerializeField] private bool _applySafeArea = true;
        [SerializeField] private UIAdaptationBreakpoint[] _breakpoints =
            Array.Empty<UIAdaptationBreakpoint>();

        public Vector2 DesignResolution => new Vector2(
            Mathf.Max(1f, _designResolution.x),
            Mathf.Max(1f, _designResolution.y));
        public float DefaultMatchWidthOrHeight => Mathf.Clamp01(_defaultMatchWidthOrHeight);
        public bool ApplySafeArea => _applySafeArea;
        public IReadOnlyList<UIAdaptationBreakpoint> Breakpoints => _breakpoints;

        public void Configure(
            Vector2 designResolution,
            float defaultMatchWidthOrHeight,
            bool applySafeArea,
            UIAdaptationBreakpoint[] breakpoints)
        {
            _designResolution = new Vector2(
                Mathf.Max(1f, designResolution.x),
                Mathf.Max(1f, designResolution.y));
            _defaultMatchWidthOrHeight = Mathf.Clamp01(defaultMatchWidthOrHeight);
            _applySafeArea = applySafeArea;
            _breakpoints = breakpoints ?? Array.Empty<UIAdaptationBreakpoint>();
        }

        public UIAdaptationBreakpoint ResolveBreakpoint(
            int width,
            int height,
            out float matchWidthOrHeight)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            float aspect = CalculateShapeAspect(safeWidth, safeHeight);
            UIAdaptationOrientation orientation = ResolveOrientation(safeWidth, safeHeight);

            foreach (UIAdaptationBreakpoint breakpoint in _breakpoints ?? Array.Empty<UIAdaptationBreakpoint>())
            {
                if (breakpoint != null && breakpoint.Matches(aspect, orientation))
                {
                    matchWidthOrHeight = breakpoint.MatchWidthOrHeight;
                    return breakpoint;
                }
            }

            matchWidthOrHeight = DefaultMatchWidthOrHeight;
            return null;
        }

        /// <summary>
        /// 统一使用长边/短边得到 >= 1 的屏幕形状比例。
        /// 例如 2400x1080 与 1080x2400 都得到约 2.222；横竖屏由 Orientation 单独判断。
        /// </summary>
        public static float CalculateShapeAspect(int width, int height)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            int longEdge = Mathf.Max(safeWidth, safeHeight);
            int shortEdge = Mathf.Min(safeWidth, safeHeight);
            return (float)longEdge / shortEdge;
        }

        public static UIAdaptationOrientation ResolveOrientation(int width, int height)
        {
            return Mathf.Max(1, width) >= Mathf.Max(1, height)
                ? UIAdaptationOrientation.Landscape
                : UIAdaptationOrientation.Portrait;
        }
    }
}
