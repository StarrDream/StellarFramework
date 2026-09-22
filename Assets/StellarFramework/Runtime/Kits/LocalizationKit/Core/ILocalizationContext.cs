using System;

namespace StellarFramework.Localization
{
    /// <summary>
    /// Engine-facing presentation adapters use this contract to access one localization service host
    /// without depending on each other. Core defines the contract only and does not own MonoBehaviour lifecycle.
    /// </summary>
    public interface ILocalizationContext
    {
        LocalizationService Service { get; }
        LocaleId CurrentLocale { get; }
        event EventHandler<LocalizationChangedEventArgs> LocaleChanged;
        bool TryInitialize(out string error);
    }
}
