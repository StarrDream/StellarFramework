using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>Trigger/Collision Relay 共用过滤条件。</summary>
    [Serializable]
    public struct PhysicsRelayFilter
    {
        [Tooltip("允许触发事件的 Layer。")]
        [SerializeField] private LayerMask layers;
        [Tooltip("开启后额外要求目标具有指定 Tag。")]
        [SerializeField] private bool requireTag;
        [SerializeField] private string requiredTag;

        /// <summary>允许所有 Layer、不检查 Tag 的默认过滤器。</summary>
        public static PhysicsRelayFilter All => new PhysicsRelayFilter
        {
            layers = ~0,
            requireTag = false,
            requiredTag = string.Empty
        };

        /// <summary>运行时配置过滤器。</summary>
        public void Configure(LayerMask layerMask, bool shouldRequireTag = false, string tag = null)
        {
            layers = layerMask;
            requireTag = shouldRequireTag;
            requiredTag = tag ?? string.Empty;
        }

        /// <summary>判断目标是否通过 Layer/Tag 条件。</summary>
        public bool Matches(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            if ((layers.value & (1 << target.layer)) == 0)
            {
                return false;
            }

            return !requireTag || (!string.IsNullOrEmpty(requiredTag) && target.CompareTag(requiredTag));
        }
    }
}
