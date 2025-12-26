#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarFramework
{
    public class CombinedMeshCollider : MonoBehaviour
    {
        [Header("设置")] public bool autoCreateOnStart = true;
        public string colliderObjectName = "CombinedCollider";

        [Header("包含的Renderer类型")] public bool includeMeshRenderer = true;
        public bool includeSkinnedMeshRenderer = true;
        public bool includeParticleSystemRenderer;
        public bool includeLineRenderer;
        public bool includeTrailRenderer;

        [Header("SkinnedMeshRenderer设置")] public bool bakePoseForSkinnedMesh = true;

        private void Start()
        {
            // 运行时通常不需要自动创建，除非特殊需求
            // if (autoCreateOnStart) CreateCombinedMeshCollider();
        }

        [ContextMenu("创建碰撞盒")]
        public GameObject CreateCombinedMeshCollider()
        {
            var combineInstances = new List<CombineInstance>();
            CollectMeshesFromRenderers(combineInstances);

            if (combineInstances.Count == 0)
            {
                LogKit.LogWarning("没有找到可用的Mesh进行合并！");
                return null;
            }

            //  预计算顶点数
            long totalVerts = 0;
            foreach (var ci in combineInstances)
                if (ci.mesh != null)
                    totalVerts += ci.mesh.vertexCount;

            var combinedMesh = new Mesh();

            //  安全检查：防止顶点越界导致崩溃
            if (totalVerts > 65535)
            {
                if (SystemInfo.supports32bitsIndexBuffer)
                {
                    combinedMesh.indexFormat = IndexFormat.UInt32;
                }
                else
                {
                    LogKit.LogError($"[CombinedMeshCollider] 顶点数 ({totalVerts}) 超过 16位限制且设备不支持 32位索引，合并终止以防止崩溃。");
                    return null;
                }
            }

            combinedMesh.CombineMeshes(combineInstances.ToArray());
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();

            return CreateColliderObject(combinedMesh, combineInstances.Count);
        }

        private void CollectMeshesFromRenderers(List<CombineInstance> combineInstances)
        {
            if (includeMeshRenderer) CollectFromMeshRenderers(combineInstances);
            if (includeSkinnedMeshRenderer) CollectFromSkinnedMeshRenderers(combineInstances);
            if (includeParticleSystemRenderer) CollectFromParticleSystemRenderers(combineInstances);
            if (includeLineRenderer) CollectFromLineRenderers(combineInstances);
            if (includeTrailRenderer) CollectFromTrailRenderers(combineInstances);
        }

        private void CollectFromMeshRenderers(List<CombineInstance> combineInstances)
        {
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var meshRenderer in meshRenderers)
            {
                if (!meshRenderer.enabled) continue;
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = meshFilter.sharedMesh;
                    combineInstance.transform = transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                    combineInstances.Add(combineInstance);
                }
            }
        }

        private void CollectFromSkinnedMeshRenderers(List<CombineInstance> combineInstances)
        {
            var skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (!skinnedMeshRenderer.enabled) continue;
                if (skinnedMeshRenderer.sharedMesh != null)
                {
                    var bakedMesh = new Mesh();
                    if (bakePoseForSkinnedMesh && Application.isPlaying)
                        skinnedMeshRenderer.BakeMesh(bakedMesh);
                    else
                        bakedMesh = skinnedMeshRenderer.sharedMesh;

                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = bakedMesh;
                    combineInstance.transform = transform.worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix;
                    combineInstances.Add(combineInstance);
                }
            }
        }

        private void CollectFromParticleSystemRenderers(List<CombineInstance> combineInstances)
        {
            var particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var particleRenderer in particleRenderers)
            {
                if (!particleRenderer.enabled) continue;
                if (particleRenderer.mesh != null)
                {
                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = particleRenderer.mesh;
                    combineInstance.transform = transform.worldToLocalMatrix * particleRenderer.transform.localToWorldMatrix;
                    combineInstances.Add(combineInstance);
                }
            }
        }

        private void CollectFromLineRenderers(List<CombineInstance> combineInstances)
        {
            var lineRenderers = GetComponentsInChildren<LineRenderer>();
            foreach (var lineRenderer in lineRenderers)
            {
                if (!lineRenderer.enabled || lineRenderer.positionCount < 2) continue;
                var lineMesh = CreateMeshFromLineRenderer(lineRenderer);
                if (lineMesh != null)
                {
                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = lineMesh;
                    combineInstance.transform = transform.worldToLocalMatrix * lineRenderer.transform.localToWorldMatrix;
                    combineInstances.Add(combineInstance);
                }
            }
        }

        private void CollectFromTrailRenderers(List<CombineInstance> combineInstances)
        {
            var trailRenderers = GetComponentsInChildren<TrailRenderer>();
            foreach (var trailRenderer in trailRenderers)
            {
                if (!trailRenderer.enabled) continue;
                var trailMesh = CreateMeshFromTrailRenderer(trailRenderer);
                if (trailMesh != null)
                {
                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = trailMesh;
                    combineInstance.transform = transform.worldToLocalMatrix * trailRenderer.transform.localToWorldMatrix;
                    combineInstances.Add(combineInstance);
                }
            }
        }

        private Mesh CreateMeshFromLineRenderer(LineRenderer lineRenderer)
        {
            if (lineRenderer.positionCount < 2) return null;
            var mesh = new Mesh();
            lineRenderer.BakeMesh(mesh, true);
            return mesh.vertexCount > 0 ? mesh : null;
        }

        private Mesh CreateMeshFromTrailRenderer(TrailRenderer trailRenderer)
        {
            var mesh = new Mesh();
            trailRenderer.BakeMesh(mesh, true);
            return mesh.vertexCount > 0 ? mesh : null;
        }

        private GameObject CreateColliderObject(Mesh combinedMesh, int meshCount)
        {
            var colliderObject = new GameObject(colliderObjectName);
            colliderObject.transform.SetParent(transform);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;

            var meshCollider = colliderObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = combinedMesh;
            meshCollider.convex = false;

            LogKit.Log($"成功合并 {meshCount} 个Mesh，顶点数: {combinedMesh.vertexCount}");
            return colliderObject;
        }

        public void ClearColliders()
        {
            var existingCollider = transform.Find(colliderObjectName);
            if (existingCollider != null)
            {
                if (Application.isPlaying) Destroy(existingCollider.gameObject);
                else DestroyImmediate(existingCollider.gameObject);
            }
        }
    }
}
#endif