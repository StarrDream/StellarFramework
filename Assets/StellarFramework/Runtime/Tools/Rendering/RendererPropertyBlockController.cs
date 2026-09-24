using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 对 Renderer.MaterialPropertyBlock 的低 GC 运行时封装。
    /// 用于每实例颜色、浮点、向量、纹理等变化，避免访问 Renderer.material 导致材质实例化。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RendererPropertyBlockController : MonoBehaviour
    {
        [Tooltip("默认使用同物体 Renderer；也可显式指定其他 Renderer。")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("-1 表示 Renderer 级 PropertyBlock；>=0 表示指定材质槽。")]
        [SerializeField] private int materialIndex = -1;

        private MaterialPropertyBlock _block;
        private List<Material> _sharedMaterialScratch;

        /// <summary>当前写入目标 Renderer。</summary>
        public Renderer TargetRenderer => EnsureRenderer();

        /// <summary>当前材质槽；-1 表示 Renderer 级 PropertyBlock。</summary>
        public int MaterialIndex => materialIndex;

        private void Awake()
        {
            EnsureRenderer();
            EnsureBlock();
        }

        /// <summary>替换写入目标；传 null 时会回退到同物体 Renderer。</summary>
        public void SetRenderer(Renderer renderer)
        {
            targetRenderer = renderer;
        }

        /// <summary>设置材质槽；负数统一表示 Renderer 级 PropertyBlock。</summary>
        public void SetMaterialIndex(int index)
        {
            materialIndex = index < 0 ? -1 : index;
        }

        /// <summary>按 Shader property id 写入 float。</summary>
        public bool SetFloat(int propertyId, float value)
        {
            if (!PrepareForWrite()) return false;
            _block.SetFloat(propertyId, value);
            ApplyBlock();
            return true;
        }

        /// <summary>按 Shader property id 写入 int。</summary>
        public bool SetInt(int propertyId, int value)
        {
            if (!PrepareForWrite()) return false;
            _block.SetInt(propertyId, value);
            ApplyBlock();
            return true;
        }

        /// <summary>按 Shader property id 写入 Color。</summary>
        public bool SetColor(int propertyId, Color value)
        {
            if (!PrepareForWrite()) return false;
            _block.SetColor(propertyId, value);
            ApplyBlock();
            return true;
        }

        /// <summary>按 Shader property id 写入 Vector4。</summary>
        public bool SetVector(int propertyId, Vector4 value)
        {
            if (!PrepareForWrite()) return false;
            _block.SetVector(propertyId, value);
            ApplyBlock();
            return true;
        }

        /// <summary>按 Shader property id 写入 Texture。</summary>
        public bool SetTexture(int propertyId, Texture value)
        {
            if (!PrepareForWrite()) return false;
            _block.SetTexture(propertyId, value);
            ApplyBlock();
            return true;
        }

        /// <summary>
        /// 清除当前 Renderer 或材质槽的整个 PropertyBlock。
        /// 注意：这会同时清除其他系统写在同一层级 PropertyBlock 中的属性。
        /// </summary>
        public bool ClearAll()
        {
            Renderer renderer = EnsureRenderer();
            if (renderer == null)
            {
                return false;
            }

            if (materialIndex < 0)
            {
                renderer.SetPropertyBlock(null);
            }
            else
            {
                if (!HasMaterialSlot(renderer, materialIndex))
                {
                    return false;
                }

                renderer.SetPropertyBlock(null, materialIndex);
            }

            _block?.Clear();
            return true;
        }

        private bool PrepareForWrite()
        {
            Renderer renderer = EnsureRenderer();
            if (renderer == null)
            {
                return false;
            }

            if (materialIndex >= 0 && !HasMaterialSlot(renderer, materialIndex))
            {
                return false;
            }

            EnsureBlock();
            _block.Clear();
            if (materialIndex < 0)
            {
                renderer.GetPropertyBlock(_block);
            }
            else
            {
                renderer.GetPropertyBlock(_block, materialIndex);
            }

            return true;
        }

        private void ApplyBlock()
        {
            if (materialIndex < 0)
            {
                targetRenderer.SetPropertyBlock(_block);
            }
            else
            {
                targetRenderer.SetPropertyBlock(_block, materialIndex);
            }
        }

        private Renderer EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            return targetRenderer;
        }

        private void EnsureBlock()
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
        }

        private bool HasMaterialSlot(Renderer renderer, int index)
        {
            if (index < 0)
            {
                return true;
            }

            if (_sharedMaterialScratch == null)
            {
                _sharedMaterialScratch = new List<Material>(4);
            }

            _sharedMaterialScratch.Clear();
            renderer.GetSharedMaterials(_sharedMaterialScratch);
            return index < _sharedMaterialScratch.Count;
        }
    }
}
