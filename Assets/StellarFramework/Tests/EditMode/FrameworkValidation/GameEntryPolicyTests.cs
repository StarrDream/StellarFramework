using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class GameEntryPolicyTests
    {
        [Test]
        public void GameEntryDisposesArchitectureOnlyOnApplicationQuit()
        {
            string source = ReadAssetText("Assets/StellarFramework/GameEntry.cs");

            Assert.That(source, Does.Contain("private void OnApplicationQuit()"));
            Assert.That(source, Does.Not.Contain("private void OnDestroy()"));
            Assert.That(source, Does.Contain("GameApp.Interface.Dispose()"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
