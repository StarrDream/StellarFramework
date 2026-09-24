using System;
using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework.RuntimeTools
{
    /// <summary>可序列化 Collision 事件。</summary>
    [Serializable]
    public sealed class CollisionUnityEvent : UnityEvent<Collision>
    {
    }

    /// <summary>
    /// 把 OnCollisionEnter/Stay/Exit 转成 Inspector UnityEvent 与 C# event。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollisionRelay : MonoBehaviour
    {
        [SerializeField] private PhysicsRelayFilter filter = PhysicsRelayFilter.All;
        [SerializeField] private CollisionUnityEvent onEnter = new CollisionUnityEvent();
        [SerializeField] private CollisionUnityEvent onStay = new CollisionUnityEvent();
        [SerializeField] private CollisionUnityEvent onExit = new CollisionUnityEvent();

        /// <summary>C# Collision Enter 事件。</summary>
        public event Action<Collision> Entered;
        /// <summary>C# Collision Stay 事件。</summary>
        public event Action<Collision> Stayed;
        /// <summary>C# Collision Exit 事件。</summary>
        public event Action<Collision> Exited;

        private void Reset()
        {
            filter = PhysicsRelayFilter.All;
        }

        /// <summary>运行时替换过滤条件。</summary>
        public void SetFilter(PhysicsRelayFilter newFilter)
        {
            filter = newFilter;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Passes(collision)) return;
            onEnter?.Invoke(collision);
            Entered?.Invoke(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!Passes(collision)) return;
            onStay?.Invoke(collision);
            Stayed?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!Passes(collision)) return;
            onExit?.Invoke(collision);
            Exited?.Invoke(collision);
        }

        private bool Passes(Collision collision)
        {
            return collision != null && filter.Matches(collision.gameObject);
        }
    }
}
