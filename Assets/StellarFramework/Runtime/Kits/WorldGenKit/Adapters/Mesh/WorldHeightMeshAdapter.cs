using System;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarFramework.WorldGenKit.Unity.MeshAdapter
{
    public readonly struct WorldHeightMeshSettings
    {
        private readonly bool _initialized;

        public float HorizontalScale { get; }
        public float VerticalScale { get; }
        public bool RecalculateNormals { get; }

        public bool IsValid => _initialized &&
            IsFinite(HorizontalScale) &&
            HorizontalScale > 0f &&
            IsFinite(VerticalScale);

        public WorldHeightMeshSettings(
            float horizontalScale,
            float verticalScale,
            bool recalculateNormals = true)
        {
            if (!IsFinite(horizontalScale) || horizontalScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(horizontalScale));
            if (!IsFinite(verticalScale))
                throw new ArgumentOutOfRangeException(nameof(verticalScale));

            HorizontalScale = horizontalScale;
            VerticalScale = verticalScale;
            RecalculateNormals = recalculateNormals;
            _initialized = true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Unity-only heightfield projection. Scratch arrays are caller-owned so repeated rebuilds can reuse managed buffers.
    /// </summary>
    public static class WorldHeightMeshAdapter
    {
        public static int GetVertexCount(in WorldPlanarSampleLayout layout) => layout.Count;

        public static int GetTriangleIndexCount(in WorldPlanarSampleLayout layout)
        {
            if (layout.Width <= 1 || layout.Height <= 1) return 0;
            return checked(checked((layout.Width - 1) * (layout.Height - 1)) * 6);
        }

        public static void Build(
            UnityEngine.Mesh mesh,
            WorldGenerationDataSet data,
            ChannelHandle<float> heightChannel,
            in WorldPlanarSampleLayout layout,
            Vector3[] vertices,
            Vector2[] uv,
            int[] triangleIndices,
            in WorldHeightMeshSettings settings)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!settings.IsValid) throw new ArgumentException("Height mesh settings are invalid.", nameof(settings));

            int vertexCount = GetVertexCount(in layout);
            int triangleIndexCount = GetTriangleIndexCount(in layout);
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (uv == null) throw new ArgumentNullException(nameof(uv));
            if (triangleIndices == null) throw new ArgumentNullException(nameof(triangleIndices));
            if (vertices.Length != vertexCount)
                throw new ArgumentException("Vertex buffer length must exactly match the planar sample count.", nameof(vertices));
            if (uv.Length != vertexCount)
                throw new ArgumentException("UV buffer length must exactly match the planar sample count.", nameof(uv));
            if (triangleIndices.Length != triangleIndexCount)
                throw new ArgumentException("Triangle index buffer length does not match the planar topology.", nameof(triangleIndices));

            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(heightChannel, out DenseChannelStorage<float> heights))
                throw new InvalidOperationException("Height mesh projection requires a bound dense float channel.");
            if (heights.Length < layout.Count)
                throw new InvalidOperationException("Dense height channel is smaller than the planar sample layout.");

            double spacing = layout.SampleStep * (double)settings.HorizontalScale;
            if (double.IsNaN(spacing) || double.IsInfinity(spacing) || spacing > float.MaxValue)
                throw new InvalidOperationException("Height mesh horizontal spacing cannot be represented by Unity float coordinates.");

            ReadOnlySpan<float> values = heights.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                float value = values[i];
                double projected = value * (double)settings.VerticalScale;
                if (float.IsNaN(value) || float.IsInfinity(value) ||
                    double.IsNaN(projected) || double.IsInfinity(projected) ||
                    projected > float.MaxValue || projected < -float.MaxValue)
                    throw new InvalidOperationException("Height mesh source contains an invalid projected height at sample " + i + ".");
            }

            float uDenominator = layout.Width > 1 ? 1f / (layout.Width - 1) : 0f;
            float vDenominator = layout.Height > 1 ? 1f / (layout.Height - 1) : 0f;
            for (int y = 0; y < layout.Height; y++)
            {
                int row = y * layout.Width;
                float z = (float)(y * spacing);
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = row + x;
                    vertices[index] = new Vector3(
                        (float)(x * spacing),
                        values[index] * settings.VerticalScale,
                        z);
                    uv[index] = new Vector2(x * uDenominator, y * vDenominator);
                }
            }

            int write = 0;
            for (int y = 0; y < layout.Height - 1; y++)
            {
                int row = y * layout.Width;
                int nextRow = row + layout.Width;
                for (int x = 0; x < layout.Width - 1; x++)
                {
                    int a = row + x;
                    int b = nextRow + x;
                    int c = a + 1;
                    int d = b + 1;

                    triangleIndices[write++] = a;
                    triangleIndices[write++] = b;
                    triangleIndices[write++] = c;
                    triangleIndices[write++] = c;
                    triangleIndices[write++] = b;
                    triangleIndices[write++] = d;
                }
            }

            mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.Clear(false);
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangleIndices;
            if (settings.RecalculateNormals && triangleIndexCount > 0)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
