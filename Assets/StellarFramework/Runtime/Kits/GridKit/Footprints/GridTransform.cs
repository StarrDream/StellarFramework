using System;

namespace StellarFramework
{
    /// <summary>Footprint 变换：先 ReflectX，再 ReflectY，最后按逻辑 XY 顺时针旋转。</summary>
    public readonly struct GridTransform : IEquatable<GridTransform>
    {
        public GridRotation Rotation { get; }
        public bool ReflectX { get; }
        public bool ReflectY { get; }

        public static GridTransform Identity => new GridTransform(GridRotation.Deg0, false, false);

        public GridTransform(GridRotation rotation, bool reflectX = false, bool reflectY = false)
        {
            if (rotation < GridRotation.Deg0 || rotation > GridRotation.Deg270)
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }

            Rotation = rotation;
            ReflectX = reflectX;
            ReflectY = reflectY;
        }

        public bool TryApply(GridOffset offset, out GridOffset result)
        {
            long x = offset.X;
            long y = offset.Y;
            if (ReflectX) x = -x;
            if (ReflectY) y = -y;

            long transformedX;
            long transformedY;
            switch (Rotation)
            {
                case GridRotation.Deg0:
                    transformedX = x;
                    transformedY = y;
                    break;
                case GridRotation.Deg90:
                    transformedX = y;
                    transformedY = -x;
                    break;
                case GridRotation.Deg180:
                    transformedX = -x;
                    transformedY = -y;
                    break;
                case GridRotation.Deg270:
                    transformedX = -y;
                    transformedY = x;
                    break;
                default:
                    result = default(GridOffset);
                    return false;
            }

            if (transformedX < int.MinValue || transformedX > int.MaxValue ||
                transformedY < int.MinValue || transformedY > int.MaxValue)
            {
                result = default(GridOffset);
                return false;
            }

            result = new GridOffset((int)transformedX, (int)transformedY);
            return true;
        }

        public GridOffset Apply(GridOffset offset)
        {
            if (!TryApply(offset, out GridOffset result))
            {
                throw new OverflowException("GridTransform result cannot be represented by Int32.");
            }

            return result;
        }

        public bool Equals(GridTransform other) => Rotation == other.Rotation &&
            ReflectX == other.ReflectX && ReflectY == other.ReflectY;
        public override bool Equals(object obj) => obj is GridTransform && Equals((GridTransform)obj);
        public override int GetHashCode() => ((int)Rotation * 397) ^ (ReflectX ? 1 : 0) ^ (ReflectY ? 2 : 0);
        public override string ToString() => string.Format("{0}, ReflectX={1}, ReflectY={2}", Rotation, ReflectX, ReflectY);

        public static bool operator ==(GridTransform left, GridTransform right) => left.Equals(right);
        public static bool operator !=(GridTransform left, GridTransform right) => !left.Equals(right);
    }
}
