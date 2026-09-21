using StellarFramework.Localization.UnityUGUI;
using UnityEngine;

namespace StellarFramework.Demo
{
    public sealed class DemoLocaleSwitcher : MonoBehaviour
    {
        [SerializeField] private LocalizationContext context;

        public LocalizationContext Context => context;

        public void Configure(LocalizationContext localizationContext)
        {
            context = localizationContext;
        }

        public void UseChinese() => SetLocale("zh-CN");
        public void UseEnglish() => SetLocale("en-US");

        private void SetLocale(string locale)
        {
            if (context == null)
            {
                Debug.LogError("[DemoLocaleSwitcher] LocalizationContext is not assigned.", this);
                return;
            }

            if (!context.SetLocale(locale, out string error))
                Debug.LogError("[DemoLocaleSwitcher] " + error, this);
        }
    }
}
