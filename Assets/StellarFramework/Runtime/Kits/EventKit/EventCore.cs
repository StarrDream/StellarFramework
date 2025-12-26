using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Event
{
    /// <summary>
    /// 事件注销接口
    /// </summary>
    public interface IUnRegister
    {
        /// <summary>
        /// 立即注销
        /// </summary>
        void UnRegister();

        /// <summary>
        /// 绑定生命周期：当指定 GameObject 销毁时自动注销
        /// </summary>
        IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject);
    }

    /// <summary>
    /// 注销接口的具体实现
    /// </summary>
    public class CustomUnRegister : IUnRegister
    {
        private Action _onUnRegister;

        public CustomUnRegister(Action onUnRegister)
        {
            _onUnRegister = onUnRegister;
        }

        public void UnRegister()
        {
            _onUnRegister?.Invoke();
            _onUnRegister = null;
        }

        public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
        {
            if (gameObject == null)
            {
                UnRegister();
                return this;
            }

            // 自动查找或添加辅助组件
            if (!gameObject.TryGetComponent<EventUnregisterTrigger>(out var trigger))
            {
                trigger = gameObject.AddComponent<EventUnregisterTrigger>();
                // 隐藏组件，保持 Inspector 干净
                trigger.hideFlags = HideFlags.HideInInspector;
            }

            trigger.Add(this);
            return this;
        }
    }

    /// <summary>
    /// 自动挂载的辅助组件，用于监听 OnDestroy
    /// </summary>
    [DisallowMultipleComponent]
    public class EventUnregisterTrigger : MonoBehaviour
    {
        private readonly HashSet<IUnRegister> _unRegisters = new HashSet<IUnRegister>();

        public void Add(IUnRegister unRegister)
        {
            _unRegisters.Add(unRegister);
        }

        private void OnDestroy()
        {
            foreach (var unRegister in _unRegisters)
            {
                unRegister.UnRegister();
            }

            _unRegisters.Clear();
        }
    }
}