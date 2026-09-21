using System;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public static class WorldHeightAuthoringOperations
    {
        public static void Raise(
            ReadOnlySpan<float> baseValues,
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float amount)
        {
            ValidatePositiveFinite(amount, nameof(amount));
            ApplyDelta(baseValues, overrides, in bounds, amount);
        }

        public static void Lower(
            ReadOnlySpan<float> baseValues,
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float amount)
        {
            ValidatePositiveFinite(amount, nameof(amount));
            ApplyDelta(baseValues, overrides, in bounds, -amount);
        }

        public static void SetHeight(
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float height)
        {
            ValidateFinite(height, nameof(height));
            ValidateBounds(overrides, in bounds);
            Fill(overrides, in bounds, height);
        }

        public static void Flatten(
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float targetHeight)
        {
            SetHeight(overrides, in bounds, targetHeight);
        }

        public static void Smooth(
            ReadOnlySpan<float> baseValues,
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float factor,
            Span<float> scratch)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            ValidateFinite(factor, nameof(factor));
            if (factor < 0f || factor > 1f)
                throw new ArgumentOutOfRangeException(nameof(factor), "Smooth factor must be in [0,1].");
            ValidateBase(baseValues, overrides);
            ValidateBounds(overrides, in bounds);
            if (scratch.Length < bounds.Count)
                throw new ArgumentException("Scratch buffer is smaller than the edit bounds sample count.", nameof(scratch));

            int write = 0;
            int width = overrides.Layout.Width;
            int height = overrides.Layout.Height;
            for (int y = bounds.Y; y < bounds.TopExclusive; y++)
            {
                int row = y * width;
                for (int x = bounds.X; x < bounds.RightExclusive; x++)
                {
                    int index = row + x;
                    float current = overrides.GetComposedValueByIndexUnchecked(baseValues, index);
                    double sum = current;
                    int count = 1;

                    if (x > 0)
                    {
                        sum += overrides.GetComposedValueByIndexUnchecked(baseValues, index - 1);
                        count++;
                    }
                    if (x + 1 < width)
                    {
                        sum += overrides.GetComposedValueByIndexUnchecked(baseValues, index + 1);
                        count++;
                    }
                    if (y > 0)
                    {
                        sum += overrides.GetComposedValueByIndexUnchecked(baseValues, index - width);
                        count++;
                    }
                    if (y + 1 < height)
                    {
                        sum += overrides.GetComposedValueByIndexUnchecked(baseValues, index + width);
                        count++;
                    }

                    float average = (float)(sum / count);
                    scratch[write++] = current + ((average - current) * factor);
                }
            }

            write = 0;
            for (int y = bounds.Y; y < bounds.TopExclusive; y++)
            {
                int row = y * width;
                for (int x = bounds.X; x < bounds.RightExclusive; x++)
                    overrides.SetByIndexUnchecked(row + x, scratch[write++]);
            }
            overrides.MarkDirty(in bounds);
        }

        private static void ApplyDelta(
            ReadOnlySpan<float> baseValues,
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float delta)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            ValidateBase(baseValues, overrides);
            ValidateBounds(overrides, in bounds);

            int width = overrides.Layout.Width;
            for (int y = bounds.Y; y < bounds.TopExclusive; y++)
            {
                int row = y * width;
                for (int x = bounds.X; x < bounds.RightExclusive; x++)
                {
                    int index = row + x;
                    float current = overrides.GetComposedValueByIndexUnchecked(baseValues, index);
                    float next = current + delta;
                    if (float.IsNaN(next) || float.IsInfinity(next))
                        throw new OverflowException("Height edit produced a non-finite value.");
                    overrides.SetByIndexUnchecked(index, next);
                }
            }
            overrides.MarkDirty(in bounds);
        }

        private static void Fill(
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float value)
        {
            int width = overrides.Layout.Width;
            for (int y = bounds.Y; y < bounds.TopExclusive; y++)
            {
                int row = y * width;
                for (int x = bounds.X; x < bounds.RightExclusive; x++)
                    overrides.SetByIndexUnchecked(row + x, value);
            }
            overrides.MarkDirty(in bounds);
        }

        private static void ValidateBase(
            ReadOnlySpan<float> baseValues,
            WorldDenseOverrideLayer<float> overrides)
        {
            if (baseValues.Length != overrides.Layout.Count)
                throw new ArgumentException("Base height count must match authoring layout.", nameof(baseValues));
        }

        private static void ValidateBounds<T>(
            WorldDenseOverrideLayer<T> overrides,
            in WorldSampleRect bounds)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            WorldPlanarSampleLayout layout = overrides.Layout;
            if (!bounds.FitsWithin(in layout))
                throw new ArgumentOutOfRangeException(nameof(bounds), "Edit bounds exceed authoring layout.");
        }

        private static void ValidatePositiveFinite(float value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value <= 0f) throw new ArgumentOutOfRangeException(parameterName, "Value must be > 0.");
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }
    }
}
