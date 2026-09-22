using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.UI.Adaptation
{
    /// <summary>
    /// Runtime display geometry consumed by UIKit adaptation features.
    /// Coordinates use Unity screen-pixel space with a bottom-left origin.
    /// </summary>
    public readonly struct UIDisplayGeometry
    {
        private const float GeometryEpsilon = 0.5f;
        private static readonly Rect[] EmptyCutouts = Array.Empty<Rect>();
        private readonly Rect[] _cutouts;

        public int Width { get; }
        public int Height { get; }
        public Rect SafeArea { get; }
        /// <summary>
        /// True when the platform/provider supplied a usable Safe Area. When false, SafeArea is
        /// normalized to the full screen so consumers never receive a zero/negative layout rect.
        /// </summary>
        public bool SafeAreaDataValid { get; }
        public Rect FullScreenRect => new Rect(0f, 0f, Width, Height);
        public IReadOnlyList<Rect> Cutouts => _cutouts ?? EmptyCutouts;
        public int CutoutCount => _cutouts?.Length ?? 0;
        public bool HasCutouts => CutoutCount > 0;
        public bool HasSafeAreaInsets =>
            SafeAreaDataValid &&
            (Mathf.Abs(SafeArea.xMin) > GeometryEpsilon ||
             Mathf.Abs(SafeArea.yMin) > GeometryEpsilon ||
             Mathf.Abs(SafeArea.xMax - Width) > GeometryEpsilon ||
             Mathf.Abs(SafeArea.yMax - Height) > GeometryEpsilon);

        public UIDisplayGeometry(int width, int height, Rect safeArea, Rect[] cutouts)
            : this(width, height, safeArea, cutouts, true)
        {
        }

        public UIDisplayGeometry(
            int width,
            int height,
            Rect safeArea,
            Rect[] cutouts,
            bool safeAreaDataValid)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            SafeArea = safeArea;
            SafeAreaDataValid = safeAreaDataValid;
            _cutouts = cutouts ?? EmptyCutouts;
        }

        public Rect GetCutout(int index)
        {
            if (_cutouts == null || index < 0 || index >= _cutouts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _cutouts[index];
        }
    }
}
