using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    public static class FindUsedAssetsTool
    {
        // Hub 直接调用这两个方法
        public static void FindAndSelectMaterials()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                Debug.LogError("[FindUsedAssetsTool] 未选中任何 GameObject");
                return;
            }

            HashSet<Material> materials = new HashSet<Material>();

            for (int i = 0; i < roots.Length; i++)
            {
                CollectMaterialsFromGameObject(roots[i], materials);
            }

            var list = materials.Where(m => m != null).Cast<Object>().ToArray();
            Selection.objects = list;

            Debug.Log($"[FindUsedAssetsTool] 定位材质完成：{list.Length} 个");
        }

        public static void FindAndSelectTextures()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                Debug.LogError("[FindUsedAssetsTool] 未选中任何 GameObject");
                return;
            }

            HashSet<Texture> textures = new HashSet<Texture>();

            for (int i = 0; i < roots.Length; i++)
            {
                CollectTexturesFromGameObject(roots[i], textures);
            }

            var list = textures.Where(t => t != null).Cast<Object>().ToArray();
            Selection.objects = list;

            Debug.Log($"[FindUsedAssetsTool] 定位贴图完成：{list.Length} 个");
        }

        private static void CollectMaterialsFromGameObject(GameObject root, HashSet<Material> result)
        {
            if (!root) return;

            // Renderer
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int k = 0; k < mats.Length; k++)
                    result.Add(mats[k]);
            }

            // UI Graphic
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (!g) continue;
                result.Add(g.material);
            }
        }

        private static void CollectTexturesFromGameObject(GameObject root, HashSet<Texture> result)
        {
            if (!root) return;

            // Renderer -> Material -> Shader properties
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int k = 0; k < mats.Length; k++)
                {
                    var m = mats[k];
                    if (!m || !m.shader) continue;

                    CollectTexturesFromMaterial(m, result);
                }
            }

            // RawImage.texture
            var rawImages = root.GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < rawImages.Length; i++)
            {
                var ri = rawImages[i];
                if (!ri) continue;
                result.Add(ri.texture);
            }

            // Image.sprite.texture
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (!img) continue;
                if (img.sprite) result.Add(img.sprite.texture);
            }

            // SpriteRenderer.sprite.texture
            var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (!sr) continue;
                if (sr.sprite) result.Add(sr.sprite.texture);
            }
        }

        private static void CollectTexturesFromMaterial(Material mat, HashSet<Texture> result)
        {
            // ShaderUtil 是 Editor-only API
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(mat.shader, i);
                var tex = mat.GetTexture(propName);
                if (tex) result.Add(tex);
            }
        }
    }
}