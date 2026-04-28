using System;
using System.Collections.Generic;

namespace StellarFramework.Settings
{
    public static class SettingsKit
    {
        private static SettingsManager Manager => SettingsManager.Instance;

        public static bool IsInitialized => Manager.IsInitialized;
        public static bool HasDirtySettings => Manager.HasDirtySettings;

        public static event Action<SettingEntry> SettingChanged
        {
            add => Manager.SettingChanged += value;
            remove => Manager.SettingChanged -= value;
        }

        public static void ConfigureStorage(ISettingsStorage storage)
        {
            Manager.Configure(storage);
        }

        public static void RegisterProvider(ISettingsPageProvider provider)
        {
            Manager.RegisterProvider(provider);
        }

        public static void InstallDefaultProviders(DefaultSettingsInstallOptions options = null)
        {
            Manager.InstallDefaultProviders(options ?? new DefaultSettingsInstallOptions());
        }

        public static void Init()
        {
            Manager.Init();
        }

        public static IReadOnlyList<SettingsPageDefinition> GetPages()
        {
            return Manager.GetPages();
        }

        public static IReadOnlyList<SettingEntry> GetEntriesForPage(string pageId)
        {
            return Manager.GetEntriesForPage(pageId);
        }

        public static bool TryGetEntry(string key, out SettingEntry entry)
        {
            return Manager.TryGetEntry(key, out entry);
        }

        public static T GetValue<T>(string key, T fallback = default)
        {
            return Manager.GetValue(key, fallback);
        }

        public static bool TrySetValue(string key, object rawValue, out string error)
        {
            return Manager.TrySetValue(key, rawValue, out error);
        }

        public static bool ApplyPending(out string error)
        {
            return Manager.ApplyPending(out error);
        }

        public static bool Save(out string error)
        {
            return Manager.Save(out error);
        }

        public static bool RevertPending(out string error)
        {
            return Manager.RevertPending(out error);
        }

        public static void ResetPage(string pageId)
        {
            Manager.ResetPage(pageId);
        }

        public static void ResetAll()
        {
            Manager.ResetAll();
        }
    }
}
