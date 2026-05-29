using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class OnboardingSurfacePolicyTests
    {
        [Test]
        public void QuickStartKeepsFrameworkValidationAsFirstRuntimeStep()
        {
            string docPath = Path.Combine(
                Application.dataPath,
                "StellarFramework/快速开始.md");

            string source = File.ReadAllText(docPath);

            Assert.That(source, Does.Contain("FrameworkValidation_Playable.unity"));
            Assert.That(source, Does.Contain("UIKit_Playable.unity"));
            Assert.That(source, Does.Contain("ResKit_Playable.unity"));
        }
    }
}

