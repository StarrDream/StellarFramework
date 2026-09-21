using System;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>Scene binding with stable id and generation-safe runtime handle.</summary>
    [DisallowMultipleComponent]
    public sealed class FlowBinding : MonoBehaviour
    {
        [SerializeField] private string bindingId;
        [Tooltip("Optional object exposed to Operation adapters. When empty, the FlowBinding component itself is bound for backward compatibility.")]
        [SerializeField] private UnityEngine.Object target;
        private FlowHost _host;
        private FlowBindingHandle _handle;
        private bool _waitingForInitialization;

        public FlowBindingId BindingId => bindingId;
        public FlowBindingHandle Handle => _handle;
        public UnityEngine.Object Target => target;
        public object BoundValue => target != null ? target : (object)this;

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
            _handle = _host.Bind(bindingId, BoundValue);
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
