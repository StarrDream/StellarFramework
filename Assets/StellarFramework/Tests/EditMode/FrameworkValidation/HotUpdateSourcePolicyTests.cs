using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdateSourcePolicyTests
    {
        [Test]
        public void HotUpdateMainPrintsHybridClrSuccessMessage()
        {
            string source = File.ReadAllText(ToAbsoluteAssetPath(
                "Assets/GameHotUpdate/Source/HotUpdateMain.cs"));

            Assert.That(source, Does.Contain("Hello HybridCLR , 热更成功 ;"));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
