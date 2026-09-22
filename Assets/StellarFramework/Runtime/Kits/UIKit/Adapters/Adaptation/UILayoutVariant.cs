using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StellarFramework.UI.Adaptation
{
    [Serializable]
    public sealed class UILayoutTargetState
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private Vector2 _anchorMin;
        [SerializeField] private Vector2 _anchorMax;
        [SerializeField] private Vector2 _pivot;
        [SerializeField] private Vector2 _anchoredPosition;
        [SerializeField] private Vector2 _sizeDelta;
        [SerializeField] private Vector3 _localScale = Vector3.one;
        [SerializeField] private bool _activeSelf = true;

        public RectTransform Target => _target;

        public static UILayoutTargetState Capture(RectTransform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new UILayoutTargetState
            {
                _target = target,
                _anchorMin = target.anchorMin,
                _anchorMax = target.anchorMax,
                _pivot = target.pivot,
                _anchoredPosition = target.anchoredPosition,
                _sizeDelta = target.sizeDelta,
                _localScale = target.localScale,
                _activeSelf = target.gameObject.activeSelf
            };
        }

        public void Apply()
        {
            if (_target == null)
            {
                return;
            }

            _target.anchorMin = _anchorMin;
            _target.anchorMax = _anchorMax;
            _target.pivot = _pivot;
            _target.anchoredPosition = _anchoredPosition;
            _target.sizeDelta = _sizeDelta;
            _target.localScale = _localScale;
            if (_target.gameObject.activeSelf != _activeSelf)
            {
                _target.gameObject.SetActive(_activeSelf);
            }
        }
    }

    [Serializable]
    public sealed class UILayoutVariantDefinition
    {
        [SerializeField] private string _breakpointId = "default";
        [SerializeField] private UILayoutTargetState[] _states =
            Array.Empty<UILayoutTargetState>();

        public string BreakpointId =>
            string.IsNullOrWhiteSpace(_breakpointId) ? "default" : _breakpointId.Trim();
        public IReadOnlyList<UILayoutTargetState> States => _states;

        public UILayoutVariantDefinition() { }

        public UILayoutVariantDefinition(
            string breakpointId,
            UILayoutTargetState[] states)
        {
            _breakpointId = string.IsNullOrWhiteSpace(breakpointId)
                ? "default"
                : breakpointId.Trim();
            _states = states ?? Array.Empty<UILayoutTargetState>();
        }

        public void Apply()
        {
            foreach (UILayoutTargetState state in _states ?? Array.Empty<UILayoutTargetState>())
            {
                state?.Apply();
            }
        }
    }

    /// <summary>
    /// Applies an authored RectTransform snapshot only when the active adaptation breakpoint changes.
    /// It never polls screen state by itself; UIAdaptationController owns screen detection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UILayoutVariant : MonoBehaviour
    {
        [SerializeField] private UIAdaptationController _controller;
        [SerializeField] private UILayoutVariantDefinition[] _variants =
            Array.Empty<UILayoutVariantDefinition>();
        [SerializeField] private bool _autoResolveController = true;

        public UIAdaptationController Controller => _controller;
        public IReadOnlyList<UILayoutVariantDefinition> Variants => _variants;

        private void OnEnable()
        {
            ResolveController();
            if (_controller == null)
            {
                return;
            }

            _controller.BreakpointChanged += HandleBreakpointChanged;
            if (!string.IsNullOrWhiteSpace(_controller.CurrentBreakpointId))
            {
                ApplyVariant(_controller.CurrentBreakpointId);
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.BreakpointChanged -= HandleBreakpointChanged;
            }
        }

        public void Configure(
            UIAdaptationController controller,
            UILayoutVariantDefinition[] variants)
        {
            if (_controller != null && isActiveAndEnabled)
            {
                _controller.BreakpointChanged -= HandleBreakpointChanged;
            }

            _controller = controller;
            _variants = variants ?? Array.Empty<UILayoutVariantDefinition>();

            if (_controller != null && isActiveAndEnabled)
            {
                _controller.BreakpointChanged += HandleBreakpointChanged;
            }
        }

        public void SetVariant(
            string breakpointId,
            UILayoutTargetState[] states)
        {
            string normalizedId = string.IsNullOrWhiteSpace(breakpointId)
                ? "default"
                : breakpointId.Trim();
            var list = (_variants ?? Array.Empty<UILayoutVariantDefinition>()).ToList();
            int index = list.FindIndex(item =>
                item != null &&
                string.Equals(item.BreakpointId, normalizedId, StringComparison.Ordinal));
            var definition = new UILayoutVariantDefinition(normalizedId, states);
            if (index >= 0)
            {
                list[index] = definition;
            }
            else
            {
                list.Add(definition);
            }
            _variants = list.ToArray();
        }

        public bool ApplyVariant(string breakpointId)
        {
            string normalizedId = string.IsNullOrWhiteSpace(breakpointId)
                ? "default"
                : breakpointId.Trim();
            UILayoutVariantDefinition definition =
                (_variants ?? Array.Empty<UILayoutVariantDefinition>())
                .FirstOrDefault(item =>
                    item != null &&
                    string.Equals(
                        item.BreakpointId,
                        normalizedId,
                        StringComparison.Ordinal));
            if (definition == null)
            {
                return false;
            }

            definition.Apply();
            return true;
        }

        public UILayoutTargetState[] CaptureCurrentHierarchy(
            RectTransform captureRoot,
            bool includeInactive = true)
        {
            if (captureRoot == null)
            {
                return Array.Empty<UILayoutTargetState>();
            }

            RectTransform[] targets =
                captureRoot.GetComponentsInChildren<RectTransform>(includeInactive);
            return targets
                .Select(UILayoutTargetState.Capture)
                .ToArray();
        }

        private void ResolveController()
        {
            if (_controller != null || !_autoResolveController)
            {
                return;
            }
            _controller = GetComponentInParent<UIAdaptationController>(true);
        }

        private void HandleBreakpointChanged(string breakpointId)
        {
            ApplyVariant(breakpointId);
        }
    }
}
