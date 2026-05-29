using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class QuickStartCatalogPolicyTests
    {
        [Test]
        public void QuickStartModuleDefinesHappyPathInFixedOrder()
        {
            string source = ReadQuickStartSource();

            Assert.That(source, Does.Contain("Title = \"1. 构建样例\""));
            Assert.That(source, Does.Contain("FrameworkValidationScenePath"));
            Assert.That(source, Does.Contain("UIKitScenePath"));
            Assert.That(source, Does.Contain("ResKitScenePath"));
        }

        [Test]
        public void QuickStartReferencedPathsExistOnDisk()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Scenes/FrameworkValidation_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Scenes/ResKit_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/快速开始.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Samples/KitSamples/Samples_Index.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md")), Is.True);
        }

        private static string ReadQuickStartSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StellarFramework/Editor/StellarToolsHub/Modules/QuickStartHubModule.cs");
            return File.ReadAllText(path);
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}


