using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StellarFramework
{
    /// <summary>
    ///     创建三角面
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TriangularScanMesh : MonoBehaviour
    {
        [Header("几何参数")] public float range = 20f; // 顶点到底面距离 (沿本地Z)
        public float baseRadius = 8f; // 控制底面横向尺寸
        public float verticalSkew = 0.8f; // 底面上方顶点高度系数
        public bool generateBottom; // 是否生成底面
        public bool barycentricEdge = true; // 是否写入barycentric（边缘线描绘用）

        [Header("自动重建")] public bool autoRegenerate = true;

        private Mesh _mesh;
        private MeshFilter _mf;
        private bool o_bottom, o_bary;

        // 记录旧值
        private float o_range, o_base, o_skew;

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && autoRegenerate)
                if (Changed())
                    GenerateMesh();
#endif
        }

        private void OnEnable()
        {
            Ensure();
            if (_mesh == null || _mf.sharedMesh == null)
                GenerateMesh();
        }

        private void Ensure()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mesh == null) _mesh = _mf.sharedMesh;
        }

        private bool Changed()
        {
            return
                Mathf.Abs(o_range - range) > 1e-4f ||
                Mathf.Abs(o_base - baseRadius) > 1e-4f ||
                Mathf.Abs(o_skew - verticalSkew) > 1e-4f ||
                o_bottom != generateBottom ||
                o_bary != barycentricEdge;
        }

        public void GenerateMesh()
        {
            if (range <= 0) range = 0.01f;
            if (baseRadius <= 0) baseRadius = 0.01f;

            Ensure();
            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.name = "TriangularScanMesh_Generated";
                _mf.sharedMesh = _mesh;
            }
            else
            {
                _mesh.Clear();
            }

            // 底面三个点 (局部 z=range)
            var b1 = new Vector3(-baseRadius, -baseRadius * 0.2f, range);
            var b2 = new Vector3(baseRadius, -baseRadius * 0.2f, range);
            var b3 = new Vector3(0f, baseRadius * verticalSkew, range);
            var apex = Vector3.zero;

            // 侧面三个三角： (apex,b1,b2) (apex,b2,b3) (apex,b3,b1)
            // 为了边缘线，用不共享顶点方式（每个三角独立 3 顶点）
            var sideTris = 3;
            var sideVerts = sideTris * 3;
            var bottomVerts = generateBottom ? 3 : 0;

            var verts = new Vector3[sideVerts + bottomVerts];
            var bary = new Vector3[verts.Length]; // 存 barycentric（放 UV1）
            var uv = new Vector2[verts.Length];
            var indices = new int[verts.Length];

            var v = 0;

            void AddTri(Vector3 A, Vector3 B, Vector3 C)
            {
                verts[v] = A;
                bary[v] = barycentricEdge ? new Vector3(1, 0, 0) : Vector3.zero;
                uv[v] = Vector2.zero;
                indices[v] = v;
                v++;

                verts[v] = B;
                bary[v] = barycentricEdge ? new Vector3(0, 1, 0) : Vector3.zero;
                uv[v] = Vector2.right;
                indices[v] = v;
                v++;

                verts[v] = C;
                bary[v] = barycentricEdge ? new Vector3(0, 0, 1) : Vector3.zero;
                uv[v] = Vector2.up;
                indices[v] = v;
                v++;
            }

            // 侧面
            AddTri(apex, b1, b2);
            AddTri(apex, b2, b3);
            AddTri(apex, b3, b1);

            // 底面（可选）方向：让法线朝上 (b1,b3,b2)
            if (generateBottom)
                AddTri(b1, b3, b2);

            _mesh.vertices = verts;
            _mesh.triangles = indices;
            _mesh.uv = uv;

            if (barycentricEdge)
                _mesh.SetUVs(1, new List<Vector3>(bary));
            else
                _mesh.SetUVs(1, (List<Vector3>)null);

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            // 保存旧值
            o_range = range;
            o_base = baseRadius;
            o_skew = verticalSkew;
            o_bottom = generateBottom;
            o_bary = barycentricEdge;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(_mesh);
#endif
        }
    }
}