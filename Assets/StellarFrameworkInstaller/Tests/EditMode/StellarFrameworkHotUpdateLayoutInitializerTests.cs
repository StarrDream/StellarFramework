using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFrameworkInstaller.Tests
{
    public sealed class StellarFrameworkHotUpdateLayoutInitializerTests
    {
        [SetUp]
        public void SetUp()
        {
            DeleteAssetPath(TestRoot);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteAssetPath(TestRoot);
        }

        [Test]
        public void CreateGameHotUpdateLayoutCreatesExpectedFoldersAndManifest()
        {
            StellarFrameworkInstallerReport report = new StellarFrameworkInstallerReport();

            StellarFrameworkHotUpdateLayoutInitializer.CreateGameHotUpdateLayout(TestRoot, report);

            Assert.That(Directory.Exists(ToAbsoluteAssetPath(TestRoot + "/Code")), Is.True);
            Assert.That(Directory.Exists(ToAbsoluteAssetPath(TestRoot + "/Metadata")), Is.True);
            Assert.That(Directory.Exists(ToAbsoluteAssetPath(TestRoot + "/Manifest")), Is.True);
            Assert.That(Directory.Exists(ToAbsoluteAssetPath(TestRoot + "/Source")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath(TestRoot + "/Manifest/HotUpdateManifest.json")), Is.True);
        }

        [Test]
        public void CreateGameHotUpdateLayoutDoesNotOverwriteExistingManifest()
        {
            Directory.CreateDirectory(ToAbsoluteAssetPath(TestRoot + "/Manifest"));
            string manifestPath = ToAbsoluteAssetPath(TestRoot + "/Manifest/HotUpdateManifest.json");
            File.WriteAllText(manifestPath, "{\"custom\":true}");

            StellarFrameworkHotUpdateLayoutInitializer.CreateGameHotUpdateLayout(
                TestRoot,
                new StellarFrameworkInstallerReport());

            Assert.That(File.ReadAllText(manifestPath), Is.EqualTo("{\"custom\":true}"));
        }

        private const string TestRoot = "Temp/StellarFrameworkInstallerTests/GameHotUpdate";

        private static void DeleteAssetPath(string assetPath)
        {
            string fullPath = ToAbsoluteAssetPath(assetPath);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }

            string metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
