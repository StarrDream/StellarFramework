using System;
using TMPro;
using UnityEngine;

namespace StellarFramework.Localization.TMP
{
    [DisallowMultipleComponent]
    public sealed class LocalizedTMPTextView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _contextProvider;
        [SerializeField] private TMP_Text _target;
        [SerializeField] private string _key;
        [SerializeField, HideInInspector] private string _bindingId;
        [SerializeField] private bool _autoResolveContext = true;

        private ILocalizationContext _context;
        private bool _isBound;

        public string Key => _key ?? string.Empty;
        public string BindingId => _bindingId ?? string.Empty;
        public TMP_Text Target => _target;
        public MonoBehaviour ContextProvider => _contextProvider;
        public bool AutoResolveContext => _autoResolveContext;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!Bind(out string error))
            {
                Debug.LogError("[LocalizedTMPTextView] " + error, this);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            Unbind();
        }

        public void ConfigureBinding(
            string bindingId,
            TMP_Text target,
            string key,
            MonoBehaviour contextProvider = null)
        {
            bool rebind = _isBound;
            if (rebind)
            {
                Unbind();
            }

            _bindingId = bindingId == null ? string.Empty : bindingId.Trim();
            _target = target;
            _key = key == null ? string.Empty : key.Trim();
            _contextProvider = contextProvider;
            _context = contextProvider as ILocalizationContext;

            if (rebind && !Bind(out string error))
            {
                Debug.LogError("[LocalizedTMPTextView] " + error, this);
            }
        }

        public bool Bind(out string error)
        {
            if (_isBound)
            {
                return Refresh(out error);
            }

            if (!Refresh(out error))
            {
                return false;
            }

            _context.LocaleChanged += HandleLocaleChanged;
            _isBound = true;
            return true;
        }

        public void Unbind()
        {
            if (!_isBound)
            {
                return;
            }

            if (_context != null)
            {
                _context.LocaleChanged -= HandleLocaleChanged;
            }
            _isBound = false;
        }

        public bool Refresh(out string error)
        {
            if (!TryResolveContext(out error))
            {
                return false;
            }
            if (_target == null)
            {
                error = "TMP target is not assigned.";
                return false;
            }
            if (!_context.TryInitialize(out error))
            {
                return false;
            }
            if (!LocalizationKey.TryCreate(_key, out LocalizationKey key, out error))
            {
                return false;
            }

            LocalizationLookupResult lookup = _context.Service.Lookup(key);
            if (!lookup.Success)
            {
                error = "Lookup failed with status " + lookup.Status + " for key '" + key + "'.";
                return false;
            }

            _target.text = lookup.Value;
            error = null;
            return true;
        }

        private bool TryResolveContext(out string error)
        {
            if (_contextProvider != null)
            {
                _context = _contextProvider as ILocalizationContext;
                if (_context == null)
                {
                    error = _contextProvider.GetType().FullName + " does not implement ILocalizationContext.";
                    return false;
                }
                error = null;
                return true;
            }

            if (!_autoResolveContext)
            {
                error = "Localization context provider is not assigned.";
                return false;
            }

            MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ILocalizationContext context)
                {
                    _contextProvider = behaviours[i];
                    _context = context;
                    error = null;
                    return true;
                }
            }

            error = "No parent MonoBehaviour implements ILocalizationContext.";
            return false;
        }

        private void HandleLocaleChanged(object sender, LocalizationChangedEventArgs args)
        {
            if (!Refresh(out string error))
            {
                Debug.LogError("[LocalizedTMPTextView] " + error, this);
            }
        }
    }
}
