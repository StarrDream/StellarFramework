using System;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>场景中的显式稳定绑定槽。对象生命周期由 Unity 管理，引用由 FlowHost 注册表管理。</summary>
    [DisallowMultipleComponent]
    public sealed class FlowBinding : MonoBehaviour
    {
        [SerializeField] private string bindingId;
        private FlowHost _host;
        private FlowBindingHandle _handle;

        public FlowBindingId BindingId => bindingId;
        public FlowBindingHandle Handle => _handle;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(bindingId)) throw new InvalidOperationException("FlowBinding.bindingId 不能为空。");
            _host = GetComponentInParent<FlowHost>();
            if (_host == null) throw new InvalidOperationException("FlowBinding 必须位于 FlowHost 下方。");
            _handle = _host.Bind(bindingId, this);
        }

        private void OnDisable()
        {
            if (_host == null) return;
            _host.Unbind(bindingId);
            _host = null;
            _handle = default(FlowBindingHandle);
        }
    }
}
