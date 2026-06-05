using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class VerificationSurfacePolicyTests
    {
        [Test]
        public void VerificationAreaExistsOutsideFrameworkPayload()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/README.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Editor/ReleaseVerificationHubModule.cs")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Scenes/FrameworkValidation_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Example_FrameworkValidation/FrameworkValidationRunner.cs")), Is.True);
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
