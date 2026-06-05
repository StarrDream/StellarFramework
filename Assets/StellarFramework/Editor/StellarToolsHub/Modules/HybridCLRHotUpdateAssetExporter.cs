using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public sealed class HybridCLRHotUpdateExportItem
    {
        public string SourcePath;
        public string DestinationAssetPath;
        public long Bytes;
        public string Sha256;
    }

    public sealed class HybridCLRHotUpdateExportReport
    {
        public readonly List<HybridCLRHotUpdateExportItem> HotUpdateDlls =
            new List<HybridCLRHotUpdateExportItem>();

        public readonly List<HybridCLRHotUpdateExportItem> AotMetadataDlls =
            new List<HybridCLRHotUpdateExportItem>();

        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public string ManifestAssetPath;
        public string StreamingAssetsManifestPath;
        public string ManifestJson;

        public bool Success => Errors.Count == 0;
        public int CopiedCount => HotUpdateDlls.Count + AotMetadataDlls.Count;
    }

    public static class HybridCLRHotUpdateAssetExporter
    {
        public const string DefaultHotUpdateSourceRoot = "HybridCLRData/HotUpdateDlls";
        public const string DefaultAotSourceRoot = "HybridCLRData/AssembliesPostIl2CppStrip";
        public const string DefaultHotUpdateAssetFolder = "Assets/GameHotUpdate/Code";
        public const string DefaultAotMetadataAssetFolder = "Assets/GameHotUpdate/Metadata";
        public const string DefaultManifestAssetFolder = "Assets/GameHotUpdate/Manifest";
        public const string DefaultManifestFileName = "HotUpdateManifest.json";

        private static readonly string[] DefaultAotAssemblyNames =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule"
        };

        public static void ExportCurrentBuildTargetFromMenu()
        {
            HybridCLRHotUpdateExportReport report = ExportGeneratedAssets(EditorUserBuildSettings.activeBuildTarget);
            LogReport(report);
            AssetDatabase.Refresh();
        }

        public static HybridCLRHotUpdateExportReport ExportGeneratedAssets(BuildTarget target,
            IEnumerable<string> hotUpdateAssemblyNames = null,
            IEnumerable<string> aotAssemblyNames = null,
            bool copyAllHotUpdateWhenEmpty = true,
            bool overwrite = true)
        {
            HybridCLRHotUpdateExportReport report = new HybridCLRHotUpdateExportReport();
            string targetName = target.ToString();
            string hotUpdateSource = Path.Combine(ProjectRoot, DefaultHotUpdateSourceRoot, targetName);
            string aotSource = Path.Combine(ProjectRoot, DefaultAotSourceRoot, targetName);

            List<string> resolvedHotUpdateNames = ToAssemblyNameList(hotUpdateAssemblyNames);
            if (resolvedHotUpdateNames.Count == 0)
            {
                resolvedHotUpdateNames = GetConfiguredHybridCLRHotUpdateAssemblyNames(report);
            }

            if (resolvedHotUpdateNames.Count == 0 && copyAllHotUpdateWhenEmpty)
            {
                resolvedHotUpdateNames = GetAllDllAssemblyNames(hotUpdateSource);
                report.Warnings.Add(
                    "HybridCLR settings did not provide hot update assemblies. Copied all generated hot update DLLs.");
            }

            if (resolvedHotUpdateNames.Count == 0)
            {
                report.Errors.Add("No hot update assemblies were configured or found.");
            }
            else
            {
                HybridCLRHotUpdateExportReport hotReport = ExportDllDirectory(
                    hotUpdateSource,
                    DefaultHotUpdateAssetFolder,
                    resolvedHotUpdateNames,
                    overwrite);
                report.HotUpdateDlls.AddRange(hotReport.HotUpdateDlls);
                report.Warnings.AddRange(hotReport.Warnings);
                report.Errors.AddRange(hotReport.Errors);
            }

            List<string> resolvedAotNames = ToAssemblyNameList(aotAssemblyNames);
            if (resolvedAotNames.Count == 0)
            {
                resolvedAotNames = GetConfiguredHybridCLRAotAssemblyNames(report);
            }

            if (resolvedAotNames.Count == 0)
            {
                resolvedAotNames = DefaultAotAssemblyNames
                    .Where(name => File.Exists(Path.Combine(aotSource, name + ".dll")))
                    .ToList();
                report.Warnings.Add("HybridCLR settings did not provide AOT metadata assemblies. Used default AOT list.");
            }

            if (resolvedAotNames.Count == 0)
            {
                report.Errors.Add("No AOT metadata assemblies were configured or found.");
            }
            else
            {
                HybridCLRHotUpdateExportReport aotReport = ExportDllDirectory(
                    aotSource,
                    DefaultAotMetadataAssetFolder,
                    resolvedAotNames,
                    overwrite);
                report.AotMetadataDlls.AddRange(aotReport.HotUpdateDlls);
                report.Warnings.AddRange(aotReport.Warnings);
                report.Errors.AddRange(aotReport.Errors);
            }

            if (report.HotUpdateDlls.Count > 0 && report.AotMetadataDlls.Count > 0)
            {
                WriteManifestFiles(report, target);
            }

            AssetDatabase.Refresh();
            return report;
        }

        public static string BuildManifestJson(HybridCLRHotUpdateExportReport report, BuildTarget target,
            string entryClass, string entryMethod)
        {
            if (report == null || report.HotUpdateDlls.Count == 0)
            {
                return string.Empty;
            }

            HybridCLRHotUpdateExportItem hotUpdateItem = report.HotUpdateDlls[0];
            HotUpdateManifestEditorData manifest = new HotUpdateManifestEditorData
            {
                version = 1,
                buildTarget = target.ToString(),
                hotUpdateAssemblyKey = hotUpdateItem.DestinationAssetPath,
                hotUpdateAssemblySha256 = hotUpdateItem.Sha256,
                hotUpdateEntryClass = string.IsNullOrWhiteSpace(entryClass)
                    ? "HotUpdate.HotUpdateMain"
                    : entryClass.Trim(),
                hotUpdateEntryMethod = string.IsNullOrWhiteSpace(entryMethod)
                    ? "Main"
                    : entryMethod.Trim(),
                aotMetadataKeys = report.AotMetadataDlls
                    .Select(item => item.DestinationAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };

            return JsonUtility.ToJson(manifest, true);
        }

        public static HybridCLRHotUpdateExportReport ExportDllDirectory(string sourceDirectory,
            string destinationAssetFolder,
            IEnumerable<string> assemblyNames,
            bool overwrite)
        {
            HybridCLRHotUpdateExportReport report = new HybridCLRHotUpdateExportReport();
            string absoluteSourceDirectory = ToAbsoluteProjectPath(sourceDirectory);
            string normalizedDestinationFolder = NormalizeAssetPath(destinationAssetFolder);

            if (!Directory.Exists(absoluteSourceDirectory))
            {
                report.Errors.Add("源目录不存在：" + absoluteSourceDirectory);
                return report;
            }

            if (!normalizedDestinationFolder.StartsWith("Assets/", StringComparison.Ordinal) &&
                !string.Equals(normalizedDestinationFolder, "Assets", StringComparison.Ordinal))
            {
                report.Errors.Add("目标目录必须是 Assets 路径：" + destinationAssetFolder);
                return report;
            }

            string absoluteDestinationFolder = ToAbsoluteProjectPath(normalizedDestinationFolder);
            Directory.CreateDirectory(absoluteDestinationFolder);

            List<string> names = ToAssemblyNameList(assemblyNames);
            if (names.Count == 0)
            {
                names = GetAllDllAssemblyNames(absoluteSourceDirectory);
            }

            foreach (string assemblyName in names)
            {
                string dllFileName = EnsureDllFileName(assemblyName);
                string sourcePath = Path.Combine(absoluteSourceDirectory, dllFileName);
                if (!File.Exists(sourcePath))
                {
                    report.Warnings.Add("没有找到 DLL，已跳过：" + sourcePath);
                    continue;
                }

                string destinationAssetPath = BuildBytesAssetPath(normalizedDestinationFolder, dllFileName);
                string destinationPath = ToAbsoluteProjectPath(destinationAssetPath);
                if (File.Exists(destinationPath) && !overwrite)
                {
                    report.Warnings.Add("目标文件已存在，已跳过：" + destinationAssetPath);
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(sourcePath);
                File.WriteAllBytes(destinationPath, bytes);
                AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);

                report.HotUpdateDlls.Add(new HybridCLRHotUpdateExportItem
                {
                    SourcePath = sourcePath.Replace('\\', '/'),
                    DestinationAssetPath = destinationAssetPath,
                    Bytes = bytes.LongLength,
                    Sha256 = ComputeSha256Hex(bytes)
                });
            }

            if (report.HotUpdateDlls.Count == 0 && report.Errors.Count == 0)
            {
                report.Warnings.Add("没有从源目录复制任何 DLL：" + absoluteSourceDirectory);
            }

            return report;
        }

        public static string BuildBytesAssetPath(string destinationAssetFolder, string dllFileName)
        {
            string folder = NormalizeAssetPath(destinationAssetFolder).TrimEnd('/');
            return folder + "/" + EnsureDllFileName(dllFileName) + ".bytes";
        }

        public static string ComputeSha256Hex(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static string GetGeneratedHotUpdateSourceDirectory(BuildTarget target)
        {
            return Path.Combine(ProjectRoot, DefaultHotUpdateSourceRoot, target.ToString()).Replace('\\', '/');
        }

        public static string GetGeneratedAotSourceDirectory(BuildTarget target)
        {
            return Path.Combine(ProjectRoot, DefaultAotSourceRoot, target.ToString()).Replace('\\', '/');
        }

        public static void LogReport(HybridCLRHotUpdateExportReport report)
        {
            if (report == null)
            {
                Debug.LogError("[HybridCLRHotUpdateAssetExporter] 导出报告为空。");
                return;
            }

            foreach (string warning in report.Warnings)
            {
                Debug.LogWarning("[HybridCLRHotUpdateAssetExporter] " + warning);
            }

            foreach (string error in report.Errors)
            {
                Debug.LogError("[HybridCLRHotUpdateAssetExporter] " + error);
            }

            foreach (HybridCLRHotUpdateExportItem item in report.HotUpdateDlls)
            {
                Debug.Log(
                    $"[HybridCLRHotUpdateAssetExporter] HotUpdate: {item.DestinationAssetPath}, SHA256={item.Sha256}");
            }

            foreach (HybridCLRHotUpdateExportItem item in report.AotMetadataDlls)
            {
                Debug.Log(
                    $"[HybridCLRHotUpdateAssetExporter] AOT: {item.DestinationAssetPath}, SHA256={item.Sha256}");
            }

            if (!string.IsNullOrWhiteSpace(report.ManifestAssetPath))
            {
                Debug.Log("[HybridCLRHotUpdateAssetExporter] Manifest: " + report.ManifestAssetPath);
            }

            if (!string.IsNullOrWhiteSpace(report.StreamingAssetsManifestPath))
            {
                Debug.Log("[HybridCLRHotUpdateAssetExporter] StreamingAssets Manifest: " +
                          report.StreamingAssetsManifestPath);
            }

            Debug.Log(
                $"[HybridCLRHotUpdateAssetExporter] 导出完成。成功={report.Success}, 复制数量={report.CopiedCount}");
        }

        private static void WriteManifestFiles(HybridCLRHotUpdateExportReport report, BuildTarget target)
        {
            string entryClass;
            string entryMethod;
            ReadHotUpdateEntrySettings(out entryClass, out entryMethod);
            string manifestJson = BuildManifestJson(
                report,
                target,
                entryClass,
                entryMethod);

            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                report.Warnings.Add("Manifest JSON 为空，未生成 HotUpdateManifest.json。");
                return;
            }

            string manifestAssetPath = NormalizeAssetPath(DefaultManifestAssetFolder + "/" + DefaultManifestFileName);
            string manifestAbsolutePath = ToAbsoluteProjectPath(manifestAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestAbsolutePath));
            File.WriteAllText(manifestAbsolutePath, manifestJson, Encoding.UTF8);
            AssetDatabase.ImportAsset(manifestAssetPath, ImportAssetOptions.ForceUpdate);

            string streamingAssetsAssetPath = NormalizeAssetPath(
                "Assets/StreamingAssets/aa/" + DefaultManifestFileName);
            string streamingAssetsAbsolutePath = ToAbsoluteProjectPath(streamingAssetsAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(streamingAssetsAbsolutePath));
            File.WriteAllText(streamingAssetsAbsolutePath, manifestJson, Encoding.UTF8);
            AssetDatabase.ImportAsset(streamingAssetsAssetPath, ImportAssetOptions.ForceUpdate);

            report.ManifestAssetPath = manifestAssetPath;
            report.StreamingAssetsManifestPath = streamingAssetsAssetPath;
            report.ManifestJson = manifestJson;
        }

        private static void ReadHotUpdateEntrySettings(out string entryClass, out string entryMethod)
        {
            entryClass = "HotUpdate.HotUpdateMain";
            entryMethod = "Main";

            Type settingsType = Type.GetType("StellarFramework.HotUpdate.HotUpdateSettings, StellarFramework.HotUpdateKit");
            if (settingsType == null)
            {
                return;
            }

            MethodInfo loadMethod = settingsType.GetMethod(
                "LoadOrCreateDefault",
                BindingFlags.Public | BindingFlags.Static);
            if (loadMethod == null)
            {
                return;
            }

            object settings = loadMethod.Invoke(null, new object[] { "HotUpdateSettings" });
            if (settings == null)
            {
                return;
            }

            entryClass = ReadOptionalStringProperty(settingsType, settings, "HotUpdateEntryClass", entryClass);
            entryMethod = ReadOptionalStringProperty(settingsType, settings, "HotUpdateEntryMethod", entryMethod);
        }

        private static string ReadOptionalStringProperty(Type type, object instance, string propertyName, string fallback)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(string))
            {
                return fallback;
            }

            string value = property.GetValue(instance, null) as string;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(ProjectRoot, path);
        }

        private static string EnsureDllFileName(string assemblyNameOrFileName)
        {
            string name = Path.GetFileName((assemblyNameOrFileName ?? string.Empty).Trim());
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            if (name.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, name.Length - ".bytes".Length);
            }

            return name + ".dll";
        }

        private static List<string> ToAssemblyNameList(IEnumerable<string> assemblyNames)
        {
            if (assemblyNames == null)
            {
                return new List<string>();
            }

            return assemblyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => Path.GetFileNameWithoutExtension(EnsureDllFileName(name.Trim())))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> GetAllDllAssemblyNames(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(sourceDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> GetConfiguredHybridCLRHotUpdateAssemblyNames(
            HybridCLRHotUpdateExportReport report)
        {
            object value;
            if (!TryGetHybridCLRSettingsUtilProperty("HotUpdateAssemblyNamesExcludePreserved", out value, report))
            {
                return new List<string>();
            }

            return ToStringList(value);
        }

        private static List<string> GetConfiguredHybridCLRAotAssemblyNames(
            HybridCLRHotUpdateExportReport report)
        {
            object value;
            if (!TryGetHybridCLRSettingsUtilProperty("AOTAssemblyNames", out value, report))
            {
                return new List<string>();
            }

            return ToStringList(value);
        }

        private static bool TryGetHybridCLRSettingsUtilProperty(string propertyName, out object value,
            HybridCLRHotUpdateExportReport report)
        {
            value = null;
            Type settingsUtilType = Type.GetType("HybridCLR.Editor.SettingsUtil, HybridCLR.Editor");
            if (settingsUtilType == null)
            {
                report.Warnings.Add("HybridCLR.Editor.SettingsUtil was not found. Falling back to generated folders.");
                return false;
            }

            PropertyInfo property = settingsUtilType.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                report.Warnings.Add("HybridCLR setting property was not found: " + propertyName);
                return false;
            }

            try
            {
                value = property.GetValue(null, null);
                return true;
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Could not read HybridCLR setting " + propertyName + ": " + ex.GetBaseException().Message);
                return false;
            }
        }

        private static List<string> ToStringList(object value)
        {
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return new List<string>();
            }

            List<string> result = new List<string>();
            foreach (object item in enumerable)
            {
                string text = item as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(text.Trim());
                }
            }

            return result.Distinct(StringComparer.Ordinal).ToList();
        }

        [Serializable]
        private sealed class HotUpdateManifestEditorData
        {
            public int version;
            public string buildTarget;
            public string hotUpdateAssemblyKey;
            public string hotUpdateAssemblySha256;
            public string hotUpdateEntryClass;
            public string hotUpdateEntryMethod;
            public List<string> aotMetadataKeys;
        }
    }

    [StellarTool("HybridCLR DLL 导出", "热更新", -20)]
    public sealed class HybridCLRHotUpdateExporterHubModule : ToolModule
    {
        private bool _copyAllWhenSettingsEmpty = true;
        private string _hotUpdateAssemblies = "";
        private string _aotAssemblies = "mscorlib,System,System.Core,UnityEngine.CoreModule";
        private Vector2 _scroll;
        private HybridCLRHotUpdateExportReport _lastReport;

        public override string Icon => "d_Assembly Icon";
        public override string Description => "复制 HybridCLR 生成的 DLL 到 Assets，改名为 .dll.bytes，并生成 HotUpdateManifest.json。";

        public override void OnGUI()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

            Section("生成产物");
            EditorGUILayout.HelpBox(
                "热更 DLL 目录：\n" +
                HybridCLRHotUpdateAssetExporter.GetGeneratedHotUpdateSourceDirectory(target) + "\n\n" +
                "AOT Metadata 目录：\n" +
                HybridCLRHotUpdateAssetExporter.GetGeneratedAotSourceDirectory(target),
                MessageType.Info);

            Section("复制设置");
            _copyAllWhenSettingsEmpty = EditorGUILayout.Toggle(
                new GUIContent("设置为空时复制全部热更 DLL", "当 HybridCLR Settings 没有提供热更程序集列表时，复制生成目录下的所有热更 DLL。"),
                _copyAllWhenSettingsEmpty);
            _hotUpdateAssemblies = EditorGUILayout.TextField(
                new GUIContent("热更程序集", "可选。多个程序集用英文逗号分隔，例如 HotUpdate,GameLogic。留空时优先读取 HybridCLR Settings。"),
                _hotUpdateAssemblies);
            EditorGUILayout.HelpBox(
                "热更程序集留空时优先读取 HybridCLR Settings；如果仍为空，可选择复制全部生成 DLL。多个值用英文逗号分隔。",
                MessageType.None);
            _aotAssemblies = EditorGUILayout.TextField(
                new GUIContent("AOT Metadata", "要复制为 metadata .dll.bytes 的 AOT 程序集列表，多个值用英文逗号分隔。"),
                _aotAssemblies);

            if (PrimaryButton(new GUIContent("导出为 dll.bytes", "复制 HybridCLR 生成目录中的 DLL，改名为 .dll.bytes，计算 SHA256，并生成 HotUpdateManifest.json。"), GUILayout.Height(32)))
            {
                _lastReport = HybridCLRHotUpdateAssetExporter.ExportGeneratedAssets(
                    target,
                    SplitCsv(_hotUpdateAssemblies),
                    SplitCsv(_aotAssemblies),
                    _copyAllWhenSettingsEmpty,
                    overwrite: true);
                HybridCLRHotUpdateAssetExporter.LogReport(_lastReport);
            }

            EditorGUILayout.HelpBox(
                "按钮会复制 DLL、计算 SHA256，并写入 Assets/GameHotUpdate/Manifest 与 StreamingAssets/aa 下的 HotUpdateManifest.json。",
                MessageType.None);

            DrawLastReport();
        }

        private void DrawLastReport()
        {
            if (_lastReport == null)
            {
                return;
            }

            Section("上次结果");
            MessageType messageType = _lastReport.Success ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(
                $"成功={_lastReport.Success}, 复制数量={_lastReport.CopiedCount}, 警告={_lastReport.Warnings.Count}, 错误={_lastReport.Errors.Count}",
                messageType);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120));
            foreach (string warning in _lastReport.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            foreach (string error in _lastReport.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (HybridCLRHotUpdateExportItem item in _lastReport.HotUpdateDlls)
            {
                EditorGUILayout.SelectableLabel($"{item.DestinationAssetPath}\nSHA256={item.Sha256}",
                    EditorStyles.textArea,
                    GUILayout.Height(38));
            }

            foreach (HybridCLRHotUpdateExportItem item in _lastReport.AotMetadataDlls)
            {
                EditorGUILayout.SelectableLabel($"{item.DestinationAssetPath}\nSHA256={item.Sha256}",
                    EditorStyles.textArea,
                    GUILayout.Height(38));
            }

            EditorGUILayout.EndScrollView();
        }

        private static IEnumerable<string> SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value.Split(',')
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }
    }
}
