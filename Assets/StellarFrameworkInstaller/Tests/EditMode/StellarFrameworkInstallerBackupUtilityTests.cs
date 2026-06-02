using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFrameworkInstaller.Tests
{
    public sealed class StellarFrameworkInstallerBackupUtilityTests
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
        public void BackupAssetPathCopiesFileIntoTimestampedFolder()
        {
            string sourceAssetPath = TestRoot + "/Source/Config.asset";
            Directory.CreateDirectory(ToAbsoluteAssetPath(TestRoot + "/Source"));
            File.WriteAllText(ToAbsoluteAssetPath(sourceAssetPath), "config");

            StellarFrameworkInstallerReport report = new StellarFrameworkInstallerReport();
            string backupFolder = StellarFrameworkInstallerBackupUtility.BackupAssetPath(
                sourceAssetPath,
                TestRoot + "/Backups",
                "20260602_120000",
                report);

            Assert.That(File.Exists(ToAbsoluteAssetPath(backupFolder + "/Config.asset")), Is.True);
            Assert.That(File.ReadAllText(ToAbsoluteAssetPath(backupFolder + "/Config.asset")), Is.EqualTo("config"));
        }

        [Test]
        public void BackupAssetPathReportsMissingSourceWithoutCreatingBackup()
        {
            StellarFrameworkInstallerReport report = new StellarFrameworkInstallerReport();

            string backupFolder = StellarFrameworkInstallerBackupUtility.BackupAssetPath(
                TestRoot + "/Missing",
                TestRoot + "/Backups",
                "20260602_120000",
                report);

            Assert.That(backupFolder, Is.Empty);
            Assert.That(report.Errors, Is.Not.Empty);
        }

        private const string TestRoot = "Temp/StellarFrameworkInstallerTests/Backup";

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
