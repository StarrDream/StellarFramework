using System;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>Scene binding with stable id and generation-safe runtime handle.</summary>
    [DisallowMultipleComponent]
    public sealed class FlowBinding : MonoBehaviour
    {
        [SerializeField] private string bindingId;
        private FlowHost _host;
        private FlowBindingHandle _handle;
        private bool _waitingForInitialization;

        public FlowBindingId BindingId => bindingId;
        public FlowBindingHandle Handle => _handle;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(bindingId))
                throw new InvalidOperationException("FlowBinding.bindingId cannot be empty.");

            _host = GetComponentInParent<FlowHost>();
            if (_host == null)
                throw new InvalidOperationException("FlowBinding must be placed under a FlowHost.");

            if (_host.IsInitialized)
            {
                RegisterBinding();
                return;
            }

            _waitingForInitialization = true;
            _host.Initialized += OnHostInitialized;
        }

        private void OnHostInitialized(FlowHost host)
        {
            if (!_waitingForInitialization || !ReferenceEquals(host, _host)) return;
            _host.Initialized -= OnHostInitialized;
            _waitingForInitialization = false;
            if (isActiveAndEnabled) RegisterBinding();
        }

        private void RegisterBinding()
        {
            if (_host == null || !_host.IsInitialized)
                throw new InvalidOperationException("FlowBinding cannot register before FlowHost initialization.");
            if (_handle.IsValid)
                throw new InvalidOperationException($"FlowBinding is already registered: {bindingId}");
            _handle = _host.Bind(bindingId, this);
        }

        private void OnDisable()
        {
            if (_host == null) return;
            if (_waitingForInitialization)
            {
                _host.Initialized -= OnHostInitialized;
                _waitingForInitialization = false;
            }

            if (_handle.IsValid && _host.IsInitialized) _host.Unbind(_handle);
            _handle = default(FlowBindingHandle);
            _host = null;
        }
    }
}
