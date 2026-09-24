using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// Renderer / Collider 世界 Bounds 计算工具。
    /// 提供便利重载，也提供可复用 scratch List 的低分配重载。
    /// </summary>
    public static class BoundsUtility
    {
        /// <summary>计算所有子 Renderer 的世界 Bounds。</summary>
        public static bool TryCalculateRendererBounds(
            Transform root,
            bool includeInactive,
            out Bounds bounds)
        {
            var scratch = new List<Renderer>(16);
            return TryCalculateRendererBounds(root, includeInactive, scratch, out bounds);
        }

        /// <summary>使用调用方复用的 scratch List 计算 Renderer Bounds。</summary>
        public static bool TryCalculateRendererBounds(
            Transform root,
            bool includeInactive,
            List<Renderer> scratch,
            out Bounds bounds)
        {
            EnsureArguments(root, scratch);
            scratch.Clear();
            root.GetComponentsInChildren(includeInactive, scratch);
            return EncapsulateRenderers(scratch, out bounds);
        }

        /// <summary>计算所有子 Collider 的世界 Bounds。</summary>
        public static bool TryCalculateColliderBounds(
            Transform root,
            bool includeInactive,
            out Bounds bounds)
        {
            var scratch = new List<Collider>(16);
            return TryCalculateColliderBounds(root, includeInactive, scratch, out bounds);
        }

        /// <summary>使用调用方复用的 scratch List 计算 Collider Bounds。</summary>
        public static bool TryCalculateColliderBounds(
            Transform root,
            bool includeInactive,
            List<Collider> scratch,
            out Bounds bounds)
        {
            EnsureArguments(root, scratch);
            scratch.Clear();
            root.GetComponentsInChildren(includeInactive, scratch);
            return EncapsulateColliders(scratch, out bounds);
        }

        private static bool EncapsulateRenderers(IReadOnlyList<Renderer> renderers, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static bool EncapsulateColliders(IReadOnlyList<Collider> colliders, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return initialized;
        }

        private static void EnsureArguments<T>(Transform root, List<T> scratch)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (scratch == null)
            {
                throw new ArgumentNullException(nameof(scratch));
            }
        }
    }
}
