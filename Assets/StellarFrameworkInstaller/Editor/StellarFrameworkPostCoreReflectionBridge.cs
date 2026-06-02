using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkPostCoreReflectionBridge
    {
        public static bool EnsureResKitRuntimeSettings(StellarFrameworkInstallerReport report)
        {
            Type settingsType = FindType("StellarFramework.Res.ResKitRuntimeSettings");
            if (settingsType == null)
            {
                report?.AddWarning("尚未检测到 ResKitRuntimeSettings 类型。Core 导入和编译完成后可再次运行安装器。");
                return false;
            }

            UnityEngine.Object settings = AssetDatabase.LoadAssetAtPath(
                StellarFrameworkInstallerConstants.ResKitRuntimeSettingsAssetPath,
                settingsType);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance(settingsType);
                string directory = System.IO.Path.GetDirectoryName(StellarFrameworkInstallerConstants.ResKitRuntimeSettingsAssetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(StellarFrameworkInstallerPathUtility.ToFullPath(directory));
                }

                AssetDatabase.CreateAsset(settings, StellarFrameworkInstallerConstants.ResKitRuntimeSettingsAssetPath);
                report?.AddMessage("已创建 ResKitRuntimeSettings 默认资产。");
            }
            else
            {
                report?.AddMessage("已检测到 ResKitRuntimeSettings，保留现有资产。");
            }

            SerializedObject serialized = new SerializedObject(settings);
            SetStringArray(serialized, "addressablesDefaultHotUpdateLabels", new[] { "hotupdate" });
            SetStringArray(serialized, "addressablesDefaultUpdateKeys", new[] { "hotupdate" });
            SetString(serialized, "hotUpdateAssemblyKey", "HotUpdate.dll.bytes");
            SetString(serialized, "hotUpdateEntryClass", "HotUpdate.HotUpdateMain");
            SetString(serialized, "hotUpdateEntryMethod", "Main");
            SetStringArray(serialized, "aotMetadataKeys", new[]
            {
                "mscorlib.dll.bytes",
                "System.dll.bytes",
                "System.Core.dll.bytes"
            });
            SetBool(serialized, "hotUpdateManifestFallbackToStreamingAssets", true);
            SetBool(serialized, "hotUpdateManifestFallbackToResources", true);
            SetInt(serialized, "hotUpdateManifestHttpTimeoutSeconds", 30);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool EnsureAAWorkflowConfig(StellarFrameworkInstallerReport report)
        {
            Type storeType = FindType("StellarFramework.Editor.Modules.AAWorkflowConfigStore");
            if (storeType == null)
            {
                report?.AddWarning("尚未检测到 AAWorkflowConfigStore。Core 和 ToolsHub 编译完成后可再次运行热更新安装。");
                return false;
            }

            storeType.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object configSet = storeType.GetProperty("ConfigSet", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null, null);
            storeType.GetMethod("Save", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

            if (configSet == null)
            {
                report?.AddWarning("未能创建 AAWorkflowConfigSet。");
                return false;
            }

            report?.AddMessage("已创建或加载 AAWorkflowConfigSet 默认配置。");
            return true;
        }

        public static bool TryApplyAAWorkflowDefaults(StellarFrameworkInstallerReport report)
        {
            if (!StellarFrameworkAddressablesReflectionBridge.IsAddressablesEditorAvailable())
            {
                report?.AddWarning("Addressables Editor 不可用，跳过 AA 默认分组配置。");
                return false;
            }

            Type storeType = FindType("StellarFramework.Editor.Modules.AAWorkflowConfigStore");
            Type configSetType = FindType("StellarFramework.Editor.Modules.AAWorkflowConfigSet");
            Type modeType = FindType("StellarFramework.Editor.Modules.AAWorkflowMode");
            Type configuratorType = FindType("StellarFramework.Editor.Modules.AAAddressablesConfigurator");
            Type reportType = FindType("StellarFramework.Editor.Modules.AAHotUpdatePublishRunReport");

            if (storeType == null || configSetType == null || modeType == null || configuratorType == null || reportType == null)
            {
                report?.AddWarning("AA 工作流反射类型不完整，跳过 Addressables Profile/Group 写入。");
                return false;
            }

            object configSet = storeType.GetProperty("ConfigSet", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null, null);
            MethodInfo getFirstConfigMethod = configSetType.GetMethod("GetFirstConfig", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo tryApplyMethod = configuratorType.GetMethod("TryApply", BindingFlags.Public | BindingFlags.Static);
            if (configSet == null || getFirstConfigMethod == null || tryApplyMethod == null)
            {
                report?.AddWarning("AA 工作流方法不完整，跳过 Addressables Profile/Group 写入。");
                return false;
            }

            bool appliedLocal = TryApplyConfig(configSet, getFirstConfigMethod, tryApplyMethod, modeType, reportType, "LocalBuiltIn", report);
            bool appliedRemote = TryApplyConfig(configSet, getFirstConfigMethod, tryApplyMethod, modeType, reportType, "RemoteHotUpdate", report);
            return appliedLocal || appliedRemote;
        }

        private static bool TryApplyConfig(
            object configSet,
            MethodInfo getFirstConfigMethod,
            MethodInfo tryApplyMethod,
            Type modeType,
            Type reportType,
            string modeName,
            StellarFrameworkInstallerReport report)
        {
            object mode = Enum.Parse(modeType, modeName);
            object config = getFirstConfigMethod.Invoke(configSet, new[] { mode });
            object aaReport = Activator.CreateInstance(reportType);
            object result = tryApplyMethod.Invoke(null, new[]
            {
                config,
                (object)EditorUserBuildSettings.activeBuildTarget,
                aaReport
            });

            CopyAAReportMessages(aaReport, report);
            return result is bool value && value;
        }

        private static void CopyAAReportMessages(object aaReport, StellarFrameworkInstallerReport installerReport)
        {
            if (aaReport == null || installerReport == null)
            {
                return;
            }

            CopyStringListField(aaReport, "Messages", installerReport.AddMessage);
            CopyStringListField(aaReport, "Warnings", installerReport.AddWarning);
            CopyStringListField(aaReport, "Errors", installerReport.AddError);
        }

        private static void CopyStringListField(object source, string fieldName, Action<string> add)
        {
            FieldInfo field = source.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (!(field?.GetValue(source) is IEnumerable values))
            {
                return;
            }

            foreach (object value in values)
            {
                if (value != null)
                {
                    add(value.ToString());
                }
            }
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetStringArray(SerializedObject serialized, string propertyName, string[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static Type FindType(string fullName)
        {
            return StellarFrameworkAddressablesReflectionBridge.FindType(fullName);
        }
    }
}
