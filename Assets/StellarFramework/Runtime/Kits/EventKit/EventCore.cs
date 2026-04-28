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

        public CustomUnRegister(Action onUnRegister)
        {
            _onUnRegister = onUnRegister;
        }

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
                LogKit.LogError("[CustomUnRegister] 生命周期绑定失败: gameObject 为空");
                UnRegister();
                return this;
            }

            if (!TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
            {
                LogKit.LogError($"[CustomUnRegister] 生命周期绑定失败: 无法挂载销毁触发器, TriggerObject={gameObject.name}");
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
                LogKit.LogError("[CustomUnRegister] 生命周期绑定失败: mono 为空");
                UnRegister();
                return this;
            }

            return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
        }

        public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
        {
            if (mono == null || mono.gameObject == null)
            {
                LogKit.LogError("[CustomUnRegister] 生命周期绑定失败: mono 或 gameObject 为空");
                UnRegister();
                return this;
            }

            if (!TryAttachDisableTrigger(mono.gameObject, out EventUnregisterOnDisableTrigger trigger))
            {
                LogKit.LogError($"[CustomUnRegister] 生命周期绑定失败: 无法挂载失活触发器, TriggerObject={mono.gameObject.name}");
                UnRegister();
                return this;
            }

            trigger.Add(this);
            return this;
        }

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

        public void Add(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                LogKit.LogError($"[EventUnregisterTrigger] Add 失败: unRegister 为空, TriggerObject={gameObject.name}");
                return;
            }

            _unRegisters.Add(unRegister);
        }

        private void OnDestroy()
        {
            foreach (IUnRegister unRegister in _unRegisters)
            {
                unRegister?.UnRegister();
            }

            _unRegisters.Clear();
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

        public void Add(IUnRegister unRegister)
        {
            if (unRegister == null)
            {
                LogKit.LogError(
                    $"[EventUnregisterOnDisableTrigger] Add 失败: unRegister 为空, TriggerObject={gameObject.name}");
                return;
            }

            _unRegisters.Add(unRegister);
        }

        private void OnDisable()
        {
            foreach (IUnRegister unRegister in _unRegisters)
            {
                unRegister?.UnRegister();
            }

            _unRegisters.Clear();
        }
    }
}
