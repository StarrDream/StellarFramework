using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// 无依赖的安装状态页：通过已加载程序集呈现当前项目实际拥有的 Kit 与可选能力。
    /// </summary>
    [StellarTool("Kit 安装状态", "Start Here", 1)]
    public sealed class KitInstallationHubModule : ToolModule
    {
        private static readonly KitAssemblyInfo[] KnownKits =
        {
            new KitAssemblyInfo("ActionKit", "StellarFramework.ActionKit"),
            new KitAssemblyInfo("AudioKit.Core", "StellarFramework.AudioKit"),
            new KitAssemblyInfo("AudioKit.ResKitAdapter", "StellarFramework.AudioKit.ResKit", true),
            new KitAssemblyInfo("BindableKit", "StellarFramework.BindableKit"),
            new KitAssemblyInfo("ConfigKit.Core", "StellarFramework.ConfigKit.Core"),
            new KitAssemblyInfo("ConfigKit.NewtonsoftJson", "StellarFramework.ConfigKit.Json", true),
            new KitAssemblyInfo("EventKit", "StellarFramework.EventKit"),
            new KitAssemblyInfo("FSMKit", "StellarFramework.FSMKit"),
            new KitAssemblyInfo("HttpKit", "StellarFramework.HttpKit"),
            new KitAssemblyInfo("LogKit", "StellarFramework.LogKit"),
            new KitAssemblyInfo("PoolKit", "StellarFramework.PoolKit"),
            new KitAssemblyInfo("ResKit.Core", "StellarFramework.ResKit"),
            new KitAssemblyInfo("ResKit.AssetBundle", "StellarFramework.ResKit.AssetBundle", true),
            new KitAssemblyInfo("ResKit.Addressables", "StellarFramework.ResKit.Addressables", true),
            new KitAssemblyInfo("SettingsKit.Core", "StellarFramework.SettingsKit"),
            new KitAssemblyInfo("SettingsKit.UnityAdapters", "StellarFramework.SettingsKit.UnityAdapters", true),
            new KitAssemblyInfo("SettingsKit.AudioKitAdapter", "StellarFramework.SettingsKit.AudioKit", true),
            new KitAssemblyInfo("SingletonKit", "StellarFramework.SingletonKit"),
            new KitAssemblyInfo("UIKit.Core", "StellarFramework.UIKit"),
            new KitAssemblyInfo("UIKit.ResKitAdapter", "StellarFramework.UIKit.ResKit", true),
            new KitAssemblyInfo("HotUpdate.Core", "StellarFramework.HotUpdateKit", true),
            new KitAssemblyInfo("HotUpdate.Addressables", "StellarFramework.HotUpdateKit.Addressables", true),
            new KitAssemblyInfo("HotUpdate.HybridCLR", "StellarFramework.HotUpdateKit.HybridCLR", true)
        };

        public override string Description => "自动扫描当前已加载程序集，展示已导入 Kit 与可选 Adapter；不依赖任何具体 Kit。";

        public override void OnGUI()
        {
            HashSet<string> loadedAssemblies = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name),
                StringComparer.Ordinal);

            int installedCount = KnownKits.Count(info => loadedAssemblies.Contains(info.AssemblyName));
            EditorGUILayout.HelpBox(
                $"已识别 {installedCount}/{KnownKits.Length} 个 Kit / Adapter。此结果基于当前实际加载程序集，刷新或重新编译后会自动更新。",
                MessageType.Info);

            DrawSection("核心 Kit", loadedAssemblies, false);
            GUILayout.Space(8);
            DrawSection("可选 Adapter 与能力", loadedAssemblies, true);

            GUILayout.Space(12);
            if (GUILayout.Button("重新扫描", Window.PrimaryButtonStyle))
            {
                Window.Repaint();
            }
        }

        private static void DrawSection(string title, HashSet<string> loadedAssemblies, bool adaptersOnly)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (KitAssemblyInfo info in KnownKits.Where(item => item.IsAdapter == adaptersOnly))
            {
                bool installed = loadedAssemblies.Contains(info.AssemblyName);
                GUIContent content = new GUIContent(
                    installed ? $"✓  {info.DisplayName}" : $"○  {info.DisplayName}",
                    info.AssemblyName);
                EditorGUILayout.LabelField(content, installed ? EditorStyles.label : EditorStyles.miniLabel);
            }
        }

        private readonly struct KitAssemblyInfo
        {
            public readonly string DisplayName;
            public readonly string AssemblyName;
            public readonly bool IsAdapter;

            public KitAssemblyInfo(string displayName, string assemblyName, bool isAdapter = false)
            {
                DisplayName = displayName;
                AssemblyName = assemblyName;
                IsAdapter = isAdapter;
            }
        }
    }
}
