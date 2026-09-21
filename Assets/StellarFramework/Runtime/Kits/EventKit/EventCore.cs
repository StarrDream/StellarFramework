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

        /// <summary>
        /// 绑定生命周期：当指定 MonoBehaviour 所在的 GameObject 销毁时自动注销
        /// </summary>
        IUnRegister UnRegisterWhenGameObjectDestroyed(MonoBehaviour mono);

        /// <summary>
        /// 绑定生命周期：当指定 MonoBehaviour 所在的 GameObject 失活时自动注销
        /// 规范要求：必须通过 gameObject.SetActive(false) 触发
        /// </summary>
        IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono);
    }

    /// <summary>
    /// 注销接口的通用实现
    /// 我负责把注销动作包装成生命周期可绑定的对象，避免业务层直接操作底层触发器。
    /// </summary>
    public class CustomUnRegister : IUnRegister
    {
        private Action _onUnRegister;
        private bool _isUnregistered;

        /// <summary>
        /// 用指定注销动作创建句柄。传入 null 代表安全的空句柄。
        /// </summary>
        public CustomUnRegister(Action onUnRegister)
        {
            _onUnRegister = onUnRegister;
        }

        /// <summary>
        /// 执行注销动作。该操作具有幂等性，多次调用只会执行一次底层回调。
        /// </summary>
        public void UnRegister()
        {
            if (_isUnregistered)
            {
                return;
            }

            _isUnregistered = true;
            _onUnRegister?.Invoke();
            _onUnRegister = null;
        }

        public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.LogError("[CustomUnRegister] 生命周期绑定失败: gameObject 为空");
                UnRegister();
                return this;
            }

            if (!TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
            {
                Debug.LogError($"[CustomUnRegister] 生命周期绑定失败: 无法挂载销毁触发器, TriggerObject={gameObject.name}");
                UnRegister();
                return this;
            }

            trigger.Add(this);
            return this;
        }

        public IUnRegister UnRegisterWhenGameObjectDestroyed(MonoBehaviour mono)
        {
            if (mono == null)
            {
                Debug.LogError("[CustomUnRegister] 生命周期绑定失败: mono 为空");
                UnRegister();
                return this;
            }

            return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
        }

        public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
        {
            if (mono == null || mono.gameObject == null)
            {
                Debug.LogError("[CustomUnRegister] 生命周期绑定失败: mono 或 gameObject 为空");
                UnRegister();
                return this;
            }

            if (!TryAttachDisableTrigger(mono.gameObject, out EventUnregisterOnDisableTrigger trigger))
            {
                Debug.LogError($"[CustomUnRegister] 生命周期绑定失败: 无法挂载失活触发器, TriggerObject={mono.gameObject.name}");
                UnRegister();
                return this;
            }

            trigger.Add(this);
            return this;
        }

        /// <summary>
        /// 尝试取得或自动挂载 OnDestroy 注销触发器。
        /// 仅有效 Scene 中的 GameObject 可以动态挂载。
        /// </summary>
        public static bool TryAttachDestroyTrigger(GameObject gameObject, out EventUnregisterTrigger trigger)
        {
            trigger = null;

            if (gameObject == null)
            {
                return false;
            }

            if (gameObject.TryGetComponent(out trigger))
            {
                return true;
            }

            if (!gameObject.scene.IsValid())
            {
                return false;
            }

            trigger = gameObject.AddComponent<EventUnregisterTrigger>();
            trigger.hideFlags = HideFlags.HideInInspector;
            return trigger != null;
        }

        /// <summary>
        /// 尝试取得或自动挂载 OnDisable 注销触发器。
        /// </summary>
        public static bool TryAttachDisableTrigger(GameObject gameObject, out EventUnregisterOnDisableTrigger trigger)
        {
            trigger = null;

            if (gameObject == null)
            {
                return false;
            }

            if (gameObject.TryGetComponent(out trigger))
            {
                return true;
            }

            if (!gameObject.scene.IsValid())
            {
                return false;
            }

            trigger = gameObject.AddComponent<EventUnregisterOnDisableTrigger>();
            trigger.hideFlags = HideFlags.HideInInspector;
            return trigger != null;
        }
    }

    /// <summary>
    /// 自动挂载的辅助组件，用于监听 OnDestroy
    /// </summary>
    [DisallowMultipleComponent]
    public class EventUnregisterTrigger : MonoBehaviour
    {
        private readonly HashSet<IUnRegister> _unRegisters = new HashSet<IUnRegister>();
        private bool _isUnregistering;

        /// <summary>
        /// 将注销句柄绑定到当前 GameObject 的 OnDestroy。
        /// </summary>
        public void Add(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                Debug.LogError($"[EventUnregisterTrigger] Add 失败: unRegister 为空, TriggerObject={gameObject.name}");
                return;
            }

            _unRegisters.Add(unRegister);
        }

        /// <summary>
        /// 解除绑定：当 Token 被主动回收时调用。
        /// 防止已回收并复用的 Token 在宿主对象销毁时被误取消注册（use-after-free）。
        /// </summary>
        public void Remove(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                return;
            }

            // OnDestroy 会主动驱动集合中的 Token 注销；池化 EventToken 在回收时又会反向调用
            // trigger.Remove(this)。此时不能修改正在 foreach 的 HashSet，否则会抛
            // InvalidOperationException: Collection was modified。
            if (_isUnregistering)
            {
                return;
            }

            _unRegisters.Remove(unRegister);
        }

        private void OnDestroy()
        {
            _isUnregistering = true;
            try
            {
                foreach (IUnRegister unRegister in _unRegisters)
                {
                    unRegister?.UnRegister();
                }
            }
            finally
            {
                _isUnregistering = false;
                _unRegisters.Clear();
            }
        }
    }

    /// <summary>
    /// 自动挂载的辅助组件，用于监听 OnDisable
    /// 依赖宿主 GameObject 被 SetActive(false) 时触发
    /// </summary>
    [DisallowMultipleComponent]
    public class EventUnregisterOnDisableTrigger : MonoBehaviour
    {
        private readonly HashSet<IUnRegister> _unRegisters = new HashSet<IUnRegister>();
        private bool _isUnregistering;

        /// <summary>
        /// 将注销句柄绑定到当前 GameObject 的 OnDisable。
        /// </summary>
        public void Add(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                Debug.LogError(
                    $"[EventUnregisterOnDisableTrigger] Add 失败: unRegister 为空, TriggerObject={gameObject.name}");
                return;
            }

            _unRegisters.Add(unRegister);
        }

        /// <summary>
        /// 解除绑定：当 Token 被主动回收时调用。
        /// 防止已回收并复用的 Token 在宿主对象失活时被误取消注册（use-after-free）。
        /// </summary>
        public void Remove(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                return;
            }

            // 与 OnDestroy Trigger 相同：EventToken 回收时会反向解除生命周期绑定。
            // OnDisable 正在遍历时忽略这次 Remove，最后统一 Clear，避免修改迭代中的 HashSet。
            if (_isUnregistering)
            {
                return;
            }

            _unRegisters.Remove(unRegister);
        }

        private void OnDisable()
        {
            _isUnregistering = true;
            try
            {
                foreach (IUnRegister unRegister in _unRegisters)
                {
                    unRegister?.UnRegister();
                }
            }
            finally
            {
                _isUnregistering = false;
                _unRegisters.Clear();
            }
        }
    }
}
