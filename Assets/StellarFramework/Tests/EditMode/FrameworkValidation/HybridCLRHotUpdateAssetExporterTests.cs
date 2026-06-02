using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StellarFramework.HotUpdate;
using UnityEngine;
using UnityEditor;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HybridCLRHotUpdateAssetExporterTests
    {
        private const string ExporterTypeName =
            "StellarFramework.Editor.Modules.HybridCLRHotUpdateAssetExporter, StellarFramework.ToolsHub.Editor";

        private static readonly string TempAssetFolder = "Assets/Temp/HybridCLRHotUpdateAssetExporterTests";
        private static readonly string TempAbsoluteFolder =
            Path.Combine(Application.dataPath, "Temp", "HybridCLRHotUpdateAssetExporterTests");

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TempAbsoluteFolder))
            {
                Directory.Delete(TempAbsoluteFolder, true);
            }

            string metaPath = TempAbsoluteFolder + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        [Test]
        public void BuildBytesAssetPathConvertsDllNameToDllBytesAssetPath()
        {
            Type exporterType = RequireExporterType();
            MethodInfo method = exporterType.GetMethod(
                "BuildBytesAssetPath",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object result = method.Invoke(null, new object[]
            {
                "Assets/GameHotUpdate/Code",
                "HotUpdate.dll"
            });

            Assert.That(result, Is.EqualTo("Assets/GameHotUpdate/Code/HotUpdate.dll.bytes"));
        }

        [Test]
        public void ComputeSha256HexReturnsLowercaseHex()
        {
            Type exporterType = RequireExporterType();
            MethodInfo method = exporterType.GetMethod(
                "ComputeSha256Hex",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            byte[] bytes = System.Text.Encoding.ASCII.GetBytes("abc");
            object result = method.Invoke(null, new object[] { bytes });

            Assert.That(result, Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public void ExportDllDirectoryCopiesDllsAsDllBytes()
        {
            Type exporterType = RequireExporterType();
            MethodInfo method = exporterType.GetMethod(
                "ExportDllDirectory",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            string sourceFolder = Path.Combine(TempAbsoluteFolder, "Source");
            Directory.CreateDirectory(sourceFolder);
            File.WriteAllBytes(Path.Combine(sourceFolder, "HotUpdate.dll"), new byte[] { 1, 2, 3 });

            object report = method.Invoke(null, new object[]
            {
                sourceFolder,
                TempAssetFolder + "/Output",
                new[] { "HotUpdate" },
                true
            });

            Assert.That(report, Is.Not.Null);

            string outputPath = Path.Combine(TempAbsoluteFolder, "Output", "HotUpdate.dll.bytes");
            Assert.That(File.Exists(outputPath), Is.True);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(outputPath));
        }

        [Test]
        public void BuildManifestJsonUsesExportedDllShaAndKeys()
        {
            Type exporterType = RequireExporterType();
            Type reportType = Type.GetType(
                "StellarFramework.Editor.Modules.HybridCLRHotUpdateExportReport, StellarFramework.ToolsHub.Editor");
            Type itemType = Type.GetType(
                "StellarFramework.Editor.Modules.HybridCLRHotUpdateExportItem, StellarFramework.ToolsHub.Editor");
            Assert.That(reportType, Is.Not.Null);
            Assert.That(itemType, Is.Not.Null);

            object report = Activator.CreateInstance(reportType);
            object hotItem = Activator.CreateInstance(itemType);
            object aotItem = Activator.CreateInstance(itemType);
            itemType.GetField("DestinationAssetPath").SetValue(hotItem, "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes");
            itemType.GetField("Sha256").SetValue(hotItem, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            itemType.GetField("DestinationAssetPath").SetValue(aotItem, "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes");

            object hotUpdateDlls = reportType.GetField("HotUpdateDlls").GetValue(report);
            object aotMetadataDlls = reportType.GetField("AotMetadataDlls").GetValue(report);
            hotUpdateDlls.GetType().GetMethod("Add").Invoke(hotUpdateDlls, new[] { hotItem });
            aotMetadataDlls.GetType().GetMethod("Add").Invoke(aotMetadataDlls, new[] { aotItem });

            MethodInfo method = exporterType.GetMethod(
                "BuildManifestJson",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            string json = (string)method.Invoke(null, new object[]
            {
                report,
                BuildTarget.StandaloneWindows64,
                "HotUpdate.HotUpdateMain",
                "Main"
            });

            HotUpdateManifest manifest = HotUpdateManifest.FromJson(json);
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.hotUpdateAssemblyKey, Is.EqualTo("Assets/GameHotUpdate/Code/HotUpdate.dll.bytes"));
            Assert.That(manifest.hotUpdateAssemblySha256,
                Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            CollectionAssert.Contains(manifest.aotMetadataKeys, "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes");
        }

        private static Type RequireExporterType()
        {
            Type exporterType = Type.GetType(ExporterTypeName);
            Assert.That(exporterType, Is.Not.Null, "HybridCLR hot update asset exporter type was not found.");
            return exporterType;
        }
    }
}
