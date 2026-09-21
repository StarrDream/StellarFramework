using System;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;

namespace StellarFramework.WorldGenKit.Unity.DebugTextureAdapter
{
    public readonly struct WorldScalarDebugTextureSettings
    {
        private readonly bool _initialized;

        public float MinValue { get; }
        public float MaxValue { get; }
        public Color32 LowColor { get; }
        public Color32 HighColor { get; }
        public bool FlipY { get; }

        public bool IsValid => _initialized &&
            IsFinite(MinValue) &&
            IsFinite(MaxValue) &&
            MaxValue > MinValue;

        public WorldScalarDebugTextureSettings(
            float minValue,
            float maxValue,
            Color32 lowColor,
            Color32 highColor,
            bool flipY = false)
        {
            if (!IsFinite(minValue)) throw new ArgumentOutOfRangeException(nameof(minValue));
            if (!IsFinite(maxValue)) throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (maxValue <= minValue) throw new ArgumentOutOfRangeException(nameof(maxValue));

            MinValue = minValue;
            MaxValue = maxValue;
            LowColor = lowColor;
            HighColor = highColor;
            FlipY = flipY;
            _initialized = true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Unity-only projection of dense logical WorldGen channels into caller-owned Texture2D instances.
    /// The adapter never owns WorldGen data and does not cache project or scene state.
    /// </summary>
    public static class WorldDebugTextureAdapter
    {
        public static void UpdateScalarTexture(
            Texture2D texture,
            WorldGenerationDataSet data,
            ChannelHandle<float> channel,
            in WorldPlanarSampleLayout layout,
            Color32[] pixelBuffer,
            in WorldScalarDebugTextureSettings settings)
        {
            ValidateTextureAndBuffer(texture, pixelBuffer, in layout);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!settings.IsValid) throw new ArgumentException("Scalar debug texture settings are invalid.", nameof(settings));
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(channel, out DenseChannelStorage<float> storage))
                throw new InvalidOperationException("Debug texture projection requires a bound dense float channel.");
            if (storage.Length < layout.Count)
                throw new InvalidOperationException("Dense float channel is smaller than the planar sample layout.");

            ReadOnlySpan<float> values = storage.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                if (!IsFinite(values[i]))
                    throw new InvalidOperationException("Debug texture source contains a non-finite scalar value at sample " + i + ".");
            }

            float range = settings.MaxValue - settings.MinValue;
            for (int y = 0; y < layout.Height; y++)
            {
                int sourceRow = y * layout.Width;
                int destinationY = settings.FlipY ? layout.Height - 1 - y : y;
                int destinationRow = destinationY * layout.Width;
                for (int x = 0; x < layout.Width; x++)
                {
                    float normalized = Mathf.Clamp01((values[sourceRow + x] - settings.MinValue) / range);
                    pixelBuffer[destinationRow + x] = Lerp(settings.LowColor, settings.HighColor, normalized);
                }
            }

            texture.SetPixels32(pixelBuffer);
            texture.Apply(false, false);
        }

        public static void UpdateIndexedTexture(
            Texture2D texture,
            WorldGenerationDataSet data,
            ChannelHandle<int> channel,
            in WorldPlanarSampleLayout layout,
            Color32[] palette,
            Color32[] pixelBuffer,
            bool flipY = false)
        {
            ValidateTextureAndBuffer(texture, pixelBuffer, in layout);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Length == 0) throw new ArgumentException("Indexed debug texture palette cannot be empty.", nameof(palette));
            if (!data.TryGetStorage<int, DenseChannelStorage<int>>(channel, out DenseChannelStorage<int> storage))
                throw new InvalidOperationException("Indexed debug texture projection requires a bound dense int channel.");
            if (storage.Length < layout.Count)
                throw new InvalidOperationException("Dense int channel is smaller than the planar sample layout.");

            ReadOnlySpan<int> values = storage.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                if ((uint)values[i] >= (uint)palette.Length)
                    throw new InvalidOperationException("Indexed debug texture source contains an out-of-range palette index at sample " + i + ".");
            }

            for (int y = 0; y < layout.Height; y++)
            {
                int sourceRow = y * layout.Width;
                int destinationY = flipY ? layout.Height - 1 - y : y;
                int destinationRow = destinationY * layout.Width;
                for (int x = 0; x < layout.Width; x++)
                    pixelBuffer[destinationRow + x] = palette[values[sourceRow + x]];
            }

            texture.SetPixels32(pixelBuffer);
            texture.Apply(false, false);
        }

        private static void ValidateTextureAndBuffer(
            Texture2D texture,
            Color32[] pixelBuffer,
            in WorldPlanarSampleLayout layout)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (!texture.isReadable) throw new InvalidOperationException("Target Texture2D must be readable/writable.");
            if (texture.width != layout.Width || texture.height != layout.Height)
                throw new ArgumentException("Target Texture2D dimensions must exactly match the planar sample layout.", nameof(texture));
            if (pixelBuffer == null) throw new ArgumentNullException(nameof(pixelBuffer));
            if (pixelBuffer.Length != layout.Count)
                throw new ArgumentException("Pixel buffer length must exactly match the planar sample count.", nameof(pixelBuffer));
        }

        private static Color32 Lerp(Color32 from, Color32 to, float t) => new Color32(
            LerpByte(from.r, to.r, t),
            LerpByte(from.g, to.g, t),
            LerpByte(from.b, to.b, t),
            LerpByte(from.a, to.a, t));

        private static byte LerpByte(byte from, byte to, float t) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(from + ((to - from) * t)), 0, 255);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
