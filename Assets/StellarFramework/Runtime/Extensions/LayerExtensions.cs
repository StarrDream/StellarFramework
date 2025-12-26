using UnityEngine;

namespace StellarFramework
{
    public static class LayerExtensions
    {
        /// <summary>
        /// 检查 LayerMask 是否包含某个 Layer
        /// </summary>
        public static bool Contains(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// <summary>
        /// 检查 LayerMask 是否包含某个 GameObject 的 Layer
        /// </summary>
        public static bool Contains(this LayerMask mask, GameObject go)
        {
            return (mask.value & (1 << go.layer)) != 0;
        }
    }
}