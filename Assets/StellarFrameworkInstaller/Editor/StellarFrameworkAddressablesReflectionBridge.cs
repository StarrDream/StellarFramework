using System;
using System.Linq;
using System.Reflection;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkAddressablesReflectionBridge
    {
        public static bool IsAddressablesEditorAvailable()
        {
            return FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject") != null;
        }

        public static bool EnsureDefaultAddressablesSettings(StellarFrameworkInstallerReport report)
        {
            Type defaultObjectType = FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
            if (defaultObjectType == null)
            {
                report?.AddWarning("Addressables Editor 尚未加载，跳过默认 Addressables Settings 创建。");
                return false;
            }

            object settings = defaultObjectType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null, null);
            if (settings != null)
            {
                report?.AddMessage("已检测到 Addressables Settings。");
                return true;
            }

            MethodInfo getSettingsMethod = defaultObjectType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "GetSettings", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(bool);
                });

            if (getSettingsMethod != null)
            {
                settings = getSettingsMethod.Invoke(null, new object[] { true });
            }

            if (settings == null)
            {
                report?.AddWarning("未能自动创建 Addressables Settings，请在 Addressables Groups 窗口初始化后再运行热更新安装。");
                return false;
            }

            report?.AddMessage("已创建或加载 Addressables Settings。");
            return true;
        }

        internal static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
