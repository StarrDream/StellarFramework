using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Extras
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIMapLineRenderer : MaskableGraphic
    {
        [Header("线条设置")] public float thickness = 5f;
        public bool connectEnds = false;

        [Header("纹理支持")] [Tooltip("留空则使用纯色，赋值则可做虚线")]
        public Texture m_Texture;

        private readonly List<Vector2> _points = new List<Vector2>();

        // --- 核心修正：解决黑色线条和Mask遮挡问题 ---
        public override Texture mainTexture
        {
            get
            {
                if (m_Texture == null)
                {
                    if (material != null && material.mainTexture != null)
                        return material.mainTexture;
                    return s_WhiteTexture; // 关键：回退到白图，确保顶点颜色生效
                }

                return m_Texture;
            }
        }

        public void SetPoints(List<Vector2> newPoints)
        {
            _points.Clear();
            if (newPoints != null) _points.AddRange(newPoints);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;

            for (int i = 0; i < _points.Count - 1; i++)
                DrawSegment(vh, _points[i], _points[i + 1]);

            if (connectEnds && _points.Count > 2)
                DrawSegment(vh, _points[_points.Count - 1], _points[0]);
        }

        private void DrawSegment(VertexHelper vh, Vector2 start, Vector2 end)
        {
            Vector2 dir = end - start;
            Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
            Vector2 offset = normal * (thickness * 0.5f);

            UIVertex[] verts = new UIVertex[4];

            // 必须设置 UV，否则部分 Shader 会报错
            verts[0].position = start - offset;
            verts[0].color = color;
            verts[0].uv0 = new Vector2(0, 0);
            verts[1].position = start + offset;
            verts[1].color = color;
            verts[1].uv0 = new Vector2(0, 1);
            verts[2].position = end + offset;
            verts[2].color = color;
            verts[2].uv0 = new Vector2(1, 1);
            verts[3].position = end - offset;
            verts[3].color = color;
            verts[3].uv0 = new Vector2(1, 0);

            vh.AddUIVertexQuad(verts);
        }
    }
}