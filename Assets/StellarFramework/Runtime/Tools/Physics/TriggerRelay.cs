using System;
using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework.RuntimeTools
{
    /// <summary>可序列化 Collider 事件。</summary>
    [Serializable]
    public sealed class ColliderUnityEvent : UnityEvent<Collider>
    {
    }

    /// <summary>
    /// 把 OnTriggerEnter/Stay/Exit 转成 Inspector UnityEvent 与 C# event。
    /// 适合将物理检测与业务组件解耦，避免每个业务脚本重复实现触发器回调。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TriggerRelay : MonoBehaviour
    {
        [SerializeField] private PhysicsRelayFilter filter = PhysicsRelayFilter.All;
        [SerializeField] private ColliderUnityEvent onEnter = new ColliderUnityEvent();
        [SerializeField] private ColliderUnityEvent onStay = new ColliderUnityEvent();
        [SerializeField] private ColliderUnityEvent onExit = new ColliderUnityEvent();

        /// <summary>C# Enter 事件。</summary>
        public event Action<Collider> Entered;
        /// <summary>C# Stay 事件。</summary>
        public event Action<Collider> Stayed;
        /// <summary>C# Exit 事件。</summary>
        public event Action<Collider> Exited;

        private void Reset()
        {
            filter = PhysicsRelayFilter.All;
        }

        /// <summary>运行时替换过滤条件。</summary>
        public void SetFilter(PhysicsRelayFilter newFilter)
        {
            filter = newFilter;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!filter.Matches(other != null ? other.gameObject : null)) return;
            onEnter?.Invoke(other);
            Entered?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!filter.Matches(other != null ? other.gameObject : null)) return;
            onStay?.Invoke(other);
            Stayed?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!filter.Matches(other != null ? other.gameObject : null)) return;
            onExit?.Invoke(other);
            Exited?.Invoke(other);
        }
    }
}
