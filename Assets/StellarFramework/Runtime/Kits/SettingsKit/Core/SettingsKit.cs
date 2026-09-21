using System;
using System.Collections.Generic;

namespace StellarFramework.Settings
{
    /// <summary>
    /// SettingsManager 的静态业务门面。
    /// 简单项目可直接使用本类；需要多实例/测试隔离时可直接构造 SettingsManager。
    /// </summary>
    public static class SettingsKit
    {
        private static SettingsManager Manager => SettingsManager.Instance;

        /// <summary>默认 Manager 是否已初始化。</summary>
        public static bool IsInitialized => Manager.IsInitialized;
        /// <summary>默认 Manager 是否存在未保存改动。</summary>
        public static bool HasDirtySettings => Manager.HasDirtySettings;

        /// <summary>默认 Manager 的设置变化通知。</summary>
        public static event Action<SettingEntry> SettingChanged
        {
            add => Manager.SettingChanged += value;
            remove => Manager.SettingChanged -= value;
        }

        /// <summary>初始化前配置持久化后端。</summary>
        public static void ConfigureStorage(ISettingsStorage storage)
        {
            Manager.Configure(storage);
        }

        /// <summary>注册设置 Provider。</summary>
        public static void RegisterProvider(ISettingsPageProvider provider)
        {
            Manager.RegisterProvider(provider);
        }

        /// <summary>按 Options 安装框架内置页面。</summary>
        public static void InstallDefaultProviders(DefaultSettingsInstallOptions options = null)
        {
            Manager.InstallDefaultProviders(options ?? new DefaultSettingsInstallOptions());
        }

        /// <summary>初始化默认 Manager。</summary>
        public static void Init()
        {
            Manager.Init();
        }

        /// <summary>取得页面列表。</summary>
        public static IReadOnlyList<SettingsPageDefinition> GetPages()
        {
            return Manager.GetPages();
        }

        /// <summary>取得指定页面 Entry。</summary>
        public static IReadOnlyList<SettingEntry> GetEntriesForPage(string pageId)
        {
            return Manager.GetEntriesForPage(pageId);
        }

        /// <summary>查询 Entry。</summary>
        public static bool TryGetEntry(string key, out SettingEntry entry)
        {
            return Manager.TryGetEntry(key, out entry);
        }

        /// <summary>读取当前值。</summary>
        public static T GetValue<T>(string key, T fallback = default)
        {
            return Manager.GetValue(key, fallback);
        }

        /// <summary>验证并修改当前值。</summary>
        public static bool TrySetValue(string key, object rawValue, out string error)
        {
            return Manager.TrySetValue(key, rawValue, out error);
        }

        /// <summary>应用所有延迟生效的 Dirty 设置，但不保存。</summary>
        public static bool ApplyPending(out string error)
        {
            return Manager.ApplyPending(out error);
        }

        /// <summary>应用并保存全部当前设置。</summary>
        public static bool Save(out string error)
        {
            return Manager.Save(out error);
        }

        /// <summary>放弃所有未保存改动并恢复到 SavedValue。</summary>
        public static bool RevertPending(out string error)
        {
            return Manager.RevertPending(out error);
        }

        /// <summary>把指定页面恢复到默认值，不自动保存。</summary>
        public static void ResetPage(string pageId)
        {
            Manager.ResetPage(pageId);
        }

        /// <summary>把全部设置恢复到默认值，不自动保存。</summary>
        public static void ResetAll()
        {
            Manager.ResetAll();
        }
    }
}
