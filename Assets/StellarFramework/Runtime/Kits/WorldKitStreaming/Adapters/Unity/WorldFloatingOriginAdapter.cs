using System;
using StellarFramework.WorldKit;
using UnityEngine;

namespace StellarFramework.WorldKit.Streaming.UnityAdapter
{
    public readonly struct WorldFloatingOriginSettings
    {
        private readonly bool _initialized;

        public double RecenterThreshold { get; }
        public double SnapSize { get; }
        public bool IsValid => _initialized &&
            IsFinite(RecenterThreshold) && RecenterThreshold > 0d &&
            IsFinite(SnapSize) && SnapSize > 0d;

        public WorldFloatingOriginSettings(double recenterThreshold, double snapSize)
        {
            if (!IsFinite(recenterThreshold) || recenterThreshold <= 0d)
                throw new ArgumentOutOfRangeException(nameof(recenterThreshold));
            if (!IsFinite(snapSize) || snapSize <= 0d)
                throw new ArgumentOutOfRangeException(nameof(snapSize));
            if (snapSize > recenterThreshold)
                throw new ArgumentOutOfRangeException(nameof(snapSize), "Snap size cannot exceed the recenter threshold.");

            RecenterThreshold = recenterThreshold;
            SnapSize = snapSize;
            _initialized = true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Unity-only conversion between high-precision logical WorldPoint2D and small local float coordinates.
    /// It never mutates Transforms itself; the host applies returned sceneDelta to its presentation roots.
    /// </summary>
    public static class WorldFloatingOriginAdapter
    {
        public static Vector3 ToUnityPosition(
            WorldPoint2D logicalPosition,
            WorldPoint2D logicalOrigin,
            float unityY = 0f)
        {
            if (float.IsNaN(unityY) || float.IsInfinity(unityY))
                throw new ArgumentOutOfRangeException(nameof(unityY));

            double x = logicalPosition.X - logicalOrigin.X;
            double z = logicalPosition.Y - logicalOrigin.Y;
            ValidateFloatRange(x, nameof(logicalPosition));
            ValidateFloatRange(z, nameof(logicalPosition));
            return new Vector3((float)x, unityY, (float)z);
        }

        public static WorldPoint2D ToLogicalPosition(
            Vector3 unityPosition,
            WorldPoint2D logicalOrigin)
        {
            if (!IsFinite(unityPosition.x) || !IsFinite(unityPosition.z))
                throw new ArgumentOutOfRangeException(nameof(unityPosition));
            return new WorldPoint2D(
                logicalOrigin.X + unityPosition.x,
                logicalOrigin.Y + unityPosition.z);
        }

        public static bool TryComputeRecenter(
            WorldPoint2D logicalFocus,
            WorldPoint2D currentOrigin,
            in WorldFloatingOriginSettings settings,
            out WorldPoint2D nextOrigin,
            out Vector3 sceneDelta)
        {
            if (!settings.IsValid)
                throw new ArgumentException("Floating-origin settings are invalid.", nameof(settings));

            double relativeX = logicalFocus.X - currentOrigin.X;
            double relativeY = logicalFocus.Y - currentOrigin.Y;
            if (Math.Abs(relativeX) <= settings.RecenterThreshold &&
                Math.Abs(relativeY) <= settings.RecenterThreshold)
            {
                nextOrigin = currentOrigin;
                sceneDelta = Vector3.zero;
                return false;
            }

            double nextX = Snap(logicalFocus.X, settings.SnapSize);
            double nextY = Snap(logicalFocus.Y, settings.SnapSize);
            nextOrigin = new WorldPoint2D(nextX, nextY);

            double shiftX = currentOrigin.X - nextX;
            double shiftZ = currentOrigin.Y - nextY;
            ValidateFloatRange(shiftX, nameof(logicalFocus));
            ValidateFloatRange(shiftZ, nameof(logicalFocus));
            sceneDelta = new Vector3((float)shiftX, 0f, (float)shiftZ);
            return true;
        }

        private static double Snap(double value, double snapSize)
        {
            double scaled = value / snapSize;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled))
                throw new ArgumentOutOfRangeException(nameof(value));
            double snapped = Math.Round(scaled, MidpointRounding.AwayFromZero) * snapSize;
            if (double.IsNaN(snapped) || double.IsInfinity(snapped))
                throw new ArgumentOutOfRangeException(nameof(value));
            return snapped;
        }

        private static void ValidateFloatRange(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value > float.MaxValue || value < -float.MaxValue)
                throw new ArgumentOutOfRangeException(paramName, "Logical-relative coordinate cannot be represented by Unity float space.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
